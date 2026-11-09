using UnityEngine;
using TMPro;
namespace Mindscape.Minigames
{
    [AddComponentMenu("Mindscape/Minigames/Emotion Check")]
    // Practice minigame (emotion check) for the MinigameTestLab scene. Not used in the story episodes.
    public class EmotionCheckMinigame : MinigamePopup
    {
        public override string Title=>"Emotion Check";
        public override string Instructions=>"Use the clues to choose how the character might feel. People can feel more than one emotion.";
        readonly string[] situations={"Sam's hands shake before presenting to the class.","Alex smiles after finishing a difficult assignment.","Jo feels left out when friends meet without them."};
        readonly string[] emotions={"Anxious","Proud","Sad","Calm"}; int round; TMP_Text scenario;
        protected override void BeginGame()
        {
            round=0; scenario=MinigameUI.Text(content,situations[0],20,25,1110,115,32);
            for(int i=0;i<4;i++) {int id=i; MinigameUI.Button(content,emotions[i],20+(i%2)*580,190+(i/2)*100,550,80,()=>Choose(id));}
        }
        public void Choose(int id)
        {
            if(!Playing || id<0 || id>3)return;
            if(id!=round){Say("That can be a feeling too. Which option best fits these particular clues?");return;}
            round++; if(round==3)Complete("You recognized feelings in three situations."); else { scenario.text=situations[round]; Say("That fits the clues. Try the next situation."); }
        }
    }
}
