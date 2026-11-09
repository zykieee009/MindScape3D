using UnityEngine;

namespace Mindscape.Data
{
    public static class MindscapeProgress
    {
        public static readonly int[] EpisodeCounts = { 4, 5, 8 };
        public static int ChapterCount => EpisodeCounts.Length;
        public static int TotalEpisodes { get { int n = 0; foreach (int c in EpisodeCounts) n += c; return n; } }
        public static int EpisodeCount(int chapter) => chapter >= 1 && chapter <= ChapterCount ? EpisodeCounts[chapter - 1] : 0;

        static string SaveKey(int chapter, int episode) => MindscapeDatabase.ProfileKey(
            chapter == 1 ? "Mindscape.AcademicPressure.Episode" + episode :
            chapter == 2 ? "Mindscape.FamilyExpectations.Episode" + episode :
            "Mindscape.SocialDynamics.Episode" + episode);

        // The Continue slot (episode 1 = tutorial, story episodes start at 2) and the furthest chapter started,
        // kept per player like every other save on this device.
        public static string ChapterKey => MindscapeDatabase.ProfileKey("Mindscape.Chapter");
        public static string EpisodeKey => MindscapeDatabase.ProfileKey("Mindscape.Episode");
        public static string ChapterUnlockedKey => MindscapeDatabase.ProfileKey("Mindscape.ChapterUnlocked");
        public static bool HasSaveSlot => PlayerPrefs.HasKey(ChapterKey) && PlayerPrefs.HasKey(EpisodeKey);

        // The first unlocked episode not yet finished, as a save slot (chapter, story episode + 1); false when all are done.
        public static bool NextUnfinished(out int chapter, out int slotEpisode)
        {
            for (chapter = 1; chapter <= ChapterCount; chapter++)
                for (int e = 1; e <= EpisodeCount(chapter); e++)
                    if (IsEpisodeUnlocked(chapter, e) && !IsComplete(chapter, e)) { slotEpisode = e + 1; return true; }
            chapter = 0; slotEpisode = 0;
            return false;
        }

        public static bool IsComplete(int chapter, int episode) =>
            MindscapeCloud.IsSignedIn ? MindscapeCloud.IsComplete(chapter, episode) : PlayerPrefs.GetInt(SaveKey(chapter, episode), 0) == 1;

        public static int CompletedIn(int chapter)
        {
            int n = 0;
            for (int e = 1; e <= EpisodeCount(chapter); e++) if (IsComplete(chapter, e)) n++;
            return n;
        }
        public static int CompletedTotal { get { int n = 0; for (int c = 1; c <= ChapterCount; c++) n += CompletedIn(c); return n; } }

        public static bool IsChapterComplete(int chapter) => CompletedIn(chapter) == EpisodeCount(chapter);

        public static bool IsChapterUnlocked(int chapter) => chapter == 1 || (chapter > 1 && chapter <= ChapterCount && IsChapterComplete(chapter - 1));

        public static bool IsEpisodeUnlocked(int chapter, int episode) =>
            IsChapterUnlocked(chapter) && episode >= 1 && episode <= EpisodeCount(chapter) && (episode == 1 || IsComplete(chapter, episode - 1));
    }
}
