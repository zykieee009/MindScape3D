using System.Collections;
using UnityEngine;
using UnityEngine.UI;
namespace Mindscape.Minigames
{
    [AddComponentMenu("Mindscape/Minigames/Memory Match")]
    // Practice minigame (memory match) for the MinigameTestLab scene. Not used in the story episodes.
    public class MemoryMatchMinigame : MinigamePopup
    {
        public override string Title=>"Memory Match";
        public override string Instructions=>"Flip two cards. Match each stressor to its coping strategy, rather than identical words.";
        readonly string[] words={"Too many tasks","Prioritize tasks","Feeling isolated","Talk to someone","Tired after studying","Take a short break","Confused by homework","Ask for help"};
        int[] deck; bool[] matched; Button[] cards; int first,pairs; bool busy;
        protected override void BeginGame()
        {
            first=-1; pairs=0; busy=false; deck=new int[8]; matched=new bool[8]; cards=new Button[8];
            for(int i=0;i<8;i++)deck[i]=i;
            for(int i=7;i>0;i--) { int j=Random.Range(0,i+1); int t=deck[i]; deck[i]=deck[j]; deck[j]=t; }
            for(int i=0;i<8;i++) { int id=i; cards[i]=MinigameUI.Button(content,"FLIP "+(i+1),(i%4)*292,(i/4)*190+10,270,165,()=>Flip(id)); }
            Say("Find all four stressor / strategy pairs.");
        }
        public void Flip(int index)
        {
            if(!Playing || busy || index<0 || index>=8 || matched[index] || index==first)return;
            MinigameUI.Label(cards[index],words[deck[index]]);
            if(first<0){first=index;return;}
            int previous=first; first=-1;
            if(deck[previous]/2==deck[index]/2)
            {
                matched[previous]=matched[index]=true; cards[previous].interactable=cards[index].interactable=false; pairs++;
                if(pairs==4)Complete("All four coping pairs found."); else Say(pairs+" / 4 pairs matched.");
            }
            else StartCoroutine(Hide(previous,index));
        }
        IEnumerator Hide(int a,int b) { busy=true; Say("Different pair. Remember the cards and try again."); yield return new WaitForSecondsRealtime(1.2f); MinigameUI.Label(cards[a],"FLIP "+(a+1)); MinigameUI.Label(cards[b],"FLIP "+(b+1)); busy=false; }
    }
}
