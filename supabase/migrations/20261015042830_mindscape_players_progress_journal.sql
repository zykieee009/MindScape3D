-- Exported from the live project (supabase_migrations.schema_migrations) on 2026-11-09 so a new project can be
-- rebuilt from the repo. Applied remotely on 2026-10-15.
-- Username/password accounts, episode progress and the reflection journal for Mindscape 3D.
-- Clients never touch the tables directly: RLS is on with no policies, and every read/write goes through
-- the SECURITY DEFINER functions below, which resolve the caller from a session token.

create schema if not exists private;
revoke all on schema private from public, anon, authenticated;

create table public.players (
  id uuid primary key default gen_random_uuid(),
  username text not null check (username ~ '^[A-Za-z0-9_.]{3,24}$'),
  password_hash text not null,
  created_at timestamptz not null default now(),
  last_login_at timestamptz
);
create unique index players_username_lower_key on public.players (lower(username));

create table public.player_sessions (
  token_hash bytea primary key,
  player_id uuid not null references public.players (id) on delete cascade,
  created_at timestamptz not null default now(),
  expires_at timestamptz not null default now() + interval '30 days'
);
create index player_sessions_player_id_idx on public.player_sessions (player_id);

create table public.episode_progress (
  player_id uuid not null references public.players (id) on delete cascade,
  chapter smallint not null check (chapter between 1 and 9),
  episode smallint not null check (episode between 1 and 20),
  completed boolean not null default false,
  started_at timestamptz not null default now(),
  completed_at timestamptz,                                   -- first time the episode was finished
  completion_seconds integer check (completion_seconds >= 0), -- play time of that first finish
  primary key (player_id, chapter, episode)
);

create table public.journal_entries (
  id bigint generated always as identity primary key,
  player_id uuid not null references public.players (id) on delete cascade,
  chapter smallint not null check (chapter between 1 and 9),
  episode smallint not null check (episode between 1 and 20),
  prompt text not null default '' check (char_length(prompt) <= 300),
  entry text not null default '' check (char_length(entry) <= 3000),
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  unique (player_id, chapter, episode)
);

alter table public.players enable row level security;
alter table public.player_sessions enable row level security;
alter table public.episode_progress enable row level security;
alter table public.journal_entries enable row level security;
revoke all on public.players, public.player_sessions, public.episode_progress, public.journal_entries from anon, authenticated;

-- ------------------------------------------------------------------ private helpers

create function private.issue_session(p_player_id uuid)
returns json language plpgsql security definer set search_path = '' as $$
declare
  v_token text := encode(extensions.gen_random_bytes(32), 'hex');
begin
  delete from public.player_sessions where player_id = p_player_id and expires_at < now();
  insert into public.player_sessions (token_hash, player_id) values (extensions.digest(v_token, 'sha256'), p_player_id);
  update public.players set last_login_at = now() where id = p_player_id;
  return json_build_object('token', v_token, 'username', (select username from public.players where id = p_player_id));
end $$;

create function private.player_from_token(p_token text)
returns uuid language plpgsql stable security definer set search_path = '' as $$
declare v_player uuid;
begin
  select player_id into v_player from public.player_sessions
   where token_hash = extensions.digest(coalesce(p_token, ''), 'sha256') and expires_at > now();
  if v_player is null then raise exception 'Your session has expired. Please log in again.' using errcode = '28000'; end if;
  return v_player;
end $$;

-- ------------------------------------------------------------------ accounts

create function public.register_player(p_username text, p_password text)
returns json language plpgsql security definer set search_path = '' as $$
declare v_id uuid;
begin
  p_username := btrim(coalesce(p_username, ''));
  if p_username !~ '^[A-Za-z0-9_.]{3,24}$' then
    raise exception 'Usernames are 3-24 characters: letters, numbers, _ or .' using errcode = '22023';
  end if;
  if char_length(coalesce(p_password, '')) not between 6 and 72 then
    raise exception 'Passwords must be 6-72 characters.' using errcode = '22023';
  end if;
  if exists (select 1 from public.players where lower(username) = lower(p_username)) then
    raise exception 'That username is already taken.' using errcode = '23505';
  end if;
  insert into public.players (username, password_hash)
  values (p_username, extensions.crypt(p_password, extensions.gen_salt('bf', 10)))
  returning id into v_id;
  return private.issue_session(v_id);
