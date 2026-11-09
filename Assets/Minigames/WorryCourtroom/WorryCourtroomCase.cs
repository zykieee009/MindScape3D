using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mindscape.Minigames
{
    public enum ThinkingTrap { None, MindReading, FortuneTelling, AllOrNothing, Catastrophizing, EmotionalReasoning, Labeling }

    [Serializable]
    public class TrapInfo
    {
        public ThinkingTrap trap;
        [Tooltip("Name shown to the player when this trap is spotted.")]
        public string displayName;
        [TextArea(1, 3)] public string description;
    }

    [Serializable]
    public class EvidenceCard
    {
        [TextArea(1, 3)] public string text;
        [Tooltip("Tick if this is a checkable fact. Leave unticked for a feeling or a guess.")]
        public bool isFact;
        [Tooltip("Only used when 'Is Fact' is off. Which thinking trap this guess is.")]
        public ThinkingTrap trap;
    }

    [Serializable]
    public class AnswerOption
    {
        [TextArea(1, 3)] public string text;
        [Tooltip("Tick for a helpful answer the player should pick.")]
        public bool isGood;
        [Tooltip("Shown when the player picks this answer and it is wrong.")]
        [TextArea(1, 3)] public string whyNot;
    }

    // One courtroom trial. Create more with Assets > Create > Mindscape > Worry Courtroom Case.
    [CreateAssetMenu(menuName = "Mindscape/Worry Courtroom Case", fileName = "NewWorryCase")]
    public class WorryCourtroomCase : ScriptableObject
    {
        [Header("Case")]
        public string chapterLabel = "Chapter 1";
        public string caseName = "The quiz";
        [Tooltip("The worry on trial, spoken by the Worry in the dock.")]
        [TextArea(1, 3)] public string worry = "I failed this quiz. I'll never pass this class.";

        [Header("Prosecution: evidence cards (shown one at a time)")]
        public List<EvidenceCard> evidence = new List<EvidenceCard>();

        [Header("Defence: pick the good facts (wrong ones cost a strike)")]
        public List<AnswerOption> defence = new List<AnswerOption>();

        [Header("Friend: what would you tell a friend?")]
        public List<AnswerOption> friend = new List<AnswerOption>();

        [Header("Verdict: the balanced thought")]
        public List<AnswerOption> verdict = new List<AnswerOption>();

        [Header("Plan: one small step")]
        public List<AnswerOption> plan = new List<AnswerOption>();

        [Header("Thinking traps (names and explanations shown to the player)")]
        public List<TrapInfo> trapInfo = DefaultTraps();

        [Header("Optional text overrides for this case. Blank = use the text on the component.")]
        [TextArea(1, 3)] public string introLead;
        [TextArea(1, 3)] public string defenceLead;
        [TextArea(1, 3)] public string friendPrompt;
        [TextArea(1, 3)] public string verdictPrompt;
        [TextArea(1, 3)] public string planPrompt;

        public static List<TrapInfo> DefaultTraps()
        {
            return new List<TrapInfo>
            {
                new TrapInfo { trap = ThinkingTrap.MindReading, displayName = "Mind reading", description = "Assuming you know what other people think without checking." },
                new TrapInfo { trap = ThinkingTrap.FortuneTelling, displayName = "Fortune telling", description = "Predicting the future as if it were already certain." },
                new TrapInfo { trap = ThinkingTrap.AllOrNothing, displayName = "All-or-nothing", description = "Seeing things in extremes, like always, never, perfect, or ruined." },
                new TrapInfo { trap = ThinkingTrap.Catastrophizing, displayName = "Catastrophizing", description = "Jumping to the worst possible outcome." },
                new TrapInfo { trap = ThinkingTrap.EmotionalReasoning, displayName = "Emotional reasoning", description = "Treating a feeling as proof that something is true." },
                new TrapInfo { trap = ThinkingTrap.Labeling, displayName = "Labeling", description = "Calling yourself a harsh name because of one event." }
            };
        }

        public TrapInfo FindTrap(ThinkingTrap t)
        {
            foreach (var i in trapInfo) if (i.trap == t) return i;
            return new TrapInfo { trap = t, displayName = Nicify(t.ToString()), description = "" };
        }

        static string Nicify(string s)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++) { if (i > 0 && char.IsUpper(s[i])) sb.Append(' '); sb.Append(s[i]); }
            return sb.ToString();
        }
    }
}
