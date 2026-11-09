-- The self-assessment is now asked after every story episode, so each answer keeps its episode (0 = older answers).
-- Additive: older game builds keep calling save_assessment / get_assessments and still work.
alter table public.assessments add column if not exists episode smallint not null default 0;

create or replace function public.save_episode_assessment(p_token text, p_chapter integer, p_episode integer, p_overwhelm_level integer, p_coping_strategy text)
 returns void
 language plpgsql
 security definer
 set search_path to ''
as $function$
begin
  insert into public.assessments (player_id, chapter, episode, overwhelm_level, coping_strategy)
  values (private.player_from_token(p_token), p_chapter, greatest(coalesce(p_episode, 0), 0), least(greatest(p_overwhelm_level, 1), 5), left(coalesce(p_coping_strategy, ''), 200));
end $function$;

revoke all on function public.save_episode_assessment(text, integer, integer, integer, text) from public;
grant execute on function public.save_episode_assessment(text, integer, integer, integer, text) to anon, authenticated, service_role;

-- get_assessments returns the episode too (its result type changes, so it is recreated).
drop function if exists public.get_assessments(text);
create function public.get_assessments(p_token text)
 returns table(chapter smallint, episode smallint, overwhelm_level smallint, coping_strategy text, created_at timestamp with time zone)
 language sql
 stable security definer
 set search_path to ''
as $function$
  select chapter, episode, overwhelm_level, coping_strategy, created_at
    from public.assessments where player_id = private.player_from_token(p_token)
   order by created_at desc limit 200;
$function$;

grant execute on function public.get_assessments(text) to anon, authenticated, service_role;
