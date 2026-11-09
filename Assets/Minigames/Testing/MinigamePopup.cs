using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Mindscape.Minigames
{
    public abstract class MinigamePopup : MonoBehaviour
    {
        public UnityEvent onCompleted = new UnityEvent();
        public UnityEvent onClosed = new UnityEvent();
        public bool IsOpen { get; private set; }
        public bool IsComplete { get; private set; }
        public abstract string Title { get; }
        public abstract string Instructions { get; }
        protected RectTransform content;
        protected TMP_Text feedback;
        GameObject canvas;
        static MinigamePopup current;
        CursorLockMode oldLock; bool oldVisible;
        public void Open()
        {
            if (!isActiveAndEnabled) return;
            if(current != null && current != this) current.Close();
            if(IsOpen) { Replay(); return; }
            current=this; oldLock=Cursor.lockState; oldVisible=Cursor.visible;
            Cursor.lockState=CursorLockMode.None; Cursor.visible=true; IsOpen=true;
            Build();
        }
        void Build()
        {
            IsComplete=false;
            var root=MinigameUI.Canvas(transform,100); canvas=root.parent.gameObject;
            MinigameUI.Text(root,"MINDSCAPE / PRACTICE",50,26,850,30,16).color=MinigameUI.Mint;
            MinigameUI.Text(root,Title,50,65,850,58,38);
            MinigameUI.Text(root,Instructions,50,126,1140,65,22);
            content=MinigameUI.Rect("Play Area",root,50,210,1180,450);
            feedback=MinigameUI.Text(root,"",50,675,850,78,23);
            MinigameUI.Button(root,"Replay",960,686,120,55,Replay);
            MinigameUI.Button(root,"Close",1095,686,135,55,Close);
            BeginGame();
        }
        public void Replay()
        {
            if(!IsOpen) return;
            StopAllCoroutines(); ClearCanvas(); Build();
        }
        public void Close()
        {
            if(!IsOpen) return;
            IsOpen=false; StopAllCoroutines(); ClearCanvas();
            if(current==this) { current=null; Cursor.lockState=oldLock; Cursor.visible=oldVisible; }
            onClosed.Invoke();
        }
        void ClearCanvas() { if(canvas!=null) { canvas.SetActive(false); Destroy(canvas); } }
        protected virtual void OnDisable() { Close(); }
        protected abstract void BeginGame();
        protected void Complete(string message)
        {
            if(IsComplete || !IsOpen) return;
            IsComplete=true; feedback.color=MinigameUI.Mint; feedback.text="Complete! " + message; onCompleted.Invoke();
        }
        protected void Say(string message) { feedback.text=message; }
        protected bool Playing => IsOpen && !IsComplete;
    }
}
