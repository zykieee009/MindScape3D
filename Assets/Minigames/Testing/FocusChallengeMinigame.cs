using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace Mindscape.Minigames
{
    [AddComponentMenu("Mindscape/Minigames/Focus Challenge")]
    // Practice minigame (focus challenge) for the MinigameTestLab scene. Not used in the story episodes.
    public class FocusChallengeMinigame : MinigamePopup
    {
        [Min(1)] public int targetScore=5;
        [Min(.8f)] public float itemSeconds=2.5f;
        public override string Title=>"Focus Challenge";
        public override string Instructions=>"Click STUDY NOTES when they appear. Let phone, game, and social distractions disappear.";
        Button item; TMP_Text scoreLabel; float remaining; int round,score; bool important;
        protected override void BeginGame()
        {
            score=round=0; scoreLabel=MinigameUI.Text(content,"",30,0,1120,50,27);
            item=MinigameUI.Button(content,"",350,140,420,125,ClickItem); NextItem();
        }
        void Update()
        {
            if(!Playing)return; remaining-=Time.unscaledDeltaTime;
            if(remaining<=0) { if(important)Say("Notes passed by. Watch for the next set."); else Say("Good focus: distraction ignored."); NextItem(); }
        }
        void NextItem()
        {
            important=round%2==0; round++; remaining=Mathf.Max(.8f,itemSeconds);
            string[] distractions={"PHONE NOTIFICATION","GAME INVITE","SOCIAL FEED"};
            MinigameUI.Label(item,important?"STUDY NOTES":distractions[Random.Range(0,3)]);
            var rect=(RectTransform)item.transform; rect.anchoredPosition=new Vector2(Random.Range(40,690),-Random.Range(90,260));
            scoreLabel.text="Notes collected: "+score+" / "+Mathf.Max(1,targetScore);
        }
        public void ClickItem()
        {
            if(!Playing)return;
            if(important) { score++; Say("Useful information collected."); } else { score=Mathf.Max(0,score-1); Say("That was a distraction. One note lost; keep going."); }
            if(score>=Mathf.Max(1,targetScore)) { scoreLabel.text="Notes collected: "+score; item.interactable=false; Complete("You kept your attention on the study material."); }
            else NextItem();
        }
    }
}
