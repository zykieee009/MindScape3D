using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Mindscape.Minigames
{
    [AddComponentMenu("Mindscape/Minigames/Breathing Rhythm")]
    // Practice minigame (breathing rhythm) for the MinigameTestLab scene. Not used in the story episodes.
    public class BreathingRhythmMinigame : MinigamePopup
    {
        [Min(.5f)] public float inhaleSeconds=4, holdSeconds=2, exhaleSeconds=6;
        [Min(1)] public int cycles=2;
        public override string Title => "Breathing Rhythm";
        public override string Instructions => "Follow the circle: inhale, hold, exhale. Click it once during each phase to follow the rhythm.";
        Image circle; TMP_Text phaseText; int phase,hits; float elapsed; bool tapped;
        protected override void BeginGame()
        {
            phase=hits=0; elapsed=0; tapped=false;
            var b=MinigameUI.Button(content,"",450,40,240,240,Tap); circle=b.GetComponent<Image>(); circle.sprite=MinigameUI.Circle; circle.color=MinigameUI.Mint;
            circle.rectTransform.pivot=new Vector2(.5f,.5f); circle.rectTransform.anchoredPosition=new Vector2(590,-180);
            phaseText=MinigameUI.Text(content,"",240,330,700,90,27); phaseText.alignment=TextAlignmentOptions.Center; Draw();
        }
        public void Tap() { if(!Playing || phase<0 || tapped) return; tapped=true; hits++; Say("Rhythm noted. Follow the circle into the next phase."); }
        void Update()
        {
            if(!Playing || phase<0) return;
            elapsed+=Time.unscaledDeltaTime;
            if(elapsed>=Duration())
            {
                elapsed=0; phase++; tapped=false;
                if(phase>=Mathf.Max(1,cycles)*3)
                { if(hits>=Mathf.Max(1,cycles)*3) Complete("You followed every breathing phase."); else { Say("You followed "+hits+" phases. Replay to tap once in every phase."); phaseText.text="Exercise finished"; phase=-1; } return; }
            }
            if(phase>=0) Draw();
        }
        float Duration() => Mathf.Max(.5f, phase%3==0?inhaleSeconds:phase%3==1?holdSeconds:exhaleSeconds);
        void Draw()
        {
            if(phase<0) return;
            float t=elapsed/Duration(); int p=phase%3;
            circle.rectTransform.localScale=Vector3.one*(p==0?Mathf.Lerp(.65f,1.15f,t):p==1?1.15f:Mathf.Lerp(1.15f,.65f,t));
            phaseText.text=(p==0?"Inhale":p==1?"Hold":"Exhale")+"  /  "+Mathf.CeilToInt(Duration()-elapsed)+"s\nCycle "+(phase/3+1)+" of "+Mathf.Max(1,cycles)+(tapped?"  ·  Registered":"  ·  Click the circle");
        }
    }
}
