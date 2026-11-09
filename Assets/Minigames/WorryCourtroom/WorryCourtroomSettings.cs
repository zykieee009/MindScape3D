using System;
using UnityEngine;

namespace Mindscape.Minigames
{
    // Every number that shapes how the trial plays. Edit on the Worry Courtroom component.
    [Serializable]
    public class CourtRules
    {
        [Range(1, 10)] public int maxStrikes = 3;
        [Tooltip("Worry meter at the start of a case.")] public float startingWorry = 100;
        [Header("Worry removed by each right answer")]
        public float gainPerEvidence = 3;
        public float gainPerDefence = 8;
        public float gainFriend = 14;
        public float gainVerdict = 30;
        public float gainPlan = 14;
        [Header("Rounds")]
        [Min(1)] public int defencePicksRequired = 3;
        public bool shuffleEvidence = true;
        public bool shuffleOptions = true;
        [Tooltip("Show this many options per row in lists with at least 'Two Column Threshold' answers.")]
        public int twoColumnThreshold = 4;
        [Header("Stars (by total wrong answers)")]
        [Tooltip("3 stars when total strikes are at or below this.")] public int threeStarsMaxStrikes = 2;
        [Tooltip("2 stars when total strikes are at or below this.")] public int twoStarsMaxStrikes = 5;
        [Header("Look")]
        [Tooltip("How fast the meter and the Worry figure change, in percent per second.")] public float meterSpeed = 70;
        public float shakeSeconds = 0.35f;
        public float shakeDistance = 7;
        [Header("Worry figure size")]
        [Range(0.1f, 1f)] public float smallestScale = 0.4f;
        [Range(0.1f, 1f)] public float lowestOpacity = 0.6f;
        [Tooltip("Worry level above this shows the first mood (scowl).")] public float moodWorriedAbove = 66;
        [Tooltip("Worry level above this (and below the line above) shows the second mood.")] public float moodNeutralAbove = 33;
    }

    [Serializable]
    public class CourtColors
    {
        public Color good = new Color32(140, 192, 138, 255);
        public Color wrong = new Color32(224, 138, 118, 255);
        public Color optionCorrect = new Color32(140, 192, 138, 255);
        public Color optionWrong = new Color(.75f, .68f, .6f, 1);
        public Color optionNormal = new Color32(227, 212, 180, 255);
        public Color tabActive = new Color32(209, 168, 79, 255);
        public Color tabInactive = new Color32(227, 212, 180, 255);
        public Color chipActive = new Color32(209, 168, 79, 255);
        public Color chipInactive = new Color32(227, 212, 180, 255);
        public Color stepNowFill = new Color32(209, 168, 79, 255);
        public Color stepIdleFill = new Color(.17f, .12f, .09f, 1);
        public Color stepNowText = new Color32(48, 45, 40, 255);
        public Color stepDoneText = new Color32(140, 192, 138, 255);
        public Color stepIdleText = new Color32(188, 171, 141, 255);
        public Color strikeOn = new Color32(224, 138, 118, 255);
        public Color strikeOff = new Color32(74, 59, 45, 255);
        public Color starOn = new Color32(209, 168, 79, 255);
        public Color starOff = new Color32(74, 59, 45, 255);
    }

    // Every sentence the game writes itself. Use {braces} where shown; they are filled in at runtime.
    [Serializable]
    public class CourtTexts
    {
        [Header("Steps (6)")]
        public string[] stepNames = { "Charge", "Prosecution", "Defense", "Friend", "Verdict", "Plan" };

        [Header("Who speaks in the bubble")]
        public string worryName = "The Worry";
        public string prosecutorName = "The Prosecutor";
        [TextArea(1, 3)] public string prosecutorLine = "Your Honor, look at all this evidence. The worry is clearly true!";
        public string defenderName = "The Defender";
        [TextArea(1, 3)] public string defenderLine = "Objection! Let us put the facts on the other side of the scale.";
        public string friendName = "The Friend";
        [TextArea(1, 3)] public string friendLine = "If I were your friend, here is what I would say to you.";
        public string judgeName = "The Judge";
        [TextArea(1, 3)] public string judgeLine = "The court will now give its verdict.";
        public string clerkName = "The Clerk";
        [TextArea(1, 3)] public string clerkLine = "One last thing: what small step will you take?";
        [TextArea(1, 3)] public string worryFarewell = "...I was only a thought, after all.";

