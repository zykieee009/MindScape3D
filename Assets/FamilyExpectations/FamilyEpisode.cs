using Mindscape.AcademicPressure;

namespace Mindscape.FamilyExpectations
{
    // Chapter 2 · Family Expectations. Episodes 1-3 run on the Chapter 1 framework (same house, school, cast,
    // player rig and interface); Episodes 4 and 5 are FamilyFinaleEpisode. Save keys are the original ones,
    // so progress made before the move carries over.
    public static class FamilyExpectationsChapter
    {
        public const int Number = 2;
        public static readonly string[] Scenes = { "Chapter 2 - Episode 1", "Chapter 2 - Episode 2", "Chapter 2 - Episode 3", "Chapter 2 - Episode 4", "Chapter 2 - Episode 5" };
        public static readonly string[] Titles = { "The Paper on the Desk", "Before the Bell", "Running Out of Time", "The Weight Behind the Worry", "What I Left Behind" };
        public static string SaveKey(int episode) => Mindscape.Data.MindscapeDatabase.ProfileKey("Mindscape.FamilyExpectations.Episode" + episode);
    }

    public abstract class FamilyEpisode : AcademicEpisode
    {
        public override int Chapter => FamilyExpectationsChapter.Number;
        protected override string CompletionKey => FamilyExpectationsChapter.SaveKey(Episode);
        protected override string NextScene => FamilyExpectationsChapter.Scenes[Episode];
        protected override string ContinueLabel => "Continue to Episode " + (Episode + 1);
        protected override string CompletionNext => "NEXT: EPISODE " + (Episode + 1).ToString("00") + "  •  " + FamilyExpectationsChapter.Titles[Episode].ToUpperInvariant();

        // The title card every episode opens with.
        // Flashback episodes add a gold line under the title.
        protected string TitleCard => "CHAPTER 2  ·  FAMILY EXPECTATIONS\n<size=60%>Episode " + Episode.ToString("00") + "  ·  " + FamilyExpectationsChapter.Titles[Episode - 1] + "</size>" +
            (IsFlashbackEpisode ? "\n<size=38%><color=#DBB36F>FLASHBACK  ·  " + FlashbackLabel.ToUpperInvariant() + "</color></size>" : "");
    }
}
