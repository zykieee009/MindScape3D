using UnityEngine;

namespace Mindscape.SocialDynamics
{
    public static class SocialDynamicsChapter
    {
        public const int EpisodeCount = 8;
        public const string JournalPrompt = "One Thing About Me";
        public static readonly string[] Titles = { "The Invitation", "Arriving at the Reunion", "Everyone Seems to Be Moving Forward", "The Corner of the Room", "A Friend Checks In", "Choosing to Rejoin the Group", "The Walk Home", "The Mirror" };
        public static readonly string[] Locations = { "BEDROOM", "REUNION / ENTRANCE", "REUNION / CONVERSATIONS", "REUNION / QUIET CORNER", "REUNION / A FRIEND", "REUNION / GROUP PHOTO", "NIGHT STREET / HOME", "BEDROOM / MIRROR" };
        public static readonly string[] Briefings = {
            "Read Justin's invitation, revisit a photograph, and examine your desk. Choose your reply.",
            "Meet Justin at the entrance and join your old classmates at the reunion.",
            "Listen to two groups in any order, then speak with Justin about how you feel.",
            "Find a quiet corner by the window. Look at your phone and notice your thoughts.",
            "Talk with Justin. Listen to what friendship means beyond achievements.",
            "Return to the group. Choose one thing you appreciate about yourself, then join the photograph.",
            "Walk home under the streetlights as memories of tonight come back, stop to look at the photograph, greet your family, and rest.",
            "Wake in a quiet dream and walk toward the light. Then face the mirror and make room for the person you are today." };
        public static readonly string[] Appreciations = { "I keep trying even when things are difficult.", "I care about the people around me.", "I'm still learning, and I'm willing to grow." };
        // The four old friends who speak at the reunion ("Friend 1-4" in the script), in the order the builder places them:
        // Old friend 1 (NPC_01_Maya), 2 (NPC_03_Leo), 3 (NPC_05_Chloe), 4 (NPC_08_Zara).
        public static readonly string[] FriendNames = { "Bea", "Aaron", "Andrea", "Kim" };
        // Other boys in Chapter 3 take the names from the capstone list instead of their model names.
        public static readonly System.Collections.Generic.Dictionary<string, string> BoyNames = new System.Collections.Generic.Dictionary<string, string>
        {
            { "Ethan", "Sky" }, { "Julian", "Marc" }, { "Malik", "Jefferson" }, { "Noah", "Jahmir" }, { "Leo", "Aaron" },
        };
        // "Friend 2" -> "Aaron"; any other speaker is kept as written.
        public static string Speaker(string who)
        {
            if (who.StartsWith("Friend ") && int.TryParse(who.Substring(7), out int n) && n >= 1 && n <= FriendNames.Length) return FriendNames[n - 1];
            return who;
        }
        public static string Scene(int episode) => "Chapter 3 - Episode " + Mathf.Clamp(episode, 1, EpisodeCount);
        public static string SaveKey(int episode) => Mindscape.Data.MindscapeDatabase.ProfileKey("Mindscape.SocialDynamics.Episode" + episode);
    }
}
