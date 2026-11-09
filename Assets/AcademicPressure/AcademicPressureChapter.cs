using UnityEngine;

namespace Mindscape.AcademicPressure
{
    // Chapter 1 · Academic Pressure. The whole chapter happens around one school deadline:
    // House -> Classroom -> Cafeteria -> Dormitory.
    public static class AcademicPressureChapter
    {
        public const int Number = 1;
        public const string Theme = "Academic Pressure";
        public static readonly string[] Scenes = { "Chapter 1 - Episode 1", "Chapter 1 - Episode 2", "Chapter 1 - Episode 3", "Chapter 1 - Episode 4" };
        public static readonly string[] Titles = { "Tomorrow Already?", "The Announcement", "Everyone Seems Ahead", "One Step at a Time" };
        public static readonly string[] Locations = { "HOUSE", "CLASSROOM", "CAFETERIA", "DORMITORY" };
        public static readonly string[] Briefings =
        {
            "• Check the reminder on your phone and look at your unfinished project\n• Notice what you are feeling\n• Pack your things, finish your chores, and catch the bus to school",
            "• Walk into school and find your seat\n• Listen to the teacher's announcement\n• Talk with a friend and take a moment to breathe",
            "• Join the lunchtime conversation in the cafeteria\n• Notice the thought that follows\n• Examine it in the Thought Courtroom",
            "• Sit at your desk in the dorm\n• Break the project into small tasks and finish one\n• Write in your reflection journal and complete the self-reflection"
        };

        public static int EpisodeCount => Scenes.Length;
        public static string SaveKey(int episode) => Mindscape.Data.MindscapeDatabase.ProfileKey("Mindscape.AcademicPressure.Episode" + episode);
        public static bool IsComplete(int episode) => PlayerPrefs.GetInt(SaveKey(episode), 0) == 1;
        public static int CompletedCount { get { int n = 0; for (int i = 1; i <= EpisodeCount; i++) if (IsComplete(i)) n++; return n; } }

        // Choice keys recorded in the database. Later episodes read these back for small dialogue changes.
        public const string LaptopPlan = "laptop_plan";          // finish_everything | one_task
        public const string Feeling = "feeling";                 // Worried | Overwhelmed | Frustrated | Calm
        public const string FriendReply = "friend_reply";        // fine | hard_time | cannot_finish
        public const string Breathing = "breathing";             // completed | closed_early
        public const string BalancedThought = "balanced_thought";
        public const string FirstTask = "first_task";

        public static bool Load(int episode)
        {
            if (episode < 1 || episode > EpisodeCount) return false;
            return EpisodeLoadingScreen.Load(Scenes[episode - 1], episode, Number);
        }
    }
}
