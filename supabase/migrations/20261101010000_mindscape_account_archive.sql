-- Account deletion with a 30-day recovery window.
-- Deleting an account in the game archives the player row (status 'archived', delete_after = now + 30 days); an
-- archived account cannot use any data RPC and cannot log in, but it can be recovered from the login screen (emailed
-- code -> confirm -> new password) until delete_after. A daily pg_cron job then deletes the auth user, which cascades
-- to the player row and every table that references it (journal, progress, assessments, records, sessions).

alter table public.players
  add column if not exists status text not null default 'active',
  add column if not exists archived_at timestamptz,
  add column if not exists delete_after timestamptz;

do $$ begin
  alter table public.players add constraint players_status_check check (status in ('active', 'archived'));
exception when duplicate_object then null; end $$;

create index if not exists players_delete_after_idx on public.players (delete_after) where status = 'archived';

-- Data RPCs resolve the caller here, so an archived account is shut out of all of them (the game then signs out).
create or replace function private.player_from_token(p_token text)
returns uuid language plpgsql stable security definer set search_path = '' as $$
declare v_player uuid; v_status text;
begin
  if auth.uid() is not null then
    select id, status into v_player, v_status from public.players where auth_id = auth.uid();
    if v_player is null then raise exception 'Finish creating your account.' using errcode = '28000'; end if;
  else
    select s.player_id, p.status into v_player, v_status from public.player_sessions s join public.players p on p.id = s.player_id
     where s.token_hash = extensions.digest(coalesce(p_token, ''), 'sha256') and s.expires_at > now();
    if v_player is null then raise exception 'Your session has expired. Please log in again.' using errcode = '28000'; end if;
  end if;
  if v_status = 'archived' then raise exception 'This account was deleted. You can recover it from the login screen.' using errcode = '28000'; end if;
  return v_player;
end $$;

-- Unchanged, except that an archived account is never signed in through it.
create or replace function public.ensure_profile()
returns json language plpgsql security definer set search_path = '' as $$
declare v_uid uuid := auth.uid(); v_id uuid; v_name text; v_status text;
begin
  if v_uid is null then raise exception 'Please log in.' using errcode = '28000'; end if;
  select id, username, status into v_id, v_name, v_status from public.players where auth_id = v_uid;
  if v_status = 'archived' then raise exception 'This account was deleted. You can recover it from the login screen.' using errcode = '28000'; end if;
  if v_id is null then
    select btrim(coalesce(raw_user_meta_data ->> 'username', '')) into v_name
      from auth.users where id = v_uid and email_confirmed_at is not null;
    if v_name is null then raise exception 'Verify your email first.' using errcode = '28000'; end if;
    if v_name !~ '^[A-Za-z0-9_.]{3,24}$' then
      raise exception 'Usernames are 3-24 characters: letters, numbers, _ or .' using errcode = '22023';
    end if;
    if exists (select 1 from public.players where lower(username) = lower(v_name)) then
      raise exception 'That username is already taken.' using errcode = '23505';
    end if;
    insert into public.players (username, auth_id) values (v_name, v_uid) returning id into v_id;
  end if;
  update public.players set last_login_at = now() where id = v_id;
  return json_build_object('username', v_name);
end $$;

-- The caller's account: {username, status, archived_at, delete_after}, or null before the profile exists.
create or replace function public.account_state()
returns json language sql stable security definer set search_path = '' as $$
  select json_build_object('username', username, 'status', status, 'archived_at', archived_at, 'delete_after', delete_after)
    from public.players where auth_id = auth.uid();
$$;

-- "Delete account": archive the caller's account for 30 days. Returns {username, archived_at, delete_after}.
create or replace function public.archive_account()
returns json language plpgsql security definer set search_path = '' as $$
declare v_result json;
begin
  if auth.uid() is null then raise exception 'Please log in.' using errcode = '28000'; end if;
  update public.players set status = 'archived', archived_at = now(), delete_after = now() + interval '30 days'
   where auth_id = auth.uid() and status = 'active'
   returning json_build_object('username', username, 'archived_at', archived_at, 'delete_after', delete_after) into v_result;
  if v_result is null then raise exception 'No active account to delete.' using errcode = '22023'; end if;
  return v_result;
end $$;

-- "Recover account": the caller (signed in with an emailed recovery code) brings an archived account back.
create or replace function public.restore_account()
returns json language plpgsql security definer set search_path = '' as $$
declare v_result json;
begin
  if auth.uid() is null then raise exception 'Please log in.' using errcode = '28000'; end if;
  update public.players set status = 'active', archived_at = null, delete_after = null
   where auth_id = auth.uid() and status = 'archived' and delete_after > now()
   returning json_build_object('username', username) into v_result;
  if v_result is null then raise exception 'There is no deleted account to recover.' using errcode = '22023'; end if;
  return v_result;
end $$;

-- For the edge function (service role): whether this email belongs to an account that can still be recovered.
create or replace function public.recoverable_email(p_email text)
returns boolean language sql stable security definer set search_path = '' as $$
  select exists (select 1 from public.players p join auth.users u on u.id = p.auth_id
                  where lower(u.email) = lower(btrim(coalesce(p_email, ''))) and p.status = 'archived' and p.delete_after > now());
$$;

-- Permanently deletes every account whose 30 days are up. Returns how many were deleted.
create or replace function private.purge_archived_accounts()
returns integer language plpgsql security definer set search_path = '' as $$
declare v_count integer;
begin
  with gone as (
    delete from auth.users u using public.players p
     where p.auth_id = u.id and p.status = 'archived' and p.delete_after <= now()
    returning u.id)
  select count(*) into v_count from gone;
  -- Players with no auth user (the old username/password accounts) are removed directly.
  delete from public.players where status = 'archived' and delete_after <= now();
  return v_count;
end $$;

revoke all on function public.account_state(), public.archive_account(), public.restore_account() from public, anon;
grant execute on function public.account_state(), public.archive_account(), public.restore_account() to authenticated, service_role;
revoke all on function public.recoverable_email(text) from public, anon, authenticated;
grant execute on function public.recoverable_email(text) to service_role;
revoke all on function private.purge_archived_accounts() from public, anon, authenticated;

-- Once a day at 03:17 UTC.
create extension if not exists pg_cron with schema pg_catalog;
select cron.unschedule(jobid) from cron.job where jobname = 'mindscape-purge-archived-accounts';
select cron.schedule('mindscape-purge-archived-accounts', '17 3 * * *', 'select private.purge_archived_accounts()');