        [Header("Meter")]
        public string meterFormat = "WORRY LEVEL  {value}%";

        [Header("Charge")]
        public string introHeading = "Today's case: {case}";
        [TextArea(1, 3)] public string introLead = "You are the judge. A worry has been brought to court. Weigh the evidence for and against it, hear what a friend would say, and give a verdict.";
        public string introButton = "Open the trial";

        [Header("Prosecution")]
        public string sortHeading = "Prosecution: evidence for the worry";
        [TextArea(1, 3)] public string sortLead = "Is this a fact the court can check, or a feeling or a guess? Only facts are admitted.";
        public string evidenceCounter = "EVIDENCE {n} OF {total}";
        public string factButton = "Fact";
        public string guessButton = "Feeling or guess";
        [TextArea(1, 3)] public string factAdmitted = "Admitted. This is a fact. It can be checked.";
        [TextArea(1, 3)] public string guessNotAdmitted = "Not admitted. This is {trap}. {description}";
        [TextArea(1, 3)] public string wrongSaidGuess = "Look again. You could check this one, so it counts as a fact.";
        [TextArea(1, 3)] public string wrongSaidFact = "Look again. Nobody can check this one. It is a thought or a feeling.";
        public string nextEvidenceButton = "Next evidence";
        public string toDefenceButton = "On to the defense";

        [Header("Defence")]
        public string defenceHeading = "Defense: evidence against the worry";
        [TextArea(1, 3)] public string defenceLead = "Pick {needed} facts that show the worry might not be true. Admitted: {picked} of {needed}.";
        [TextArea(1, 3)] public string defenceAdmitted = "Admitted. This is a real fact that weighs against the worry.";
        public string toFriendButton = "Call the Friend";

        [Header("Friend")]
        public string friendHeading = "The Friend takes the stand";
        [TextArea(1, 3)] public string friendPrompt = "A friend has the same worry and asks for advice. What would you tell them?";
        [TextArea(1, 3)] public string friendCorrect = "Kind and realistic. You'd say this to a friend, and you can say it to yourself.";
        public string toVerdictButton = "To the verdict";

        [Header("Verdict")]
        public string verdictHeading = "The Verdict";
        [TextArea(1, 3)] public string verdictPrompt = "Pick the more balanced and helpful thought you can believe instead.";
        [TextArea(1, 3)] public string verdictCorrect = "Balanced and believable. The court no longer accepts the worry as a fact.";
        public string toPlanButton = "Make a plan";

        [Header("Plan")]
        public string planHeading = "The Plan";
        [TextArea(1, 3)] public string planPrompt = "Choose one small action that supports the new thought, then say when you'll do it.";
        [TextArea(1, 3)] public string planCorrect = "Small, specific, and doable.";
        public string whenLabel = "WHEN WILL YOU DO IT?";
        public string[] whenChoices = { "Today", "Tomorrow", "This week" };
        public string closeCaseButton = "Close the case";

        [Header("Case dismissed")]
        public string resultHeading = "Case dismissed";
        public string starsFormat = "{n} of 3 stars";
        [TextArea(3, 8)] public string caseFileFormat = "THE WORRY ON TRIAL\n\"{worry}\"\n\nTHE BALANCED THOUGHT\n{balanced}\n\nTHE PLAN\n{plan} ({when})";
        public string trapsHeading = "THINKING TRAPS SPOTTED";
        public string trapLineFormat = "- {trap}: {description}";
        public string strikesLabel = "GAVEL STRIKES:";
        public string nextCaseButton = "Next case";
        public string replayButton = "Replay this case";
        public string continueButton = "Continue";
        public string exitButton = "Exit";
        public string closeEarlyButton = "Close";
        public string doneMark = " (Done)";

        [Header("Recess (after too many strikes)")]
        public string recessHeading = "Recess";
        [TextArea(1, 4)] public string recessBody = "Three gavel strikes. The judge calls a short break. Breathe in for four counts and out for six, then come back. The helpful answers will be outlined from now on.";
        public string recessButton = "Back to court";
    }
}
