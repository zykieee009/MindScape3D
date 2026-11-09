using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace Mindscape.Minigames
{
    [AddComponentMenu("Mindscape/Minigames/Grounding Exercise")]
    // Practice minigame (5-4-3-2-1 grounding) for the MinigameTestLab scene. Not used in the story episodes.
    public class GroundingExerciseMinigame : MinigamePopup
    {
        public override string Title=>"Grounding Exercise";
        public override string Instructions=>"Explore the illustrated room. Click distinct objects for each sense: 5 see, 4 touch, 3 hear, 2 smell, 1 taste. Imagine the sounds, scents, and taste.";
        readonly string[] names={"Window","Plant","Cushion","Book","Clock","Radio","Tea","Orange","Blanket"};
        // Each bit corresponds to see, touch, hear, smell, taste.
        readonly int[] senses={1|4,1|2|8,1|2,1|2,1|4,1|2|4,1|2|8|16,1|2|8|16,1|2};
        readonly string[] senseNames={"see","touch","hear","smell","taste"};
        Button[] objects; bool[] used; int stage,count; TMP_Text prompt;
        protected override void BeginGame()
        {
            stage=count=0; used=new bool[9]; objects=new Button[9];
            MinigameUI.Panel(content,"Room wall",0,55,1180,355,new Color(.43f,.45f,.35f));
            MinigameUI.Panel(content,"Floor",0,320,1180,100,new Color(.37f,.31f,.24f));
            MinigameUI.Panel(content,"Desk",580,260,520,22,new Color(.48f,.35f,.24f));
            MinigameUI.Panel(content,"Desk leg left",610,280,18,100,new Color(.48f,.35f,.24f));
            MinigameUI.Panel(content,"Desk leg right",1060,280,18,100,new Color(.48f,.35f,.24f));
            MinigameUI.Panel(content,"Sofa",30,250,490,120,new Color(.52f,.54f,.40f));
            float[] x={30,280,55,595,910,765,965,1020,300}; float[] y={75,130,255,203,75,195,202,322,255}; float[] w={220,125,200,150,160,175,145,140,195};
            for(int i=0;i<9;i++)
            {
                int id=i; objects[i]=MinigameUI.Button(content,names[i],x[i],y[i],w[i],i==0?145:65,()=>Choose(id));
                var img=objects[i].GetComponent<Image>(); img.color=i==0?new Color(.69f,.78f,.72f):i==1?new Color(.63f,.72f,.49f):MinigameUI.Surface;
                Decorate(objects[i], i);
            }
            prompt=MinigameUI.Text(content,"",0,0,1180,50,28); Draw();
        }
        // All artwork is native uGUI geometry, so the prefab needs no external textures.
        void Decorate(Button button, int id)
        {
            var parent = button.transform;
            var label = button.GetComponentInChildren<TMP_Text>();
            Color cream = new Color(.89f, .86f, .71f);
            if (id == 0)
            {
                Shape(parent, "Window frame", 103, 0, 7, 110, cream);
                Shape(parent, "Window crossbar", 0, 65, 220, 6, cream);
                label.rectTransform.anchoredPosition = new Vector2(12, -110);
                label.rectTransform.sizeDelta = new Vector2(196, 30);
            }
            if (id == 1)
            {
                ((RectTransform)parent).sizeDelta = new Vector2(125, 115);
                button.GetComponent<Image>().color = new Color(.63f,.70f,.50f);
                Shape(parent,"Stem",60,18,5,55,MinigameUI.Mint);
                Shape(parent,"Leaf left",29,12,38,24,MinigameUI.Mint,true);
                Shape(parent,"Leaf right",63,29,37,23,new Color(.42f,.67f,.40f),true);
                Shape(parent,"Terracotta pot",40,63,45,20,new Color(.73f,.43f,.29f));
                label.rectTransform.anchoredPosition = new Vector2(8,-82);
                label.rectTransform.sizeDelta = new Vector2(109,30);
            }
            if(id == 4 || id == 7)
            {
                ((RectTransform)parent).sizeDelta = new Vector2(id == 4 ? 160 : 140, 100);
                button.GetComponent<Image>().color = MinigameUI.Surface;
                Shape(parent,"Round object",45,0,60,60,id == 4 ? cream : new Color(.94f,.53f,.16f),true);
                if(id == 4) { Shape(parent,"Hour hand",73,15,4,18,MinigameUI.Ink); Shape(parent,"Minute hand",73,30,20,4,MinigameUI.Ink); }
                else Shape(parent,"Orange leaf",71,0,23,10,MinigameUI.Mint,true);
                label.rectTransform.anchoredPosition = new Vector2(8,-65);
                label.rectTransform.sizeDelta = new Vector2(id == 4 ? 144 : 124,30);
            }
            if(id == 3)
            {
                button.GetComponent<Image>().color = new Color(.78f,.61f,.53f);
                Shape(parent,"Book spine",5,5,6,55,cream);
            }
            if(id == 8)
            {
                button.GetComponent<Image>().color = new Color(.69f,.67f,.54f);
                Shape(parent,"Blanket trim",0,57,195,5,cream);
            }
            label.transform.SetAsLastSibling();
        }
        void Shape(Transform parent,string name,float x,float y,float width,float height,Color color,bool round=false)
        {
            var image=MinigameUI.Panel(parent,name,x,y,width,height,color);
            image.raycastTarget=false;
            if(round)image.sprite=MinigameUI.Circle;
        }
        void Draw(){prompt.text="Find "+(5-stage)+" things you can "+senseNames[stage]+"  /  "+count+" found";}
        public void Choose(int id)
        {
            if(!Playing || id<0 || id>=9 || used[id])return;
            if((senses[id]&(1<<stage))==0){Say("Try another object for this sense.");return;}
            used[id]=true; objects[id].interactable=false; count++;
            if(count==5-stage)
            {
                stage++; count=0;
                if(stage==5){Complete("You explored all five senses.");return;}
                used=new bool[9]; foreach(var b in objects)b.interactable=true;
                Say("Next sense: "+senseNames[stage]+". Objects can be used again for a new sense.");
            }
            Draw();
        }
    }
}