end $$;

create function public.login_player(p_username text, p_password text)
returns json language plpgsql security definer set search_path = '' as $$
declare v_id uuid; v_hash text;
begin
  select id, password_hash into v_id, v_hash from public.players where lower(username) = lower(btrim(coalesce(p_username, '')));
  if v_id is null or v_hash <> extensions.crypt(coalesce(p_password, ''), v_hash) then
    raise exception 'Incorrect username or password.' using errcode = '28P01';
  end if;
  return private.issue_session(v_id);
end $$;

create function public.logout_player(p_token text)
returns void language sql security definer set search_path = '' as $$
  delete from public.player_sessions where token_hash = extensions.digest(coalesce(p_token, ''), 'sha256');
$$;

-- ------------------------------------------------------------------ episode progress

create function public.start_episode(p_token text, p_chapter int, p_episode int)
returns void language plpgsql security definer set search_path = '' as $$
begin
  insert into public.episode_progress (player_id, chapter, episode)
  values (private.player_from_token(p_token), p_chapter, p_episode)
  on conflict (player_id, chapter, episode) do update
    set started_at = case when episode_progress.completed then episode_progress.started_at else now() end;
end $$;

-- Marks an episode complete. The first finish keeps its date and play time; replays do not overwrite them.
create function public.complete_episode(p_token text, p_chapter int, p_episode int, p_seconds int default null)
returns void language plpgsql security definer set search_path = '' as $$
declare v_player uuid := private.player_from_token(p_token);
begin
  insert into public.episode_progress (player_id, chapter, episode, completed, completed_at, completion_seconds)
  values (v_player, p_chapter, p_episode, true, now(), greatest(coalesce(p_seconds, 0), 0))
  on conflict (player_id, chapter, episode) do update
    set completed = true,
        completed_at = coalesce(episode_progress.completed_at, now()),
        completion_seconds = coalesce(episode_progress.completion_seconds,
          greatest(coalesce(p_seconds, extract(epoch from now() - episode_progress.started_at)::int), 0));
end $$;

create function public.get_progress(p_token text)
returns table (chapter smallint, episode smallint, completed boolean, started_at timestamptz, completed_at timestamptz, completion_seconds integer)
language sql stable security definer set search_path = '' as $$
  select chapter, episode, completed, started_at, completed_at, completion_seconds
    from public.episode_progress where player_id = private.player_from_token(p_token)
   order by chapter, episode;
$$;

-- ------------------------------------------------------------------ reflection journal

create function public.save_journal(p_token text, p_chapter int, p_episode int, p_prompt text, p_entry text)
returns void language plpgsql security definer set search_path = '' as $$
begin
  insert into public.journal_entries (player_id, chapter, episode, prompt, entry)
  values (private.player_from_token(p_token), p_chapter, p_episode, left(coalesce(p_prompt, ''), 300), left(coalesce(p_entry, ''), 3000))
  on conflict (player_id, chapter, episode) do update
    set prompt = excluded.prompt, entry = excluded.entry, updated_at = now();
end $$;

create function public.get_journal(p_token text)
returns table (chapter smallint, episode smallint, prompt text, entry text, updated_at timestamptz)
language sql stable security definer set search_path = '' as $$
  select chapter, episode, prompt, entry, updated_at
    from public.journal_entries where player_id = private.player_from_token(p_token)
   order by chapter, episode;
$$;

-- Only the API functions are callable from the game.
revoke all on all functions in schema private from public, anon, authenticated;
revoke all on function public.register_player(text, text), public.login_player(text, text), public.logout_player(text),
  public.start_episode(text, int, int), public.complete_episode(text, int, int, int), public.get_progress(text),
  public.save_journal(text, int, int, text, text), public.get_journal(text) from public;
grant execute on function public.register_player(text, text), public.login_player(text, text), public.logout_player(text),
  public.start_episode(text, int, int), public.complete_episode(text, int, int, int), public.get_progress(text),
  public.save_journal(text, int, int, text, text), public.get_journal(text) to anon, authenticated;
