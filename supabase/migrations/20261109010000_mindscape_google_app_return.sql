-- 2026-11-09: "Continue with Google" inside the app. Android opens the sign-in in a Chrome Custom Tab and macOS in
-- Apple's sign-in sheet (ASWebAuthenticationSession); both close when the page goes to mindscape://google-done.
-- return_app marks a sign-in started that way, so the account function's last page redirects back to the game
-- instead of showing "go back to the game".
alter table public.google_handoffs add column if not exists return_app boolean not null default false;
