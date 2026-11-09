-- 2026-11-09: unique emails on sign-up, and "Continue with Google".
--
-- email_taken: sign-up refuses an email that already belongs to a confirmed account (or to any player, including a
-- deleted one still inside its 30 days). Supabase Auth itself answers a repeated sign-up with a silent success, so
-- without this the player would wait for a code that never comes.
--
-- google_handoffs: the game cannot receive a browser redirect on every platform (Android, WebGL, desktop), so the
-- account edge function finishes the Google sign-in itself and parks the session here under a random state that only
-- the game knows; the game polls for it. Rows live a few minutes. Only the service role (the edge function) touches it.

create or replace function public.email_taken(p_email text)
returns boolean
language sql
stable
security definer
set search_path = ''
as $$
  select exists (
    select 1 from auth.users u
     where lower(u.email) = lower(btrim(coalesce(p_email, '')))
       and (u.email_confirmed_at is not null or exists (select 1 from public.players p where p.auth_id = u.id)));
$$;

revoke execute on function public.email_taken(text) from public, anon, authenticated;
grant execute on function public.email_taken(text) to service_role;

create table if not exists public.google_handoffs (
  state text primary key check (state ~ '^[A-Za-z0-9_-]{32,128}$'),
  verifier text not null,
  session jsonb,
  error text,
  created_at timestamptz not null default now()
);

alter table public.google_handoffs enable row level security;
revoke all on table public.google_handoffs from public, anon, authenticated;
grant select, insert, update, delete on table public.google_handoffs to service_role;
