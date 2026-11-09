# Item 15: account deletion and retention decision

## Verified implementation in this branch

- `MainMenuNewController` offers sign out, but no account deletion action. `MindscapeCloud.SignOut` clears local session tokens and its in-memory cloud cache; it does not delete the Supabase Auth user, Supabase data, SQLite file, or other PlayerPrefs keys.
- `supabase/functions/account/index.ts` has signup, login, password recovery, refresh, and logout actions. Its only Auth user DELETE is cleanup of a confirmed account when profile creation fails. It has no player-initiated deletion or recovery-after-deletion endpoint.
- Signed-in journals, episode progress and records, and submitted assessments use Supabase RPCs. The client does not have a delete RPC. The repository contains no SQL migrations defining the live Supabase tables, foreign keys, backup/archive behavior, or retention job, so server-side deletion effects cannot be verified here.
- Offline journals and assessments are written to PlayerPrefs and, where available, SQLite at `Application.persistentDataPath/mindscape.db`. Story choices and some player/settings/save data also persist locally. The newest-100 cap on PlayerPrefs lists is a storage limit, not a time-based retention policy. There is no account or local-data erase flow.
- Cloud writes are asynchronous and log failures; the UI cannot currently guarantee that a submitted answer reached Supabase.

The registration and assessment notices describe these current paths. Neither promises account deletion, recovery, or a retention duration. The PDF's two-year period has **not** been adopted.

## Approval needed before finishing item 15

Approve a single deletion and retention policy that answers all of these points:

1. When a player requests account deletion, should the Supabase Auth user and linked player, journal, progress, episode-record, and assessment rows be erased immediately, or held for a recoverable period? If held, specify the exact duration, what a player can recover, and when erasure becomes final.
2. What happens to archived copies and Supabase backups: are they erased, allowed to expire on a defined schedule, or retained for a stated reason? Specify any separate backup window and who can access them.
3. Should deletion also erase this device's SQLite and PlayerPrefs data automatically, or offer a separate clear-local-data choice? Define behavior for an offline player with no account.
4. Define how long active-account data is retained, if any limit is intended, and the contact or process for a player who cannot access the app.

Once approved, implementation needs a verified Supabase schema/migration, an authenticated deletion endpoint with explicit row and backup handling, a client flow that reports success or failure, and notice wording that matches the chosen policy. Do not use the PDF's two-year suggestion as a default.
