using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
namespace Mindscape.Minigames
{
    [AddComponentMenu("Mindscape/Minigames/Test Hub")]
    // MinigameTestLab scene only: the menu that opens each of the practice minigames in this folder.
    public class MinigameTestHub : MonoBehaviour
    {
        public MinigamePopup[] minigames;
        public string menuScene = "main_menu_new";
        TMP_Text progress; bool[] completed;
        void Start()
        {
            Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
            if(minigames==null || minigames.Length==0)minigames=GetComponentsInChildren<MinigamePopup>();
            completed=new bool[minigames.Length];
            var root=MinigameUI.Canvas(transform,0);
            MinigameUI.Text(root,"MINDSCAPE / MINIGAME LAB",50,30,1100,30,17).color=MinigameUI.Mint;
            MinigameUI.Button(root,"‹  Main Menu",1010,24,220,56,BackToMenu);
            MinigameUI.Text(root,"Small exercises. A clearer mind.",50,85,1170,65,43);
            MinigameUI.Text(root,"Pick an exercise, give it a try, and play again whenever you like.",50,165,1130,65,24);
            progress=MinigameUI.Text(root,"0 / "+minigames.Length+" explored this session",50,710,1150,50,23); progress.color=MinigameUI.Mint;
            for(int i=0;i<minigames.Length;i++)
            {
                int id=i; var game=minigames[i]; if(game==null)continue;
                var b=MinigameUI.Button(root,(i+1).ToString("00")+"   "+game.Title,50+(i%2)*600,260+(i/2)*82,575,65,game.Open);
                game.onCompleted.AddListener(()=>{
                    completed[id]=true; MinigameUI.Label(b,(id+1).ToString("00")+"   "+game.Title+"   / Done");
                    int count=0; foreach(bool done in completed)if(done)count++;
                    progress.text=count+" / "+minigames.Length+" explored this session";
                });
            }
        }
        void BackToMenu()
        {
            foreach(var game in minigames) if(game!=null && game.IsOpen) game.Close();
            Time.timeScale=1; SceneManager.LoadScene(menuScene);
        }
    }
}
