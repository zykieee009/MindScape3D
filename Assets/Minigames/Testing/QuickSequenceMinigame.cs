using UnityEngine;
namespace Mindscape.Minigames
{
    [AddComponentMenu("Mindscape/Minigames/Quick Sequence")]
    // Practice minigame (quick sequence) for the MinigameTestLab scene. Not used in the story episodes.
    public class QuickSequenceMinigame : OrderedActionsMinigame
    {
        public override string Title=>"Quick Sequence";
        public override string Instructions=>"Project due tomorrow: click a sensible sequence for getting started.";
        protected override string[] Steps=>new[]{"Check the project requirements","Prioritize the essential tasks","Start a focused work session","Take a short break after the session"};
        protected override string Hint=>"Understand the task, choose priorities, begin work, then recharge.";
    }
}
