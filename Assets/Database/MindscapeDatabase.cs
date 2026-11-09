using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Mindscape.Data
{
    // The game's save database. Records go to a SQLite file (mindscape.db in Application.persistentDataPath)
    // and are mirrored to local storage (PlayerPrefs; IndexedDB in WebGL builds). Reads prefer SQLite and fall
    // back to local storage, so the game behaves the same on every platform, including WebGL where SQLite
    // is unavailable. The journal and episode progress belong to the player's account while they are signed in
    // (MindscapeCloud / Supabase), and to this device when they play offline.
    // Everything else kept on the device for one player (their name, story choices, save slot and episode flags)
    // goes under ProfileKey: offline play keeps the original keys and SQLite tables, and each signed-in account
    // gets its own PlayerPrefs keys, so a new account never starts with another player's name or progress.
    // Offline progress is never copied into an account.
    public static class MindscapeDatabase
    {
        public const string FileName = "mindscape.db";
        // The protagonist's name in the written story, used until the player types their own.
        public const string DefaultPlayerName = "Joey";
        const int SchemaVersion = 4;

        static SqliteConnection db;
        static bool initialised;

        public static bool UsingSqlite { get { Init(); return db != null; } }
        public static string DatabasePath => Path.Combine(Application.persistentDataPath, FileName);
        public static string StorageDescription => UsingSqlite ? "SQLite (" + DatabasePath + ")" : "Local storage (PlayerPrefs)";

        static void Init()
        {
            if (initialised) return;
            initialised = true;
            db = SqliteConnection.TryOpen(DatabasePath, out string error);
            if (db == null) { Debug.Log("Mindscape database: using local storage. " + error); return; }
            try
            {
                db.Execute("PRAGMA journal_mode=WAL");
                if (int.TryParse(db.Scalar("PRAGMA user_version"), out int version) && version < SchemaVersion) CreateSchema();
                Application.quitting += Close;
            }
            catch (Exception e) { Fail(e); }
        }

        static void CreateSchema()
        {
            db.Execute(@"CREATE TABLE IF NOT EXISTS players (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                name TEXT NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL)");
            db.Execute(@"CREATE TABLE IF NOT EXISTS choices (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                player_name TEXT NOT NULL,
                chapter INTEGER NOT NULL,
                episode INTEGER NOT NULL,
                choice_key TEXT NOT NULL,
                choice_value TEXT NOT NULL,
                created_at TEXT NOT NULL)");
            db.Execute("CREATE INDEX IF NOT EXISTS choices_lookup ON choices (chapter, choice_key, id)");
            db.Execute(@"CREATE TABLE IF NOT EXISTS journal_entries (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                player_name TEXT NOT NULL,
                chapter INTEGER NOT NULL,
                episode INTEGER NOT NULL,
                prompt TEXT NOT NULL,
                entry TEXT NOT NULL,
                created_at TEXT NOT NULL)");
            db.Execute(@"CREATE TABLE IF NOT EXISTS assessments (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                player_name TEXT NOT NULL,
                chapter INTEGER NOT NULL,
                episode INTEGER NOT NULL DEFAULT 0,
                overwhelm_level INTEGER NOT NULL CHECK (overwhelm_level BETWEEN 1 AND 5),
                coping_strategy TEXT NOT NULL,
                created_at TEXT NOT NULL)");
            db.Execute(@"CREATE TABLE IF NOT EXISTS episode_progress (
                chapter INTEGER NOT NULL,
                episode INTEGER NOT NULL,
                completed INTEGER NOT NULL DEFAULT 0,
                started_at TEXT NOT NULL,
                completed_at TEXT,
                completion_seconds INTEGER,
                PRIMARY KEY (chapter, episode))");
            db.Execute(@"CREATE TABLE IF NOT EXISTS episode_records (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                player_name TEXT NOT NULL,
                chapter INTEGER NOT NULL,
                episode INTEGER NOT NULL,
                result TEXT NOT NULL,
                time_limit INTEGER NOT NULL,
                seconds_left INTEGER NOT NULL,
                checkin_start INTEGER NOT NULL,
                checkin_end INTEGER NOT NULL,
                stress_peak INTEGER NOT NULL,
                stress_final INTEGER NOT NULL,
                created_at TEXT NOT NULL)");
            // Version 4: the self-assessment is asked after every episode, so each answer keeps its episode (0 = older answers).
            if (!HasColumn("assessments", "episode")) db.Execute("ALTER TABLE assessments ADD COLUMN episode INTEGER NOT NULL DEFAULT 0");
            db.Execute("PRAGMA user_version = " + SchemaVersion);
        }

        static bool HasColumn(string table, string column)
        {
            foreach (var row in db.Query("PRAGMA table_info(" + table + ")")) if (row.Length > 1 && row[1] == column) return true;
            return false;
        }

        // A broken database must never block the story: report once, then keep going on local storage.
        static void Fail(Exception e)
        {
            Debug.LogWarning("Mindscape database: SQLite disabled, continuing with local storage. " + e.Message);
            Close();
            initialised = true;   // stay on local storage for the rest of this session
        }

        // Also runs when the editor leaves Play mode (Application.quitting); the next call reopens the file.
        static void Close()
        {
            Application.quitting -= Close;
            db?.Dispose();
            db = null;
            initialised = false;
        }

        static string Now => DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        static void Write(string sql, params object[] args)
        {
            Init();
            if (db == null) return;
            try { db.Execute(sql, args); } catch (Exception e) { Fail(e); }
        }

        static string Read(string sql, params object[] args)
        {
            Init();
            if (db == null) return null;
            try { return db.Scalar(sql, args); } catch (Exception e) { Fail(e); return null; }
        }

        // ---------------------------------------------------------------- profile

        // "Mindscape.X" offline, "Mindscape.Account.<user id>.X" while signed in.
        public static string ProfileKey(string key)
        {
            if (!MindscapeCloud.IsSignedIn) return key;
            string rest = key.StartsWith("Mindscape.", StringComparison.Ordinal) ? key.Substring("Mindscape.".Length) : key;
            return "Mindscape.Account." + MindscapeCloud.AccountId + "." + rest;
        }

        // ---------------------------------------------------------------- player

        const string PlayerNameKey = "Mindscape.PlayerName";

        // The name typed by whoever is playing now: the offline player on this device, or the signed-in account.
        public static string PlayerName
        {
            get
            {
                string name = SavedPlayerName;
                return string.IsNullOrWhiteSpace(name) ? DefaultPlayerName : name;
            }
            set
            {
                string name = Clean(value, 24);
                if (name.Length == 0) name = DefaultPlayerName;
                PlayerPrefs.SetString(ProfileKey(PlayerNameKey), name); PlayerPrefs.Save();
                if (MindscapeCloud.IsSignedIn) return;
                // INSERT OR REPLACE (not UPSERT) so older system SQLite builds on Windows 10 work too.
                Write("INSERT OR REPLACE INTO players (id, name, created_at, updated_at) " +
                      "VALUES (1, ?1, COALESCE((SELECT created_at FROM players WHERE id = 1), ?2), ?2)", name, Now);
            }
        }

        public static bool HasPlayerName => !string.IsNullOrWhiteSpace(SavedPlayerName);

        // The offline player's name lives in SQLite (players, id 1) and PlayerPrefs; an account's only in its own key.
        static string SavedPlayerName
        {
            get
            {
                if (MindscapeCloud.IsSignedIn) return PlayerPrefs.GetString(ProfileKey(PlayerNameKey), "");
                string name = Read("SELECT name FROM players WHERE id = 1");
                return string.IsNullOrWhiteSpace(name) ? PlayerPrefs.GetString(PlayerNameKey, "") : name;
            }
        }

        static readonly Regex StoryName = new Regex(@"\bJoey\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Dialogue written for the default protagonist ("Joey", "JOEY") in scenes, prefabs and older story scripts
        // is shown with the active player's name. Read at display time, so it follows profile and mode changes.
        public static string Personalize(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf(DefaultPlayerName, StringComparison.OrdinalIgnoreCase) < 0) return text;
            string name = PlayerName;
            if (name == DefaultPlayerName) return text;
            return StoryName.Replace(text, m => m.Value == "JOEY" ? name.ToUpperInvariant() : name);
        }

        // ---------------------------------------------------------------- choices

        // Every decision is kept as history in SQLite; the latest value is what the story reads back.
        // A signed-in account keeps only the latest value, in its own keys, so the offline history is never read for it.
        public static void RecordChoice(int chapter, int episode, string key, string value)
        {
            PlayerPrefs.SetString(ProfileKey(ChoiceKey(chapter, key)), value ?? ""); PlayerPrefs.Save();
            if (MindscapeCloud.IsSignedIn) return;
            Write("INSERT INTO choices (player_name, chapter, episode, choice_key, choice_value, created_at) VALUES (?1, ?2, ?3, ?4, ?5, ?6)",
                  PlayerName, chapter, episode, key, value ?? "", Now);
        }

        public static string GetChoice(int chapter, string key, string fallback = "")
        {
            string value = MindscapeCloud.IsSignedIn ? null : Read("SELECT choice_value FROM choices WHERE chapter = ?1 AND choice_key = ?2 ORDER BY id DESC LIMIT 1", chapter, key);
            if (value == null) value = PlayerPrefs.GetString(ProfileKey(ChoiceKey(chapter, key)), null);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        static string ChoiceKey(int chapter, string key) => "Mindscape.C" + chapter + ".Choice." + key;

        // ---------------------------------------------------------------- reflection journal

        public static void SaveJournal(int chapter, int episode, string prompt, string entry)
        {
            entry = Clean(entry, 3000);
            if (MindscapeCloud.IsSignedIn) { MindscapeCloud.SaveJournal(chapter, episode, prompt, entry); return; }
            PlayerPrefs.SetString(JournalKey(chapter, episode), entry); PlayerPrefs.Save();
            Write("INSERT INTO journal_entries (player_name, chapter, episode, prompt, entry, created_at) VALUES (?1, ?2, ?3, ?4, ?5, ?6)",
                  PlayerName, chapter, episode, prompt ?? "", entry, Now);
        }

        // A signed-in player only sees their own account's entries, never another player's from this device.
        public static string LatestJournal(int chapter, int episode)
        {
            if (MindscapeCloud.IsSignedIn) return MindscapeCloud.GetJournal(chapter, episode);
            string entry = Read("SELECT entry FROM journal_entries WHERE chapter = ?1 AND episode = ?2 ORDER BY id DESC LIMIT 1", chapter, episode);
            return entry ?? PlayerPrefs.GetString(JournalKey(chapter, episode), "");
        }

        static string JournalKey(int chapter, int episode) => "Mindscape.C" + chapter + ".E" + episode + ".Journal";

        // ---------------------------------------------------------------- episode progress

        static readonly Dictionary<int, float> episodeStartedAt = new Dictionary<int, float>();

        public static void EpisodeStarted(int chapter, int episode)
        {
            episodeStartedAt[chapter * 100 + episode] = Time.realtimeSinceStartup;
            if (MindscapeCloud.IsSignedIn) { MindscapeCloud.EpisodeStarted(chapter, episode); return; }
            Write("INSERT OR IGNORE INTO episode_progress (chapter, episode, completed, started_at) VALUES (?1, ?2, 0, ?3)", chapter, episode, Now);
            Write("UPDATE episode_progress SET started_at = ?3 WHERE chapter = ?1 AND episode = ?2 AND completed = 0", chapter, episode, Now);
        }

        // Play time runs from EpisodeStarted. The first finish keeps its date and play time; replays do not overwrite them.
        public static void EpisodeCompleted(int chapter, int episode)
        {
            int key = chapter * 100 + episode;
            int seconds = episodeStartedAt.TryGetValue(key, out float started) ? Mathf.RoundToInt(Time.realtimeSinceStartup - started) : 0;
            episodeStartedAt.Remove(key);
            if (MindscapeCloud.IsSignedIn) { MindscapeCloud.EpisodeCompleted(chapter, episode, seconds); return; }
            string prefix = "Mindscape.C" + chapter + ".E" + episode + ".";
            if (!PlayerPrefs.HasKey(prefix + "CompletedAt")) { PlayerPrefs.SetString(prefix + "CompletedAt", Now); PlayerPrefs.SetInt(prefix + "Seconds", seconds); PlayerPrefs.Save(); }
            Write("INSERT OR IGNORE INTO episode_progress (chapter, episode, completed, started_at) VALUES (?1, ?2, 0, ?3)", chapter, episode, Now);
            Write("UPDATE episode_progress SET completed = 1, completed_at = COALESCE(completed_at, ?3), completion_seconds = COALESCE(completion_seconds, ?4) " +
                  "WHERE chapter = ?1 AND episode = ?2", chapter, episode, Now, seconds);
        }

        // ---------------------------------------------------------------- self-reflection assessment

        // Like the journal, a signed-in player's answers belong to their account; offline they stay on this device.
        public static void SaveAssessment(int chapter, int episode, int overwhelmLevel, string copingStrategy)
        {
            var record = new AssessmentRecord { chapter = chapter, episode = episode, overwhelm_level = Mathf.Clamp(overwhelmLevel, 1, 5), coping_strategy = copingStrategy ?? "", created_at = Now };
            if (MindscapeCloud.IsSignedIn) { MindscapeCloud.SaveAssessment(record); return; }
            PlayerPrefs.SetInt("Mindscape.C" + chapter + ".Assessment.Overwhelm", record.overwhelm_level);
            PlayerPrefs.SetString("Mindscape.C" + chapter + ".Assessment.Strategy", record.coping_strategy);
            AppendLocal(AssessmentsKey, record);
            Write("INSERT INTO assessments (player_name, chapter, episode, overwhelm_level, coping_strategy, created_at) VALUES (?1, ?2, ?3, ?4, ?5, ?6)",
                  PlayerName, chapter, episode, record.overwhelm_level, record.coping_strategy, record.created_at);
        }

        // Newest first.
        public static List<AssessmentRecord> GetAssessments()
        {
            if (MindscapeCloud.IsSignedIn) return MindscapeCloud.Assessments;
            var rows = Rows("SELECT chapter, episode, overwhelm_level, coping_strategy, created_at FROM assessments ORDER BY id DESC LIMIT 100");
            if (rows == null) return ReadLocal<AssessmentRecord>(AssessmentsKey);
            var list = new List<AssessmentRecord>();
            foreach (var r in rows) list.Add(new AssessmentRecord { chapter = Int(r[0]), episode = Int(r[1]), overwhelm_level = Int(r[2]), coping_strategy = r[3] ?? "", created_at = r[4] ?? "" });
            return list;
        }

        // ---------------------------------------------------------------- per-episode stress and time

        public static void SaveEpisodeRecord(EpisodeRecord record)
        {
            record.created_at = Now;
            if (MindscapeCloud.IsSignedIn) { MindscapeCloud.SaveEpisodeRecord(record); return; }
            AppendLocal(RecordsKey, record);
            Write("INSERT INTO episode_records (player_name, chapter, episode, result, time_limit, seconds_left, checkin_start, checkin_end, stress_peak, stress_final, created_at) " +
                  "VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10, ?11)",
                  PlayerName, record.chapter, record.episode, record.result ?? "", record.time_limit, record.seconds_left,
                  record.checkin_start, record.checkin_end, record.stress_peak, record.stress_final, record.created_at);
        }

        // Newest first.
        public static List<EpisodeRecord> GetEpisodeRecords()
        {
            if (MindscapeCloud.IsSignedIn) return MindscapeCloud.EpisodeRecords;
            var rows = Rows("SELECT chapter, episode, result, time_limit, seconds_left, checkin_start, checkin_end, stress_peak, stress_final, created_at FROM episode_records ORDER BY id DESC LIMIT 200");
            if (rows == null) return ReadLocal<EpisodeRecord>(RecordsKey);
            var list = new List<EpisodeRecord>();
            foreach (var r in rows)
                list.Add(new EpisodeRecord { chapter = Int(r[0]), episode = Int(r[1]), result = r[2] ?? "", time_limit = Int(r[3]), seconds_left = Int(r[4]),
                    checkin_start = Int(r[5]), checkin_end = Int(r[6]), stress_peak = Int(r[7]), stress_final = Int(r[8]), created_at = r[9] ?? "" });
            return list;
        }

        // ---------------------------------------------------------------- helpers

        const string AssessmentsKey = "Mindscape.Assessments", RecordsKey = "Mindscape.EpisodeRecords";

        [Serializable] class LocalList<T> { public List<T> items = new List<T>(); }

        // Local storage keeps the newest 100 entries (it is the only copy in WebGL builds).
        static void AppendLocal<T>(string key, T item)
        {
            var list = ReadLocal<T>(key);
            list.Insert(0, item);
            if (list.Count > 100) list.RemoveRange(100, list.Count - 100);
            PlayerPrefs.SetString(key, JsonUtility.ToJson(new LocalList<T> { items = list })); PlayerPrefs.Save();
        }

        static List<T> ReadLocal<T>(string key)
        {
            string json = PlayerPrefs.GetString(key, "");
            if (json.Length == 0) return new List<T>();
            try { return JsonUtility.FromJson<LocalList<T>>(json)?.items ?? new List<T>(); } catch (ArgumentException) { return new List<T>(); }
        }

        static List<string[]> Rows(string sql, params object[] args)
        {
            Init();
            if (db == null) return null;
            try { return db.Query(sql, args); } catch (Exception e) { Fail(e); return null; }
        }

        static int Int(string value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;

        public static string Clean(string value, int max)
        {
            value = (value ?? "").Trim();
            return value.Length > max ? value.Substring(0, max) : value;
        }
    }

    // episode is 0 for answers saved before the assessment moved to the end of every episode.
    [Serializable] public class AssessmentRecord { public int chapter, episode, overwhelm_level; public string coping_strategy, created_at; }

    // One episode run: how it ended (completed | time_up), the time limit and what was left of it, the stress
    // check-ins before and after (1-5, 0 = not asked), and the meter's peak and final value (0-100).
    [Serializable]
    public class EpisodeRecord
    {
        public int chapter, episode, time_limit, seconds_left, checkin_start, checkin_end, stress_peak, stress_final;
        public string result, created_at;
    }
}
