namespace Mindscape.AcademicPressure
{
    // The reflection journal and self-assessment asked after every story episode (AcademicEpisode.Reflect), and the
    // names the main-menu journal lists them under, in chapter and episode order.
    public static class EpisodeReflection
    {
        public const string DefaultPrompt = "What did you notice about your thoughts and feelings in this episode?";
        public static readonly string[] ChapterNames = { "Academic Pressure", "Family Expectations", "Social Dynamics" };
        public static readonly string[] OverwhelmLevels = { "1 — Not at all", "2 — Slightly", "3 — Moderately", "4 — Very", "5 — Extremely" };
        public static readonly string[] Strategies = { "Taking a breathing break", "Examining my thoughts", "Talking with someone", "Breaking tasks into smaller steps", "Reflection" };

        // Two episodes keep the prompt their story leads into; every other episode uses the default.
        public static string Prompt(int chapter, int episode) =>
            chapter == 1 && episode == 4 ? DormitoryEpisode.JournalPrompt :
            chapter == 3 && episode == 6 ? "What is one thing you appreciate about yourself?" :
            DefaultPrompt;

        public static string ChapterName(int chapter) => chapter >= 1 && chapter <= ChapterNames.Length ? ChapterNames[chapter - 1] : "";

        public static string Title(int chapter, int episode)
        {
            string[] titles = chapter == 1 ? AcademicPressureChapter.Titles
                : chapter == 2 ? Mindscape.FamilyExpectations.FamilyExpectationsChapter.Titles
                : chapter == 3 ? Mindscape.SocialDynamics.SocialDynamicsChapter.Titles : null;
            return titles != null && episode >= 1 && episode <= titles.Length ? titles[episode - 1] : "";
        }

        // "Chapter 2 · Episode 3"
        public static string Label(int chapter, int episode) => "Chapter " + chapter + " · Episode " + episode;
    }
}
