using System;
using System.Globalization;
using UnityEngine;

namespace Mindscape.Data
{
    // Three save slots per player: the offline player on this device, and each signed-in account
    // (MindscapeDatabase.ProfileKey). Saving from the pause menu keeps the episode and every step taken in it
    // (AcademicEpisode.CaptureSave); Continue on the main menu loads a slot, and the episode replays those steps and puts
    // the player back where they saved. Slots stay on this device and can be deleted from Continue.
    public static class MindscapeSaves
    {
        public const int SlotCount = 3;

        [Serializable]
        public class Slot
        {
            public int chapter, episode;   // the story episode (1-based), not the menu's save convention
            public string playerName, savedAt;
            // Where the story was: the steps taken (null in saves from before this, which restart the episode),
            // whether the player was exploring, where they stood, and the stress and time left.
            public int version;   // 2: has the steps below
            public string[] trail;
            public bool roaming;
            public float x, y, z, yaw, stress, stressPeak, timeLeft;
        }

        // The slot the main menu is loading; the episode takes it once its scene has started.
        static Slot pending;
        public static void SetPending(Slot slot) => pending = slot;
        public static Slot TakePending(int chapter, int episode)
        {
            var slot = pending; pending = null;
            return slot != null && slot.chapter == chapter && slot.episode == episode ? slot : null;
        }

        static string Key(int index) => MindscapeDatabase.ProfileKey("Mindscape.SaveSlot" + (index + 1));

        // Null for an empty slot.
        public static Slot Get(int index)
        {
            if (index < 0 || index >= SlotCount) return null;
            string json = PlayerPrefs.GetString(Key(index), "");
            if (json.Length == 0) return null;
            try { var slot = JsonUtility.FromJson<Slot>(json); return slot != null && slot.chapter > 0 && slot.episode > 0 ? slot : null; }
            catch (ArgumentException) { return null; }
        }

        public static void Save(int index, int chapter, int episode, Action<Slot> capture = null)
        {
            if (index < 0 || index >= SlotCount) return;
            var slot = new Slot { chapter = chapter, episode = episode, playerName = MindscapeDatabase.PlayerName, savedAt = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) };
            if (capture != null) { capture(slot); slot.version = 2; }
            PlayerPrefs.SetString(Key(index), JsonUtility.ToJson(slot)); PlayerPrefs.Save();
        }

        public static void Delete(int index) { PlayerPrefs.DeleteKey(Key(index)); PlayerPrefs.Save(); }

        // A save made where the player was (not just "this episode"): loading it resumes there.
        public static bool Resumes(Slot slot) => slot != null && slot.version >= 2 && slot.trail != null;

        public static bool HasAny { get { for (int i = 0; i < SlotCount; i++) if (Get(i) != null) return true; return false; } }

        // "1 Nov 2026, 9:05 PM" in the player's local time.
        public static string When(Slot slot) =>
            DateTime.TryParse(slot.savedAt, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t)
                ? t.ToLocalTime().ToString("d MMM yyyy, h:mm tt", CultureInfo.InvariantCulture) : "";
    }
}
