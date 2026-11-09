-- Exported from the live project on 2026-11-09 (applied remotely on 2026-10-15).
-- Self-reflection assessments and per-episode stress / time records, per player (same token RPC pattern as the journal).
create table if not exists public.assessments (
  id bigint generated always as identity primary key,
  player_id uuid not null references public.players(id) on delete cascade,
  chapter smallint not null,
  overwhelm_level smallint not null check (overwhelm_level between 1 and 5),
  coping_strategy text not null default '',
  created_at timestamptz not null default now()
);
create index if not exists assessments_player on public.assessments (player_id, created_at desc);
alter table public.assessments enable row level security;

create table if not exists public.episode_records (
  id bigint generated always as identity primary key,
  player_id uuid not null references public.players(id) on delete cascade,
  chapter smallint not null,
  episode smallint not null,
  result text not null check (result in ('completed', 'time_up')),
  time_limit integer not null,
  seconds_left integer not null,
  checkin_start smallint not null check (checkin_start between 0 and 5),
  checkin_end smallint not null check (checkin_end between 0 and 5),
  stress_peak smallint not null check (stress_peak between 0 and 100),
  stress_final smallint not null check (stress_final between 0 and 100),
  created_at timestamptz not null default now()
);
create index if not exists episode_records_player on public.episode_records (player_id, created_at desc);
alter table public.episode_records enable row level security;

create or replace function public.save_assessment(p_token text, p_chapter integer, p_overwhelm_level integer, p_coping_strategy text)
returns void language plpgsql security definer set search_path to '' as $$
begin
  insert into public.assessments (player_id, chapter, overwhelm_level, coping_strategy)
  values (private.player_from_token(p_token), p_chapter, least(greatest(p_overwhelm_level, 1), 5), left(coalesce(p_coping_strategy, ''), 200));
end $$;

create or replace function public.get_assessments(p_token text)
returns table(chapter smallint, overwhelm_level smallint, coping_strategy text, created_at timestamptz)
language sql stable security definer set search_path to '' as $$
  select chapter, overwhelm_level, coping_strategy, created_at
    from public.assessments where player_id = private.player_from_token(p_token)
   order by created_at desc limit 100;
$$;

create or replace function public.save_episode_record(p_token text, p_chapter integer, p_episode integer, p_result text, p_time_limit integer,
  p_seconds_left integer, p_checkin_start integer, p_checkin_end integer, p_stress_peak integer, p_stress_final integer)
returns void language plpgsql security definer set search_path to '' as $$
begin
  insert into public.episode_records (player_id, chapter, episode, result, time_limit, seconds_left, checkin_start, checkin_end, stress_peak, stress_final)
  values (private.player_from_token(p_token), p_chapter, p_episode, p_result, greatest(p_time_limit, 0), greatest(p_seconds_left, 0),
          least(greatest(p_checkin_start, 0), 5), least(greatest(p_checkin_end, 0), 5),
          least(greatest(p_stress_peak, 0), 100), least(greatest(p_stress_final, 0), 100));
end $$;

create or replace function public.get_episode_records(p_token text)
returns table(chapter smallint, episode smallint, result text, time_limit integer, seconds_left integer, checkin_start smallint,
  checkin_end smallint, stress_peak smallint, stress_final smallint, created_at timestamptz)
language sql stable security definer set search_path to '' as $$
  select chapter, episode, result, time_limit, seconds_left, checkin_start, checkin_end, stress_peak, stress_final, created_at
    from public.episode_records where player_id = private.player_from_token(p_token)
   order by created_at desc limit 200;
$$;
