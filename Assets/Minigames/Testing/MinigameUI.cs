using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Mindscape.Minigames
{
    public static class MinigameUI
    {
        // Shared with MainMenuNew.uss: wood, paper, brass, and cream.
        public static readonly Color Ink = new Color32(48,45,40,255);
        public static readonly Color Cream = new Color32(240,228,204,255);
        public static readonly Color Surface = new Color32(227,212,180,255);
        public static readonly Color Mint = new Color32(189,164,119,255);
        static TMP_FontAsset regularFont, boldFont;
        static Sprite rounded;
        public static TMP_FontAsset Font(bool bold)
        {
            var cached = bold ? boldFont : regularFont;
            if (cached != null) return cached;
            // Prefer the saved font asset (also what scene objects reference), then fall back to building one.
            cached = Resources.Load<TMP_FontAsset>("MinigameTheme/ComicNeue-" + (bold ? "Bold" : "Regular") + " SDF");
            if (cached != null) { if (bold) boldFont = cached; else regularFont = cached; return cached; }
            var source = Resources.Load<Font>("MinigameTheme/ComicNeue-" + (bold ? "Bold" : "Regular"));
            if (source == null) return TMP_Settings.defaultFontAsset;
            cached = TMP_FontAsset.CreateFontAsset(source);
            cached.name = "Comic Neue " + (bold ? "Bold" : "Regular");
            cached.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            if (bold) boldFont = cached; else regularFont = cached;
            return cached;
        }
        static TMP_FontAsset serifFont;
        // Heading face used by the pause menu ("PAUSED"), conversation speaker names and the breathing exercise.
        public static TMP_FontAsset SerifFont()
        {
            if (serifFont != null) return serifFont;
            serifFont = Resources.Load<TMP_FontAsset>("MinigameTheme/StorySerif SDF");
            if (serifFont != null) return serifFont;
            var source = Resources.Load<Font>("MainMenuNew/StorySerif");
            if (source == null) return Font(true);
            serifFont = TMP_FontAsset.CreateFontAsset(source);
            serifFont.name = "Story Serif";
            serifFont.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            return serifFont;
        }
        public static Sprite Rounded
        {
            get
            {
                if (rounded != null) return rounded;
                const int size = 64;
                const float radius = 18f;
                var texture = new Texture2D(size,size,TextureFormat.RGBA32,false);
                texture.name = "Minigame rounded button";
                texture.wrapMode = TextureWrapMode.Clamp;
                for (int y=0;y<size;y++) for (int x=0;x<size;x++)
                {
                    var point = new Vector2(Mathf.Abs(x+.5f-32),Mathf.Abs(y+.5f-32));
                    var corner = new Vector2(Mathf.Max(0,point.x-(32-radius)),Mathf.Max(0,point.y-(32-radius)));
                    texture.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(radius-corner.magnitude+.5f)));
                }
                texture.Apply();
                rounded = Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(18,18,18,18));
                return rounded;
            }
        }
        static Sprite circle;
        public static Sprite Circle
        {
            get
            {
                if (circle != null) return circle;
                var tex = new Texture2D(128,128,TextureFormat.RGBA32,false);
                for(int y=0;y<128;y++) for(int x=0;x<128;x++)
                    tex.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(64-Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(64,64)))));
                tex.Apply(); circle = Sprite.Create(tex,new Rect(0,0,128,128),new Vector2(.5f,.5f));
                return circle;
            }
        }
        public static void EnsureInput()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("Minigame EventSystem",typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }
        public static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false);
            var r = (RectTransform)go.transform; r.anchorMin=r.anchorMax=new Vector2(0,1); r.pivot=new Vector2(0,1);
            r.anchoredPosition=new Vector2(x,-y); r.sizeDelta=new Vector2(w,h); return r;
        }
        public static RectTransform Canvas(Transform parent, int order)
        {
            EnsureInput();
            var go = new GameObject("Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            go.transform.SetParent(parent,false); var c=go.GetComponent<Canvas>(); c.renderMode=RenderMode.ScreenSpaceOverlay; c.sortingOrder=order;
            var s=go.GetComponent<CanvasScaler>(); s.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution=new Vector2(1280,800); s.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var bg=go.AddComponent<Image>(); bg.color=Ink;
            var art=Rect("Main menu woodland",go.transform,0,0,0,0);
            art.anchorMin=Vector2.zero; art.anchorMax=Vector2.one; art.offsetMin=art.offsetMax=Vector2.zero;
            var background=art.gameObject.AddComponent<RawImage>();
            background.texture=Resources.Load<Texture2D>("MenuArt/Background"); background.raycastTarget=false;
            var veil=Panel(go.transform,"Warm woodland shade",0,0,0,0,new Color(48/255f,45/255f,40/255f,.93f));
            veil.rectTransform.anchorMin=Vector2.zero; veil.rectTransform.anchorMax=Vector2.one;
            veil.rectTransform.offsetMin=veil.rectTransform.offsetMax=Vector2.zero; veil.raycastTarget=false;
            var area=Rect("Content",go.transform,0,0,1280,800); area.anchorMin=area.anchorMax=new Vector2(.5f,.5f); area.pivot=new Vector2(.5f,.5f);
            return area;
        }
        public static Image Panel(Transform p,string name,float x,float y,float w,float h,Color color)
        { var r=Rect(name,p,x,y,w,h); var i=r.gameObject.AddComponent<Image>(); i.color=color; return i; }
        public static TMP_Text Text(Transform p,string text,float x,float y,float w,float h,int size=24)
        {
            var r=Rect("Label",p,x,y,w,h); var t=r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font=Font(size>=28); t.text=text; t.fontSize=size; t.color=Cream; t.raycastTarget=false; t.textWrappingMode=TextWrappingModes.Normal; return t;
        }
        public static Button Button(Transform p,string text,float x,float y,float w,float h,UnityEngine.Events.UnityAction action)
        {
            var image=Panel(p,text,x,y,w,h,Surface);
            image.sprite=Rounded; image.type=Image.Type.Sliced;
            var shadow=image.gameObject.AddComponent<Shadow>();
            shadow.effectColor=new Color(.12f,.10f,.07f,.5f); shadow.effectDistance=new Vector2(0,-4);
            var b=image.gameObject.AddComponent<Button>(); b.targetGraphic=image;
            var colors=b.colors; colors.highlightedColor=new Color(1.08f,1.07f,1.04f);
            colors.selectedColor=colors.highlightedColor; colors.pressedColor=new Color(.85f,.80f,.69f);
            colors.disabledColor=new Color(.65f,.65f,.60f,.85f); colors.fadeDuration=.14f; b.colors=colors;
            var t=Text(image.transform,text,12,8,w-24,h-16,22); t.alignment=TextAlignmentOptions.Center; t.font=Font(true); t.color=Ink;
            b.onClick.AddListener(action); return b;
        }
        public static void Label(Button b,string text) { b.GetComponentInChildren<TMP_Text>().text=text; }
    }
}
