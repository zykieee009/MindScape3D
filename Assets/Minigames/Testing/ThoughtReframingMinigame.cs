using UnityEngine;
namespace Mindscape.Minigames
{
    [AddComponentMenu("Mindscape/Minigames/Thought Reframing")]
    // Practice minigame (thought reframing) for the MinigameTestLab scene. Not used in the story episodes.
    public class ThoughtReframingMinigame : MinigamePopup
    {
        public override string Title=>"Thought Reframing";
        public override string Instructions=>"Choose a balanced response that leaves room for learning.";
        protected override void BeginGame()
        {
            MinigameUI.Text(content,"\"I failed this quiz. I'll never pass.\"",0,10,1100,70,32);
            string[] choices={"One quiz does not define me. I can review mistakes and ask for help.","I must get every answer right next time.","There is no point trying again.","I'll ignore the result and never think about it."};
            for(int i=0;i<choices.Length;i++) { int id=i; MinigameUI.Button(content,choices[i],0,100+i*76,1160,62,()=>Choose(id)); }
        }
        public void Choose(int i) { if(!Playing || i<0 || i>3)return; if(i==0)Complete("A setback can guide your next step."); else Say("Try a response that acknowledges the setback and offers a realistic next step."); }
    }
}
