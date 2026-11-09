using System.Collections;
using System.Collections.Generic;
using Mindscape.Minigames;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Mindscape.EpisodeOne
{
    /// <summary>Owns only the after-school episode. No timer, stress failure or exam grade.</summary>
    public sealed class EpisodeOneStory : MonoBehaviour, IPausable
    {
        public Transform player;
        public Camera storyCamera;
        public Transform[] memoryObjects = new Transform[3];
        public Light deskLight;
        public Light sunlight;
        public Transform examMemoryView;
        public GameObject morningItems;
        public string nextEpisodeScene = "";
        public const string SceneName = "Chapter 2 - Episode 1";
        public static string SaveKey => Mindscape.Data.MindscapeDatabase.ProfileKey("Mindscape.FamilyExpectations.Episode1");
        public enum Phase { Opening, Exploring, Memory, Ending, Complete }
        public Phase CurrentPhase { get; private set; }
        public int SeenMask { get; private set; }
        public int MemoryCount => ((SeenMask & 1) != 0 ? 1 : 0) + ((SeenMask & 2) != 0 ? 1 : 0) + ((SeenMask & 4) != 0 ? 1 : 0);
        public bool ReadyForEnding => SeenMask == 7;
        public bool IsPaused => paused;

        static readonly string[] Titles = { "Examination paper", "School bag", "Study notebook" };
        static readonly string[] Thoughts = {
            "I remember watching the clock while I tried to answer. What if I missed an important question? I don't know my result yet.",
            "Everything was scattered this morning. I rushed to collect my things, already afraid of being late.",
            "I stayed up reviewing one more lesson. I wanted to feel prepared, but by morning I was exhausted."
        };
        PlayerMovement movement;
        PlayerControls controls;
        CharacterController character;
        Animator animator;
        Transform canvas, hud, captionPanel, journal, pausePanel, completePanel;
        TMP_Text objective, checklist, prompt, caption, speaker, journalText;
        Image fade;
        Button advanceButton;
        GameObject skipButton;
        AudioSource sound;
        AudioClip tick, alarm;
        Coroutine sequence;
        bool advance, paused, journalOpen;
        int nearest = -1;
        float lineStarted, nextTick;
        float sunIntensity;
        Vector3 savedCameraPosition;
        Quaternion savedCameraRotation;
        readonly Color cream = new Color32(244, 234, 211, 255);
        readonly Color ink = new Color32(28, 37, 40, 242);
        readonly Color gold = new Color32(218, 177, 111, 255);

        IEnumerator Start()
        {
            movement = player.GetComponent<PlayerMovement>();
            controls = player.GetComponent<PlayerControls>();
            character = player.GetComponent<CharacterController>();
            animator = player.GetComponentInChildren<Animator>();
            sunIntensity = sunlight != null ? sunlight.intensity : 1;
            BuildUI();
            sound = gameObject.AddComponent<AudioSource>();
            sound.spatialBlend = 0;
            tick = Tone("Clock tick", 1100, .045f, false);
            alarm = Tone("Morning alarm", 760, 1.6f, true);
            SetLocked(true);
            while (EpisodeLoadingScreen.IsActive) yield return null;
            Mindscape.Data.MindscapeDatabase.EpisodeStarted(2, 1);
            sequence = StartCoroutine(Opening());
        }

        void Update()
        {
            if (canvas == null || EpisodeLoadingScreen.IsActive) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (journalOpen) ToggleJournal();
                else if (CurrentPhase != Phase.Complete) TogglePause();
            }
            if (paused) return;
            if (keyboard != null && keyboard.jKey.wasPressedThisFrame && CurrentPhase == Phase.Exploring) ToggleJournal();
            if (journalOpen) return;
            if (CurrentPhase == Phase.Opening || CurrentPhase == Phase.Memory || CurrentPhase == Phase.Ending)
            {
                if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)) Advance();
            }
            if (CurrentPhase == Phase.Exploring)
            {
                nearest = -1;
                float best = 3.4f;
                for (int i = 0; i < memoryObjects.Length; i++)
                {
                    Vector3 d = memoryObjects[i].position - (player.position + Vector3.up * 1.5f);
                    float distance = d.magnitude;
                    Vector3 flat = Vector3.ProjectOnPlane(d, Vector3.up);
                    float facing = flat.sqrMagnitude > .01f ? Vector3.Dot(player.forward, flat.normalized) : 1;
                    float score = distance + (1 - facing) * 1.2f;
                    if (distance <= 3.4f && facing > .05f && score < best) { best = score; nearest = i; }
                }
                prompt.text = nearest < 0 ? "Explore the bedroom. The desk is beside the wardrobe." :
                    "[ E ]  " + (nearest == 0 && ReadyForEnding ? "Pick up the paper • Remember the morning" : "Examine " + Titles[nearest]);
                if (nearest >= 0 && ((keyboard != null && keyboard.eKey.wasPressedThisFrame) || MobileInput.InteractPressed)) TryInspect(nearest);
            }
            if (Time.time >= nextTick && CurrentPhase != Phase.Complete)
            {
                sound.PlayOneShot(tick, CurrentPhase == Phase.Ending ? .32f : .075f);
                nextTick = Time.time + (CurrentPhase == Phase.Ending ? .55f : 1.1f);
            }
        }

        void LateUpdate()
        {
            for (int i = 0; i < memoryObjects.Length; i++)
            {
                var marker = memoryObjects[i].Find("Memory marker");
                if (marker == null) continue;
                marker.gameObject.SetActive(CurrentPhase == Phase.Exploring && !journalOpen && ((SeenMask & (1 << i)) == 0 || (i == 0 && ReadyForEnding)));
                marker.localPosition = new Vector3(0, .7f + Mathf.Sin(Time.time * 2) * .06f, 0);
                marker.Rotate(0, 36 * Time.deltaTime, 0, Space.World);
            }
        }

        public void Advance() { if (!paused && Time.unscaledTime - lineStarted > .25f) advance = true; }

        IEnumerator Line(string who, string text, Vector3 position, Vector3 target, float minimum = 1f)
        {
            captionPanel.gameObject.SetActive(true);
            speaker.text = Mindscape.Data.MindscapeDatabase.Personalize(who).ToUpperInvariant(); caption.text = Mindscape.Data.MindscapeDatabase.Personalize(text);
            advance = false; lineStarted = Time.unscaledTime;
            Vector3 start = storyCamera.transform.position;
            Quaternion rotation = storyCamera.transform.rotation;
            Quaternion end = Quaternion.LookRotation(target - position);
            float elapsed = 0;
            while (!advance || elapsed < minimum)
            {
                if (!paused)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.SmoothStep(0, 1, elapsed / 2.4f);
                    storyCamera.transform.SetPositionAndRotation(Vector3.Lerp(start, position, t), Quaternion.Slerp(rotation, end, t));
                }
                yield return null;
            }
        }

        IEnumerator Opening()
        {
            CurrentPhase = Phase.Opening;
            hud.gameObject.SetActive(false);
            skipButton.SetActive(true);
            Teleport(new Vector3(-22.7f, .62f, 10.5f), 180);
            storyCamera.transform.SetPositionAndRotation(new Vector3(-16, 6, 17), Quaternion.LookRotation(new Vector3(-22, 2, 7) - new Vector3(-16, 6, 17)));
            yield return Fade(1, 0, .9f);
            // A short walk establishes arrival before the interior match cut.
            if (animator != null) animator.SetFloat("Speed", 1);
            float walk = 0;
            while (walk < 2.2f) { walk += Time.deltaTime; player.position = Vector3.Lerp(new Vector3(-22.7f,.62f,10.5f), new Vector3(-22.7f,.62f,7.8f), walk/2.2f); yield return null; }
            if (animator != null) animator.SetFloat("Speed", 0);
            yield return Line("Chapter 2 / Episode 01", "The paper on the desk\nAfter school. The examination is over, but the worry has followed you home.", new Vector3(-17, 5, 13), new Vector3(-22.7f, 2, 7.8f));
            yield return Fade(0, 1, .5f);
            Teleport(new Vector3(-16.4f,.65f,-8.6f), 35);
            storyCamera.transform.position = new Vector3(-18,5.5f,-11);
            storyCamera.transform.LookAt(new Vector3(-14.8f,2,-6.9f));
            yield return Fade(1, 0, .6f);
            yield return Line("Joey / thinking", "It's finally over. I should be relieved, but I still feel worried.", new Vector3(-18,4.8f,-10), new Vector3(-14.8f,2,-7.4f));
            yield return Line("Joey / thinking", "I studied for this exam. I stayed up late because I didn't want to fail.\nBut I arrived after everyone had started.", new Vector3(-17,4.6f,-8), memoryObjects[0].position);
            yield return Line("Joey / thinking", "What if I made mistakes because I was rushing?\nI don't know my result yet, but I'm already imagining the worst.", new Vector3(-15.1f,3.8f,-9.5f), player.position + Vector3.up * 1.5f);
            BeginExploration();
        }

        public void SkipOpening()
        {
            if (CurrentPhase != Phase.Opening || paused) return;
            if (sequence != null) StopCoroutine(sequence);
            Teleport(new Vector3(-16.4f,.65f,-8.6f), 35);
            fade.color = new Color(0,0,0,0);
            BeginExploration();
        }

        void BeginExploration()
        {
            CurrentPhase = Phase.Exploring;
            captionPanel.gameObject.SetActive(false); skipButton.SetActive(false); hud.gameObject.SetActive(true);
            storyCamera.fieldOfView = 58;
            character.enabled = true;
            SetLocked(false);
            UpdateObjectives();
        }

        public bool TryInspect(int index)
        {
            if (CurrentPhase != Phase.Exploring || paused || journalOpen || index < 0 || index >= 3) return false;
            if (Vector3.Distance(player.position + Vector3.up * 1.5f, memoryObjects[index].position) > 3.4f) return false;
            savedCameraPosition = storyCamera.transform.position; savedCameraRotation = storyCamera.transform.rotation;
            sequence = StartCoroutine(index == 0 && ReadyForEnding ? Ending() : Memory(index));
            return true;
        }

        IEnumerator Memory(int index)
        {
            CurrentPhase = Phase.Memory; SetLocked(true); hud.gameObject.SetActive(false);
            Vector3 p = memoryObjects[index].position;
            yield return Line("Memory / " + Titles[index], Thoughts[index], p + new Vector3(.1f,1.9f,-1.8f), p);
            string second = index == 0 ? "The clock kept moving. I tried to remember what I had studied, but all I could think about was the time." :
                index == 1 ? "My ID, my examination materials, my bag. I was searching for everything at once." :
                "One more page. One more lesson. I thought studying longer would make the worry disappear.";
            if (index == 2 && deskLight != null) { deskLight.intensity = 4; if (sunlight != null) sunlight.intensity = .08f; }
            if (index == 1 && morningItems != null) morningItems.SetActive(true);
            if (index == 0 && examMemoryView != null)
            {
                yield return Fade(0,1,.35f);
                storyCamera.transform.SetPositionAndRotation(examMemoryView.position,examMemoryView.rotation);
                yield return Fade(1,0,.35f);
                yield return Line("Earlier / the examination",second,examMemoryView.position,examMemoryView.position+examMemoryView.forward*4);
                yield return Fade(0,1,.35f);
                storyCamera.transform.SetPositionAndRotation(savedCameraPosition,savedCameraRotation);
                yield return Fade(1,0,.35f);
            }
            else yield return Line(index == 2 ? "The night before / one more lesson" : "Examination morning / already late", second, p + new Vector3(.7f,1.5f,-1.6f), p);
            if (deskLight != null) deskLight.intensity = 2;
            if (sunlight != null) sunlight.intensity = sunIntensity;
            if (morningItems != null) morningItems.SetActive(false);
            SeenMask |= 1 << index;
            storyCamera.transform.SetPositionAndRotation(savedCameraPosition, savedCameraRotation);
            BeginExploration();
        }

        IEnumerator Ending()
        {
            CurrentPhase = Phase.Ending; SetLocked(true); hud.gameObject.SetActive(false);
            yield return Line("Joey / thinking", "Maybe if I had done things differently, the examination would have gone better.", memoryObjects[0].position + new Vector3(0,1.7f,-1.5f), memoryObjects[0].position);
            yield return Line("Joey / thinking", "If I had slept earlier. Packed my bag. Left sooner...", memoryObjects[0].position + new Vector3(0,1,-.8f), memoryObjects[0].position);
            captionPanel.gameObject.SetActive(false);
            yield return Fade(0, 1, 2);
            sound.PlayOneShot(alarm, .23f);
            PlayerPrefs.SetInt(SaveKey, 1); PlayerPrefs.Save(); Mindscape.Data.MindscapeDatabase.EpisodeCompleted(2, 1);
            CurrentPhase = Phase.Complete;
            completePanel.gameObject.SetActive(true);
        }

        public void ToggleJournal()
        {
            if (CurrentPhase != Phase.Exploring || paused) return;
            journalOpen = !journalOpen;
            journal.gameObject.SetActive(journalOpen); hud.gameObject.SetActive(!journalOpen); SetLocked(journalOpen);
            journalText.text = "";
            for (int i = 0; i < 3; i++) journalText.text += "<color=#DAB16F>" + Titles[i].ToUpperInvariant() + "</color>\n" + ((SeenMask & 1 << i) != 0 ? Thoughts[i] : "This memory has not been examined yet.") + "\n\n";
        }

        public void TogglePause()
        {
            if (CurrentPhase == Phase.Complete) return;
            paused = !paused; Time.timeScale = paused ? 0 : 1;
            pausePanel.gameObject.SetActive(paused);
            SetLocked(paused || CurrentPhase != Phase.Exploring || journalOpen);
            sound.mute = paused;
        }

        void SetLocked(bool locked)
        {
            if (controls != null) controls.dialogueOpen = locked;
            if (movement != null) movement.enabled = !locked;
            if (locked && animator != null) animator.SetFloat("Speed", 0);
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = locked;
        }

        void Teleport(Vector3 position, float yaw)
        {
            character.enabled = false;
            player.SetPositionAndRotation(position, Quaternion.Euler(0,yaw,0));
        }

        void UpdateObjectives()
        {
            objective.text = ReadyForEnding ? "Return to the examination paper" : "Examine the objects from today";
            checklist.text = MemoryCount + " / 3 MEMORIES\n";
            for (int i = 0; i < 3; i++) checklist.text += ((SeenMask & 1 << i) != 0 ? "[x] " : "[ ] ") + Titles[i] + "\n";
        }

        IEnumerator Fade(float from, float to, float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime) { fade.color = new Color(.035f,.045f,.05f,Mathf.Lerp(from,to,t/seconds)); yield return null; }
            fade.color = new Color(.035f,.045f,.05f,to);
        }

        AudioClip Tone(string name, float frequency, float seconds, bool repeating)
        {
            const int rate = 22050;
            float[] samples = new float[(int)(rate*seconds)];
            for (int i=0;i<samples.Length;i++) { float t=(float)i/rate; float envelope=repeating ? (Mathf.Repeat(t,.28f)<.13f ? .25f:0) : Mathf.Exp(-t*100)*.6f; samples[i]=Mathf.Sin(t*frequency*Mathf.PI*2)*envelope; }
            var clip=AudioClip.Create(name,samples.Length,1,rate,false); clip.SetData(samples,0); return clip;
        }

        public void Replay() { Time.timeScale=1; SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
        public void ReturnToMenu() { Time.timeScale=1; SceneManager.LoadScene("main_menu_new"); }
        public void ContinueToNextEpisode() { if (!string.IsNullOrEmpty(nextEpisodeScene) && Application.CanStreamedLevelBeLoaded(nextEpisodeScene)) { Time.timeScale=1; SceneManager.LoadScene(nextEpisodeScene); } }

        void OnDestroy()
        {
            Time.timeScale=1; Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
            if(canvas!=null) Destroy(canvas.gameObject);
            if(tick!=null) Destroy(tick); if(alarm!=null) Destroy(alarm);
        }

        RectTransform RegionBox(string name,Transform parent,Vector2 anchoredPos,Vector2 size,Vector2 pivot,Vector2 anchor,Color? color=null)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent,false);
            r.anchorMin=anchor;
            r.anchorMax=anchor;
            r.pivot=pivot;
            r.anchoredPosition=anchoredPos;
            r.sizeDelta=size;
            if(color.HasValue) { var im=r.gameObject.AddComponent<Image>(); im.color=color.Value; }
            return r;
        }

        RectTransform Region(string name, Transform parent, Vector2 min, Vector2 max, Color? color=null)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent,false); r.anchorMin=min; r.anchorMax=max; r.offsetMin=r.offsetMax=Vector2.zero;
            if(color.HasValue) { var im=r.gameObject.AddComponent<Image>(); im.color=color.Value; }
            return r;
        }
        TMP_Text Label(Transform parent,string text,Vector2 min,Vector2 max,float size,bool heading=false)
        {
            var t=Region(text,parent,min,max).gameObject.AddComponent<TextMeshProUGUI>(); t.text=text; t.font=heading?MinigameUI.SerifFont():MinigameUI.Font(false); t.fontSize=size; t.color=cream; t.raycastTarget=false; t.textWrappingMode=TextWrappingModes.Normal; t.enableAutoSizing=true; t.fontSizeMin=size*.7f; t.fontSizeMax=size; return t;
        }
        Button Button(Transform parent,string text,Vector2 min,Vector2 max,UnityEngine.Events.UnityAction action)
        {
            var r=Region(text,parent,min,max,new Color32(219,188,137,255)); var b=r.gameObject.AddComponent<Button>(); b.targetGraphic=r.GetComponent<Image>(); b.onClick.AddListener(action);
            var t=Label(r,text,new Vector2(.04f,.08f),new Vector2(.96f,.92f),20); t.color=ink; t.alignment=TextAlignmentOptions.Center; return b;
        }
        void BuildUI()
        {
            MinigameUI.EnsureInput();
            var canvasGo=new GameObject("Episode 1 • Story interface",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvas=canvasGo.transform;
            var c=canvasGo.GetComponent<Canvas>();
            c.renderMode=RenderMode.ScreenSpaceOverlay;
            c.sortingOrder=200;
            var scaler=canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);
            scaler.matchWidthOrHeight=.5f;
            hud=Region("Exploration",canvas,Vector2.zero,Vector2.one);
            var panel=RegionBox("Objective",hud,new Vector2(36f,-36f),new Vector2(460f,215f),new Vector2(0f,1f),new Vector2(0f,1f),ink);
            Label(panel,"CHAPTER 2  /  EPISODE 01",new Vector2(.06f,.79f),new Vector2(.94f,.95f),17).color=gold;
            objective=Label(panel,"",new Vector2(.06f,.52f),new Vector2(.94f,.78f),26,true);
            checklist=Label(panel,"",new Vector2(.06f,.06f),new Vector2(.94f,.50f),19);
            var bottom=RegionBox("Controls",hud,new Vector2(0f,28f),new Vector2(960f,86f),new Vector2(.5f,0f),new Vector2(.5f,0f),ink);
            prompt=Label(bottom,"",new Vector2(.03f,.44f),new Vector2(.97f,.94f),22); prompt.alignment=TextAlignmentOptions.Center;
            var help=Label(bottom,"WASD  Move    •    Mouse  Look    •    E  Examine    •    J  Journal    •    Esc  Pause",new Vector2(.03f,.06f),new Vector2(.97f,.42f),16); help.alignment=TextAlignmentOptions.Center; help.color=gold;
            var jBtn=RegionBox("Journal [J]",hud,new Vector2(-36f,-36f),new Vector2(180f,44f),new Vector2(1f,1f),new Vector2(1f,1f));
            Button(jBtn,"Journal [J]",Vector2.zero,Vector2.one,ToggleJournal);
            captionPanel=Region("Cinematic subtitles",canvas,new Vector2(.07f,.03f),new Vector2(.93f,.20f),ink); // a compact box with a margin, not a full-width bar
            speaker=Label(captionPanel,"",new Vector2(.035f,.74f),new Vector2(.70f,.94f),15); speaker.color=gold;
            caption=Label(captionPanel,"",new Vector2(.035f,.10f),new Vector2(.78f,.72f),23,true);
            advanceButton=Button(captionPanel,"Continue [Space]",new Vector2(.80f,.12f),new Vector2(.965f,.42f),Advance);
            skipButton=Button(canvas,"Skip opening",new Vector2(.85f,.90f),new Vector2(.97f,.96f),SkipOpening).gameObject;
            journal=Region("Thought journal",canvas,new Vector2(.15f,.14f),new Vector2(.85f,.86f),ink);
            Label(journal,"Thought journal",new Vector2(.06f,.80f),new Vector2(.94f,.94f),42,true);
            journalText=Label(journal,"",new Vector2(.06f,.16f),new Vector2(.94f,.78f),24);
            Button(journal,"Return [J]",new Vector2(.68f,.04f),new Vector2(.94f,.13f),ToggleJournal); journal.gameObject.SetActive(false);
            fade=Region("Fade",canvas,Vector2.zero,Vector2.one,new Color(.035f,.045f,.05f,1)).GetComponent<Image>(); fade.raycastTarget=false;
            completePanel=Region("Episode complete",canvas,new Vector2(.18f,.17f),new Vector2(.82f,.83f),ink);
            Label(completePanel,"CHAPTER 2  /  EPISODE 01 COMPLETE",new Vector2(.08f,.81f),new Vector2(.92f,.92f),19).color=gold;
            Label(completePanel,"Before the bell",new Vector2(.08f,.62f),new Vector2(.92f,.79f),55,true);
            Label(completePanel,"The clock becomes an alarm.\nThe memory takes you back to the morning of the examination.\n\n3 memories collected. Your completion checkpoint is saved.",new Vector2(.08f,.29f),new Vector2(.92f,.60f),26);
            Label(completePanel,"NEXT: EPISODE 02  •  THE MORNING FLASHBACK",new Vector2(.08f,.19f),new Vector2(.92f,.27f),17).color=gold;
            Button(completePanel,"Replay episode",new Vector2(.08f,.055f),new Vector2(.46f,.16f),Replay);
            bool nextReady=!string.IsNullOrEmpty(nextEpisodeScene)&&Application.CanStreamedLevelBeLoaded(nextEpisodeScene);
            Button(completePanel,nextReady?"Continue":"Main menu",new Vector2(.54f,.055f),new Vector2(.92f,.16f),nextReady?(UnityEngine.Events.UnityAction)ContinueToNextEpisode:ReturnToMenu);
            completePanel.gameObject.SetActive(false);
            pausePanel=Region("Pause",canvas,new Vector2(.32f,.30f),new Vector2(.68f,.70f),ink);
            Label(pausePanel,"A quiet moment",new Vector2(.08f,.67f),new Vector2(.92f,.90f),38,true);
            Button(pausePanel,"Resume [Esc]",new Vector2(.1f,.42f),new Vector2(.9f,.60f),TogglePause);
            Button(pausePanel,"Main menu",new Vector2(.1f,.16f),new Vector2(.9f,.34f),ReturnToMenu); pausePanel.gameObject.SetActive(false);
        }
    }
}
