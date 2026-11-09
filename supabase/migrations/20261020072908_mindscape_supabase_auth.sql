-- Exported from the live project on 2026-11-09 (applied remotely on 2026-10-20).
-- Accounts move to Supabase Auth (email + password, confirmed with an emailed OTP). A player row is created for a
-- confirmed auth user by ensure_profile(); every existing RPC keeps its p_token argument and now resolves the
-- caller from the auth JWT first, so old builds that still send a legacy session token keep working.

alter table public.players
  add column if not exists auth_id uuid unique references auth.users (id) on delete cascade;
alter table public.players alter column password_hash drop not null;

create or replace function private.player_from_token(p_token text)
returns uuid
language plpgsql
stable
security definer
set search_path = ''
as $$
declare v_player uuid;
begin
  if auth.uid() is not null then
    select id into v_player from public.players where auth_id = auth.uid();
    if v_player is null then raise exception 'Finish creating your account.' using errcode = '28000'; end if;
    return v_player;
  end if;
  select player_id into v_player from public.player_sessions
   where token_hash = extensions.digest(coalesce(p_token, ''), 'sha256') and expires_at > now();
  if v_player is null then raise exception 'Your session has expired. Please log in again.' using errcode = '28000'; end if;
  return v_player;
end $$;

-- Creates the player row for the signed-in, email-confirmed auth user (username comes from sign-up metadata).
create or replace function public.ensure_profile()
returns json
language plpgsql
security definer
set search_path = ''
as $$
declare v_uid uuid := auth.uid(); v_id uuid; v_name text;
begin
  if v_uid is null then raise exception 'Please log in.' using errcode = '28000'; end if;
  select id, username into v_id, v_name from public.players where auth_id = v_uid;
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

-- Used only by the account edge function (service role): username availability and username -> email lookup.
create or replace function public.username_taken(p_username text)
returns boolean
language sql
stable
security definer
set search_path = ''
as $$
  select exists (select 1 from public.players where lower(username) = lower(btrim(coalesce(p_username, ''))));
$$;

create or replace function public.email_for_username(p_username text)
returns text
language sql
stable
security definer
set search_path = ''
as $$
  select u.email::text from public.players p join auth.users u on u.id = p.auth_id
   where lower(p.username) = lower(btrim(coalesce(p_username, '')));
$$;

revoke execute on function public.ensure_profile() from public, anon;
grant execute on function public.ensure_profile() to authenticated;
revoke execute on function public.username_taken(text), public.email_for_username(text) from public, anon, authenticated;
grant execute on function public.username_taken(text), public.email_for_username(text) to service_role;
