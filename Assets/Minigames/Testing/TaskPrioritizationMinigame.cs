using UnityEngine;
namespace Mindscape.Minigames
{
    [AddComponentMenu("Mindscape/Minigames/Task Prioritization")]
    // Practice minigame (task prioritization) for the MinigameTestLab scene. Not used in the story episodes.
    public class TaskPrioritizationMinigame : OrderedActionsMinigame
    {
        public override string Title=>"Task Prioritization";
        public override string Instructions=>"It's Monday. Click tasks from most urgent to least urgent. The optional club meeting is next week.";
        protected override string[] Steps=>new[]{"Prepare for tomorrow's exam","Work on the assignment due Friday","Plan for next week's optional club meeting"};
        protected override string Hint=>"Compare deadlines: tomorrow, Friday, then next week.";
    }
}
