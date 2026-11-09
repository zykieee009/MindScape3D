using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace Mindscape.Minigames
{
    [AddComponentMenu("Mindscape/Minigames/Stress Meter")]
    // Practice minigame (stress meter) for the MinigameTestLab scene. Not used in the story episodes.
    public class StressMeterMinigame : MinigamePopup
    {
        public override string Title=>"Stress Meter";
        public override string Instructions=>"Choose up to four actions. Bring the practice meter down to 30 or less. Each action can be used once.";
        readonly string[] choices={"Get enough sleep","Cram without resting","Ask for help","Take a short break"};
        readonly int[] effects={-25,20,-20,-15}; Button[] buttons; Image fill; TMP_Text label; int stress,moves;
        protected override void BeginGame()
        {
            stress=80; moves=0; buttons=new Button[4];
            MinigameUI.Panel(content,"Meter",20,65,1120,38,MinigameUI.Surface);
            fill=MinigameUI.Panel(content,"Stress fill",20,65,896,38,MinigameUI.Mint); label=MinigameUI.Text(content,"",20,0,1120,50,28);
            for(int i=0;i<4;i++){int id=i;buttons[i]=MinigameUI.Button(content,choices[i],20+(i%2)*580,160+(i/2)*100,550,80,()=>Choose(id));} Draw();
        }
        void Draw(){ fill.rectTransform.sizeDelta=new Vector2(1120*stress/100f,38); label.text="Practice stress: "+stress+" / 100   |   Choices: "+moves+" / 4"; }
        public void Choose(int id)
        {
            if(!Playing || id<0 || id>3 || !buttons[id].interactable)return;
            buttons[id].interactable=false; stress=Mathf.Clamp(stress+effects[id],0,100); moves++; Draw();
            if(stress<=30)Complete("A balanced plan leaves room to recover.");
            else if(moves==4)Say("All choices used. Replay and try a plan with more rest and support.");
            else Say(effects[id]<0?"That choice creates space to recover.":"Skipping rest raised the practice meter. Try support or recovery next.");
        }
    }
}
