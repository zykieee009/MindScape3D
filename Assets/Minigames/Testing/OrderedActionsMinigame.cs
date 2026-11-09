using System.Collections.Generic;
using UnityEngine.UI;
namespace Mindscape.Minigames
{
    public abstract class OrderedActionsMinigame : MinigamePopup
    {
        protected abstract string[] Steps { get; }
        protected abstract string Hint { get; }
        int next; readonly List<Button> buttons=new List<Button>();
        protected override void BeginGame()
        {
            next=0; buttons.Clear(); var steps=Steps;
            int[] order=new int[steps.Length]; for(int i=0;i<order.Length;i++)order[i]=i;
            for(int i=order.Length-1;i>0;i--) { int j=UnityEngine.Random.Range(0,i+1); int t=order[i]; order[i]=order[j]; order[j]=t; }
            for(int row=0;row<order.Length;row++) { int id=order[row]; var b=MinigameUI.Button(content,steps[id],40,15+row*85,1090,67,()=>Choose(id)); buttons.Add(b); }
            Say("Choose the first action.");
        }
        public void Choose(int id)
        {
            if(!Playing || id<0 || id>=Steps.Length)return;
            if(id!=next) { Say(Hint); return; }
            foreach(var b in buttons) if(b.GetComponentInChildren<TMPro.TMP_Text>().text==Steps[id]) { MinigameUI.Label(b,(next+1)+". "+Steps[id]); b.interactable=false; }
            next++; if(next==Steps.Length)Complete("Your plan is ready."); else Say("Step "+next+" set. Choose what comes next.");
        }
    }
}
