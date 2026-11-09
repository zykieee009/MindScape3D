using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Mindscape.Data
{
    // Online accounts, episode progress and the reflection journal, kept in Supabase. Accounts use Supabase Auth
    // (username + email + password, confirmed with an emailed OTP) through the "account" edge function, which also
    // lets players log in or reset their password by username. The game's data calls are RPC functions
    // (migrations "mindscape_players_progress_journal", "mindscape_assessments_episode_records", "mindscape_supabase_auth"
    // and "mindscape_assessment_episode", in supabase/migrations); the tables themselves are closed to the publishable key. The auth session
    // (access + refresh token) is kept in PlayerPrefs and refreshed before it expires.
    // Deleting an account (migration "mindscape_account_archive") archives it for 30 days: it is signed out everywhere
    // and emailed, and can be recovered from the login screen until then; after that the server deletes it for good.
    // Writes are fire-and-forget so a slow or missing connection never blocks the story; the local database
    // MindscapeDatabase decides per call: signed in -> here, playing offline -> SQLite on this device.
    public static class MindscapeCloud
    {
        const string RpcUrl = "https://ugbmucataypgayosjtwl.supabase.co/rest/v1/rpc/";
        const string AccountUrl = "https://ugbmucataypgayosjtwl.supabase.co/functions/v1/account";
        const string PublishableKey = "sb_publishable_lT4K-TwbVL2Frcn47GDRPA_U15ZDXq4";
        const string AccessKey = "Mindscape.Cloud.Access", RefreshKey = "Mindscape.Cloud.Refresh", ExpiryKey = "Mindscape.Cloud.Expiry";
        const string LegacyTokenKey = "Mindscape.Cloud.Token"; // the old username/password session, now ignored
        const string UsernameKey = "Mindscape.Cloud.Username", OfflineKey = "Mindscape.Cloud.Offline", AccountIdKey = "Mindscape.Cloud.AccountId";
        const string SessionExpired = "28000", EmailNotConfirmed = "email_not_confirmed";

        static readonly Dictionary<int, string> journal = new Dictionary<int, string>();
        static readonly Dictionary<int, ProgressRow> progress = new Dictionary<int, ProgressRow>();
        static readonly List<AssessmentRecord> assessments = new List<AssessmentRecord>();
        static readonly List<EpisodeRecord> records = new List<EpisodeRecord>();

        public static string Username => PlayerPrefs.GetString(UsernameKey, "");
        // The signed-in account's Supabase user id (the access token's "sub"). Device-side saves for an account are
        // kept under it (MindscapeDatabase.ProfileKey), so one player's name and save slot never show up for another.
        public static string AccountId
        {
            get
            {
                if (!IsSignedIn) return "";
                string id = PlayerPrefs.GetString(AccountIdKey, "");
                if (id.Length > 0) return id;
                id = Subject(Token);   // a session stored before account ids were kept
                if (id.Length == 0) return Username.ToLowerInvariant();
                PlayerPrefs.SetString(AccountIdKey, id); PlayerPrefs.Save();
                return id;
            }
        }
        static string Token => PlayerPrefs.GetString(AccessKey, "");
        public static bool IsSignedIn => PlayerPrefs.GetString(RefreshKey, "").Length > 0;
        // The player chose "Play offline" on the login page; it is remembered until they log in.
        public static bool IsOffline => !IsSignedIn && PlayerPrefs.GetInt(OfflineKey, 0) == 1;
        // True once the signed-in player's journal and progress have been downloaded.
        public static bool IsLoaded { get; private set; }
        public static event Action Changed;

        static int Key(int chapter, int episode) => chapter * 100 + episode;

        // ---------------------------------------------------------------- accounts

        // Each returns null on success, or a message to show the player.

        // Sign-up rules, the same ones the account function enforces: Gmail or Yahoo only, and a strong password
        // (8-72 characters with a lower case letter, an upper case letter, a number and a symbol).
        public const string PasswordRule = "Use 8-72 characters with an upper case letter, a lower case letter, a number and a symbol (like ! or #).";
        public const string EmailDomainRule = "Only Gmail and Yahoo addresses are accepted (for example name@gmail.com or name@yahoo.com).";
        static readonly System.Text.RegularExpressions.Regex StrongPassword =
            new System.Text.RegularExpressions.Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,72}$");
        static readonly System.Text.RegularExpressions.Regex AllowedEmail = new System.Text.RegularExpressions.Regex(
            @"^[^\s@]+@(gmail\.com|googlemail\.com|yahoo\.com|yahoo\.com\.[a-z]{2}|yahoo\.co\.[a-z]{2}|yahoo\.[a-z]{2})$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        public static bool IsStrongPassword(string password) => StrongPassword.IsMatch(password ?? "");
        public static bool IsAllowedEmail(string email) => AllowedEmail.IsMatch((email ?? "").Trim());

        // Sign-up: emails a one-time code; the account exists once VerifySignup accepts it.
        // When the username or the email is already used, SignupField says which ("username" or "email").
        public static string SignupField { get; private set; }
        public static async Awaitable<string> SignUp(string username, string email, string password)
        {
            SignupField = null;
            var reply = await AccountCall(new AccountBody { action = "signup", username = username, email = email, password = password });
            if (!reply.ok) SignupField = reply.code == "username_unavailable" ? "username" : reply.code == "email_unavailable" ? "email" : null;
            return reply.ok ? null : reply.error;
        }

        // Asks the server whether a username and/or email can be used for a new account (pass "" to skip one).
        // Each message is null when that value is fine, or when the server could not be reached (sign-up checks again).
        public static async Awaitable<(string username, string email)> CheckAvailable(string username, string email)
        {
            var reply = await AccountCall(new AccountBody { action = "check_available", username = username ?? "", email = email ?? "" });
            if (!reply.ok) return (null, null);
            var found = JsonUtility.FromJson<AvailabilityReply>(reply.body);
            return (string.IsNullOrEmpty(found.username_error) ? null : found.username_error, string.IsNullOrEmpty(found.email_error) ? null : found.email_error);
        }

        // ---- Continue with Google
        // StartGoogle opens the account function's sign-in page, which runs the Google sign-in and keeps the session
        // under a random state only this game knows; WaitForGoogle polls for it. On Android (Custom Tab) and macOS (the
        // sign-in sheet) it opens inside the app and comes back by itself (GoogleSignInSheet); elsewhere in the browser.
        // A first-time Google player then picks a username (GoogleNeedsUsername, with GoogleSuggestion) and
        // ChooseGoogleUsername creates the account.
        static string googleState;
        static AuthSession googleSession;
        static int googleAttempt;
        public static bool GoogleNeedsUsername => googleSession != null;
        public static string GoogleSuggestion { get; private set; }

        public static void StartGoogle()
        {
            var bytes = new byte[32];
            using (var random = System.Security.Cryptography.RandomNumberGenerator.Create()) random.GetBytes(bytes);
            googleState = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            googleSession = null; GoogleSuggestion = null; googleAttempt++;
            OpenGoogleAgain();
        }
        // The same sign-in, in a new browser tab (when the first one was closed or blocked).
        public static void OpenGoogleAgain()
        {
            if (googleState == null) return;
            GoogleSignInSheet.Open(AccountUrl + "/google/start/" + googleState + (GoogleSignInSheet.InApp ? "?app=1" : ""));
        }

        // Waits (up to 5 minutes) for the browser part. Returns null once signed in or once a username is needed,
        // "" when cancelled, or a message to show.
        public static async Awaitable<string> WaitForGoogle()
        {
            int attempt = googleAttempt;
            string state = googleState;
            float giveUpAt = Time.realtimeSinceStartup + 300f;
            while (true)
            {
                // Every 2 seconds, or at once when the sign-in page has come back to the game.
                float next = Time.realtimeSinceStartup + 2f;
                while (Time.realtimeSinceStartup < next && !GoogleSignInSheet.Returned)
                {
                    await Awaitable.NextFrameAsync();
                    if (attempt != googleAttempt) return "";
                    // The macOS sheet closed without signing in: one last look, then stop waiting.
                    if (GoogleSignInSheet.Dismissed) { next = 0; break; }
                }
                bool dismissed = GoogleSignInSheet.Dismissed;
                GoogleSignInSheet.ClearReturned();
                if (Time.realtimeSinceStartup > giveUpAt) { googleState = null; return "Google sign-in timed out. Try again."; }
                var reply = await AccountCall(new AccountBody { action = "google_poll", state = state });
                if (attempt != googleAttempt) return "";
                if (!reply.ok)
                {
                    if (reply.status == 0 || reply.status >= 500)   // a dropped connection: keep waiting
                    {
                        if (dismissed) { googleState = null; return "Google sign-in was closed before it finished."; }
                        continue;
                    }
                    googleState = null;
                    return reply.error;
                }
                var found = JsonUtility.FromJson<GoogleReply>(reply.body);
                if (found.pending)
                {
                    if (dismissed) { googleState = null; GoogleSignInSheet.Close(); return "Google sign-in was closed before it finished."; }
                    continue;
                }
                googleState = null;
                GoogleSignInSheet.Close();
                if (found.need_username) { googleSession = found.session; GoogleSuggestion = found.suggestion ?? ""; return null; }
                return await Establish(reply);
            }
        }

        public static async Awaitable<string> ChooseGoogleUsername(string username)
        {
            if (googleSession == null) return "Google sign-in expired. Try again.";
            var reply = await AccountCall(new AccountBody { action = "google_username", username = username }, googleSession.access_token);
            if (!reply.ok)
            {
                if (reply.code == SessionExpired) googleSession = null;
                return reply.error;
            }
            var session = googleSession;
            googleSession = null;
            session.username = JsonUtility.FromJson<UsernameReply>(reply.body).username;
            Store(session);
            return await Refresh();
        }

        public static void CancelGoogle() { googleAttempt++; googleState = null; googleSession = null; GoogleSignInSheet.Close(); }

        public static async Awaitable<string> ResendSignup(string email)
        {
            var reply = await AccountCall(new AccountBody { action = "resend_signup", email = email });
            return reply.ok ? null : reply.error;
        }

        public static async Awaitable<string> VerifySignup(string email, string code) =>
            await Establish(await AccountCall(new AccountBody { action = "verify_signup", email = email, code = code }));

        // Set when Login fails because the account's email was never confirmed (so the page can offer the code).
        public static string UnverifiedEmail { get; private set; }

        // The identifier is a username or an email.
        public static async Awaitable<string> Login(string identifier, string password)
        {
            UnverifiedEmail = null;
            var reply = await AccountCall(new AccountBody { action = "login", identifier = identifier, password = password });
            if (!reply.ok && reply.code == EmailNotConfirmed) UnverifiedEmail = reply.email;
            return await Establish(reply);
        }

        static async Awaitable<string> Establish(Reply reply)
        {
            if (!reply.ok) return reply.error;
            Store(JsonUtility.FromJson<SessionReply>(reply.body).session);
            return await Refresh();
        }

        // Password reset: RequestReset emails a code, VerifyReset checks it, SetNewPassword then signs the player in.
        // Between the last two the short-lived reset session is only held in memory.
        static AuthSession resetSession;

        public static async Awaitable<string> RequestReset(string identifier)
        {
            resetSession = null;
            var reply = await AccountCall(new AccountBody { action = "recover", identifier = identifier });
            return reply.ok ? null : reply.error;
        }

        public static async Awaitable<string> VerifyReset(string identifier, string code)
        {
            var reply = await AccountCall(new AccountBody { action = "verify_recovery", identifier = identifier, code = code });
            if (!reply.ok) return reply.error;
            resetSession = JsonUtility.FromJson<SessionReply>(reply.body).session;
            return null;
        }

        public static async Awaitable<string> SetNewPassword(string password)
        {
            if (resetSession == null) return "Your reset code has expired. Start again.";
            var reply = await AccountCall(new AccountBody { action = "set_password", password = password }, resetSession.access_token);
            if (!reply.ok)
            {
                if (reply.code == SessionExpired) resetSession = null;
                return reply.error;
            }
            var session = resetSession;
            resetSession = null;
            session.username = JsonUtility.FromJson<UsernameReply>(reply.body).username;
            Store(session);
            return await Refresh();
        }

        public static void CancelReset() => resetSession = null;

        // Delete account: archives it for 30 days and signs out. DeletedUntil is when it is permanently deleted.
        public static DateTime DeletedUntil { get; private set; }

        public static async Awaitable<string> DeleteAccount()
        {
            if (!IsSignedIn) return "Not signed in.";
            await EnsureFresh();
            var reply = await AccountCall(new AccountBody { action = "delete_account" }, Token);
            if (!reply.ok) { if (reply.code == SessionExpired) ClearSession(); return reply.error; }
            DeletedUntil = When(JsonUtility.FromJson<DeleteReply>(reply.body).delete_after);
            ClearSession();   // the server already signed this account out everywhere
            return null;
        }

        // Recover a deleted account: RequestRecovery emails a code, VerifyRecovery checks it and fills in what is about
        // to come back (so the player can confirm), RecoverAccount sets the new password, restores and signs in.
        public static string RecoveryUsername { get; private set; }
        public static DateTime RecoveryDeletedOn { get; private set; }
        public static DateTime RecoveryDeleteAfter { get; private set; }

        public static async Awaitable<string> RequestRecovery(string email)
        {
            resetSession = null;
            var reply = await AccountCall(new AccountBody { action = "recover_start", email = email });
            return reply.ok ? null : reply.error;
        }

        public static async Awaitable<string> VerifyRecovery(string email, string code)
        {
            var reply = await AccountCall(new AccountBody { action = "recover_verify", email = email, code = code });
            if (!reply.ok) return reply.error;
            var found = JsonUtility.FromJson<RecoveryReply>(reply.body);
            resetSession = found.session;
            RecoveryUsername = found.username;
            RecoveryDeletedOn = When(found.archived_at); RecoveryDeleteAfter = When(found.delete_after);
            return null;
        }

        public static async Awaitable<string> RecoverAccount(string password)
        {
            if (resetSession == null) return "Your recovery code has expired. Start again.";
            var reply = await AccountCall(new AccountBody { action = "recover_account", password = password }, resetSession.access_token);
            if (!reply.ok)
            {
                if (reply.code == SessionExpired) resetSession = null;
                return reply.error;
            }
            var session = resetSession;
            resetSession = null;
            session.username = JsonUtility.FromJson<UsernameReply>(reply.body).username;
            Store(session);
            return await Refresh();
        }

        // A server timestamp in the player's local time (MinValue if unreadable).
        static DateTime When(string iso) =>
            DateTime.TryParse(iso, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out var t) ? t.ToLocalTime() : DateTime.MinValue;

        static void Store(AuthSession session)
        {
            PlayerPrefs.SetString(AccessKey, session.access_token);
            PlayerPrefs.SetString(RefreshKey, session.refresh_token);
            PlayerPrefs.SetString(ExpiryKey, (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + session.expires_in).ToString());
            if (!string.IsNullOrEmpty(session.username)) PlayerPrefs.SetString(UsernameKey, session.username);
            string id = Subject(session.access_token);
            if (id.Length > 0) PlayerPrefs.SetString(AccountIdKey, id);
            PlayerPrefs.DeleteKey(OfflineKey); PlayerPrefs.DeleteKey(LegacyTokenKey); PlayerPrefs.Save();
        }

        // Trades the refresh token for a new access token. A refused refresh token ends the session.
        static bool refreshing;
        static async Awaitable<bool> RefreshSession()
        {
            if (refreshing) { while (refreshing) await Awaitable.NextFrameAsync(); return IsSignedIn; }
            refreshing = true;
            try
            {
                var reply = await AccountCall(new AccountBody { action = "refresh", refresh_token = PlayerPrefs.GetString(RefreshKey, "") });
                if (reply.ok)
                {
                    var session = JsonUtility.FromJson<SessionReply>(reply.body).session;
                    session.username = null; // keeps the stored name
                    Store(session);
                    return true;
                }
                if (reply.code == SessionExpired) ClearSession();
                return false;
            }
            finally { refreshing = false; }
        }

        // Called before each data request so a long play session never runs on an expired access token.
        static async Awaitable EnsureFresh()
        {
            if (!IsSignedIn) return;
            long.TryParse(PlayerPrefs.GetString(ExpiryKey, "0"), out long expires);
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= expires - 60) await RefreshSession();
        }

        public static void PlayOffline()
        {
            if (IsSignedIn) SignOut();
            PlayerPrefs.SetInt(OfflineKey, 1); PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // Also ends offline play, so the login page comes back.
        public static void SignOut()
        {
            if (IsSignedIn) SendLogout(Token);
            ClearSession();
        }

        static void ClearSession()
        {
            PlayerPrefs.DeleteKey(AccessKey); PlayerPrefs.DeleteKey(RefreshKey); PlayerPrefs.DeleteKey(ExpiryKey); PlayerPrefs.DeleteKey(LegacyTokenKey);
            PlayerPrefs.DeleteKey(UsernameKey); PlayerPrefs.DeleteKey(OfflineKey); PlayerPrefs.DeleteKey(AccountIdKey); PlayerPrefs.Save();
            resetSession = null;
            journal.Clear(); progress.Clear(); assessments.Clear(); records.Clear(); IsLoaded = false;
            Changed?.Invoke();
        }

        // Downloads the player's journal and episode progress. Returns null on success.
        public static async Awaitable<string> Refresh()
        {
            if (!IsSignedIn) return "Not signed in.";
            string token = JsonUtility.ToJson(new TokenOnly { p_token = Token });
            var journalReply = await Call("get_journal", token);
            if (!journalReply.ok) return journalReply.error;
            var progressReply = await Call("get_progress", token);
            if (!progressReply.ok) return progressReply.error;
            var assessmentReply = await Call("get_assessments", token);
            if (!assessmentReply.ok) return assessmentReply.error;
            var recordReply = await Call("get_episode_records", token);
            if (!recordReply.ok) return recordReply.error;
            journal.Clear(); progress.Clear(); assessments.Clear(); records.Clear();
            foreach (var row in JsonUtility.FromJson<JournalRows>(Wrap(journalReply.body)).items) journal[Key(row.chapter, row.episode)] = row.entry;
            foreach (var row in JsonUtility.FromJson<ProgressRows>(Wrap(progressReply.body)).items) progress[Key(row.chapter, row.episode)] = row;
            assessments.AddRange(JsonUtility.FromJson<AssessmentRows>(Wrap(assessmentReply.body)).items);
            records.AddRange(JsonUtility.FromJson<RecordRows>(Wrap(recordReply.body)).items);
            IsLoaded = true;
            Changed?.Invoke();
            return null;
        }

        // ---------------------------------------------------------------- episode progress

        public static void EpisodeStarted(int chapter, int episode)
        {
            if (!IsSignedIn) return;
            Send("start_episode", JsonUtility.ToJson(new EpisodeCall { p_token = Token, p_chapter = chapter, p_episode = episode }));
        }

        // The server keeps the first finish and ignores replays.
        public static void EpisodeCompleted(int chapter, int episode, int seconds)
        {
            if (!IsSignedIn) return;
            int key = Key(chapter, episode);
            if (!progress.TryGetValue(key, out var row) || !row.completed)
                progress[key] = new ProgressRow { chapter = chapter, episode = episode, completed = true, completed_at = DateTime.UtcNow.ToString("o"), completion_seconds = seconds };
            Send("complete_episode", JsonUtility.ToJson(new EpisodeCall { p_token = Token, p_chapter = chapter, p_episode = episode, p_seconds = seconds }));
        }

        public static bool IsComplete(int chapter, int episode) => progress.TryGetValue(Key(chapter, episode), out var row) && row.completed;
        public static IEnumerable<ProgressRow> Progress => progress.Values;

        // ---------------------------------------------------------------- reflection journal

        public static void SaveJournal(int chapter, int episode, string prompt, string entry)
        {
            if (!IsSignedIn) return;
            journal[Key(chapter, episode)] = entry ?? "";
            Send("save_journal", JsonUtility.ToJson(new JournalCall { p_token = Token, p_chapter = chapter, p_episode = episode, p_prompt = prompt ?? "", p_entry = entry ?? "" }));
        }

        public static string GetJournal(int chapter, int episode) => journal.TryGetValue(Key(chapter, episode), out string entry) ? entry : "";

        // ---------------------------------------------------------------- self-reflection and episode records

        public static List<AssessmentRecord> Assessments => new List<AssessmentRecord>(assessments);
        public static List<EpisodeRecord> EpisodeRecords => new List<EpisodeRecord>(records);

        public static void SaveAssessment(AssessmentRecord record)
        {
            if (!IsSignedIn) return;
            assessments.Insert(0, record);
            SendAssessment(record);
        }

        // save_episode_assessment keeps the episode. A server without it yet (404) still gets the answer, chapter only.
        static async void SendAssessment(AssessmentRecord record)
        {
            var reply = await Call("save_episode_assessment", JsonUtility.ToJson(new EpisodeAssessmentCall
            {
                p_token = Token, p_chapter = record.chapter, p_episode = record.episode, p_overwhelm_level = record.overwhelm_level, p_coping_strategy = record.coping_strategy ?? ""
            }));
            if (!reply.ok && reply.status == 404)
                reply = await Call("save_assessment", JsonUtility.ToJson(new AssessmentCall { p_token = Token, p_chapter = record.chapter, p_overwhelm_level = record.overwhelm_level, p_coping_strategy = record.coping_strategy ?? "" }));
            if (!reply.ok) Debug.LogWarning("Mindscape cloud: save_assessment failed. " + reply.error);
        }

        public static void SaveEpisodeRecord(EpisodeRecord record)
        {
            if (!IsSignedIn) return;
            records.Insert(0, record);
            Send("save_episode_record", JsonUtility.ToJson(new RecordCall
            {
                p_token = Token, p_chapter = record.chapter, p_episode = record.episode, p_result = record.result, p_time_limit = record.time_limit,
                p_seconds_left = record.seconds_left, p_checkin_start = record.checkin_start, p_checkin_end = record.checkin_end,
                p_stress_peak = record.stress_peak, p_stress_final = record.stress_final
            }));
        }

        // ---------------------------------------------------------------- transport

        static async void Send(string function, string json)
        {
            var reply = await Call(function, json);
            if (!reply.ok) Debug.LogWarning("Mindscape cloud: " + function + " failed. " + reply.error);
        }

        static async void SendLogout(string accessToken)
        {
            await Post(AccountUrl, JsonUtility.ToJson(new AccountBody { action = "logout" }), accessToken);
        }

        // A data RPC, as the signed-in player. An expired access token is refreshed once and the call retried.
        static async Awaitable<Reply> Call(string function, string json)
        {
            await EnsureFresh();
            var reply = await Post(RpcUrl + function, json, Token);
            if (!reply.ok && reply.status == 401 && await RefreshSession()) reply = await Post(RpcUrl + function, json, Token);
            if (!reply.ok && reply.code == SessionExpired) ClearSession();
            return reply;
        }

        // An account action on the edge function. The bearer is the publishable key unless a reset session is given.
        static Awaitable<Reply> AccountCall(AccountBody body, string bearer = null) =>
            Post(AccountUrl, JsonUtility.ToJson(body), string.IsNullOrEmpty(bearer) ? PublishableKey : bearer);

        static async Awaitable<Reply> Post(string url, string json, string bearer)
        {
            using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)) { contentType = "application/json" };
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("apikey", PublishableKey);
                request.SetRequestHeader("Authorization", "Bearer " + bearer);
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 20;
                await request.SendWebRequest();
                string body = request.downloadHandler.text;
                if (request.result == UnityWebRequest.Result.Success) return new Reply { ok = true, body = body, status = request.responseCode };
                ErrorBody error = null;
                try { if (!string.IsNullOrEmpty(body)) error = JsonUtility.FromJson<ErrorBody>(body); } catch (ArgumentException) { }
                // 4xx replies carry the server's own message (wrong password, name taken, ...).
                bool fromServer = error != null && !string.IsNullOrEmpty(error.message) && request.responseCode >= 400 && request.responseCode < 500;
                return new Reply
                {
                    ok = false, status = request.responseCode, code = error?.code, email = error?.email,
                    error = fromServer ? error.message : "Could not reach the server. Check your connection and try again."
                };
            }
        }

        // The user id inside a Supabase access token (a JWT: header.claims.signature, base64url). Empty if unreadable.
        static string Subject(string jwt)
        {
            string[] parts = (jwt ?? "").Split('.');
            if (parts.Length < 2) return "";
            try
            {
                string claims = parts[1].Replace('-', '+').Replace('_', '/');
                claims = claims.PadRight(claims.Length + (4 - claims.Length % 4) % 4, '=');
                return JsonUtility.FromJson<Claims>(Encoding.UTF8.GetString(Convert.FromBase64String(claims)))?.sub ?? "";
            }
            catch (Exception) { return ""; }
        }

        // JsonUtility cannot read a top-level array.
        static string Wrap(string array) => "{\"items\":" + (string.IsNullOrEmpty(array) ? "[]" : array) + "}";

        struct Reply { public bool ok; public string body, error, code, email; public long status; }
        [Serializable] class AccountBody { public string action, username, email, password, identifier, code, refresh_token, state; }
        [Serializable] class AvailabilityReply { public string username_error, email_error; }
        [Serializable] class GoogleReply { public bool pending, need_username; public string suggestion; public AuthSession session; }
        [Serializable] class TokenOnly { public string p_token; }
        [Serializable] class EpisodeCall { public string p_token; public int p_chapter, p_episode, p_seconds; }
        [Serializable] class JournalCall { public string p_token; public int p_chapter, p_episode; public string p_prompt, p_entry; }
        [Serializable] class AuthSession { public string access_token, refresh_token, username; public int expires_in; }
        [Serializable] class SessionReply { public AuthSession session; }
        [Serializable] class UsernameReply { public string username; }
        [Serializable] class DeleteReply { public string delete_after; }
        [Serializable] class RecoveryReply { public AuthSession session; public string username, archived_at, delete_after; }
        [Serializable] class Claims { public string sub; }
        [Serializable] class ErrorBody { public string code, message, email; }
        [Serializable] class JournalRow { public int chapter, episode; public string prompt, entry, updated_at; }
        [Serializable] class JournalRows { public JournalRow[] items; }
        [Serializable] class ProgressRows { public ProgressRow[] items; }
        [Serializable] class AssessmentCall { public string p_token; public int p_chapter, p_overwhelm_level; public string p_coping_strategy; }
        [Serializable] class EpisodeAssessmentCall { public string p_token; public int p_chapter, p_episode, p_overwhelm_level; public string p_coping_strategy; }
        [Serializable] class RecordCall { public string p_token, p_result; public int p_chapter, p_episode, p_time_limit, p_seconds_left, p_checkin_start, p_checkin_end, p_stress_peak, p_stress_final; }
        [Serializable] class AssessmentRows { public AssessmentRecord[] items; }
        [Serializable] class RecordRows { public EpisodeRecord[] items; }
        [Serializable] public class ProgressRow { public int chapter, episode, completion_seconds; public bool completed; public string started_at, completed_at; }
    }
}
