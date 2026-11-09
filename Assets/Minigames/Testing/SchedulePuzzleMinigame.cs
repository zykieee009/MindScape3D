using UnityEngine;
using UnityEngine.UI;
namespace Mindscape.Minigames
{
    [AddComponentMenu("Mindscape/Minigames/Schedule Puzzle")]
    // Practice minigame (schedule puzzle) for the MinigameTestLab scene. Not used in the story episodes.
    public class SchedulePuzzleMinigame : MinigamePopup
    {
        public override string Title=>"Schedule Puzzle";
        public override string Instructions=>"Select an activity, then a slot. Study before a break; dinner before review; sleep last. Click a filled slot with no card selected to remove it.";
        readonly string[] activities={"Study","Break","Dinner","Review","Sleep"};
        readonly string[] times={"4:00 PM","5:00 PM","6:00 PM","7:00 PM","9:00 PM"};
        int[] placed; int selected; Button[] cards,slots;
        protected override void BeginGame()
        {
            selected=-1; placed=new[]{-1,-1,-1,-1,-1}; cards=new Button[5]; slots=new Button[5];
            for(int i=0;i<5;i++) { int id=i; int cardId=(i+2)%5; slots[i]=MinigameUI.Button(content,"",i*235,45,218,150,()=>Place(id)); cards[cardId]=MinigameUI.Button(content,activities[cardId],i*235,260,218,85,()=>Select(cardId)); }
            MinigameUI.Button(content,"Check schedule",425,375,320,65,Check); Draw();
        }
        public void Select(int id) { if(!Playing || id<0 || id>4)return; selected=id; Draw(); Say("Selected "+activities[id]+". Choose a time slot."); }
        public void Place(int slot)
        {
            if(!Playing || slot<0 || slot>4)return;
            if(selected<0){placed[slot]=-1;Draw();return;}
            for(int i=0;i<5;i++)if(placed[i]==selected)placed[i]=-1;
            placed[slot]=selected; selected=-1; Draw();
        }
        void Draw()
        {
            for(int i=0;i<5;i++) { MinigameUI.Label(slots[i],times[i]+"\n\n"+(placed[i]<0?"Empty slot":activities[placed[i]])); MinigameUI.Label(cards[i],(selected==i?"> ":"")+activities[i]); }
        }
        public void Check()
        {
            if(!Playing)return;
            for(int i=0;i<5;i++)if(placed[i]!=i){Say("Use all five slots: study, break, dinner, review, then sleep.");return;}
            Complete("Your evening includes work, rest, food, and sleep.");
        }
    }
}
