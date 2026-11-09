using System.Collections;
using Mindscape.Minigames;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Mindscape.EpisodeTwo
{
    // Chapter 2 · Episode 2 (the morning): alarm, preparing items against a timer (preparationSeconds), walking
    // to the bus stop, a flashback and boarding. The Phase enum is the story order. Text lines live in this file.
    public sealed class EpisodeTwoStory : MonoBehaviour, IPausable
    {
        public const string SceneName = "Chapter 2 - Episode 2";
        public static string SaveKey => Mindscape.Data.MindscapeDatabase.ProfileKey("Mindscape.FamilyExpectations.Episode2");
        public enum Phase { Opening, Alarm, Preparing, Leaving, Walking, Waiting, Flashback, Boarding, Departing, Complete }
        public Transform player, alarmClock, exit, stop, bus, boardingPoint, studyView;
        public Transform[] items;
        public Camera storyCamera;
        public Light sun, deskLamp;
        public GameObject carriedBag, phoneModel, routeMarkers;
        public TMP_Text clockFace;
        [Min(1)] public float preparationSeconds = 300;
        public Phase CurrentPhase { get; private set; }
        public int CollectedMask { get; private set; }
        public bool AllPacked => CollectedMask == 7;
        public bool RunningLate { get; private set; }
        public bool IsPaused { get; private set; }
        public float RemainingSeconds { get; private set; }
        public int CollectedCount => ((CollectedMask&1)!=0?1:0)+((CollectedMask&2)!=0?1:0)+((CollectedMask&4)!=0?1:0);

        static readonly string[] Names = { "Student ID", "Examination materials", "School bag" };
        static readonly string[] Hints = { "Beside the bed, near the alarm clock.", "On the study desk, beside the notebook.", "In the living room, beside the front door." };
        PlayerMovement movement;
        PlayerControls controls;
        CharacterController character;
        Animator animator;
        Transform canvasTransform, hud, subtitles, pause, completion, phone;
        TMP_Text objective, checklist, timer, prompt, subtitle, speaker, toast, phoneTime, completionText;
        Image fade, pressure;
        GameObject skip;
        AudioSource effects, ambience;
        AudioClip alarmSound, tickSound, stepSound, engineSound, pickupSound;
        Coroutine sequence;
        bool advance, phoneOpen, resumedFromSkip;
        float lineTime, toastUntil, audioAt, stepAt, phoneCount;
        Vector3 lastPosition, busInitial;
        float sunInitial;
        int nearest=-1;
        Color cream=new Color32(244,234,211,255), ink=new Color32(28,37,40,245), gold=new Color32(218,177,111,255);

        IEnumerator Start()
        {
            movement=player.GetComponent<PlayerMovement>(); controls=player.GetComponent<PlayerControls>(); character=player.GetComponent<CharacterController>(); animator=player.GetComponentInChildren<Animator>();
            RemainingSeconds=preparationSeconds; sunInitial=sun.intensity; busInitial=bus.position; lastPosition=player.position;
            BuildUI(); SetLocked(true); carriedBag.SetActive(false); phoneModel.SetActive(false); routeMarkers.SetActive(false);
            effects=gameObject.AddComponent<AudioSource>(); ambience=gameObject.AddComponent<AudioSource>();
            alarmSound=Sound("Alarm",780,1.4f,0); tickSound=Sound("Clock",1000,.04f,1); stepSound=Sound("Footstep",90,.12f,2); engineSound=Sound("Bus engine",65,2,3); pickupSound=Sound("Collected",540,.12f,1);
            ambience.clip=alarmSound; ambience.loop=true; ambience.volume=.16f;
            while(EpisodeLoadingScreen.IsActive) yield return null;
            PlayerPrefs.SetInt(Mindscape.Data.MindscapeProgress.ChapterKey,2); PlayerPrefs.SetInt(Mindscape.Data.MindscapeProgress.EpisodeKey,3); PlayerPrefs.Save(); Mindscape.Data.MindscapeDatabase.EpisodeStarted(2,2);
            sequence=StartCoroutine(Opening());
        }

        void Update()
        {
            if(hud==null || EpisodeLoadingScreen.IsActive) return;
            var k=Keyboard.current;
            if(k!=null && k.escapeKey.wasPressedThisFrame) { if(phoneOpen) TogglePhone(); else TogglePause(); }
            if(IsPaused) return;
            if(k!=null && k.pKey.wasPressedThisFrame) TogglePhone();
            if(phoneOpen) return;
            if(k!=null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame)) Advance();
            if(CurrentPhase==Phase.Preparing) TickPreparation(Time.deltaTime);
            if(IsPlayable())
            {
                UpdatePrompt();
                if(k!=null && k.eKey.wasPressedThisFrame) Interact();
                if(k!=null && k.hKey.wasPressedThisFrame) ShowHint();
                if(Time.time>stepAt && character.isGrounded && Vector3.Distance(player.position,lastPosition)>.008f)
                { effects.PlayOneShot(stepSound,.14f); stepAt=Time.time+.4f; }
                lastPosition=player.position;
            }
            if(CurrentPhase==Phase.Preparing && Time.time>audioAt)
            { effects.PlayOneShot(tickSound,RunningLate?.15f:.045f); audioAt=Time.time+(RunningLate?.65f:1.15f); }
            toast.gameObject.SetActive(Time.unscaledTime<toastUntil);
            pressure.color=new Color(.30f,.16f,.08f,CurrentPhase==Phase.Preparing && RemainingSeconds<60 ? .07f+.025f*Mathf.Sin(Time.time*2):0);
            if(CurrentPhase==Phase.Preparing) timer.text=RunningLate?"RUNNING LATE • KEEP GOING":(Mathf.CeilToInt(RemainingSeconds)/60).ToString("00")+":"+(Mathf.CeilToInt(RemainingSeconds)%60).ToString("00")+"  TO GET READY";
            if(player.position.y < -8 && IsPlayable()) Teleport(CurrentPhase==Phase.Walking?new Vector3(-22,.4f,21):new Vector3(-16.8f,.65f,-10.5f),0);
        }

        void LateUpdate()
        {
            for(int i=0;i<items.Length;i++)
            {
                var mark=items[i].Find("Objective glint"); if(mark==null) continue;
                mark.gameObject.SetActive(CurrentPhase==Phase.Preparing && (CollectedMask&(1<<i))==0);
                mark.localPosition=Vector3.up*(.65f+Mathf.Sin(Time.time*2+i)*.06f); mark.Rotate(0,40*Time.deltaTime,0);
            }
        }
        bool IsPlayable() => CurrentPhase==Phase.Alarm || CurrentPhase==Phase.Preparing || CurrentPhase==Phase.Walking || CurrentPhase==Phase.Waiting || CurrentPhase==Phase.Boarding;
        bool InRange(Transform target,float range=3.25f) => Vector3.Distance(player.position+Vector3.up*1.5f,target.position)<range;
        public void Advance() { if(!IsPaused && !phoneOpen && Time.unscaledTime-lineTime>.25f) advance=true; }

        public void TickPreparation(float delta)
        {
            if(CurrentPhase!=Phase.Preparing || IsPaused || phoneOpen || RunningLate) return;
            RemainingSeconds=Mathf.Max(0,RemainingSeconds-Mathf.Max(0,delta));
            if(RemainingSeconds<=0) { RunningLate=true; Notify("I'm running late. One thing at a time. Press H for a hint."); UpdateObjectives(); }
        }
        public void Interact()
        {
            if(IsPaused || phoneOpen) return;
            switch(CurrentPhase)
            {
                case Phase.Alarm: if(InRange(alarmClock)) SilenceAlarm(); break;
                case Phase.Preparing:
                    if(nearest>=0) TryCollect(nearest);
                    else if(InRange(exit)) TryLeave();
                    break;
                case Phase.Walking: if(InRange(stop,4)) ReachStop(); break;
                case Phase.Waiting: CheckTimeAtStop(); break;
                case Phase.Boarding: TryBoard(); break;
            }
        }
        void UpdatePrompt()
        {
            nearest=-1;
            if(CurrentPhase==Phase.Alarm) prompt.text=InRange(alarmClock)?"[ E ] Turn off the alarm":"Walk to the bedside alarm clock";
            if(CurrentPhase==Phase.Preparing)
            {
                float best=4;
                for(int i=0;i<items.Length;i++)
                {
                    if((CollectedMask&(1<<i))!=0) continue;
                    Vector3 delta=items[i].position-(player.position+Vector3.up*1.5f);
                    float facing=Vector3.Dot(player.forward,Vector3.ProjectOnPlane(delta,Vector3.up).normalized);
                    float score=delta.magnitude+(1-facing)*.6f;
                    if(delta.magnitude<3.25f && score<best) { best=score; nearest=i; }
                }
                prompt.text=nearest>=0?"[ E ] Take "+Names[nearest]:InRange(exit)?(AllPacked?"[ E ] Leave for school":"[ E ] Check your belongings"):"Find your belongings • H for a hint";
            }
            if(CurrentPhase==Phase.Walking) prompt.text=InRange(stop,4)?"[ E ] Wait at the bus stop":"Follow the gold markers to the stop • P checks the time";
            if(CurrentPhase==Phase.Waiting) prompt.text="[ E ] Check the time and wait";
            if(CurrentPhase==Phase.Boarding) prompt.text=InRange(boardingPoint,4)?"[ E ] Board the bus":"Walk to the open bus door";
        }
        public bool SilenceAlarm()
        {
            if(CurrentPhase!=Phase.Alarm || IsPaused || phoneOpen || !InRange(alarmClock)) return false;
            ambience.Stop(); CurrentPhase=Phase.Preparing; RemainingSeconds=preparationSeconds;
            Notify("My ID, my examination materials, and my bag. Where did I put everything?"); UpdateObjectives(); return true;
        }
        public bool TryCollect(int index)
        {
            if(CurrentPhase!=Phase.Preparing || IsPaused || phoneOpen || index<0 || index>=items.Length || (CollectedMask&(1<<index))!=0 || !InRange(items[index])) return false;
            CollectedMask|=1<<index; items[index].gameObject.SetActive(false); effects.PlayOneShot(pickupSound,.18f);
            if(index==2) carriedBag.SetActive(true);
            Notify(AllPacked?"Everything is packed. I need to leave now.":Names[index]+" collected. "+CollectedCount+" of 3 ready."); UpdateObjectives(); return true;
        }
        public bool TryLeave()
        {
            if(CurrentPhase!=Phase.Preparing || IsPaused || phoneOpen || !InRange(exit)) return false;
            if(!AllPacked) { Notify("I still need my belongings. Press H to find the next item."); return false; }
            sequence=StartCoroutine(LeaveHome()); return true;
        }
        public bool ReachStop()
        {
            if(CurrentPhase!=Phase.Walking || IsPaused || phoneOpen || !InRange(stop,4)) return false;
            CurrentPhase=Phase.Waiting; UpdateObjectives(); Notify("No bus yet. The traffic is barely moving."); return true;
        }
        public bool CheckTimeAtStop()
        {
            if(CurrentPhase!=Phase.Waiting || IsPaused || phoneOpen || !InRange(stop,4)) return false;
            sequence=StartCoroutine(WaitAndRemember()); return true;
        }
        public bool TryBoard()
        {
            if(CurrentPhase!=Phase.Boarding || IsPaused || phoneOpen || !InRange(boardingPoint,4)) return false;
            sequence=StartCoroutine(Departure()); return true;
        }
        public void ShowHint()
        {
            if(CurrentPhase!=Phase.Preparing || IsPaused || phoneOpen) return;
            for(int i=0;i<3;i++) if((CollectedMask&(1<<i))==0) { Notify(Names[i]+": "+Hints[i]); return; }
            Notify("Take everything to the front door, through the living room.");
        }

        IEnumerator Opening()
        {
            CurrentPhase=Phase.Opening; hud.gameObject.SetActive(false); skip.SetActive(true); ambience.Play();
            Teleport(new Vector3(-16.8f,.65f,-11),0);
            storyCamera.transform.position=new Vector3(-17.9f,4.1f,-11.5f); storyCamera.transform.LookAt(alarmClock.position);
            yield return Fade(1,0,.8f);
            yield return Line("CHAPTER 2 / EPISODE 02","Before the bell\nExamination morning. An alarm cuts through the quiet.",new Vector3(-17.3f,3.2f,-12.1f),alarmClock.position);
            yield return Line("JOEY","Wait... what time is it?\nOh no. I'm going to be late for the examination!",new Vector3(-17.6f,4.3f,-9.5f),player.position+Vector3.up*1.8f);
            FinishOpening();
        }
        public void SkipOpening()
        {
            if(CurrentPhase!=Phase.Opening || IsPaused || hud==null || EpisodeLoadingScreen.IsActive) return;
            if(sequence!=null) StopCoroutine(sequence); resumedFromSkip=true; fade.color=Color.clear; FinishOpening();
        }
        void FinishOpening()
        {
            if(resumedFromSkip) Teleport(new Vector3(-16.8f,.65f,-11),0);
            CurrentPhase=Phase.Alarm; subtitles.gameObject.SetActive(false); skip.SetActive(false); hud.gameObject.SetActive(true); SetLocked(false); UpdateObjectives();
        }
        IEnumerator LeaveHome()
        {
            CurrentPhase=Phase.Leaving; SetLocked(true); hud.gameObject.SetActive(false);
            yield return Line("JOEY",RunningLate?"I'm later than I planned. But I have everything. I need to keep going.":"Okay, I have everything. I just need to get to school!",new Vector3(-24,4,1),player.position+Vector3.up*1.7f);
            yield return Fade(0,1,.5f); Teleport(new Vector3(-22.1f,.7f,8.2f),0);
            storyCamera.transform.position=new Vector3(-22.1f,5,4); storyCamera.transform.LookAt(new Vector3(-22.1f,2,13));
            yield return Fade(1,0,.5f);
            CurrentPhase=Phase.Walking; subtitles.gameObject.SetActive(false); hud.gameObject.SetActive(true); routeMarkers.SetActive(true); SetLocked(false); UpdateObjectives();
            ambience.clip=engineSound; ambience.loop=true; ambience.volume=.035f; ambience.Play();
        }
        IEnumerator WaitAndRemember()
        {
            CurrentPhase=Phase.Flashback; SetLocked(true); hud.gameObject.SetActive(false);
            yield return Line("JOEY / AT THE STOP","Please, not today. I have an examination.\nThe traffic isn't moving. I can't make the bus arrive sooner.",stop.position+new Vector3(-3,3,-3),stop.position+Vector3.up);
            Vector3 previous=player.position; Quaternion rotation=player.rotation;
            yield return Fade(0,1,.5f); sun.intensity=.08f; deskLamp.intensity=4;
            Teleport(new Vector3(-16.7f,.65f,-8.6f),0); carriedBag.SetActive(false);
            storyCamera.transform.SetPositionAndRotation(studyView.position,studyView.rotation);
            yield return Fade(1,0,.5f);
            yield return Line("THE NIGHT BEFORE","One more page. One more lesson.\nI thought studying longer would make me feel prepared.",studyView.position,studyView.position+studyView.forward*3);
            yield return Line("JOEY / REMEMBERING","I should have slept earlier. I didn't realize how tired I would be this morning.",studyView.position+Vector3.up*.2f,studyView.position+studyView.forward*3);
            yield return Fade(0,1,.5f); sun.intensity=sunInitial; deskLamp.intensity=1; Teleport(previous,rotation.eulerAngles.y); carriedBag.SetActive(true);
            bus.gameObject.SetActive(true); bus.position=busInitial+Vector3.left*18;
            storyCamera.transform.position=stop.position+new Vector3(-5,5,-5); storyCamera.transform.LookAt(busInitial+Vector3.up*1.5f);
            yield return Fade(1,0,.5f);
            float elapsed=0; while(elapsed<3) { elapsed+=Time.deltaTime; bus.position=Vector3.Lerp(busInitial+Vector3.left*18,busInitial,Mathf.SmoothStep(0,1,elapsed/3)); yield return null; }
            yield return Line("JOEY","It's here. I can still make it. I just need to get there.",stop.position+new Vector3(-5,4,-4),bus.position+Vector3.up*1.5f);
            CurrentPhase=Phase.Boarding; subtitles.gameObject.SetActive(false); hud.gameObject.SetActive(true); SetLocked(false); UpdateObjectives();
        }
        IEnumerator Departure()
        {
            CurrentPhase=Phase.Departing; SetLocked(true); hud.gameObject.SetActive(false); routeMarkers.SetActive(false);
            yield return Fade(0,1,.4f);
            player.gameObject.SetActive(false); storyCamera.transform.position=bus.position+new Vector3(8,6,-10); storyCamera.transform.LookAt(bus.position+Vector3.up*1.5f);
            yield return Fade(1,0,.4f);
            yield return Line("JOEY / THINKING","The examination has already started.\nWhen I arrive, I'll have to begin with the time I have left.",bus.position+new Vector3(8,6,-10),bus.position+Vector3.up*1.5f);
            subtitles.gameObject.SetActive(false); effects.PlayOneShot(engineSound,.2f);
            float elapsed=0; while(elapsed<4) { elapsed+=Time.deltaTime; bus.position+=Vector3.right*Time.deltaTime*(2+elapsed); yield return null; }
            yield return Fade(0,1,1);
            CurrentPhase=Phase.Complete; PlayerPrefs.SetInt(SaveKey,1); PlayerPrefs.Save(); Mindscape.Data.MindscapeDatabase.EpisodeCompleted(2,2);
            completionText.text="You found your belongings and caught the bus.\nBecause of the delay, you reached school after the examination had begun.\n\nYour Episode 2 completion is saved.";
            completion.gameObject.SetActive(true); ambience.Stop();
        }
        IEnumerator Line(string who,string text,Vector3 position,Vector3 target)
        {
            subtitles.gameObject.SetActive(true); speaker.text=Mindscape.Data.MindscapeDatabase.Personalize(who); subtitle.text=Mindscape.Data.MindscapeDatabase.Personalize(text); advance=false; lineTime=Time.unscaledTime;
            Vector3 origin=storyCamera.transform.position; Quaternion rotation=storyCamera.transform.rotation; float elapsed=0;
            while(!advance || elapsed<.8f)
            {
                elapsed+=Time.deltaTime; float t=Mathf.SmoothStep(0,1,elapsed/1.8f); storyCamera.transform.SetPositionAndRotation(Vector3.Lerp(origin,position,t),Quaternion.Slerp(rotation,Quaternion.LookRotation(target-position),t)); yield return null;
            }
        }
        IEnumerator Fade(float from,float to,float seconds)
        { for(float t=0;t<seconds;t+=Time.deltaTime) { fade.color=new Color(.035f,.045f,.05f,Mathf.Lerp(from,to,t/seconds)); yield return null; } fade.color=new Color(.035f,.045f,.05f,to); }
        void Teleport(Vector3 position,float yaw)
        { character.enabled=false; player.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0)); movement.ResetView(yaw); character.enabled=true; lastPosition=position; }
        void SetLocked(bool locked)
        { movement.enabled=!locked; controls.dialogueOpen=locked; if(locked && animator!=null) animator.SetFloat("Speed",0); Cursor.lockState=locked?CursorLockMode.None:CursorLockMode.Locked; Cursor.visible=locked; }
        void Notify(string message) { toast.text=message; toastUntil=Time.unscaledTime+6; }
        public void TogglePause()
        {
            if(CurrentPhase==Phase.Complete || pause==null) return;
            IsPaused=!IsPaused; Time.timeScale=IsPaused?0:1; pause.gameObject.SetActive(IsPaused); SetLocked(IsPaused || !IsPlayable() || phoneOpen); effects.mute=ambience.mute=IsPaused;
        }
        public void TogglePhone()
        {
            if(IsPaused || (CurrentPhase!=Phase.Walking && CurrentPhase!=Phase.Boarding)) return;
            phoneOpen=!phoneOpen; phone.gameObject.SetActive(phoneOpen); phoneModel.SetActive(phoneOpen); hud.gameObject.SetActive(!phoneOpen); SetLocked(phoneOpen);
            if(phoneOpen) { phoneCount++; phoneTime.text="08:"+(phoneCount<2?"04":"06")+"\n\nExamination started at 08:00.\n\n"+(phoneCount<2?"Please, let the bus be close.":"Checking again won't make it come faster. I need to reach the stop."); }
        }
        void UpdateObjectives()
        {
            switch(CurrentPhase)
            {
                case Phase.Alarm: objective.text="Turn off the alarm"; break;
                case Phase.Preparing: objective.text=AllPacked?"Leave through the front door":"Find your examination belongings"; break;
                case Phase.Walking: objective.text="Reach the neighborhood bus stop"; break;
                case Phase.Waiting: objective.text="Check the time while you wait"; break;
                case Phase.Boarding: objective.text="Board the bus"; break;
            }
            checklist.text=CurrentPhase==Phase.Alarm?"Examination morning • 07:50":CurrentPhase==Phase.Preparing?CollectedCount+" / 3 READY\n":"School route • follow the gold markers\n";
            if(CurrentPhase==Phase.Preparing) for(int i=0;i<3;i++) checklist.text+=((CollectedMask&(1<<i))!=0?"[x] ":"[ ] ")+Names[i]+"\n";
            timer.gameObject.SetActive(CurrentPhase==Phase.Preparing);
        }
        public void Replay() { Time.timeScale=1; SceneManager.LoadScene(SceneName); }
        public void ContinueToNextEpisode()
        {
            if(CurrentPhase!=Phase.Complete || !Application.CanStreamedLevelBeLoaded(Mindscape.EpisodeThree.EpisodeThreeStory.SceneName)) return;
            Time.timeScale=1; EpisodeLoadingScreen.Load(Mindscape.EpisodeThree.EpisodeThreeStory.SceneName,3,2);
        }
        public void ReturnToMenu() { Time.timeScale=1; SceneManager.LoadScene("main_menu_new"); }
        void OnDestroy() { Time.timeScale=1; Cursor.lockState=CursorLockMode.None; Cursor.visible=true; if(canvasTransform!=null) Destroy(canvasTransform.gameObject); foreach(var c in new[]{alarmSound,tickSound,stepSound,engineSound,pickupSound}) if(c!=null) Destroy(c); }
        AudioClip Sound(string name,float frequency,float duration,int kind)
        {
            const int rate=22050; float[] data=new float[(int)(rate*duration)]; var random=new System.Random(73);
            for(int i=0;i<data.Length;i++) { float t=(float)i/rate; float wave=Mathf.Sin(t*frequency*Mathf.PI*2); float env=1;
                if(kind==0) env=Mathf.Repeat(t,.28f)<.13f?.35f:0;
                if(kind==1) env=Mathf.Exp(-t*45)*.45f;
                if(kind==2) { wave=(float)random.NextDouble()*2-1; env=Mathf.Exp(-t*40)*.3f; }
                if(kind==3) { wave=wave*.35f+Mathf.Sin(t*frequency*2*Mathf.PI*2)*.13f; env=.6f+.08f*Mathf.Sin(t*6*Mathf.PI); }
                data[i]=wave*env;
            }
            var clip=AudioClip.Create(name,data.Length,1,rate,false); clip.SetData(data,0); return clip;
        }

        RectTransform Box(string name,Transform parent,Vector2 anchoredPos,Vector2 size,Vector2 pivot,Vector2 anchor,Color? color=null)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent,false);
            r.anchorMin=anchor;
            r.anchorMax=anchor;
            r.pivot=pivot;
            r.anchoredPosition=anchoredPos;
            r.sizeDelta=size;
            if(color.HasValue) r.gameObject.AddComponent<Image>().color=color.Value;
            return r;
        }
        RectTransform Panel(string name,Transform parent,Vector2 a,Vector2 b,Color? color=null)
        { var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent,false); r.anchorMin=a; r.anchorMax=b; r.offsetMin=r.offsetMax=Vector2.zero; if(color.HasValue) r.gameObject.AddComponent<Image>().color=color.Value; return r; }
        TMP_Text Label(Transform parent,string text,Vector2 a,Vector2 b,float size,bool heading=false)
        { var t=Panel("Text",parent,a,b).gameObject.AddComponent<TextMeshProUGUI>(); t.text=text; t.font=heading?MinigameUI.SerifFont():MinigameUI.Font(false); t.fontSize=size; t.color=cream; t.enableAutoSizing=true; t.fontSizeMin=size*.7f; t.fontSizeMax=size; t.raycastTarget=false; t.textWrappingMode=TextWrappingModes.Normal; return t; }
        void Button(Transform parent,string text,Vector2 a,Vector2 b,UnityEngine.Events.UnityAction action)
        { var r=Panel(text,parent,a,b,gold); var button=r.gameObject.AddComponent<Button>(); button.targetGraphic=r.GetComponent<Image>(); button.onClick.AddListener(action); var t=Label(r,text,new Vector2(.04f,.08f),new Vector2(.96f,.92f),22); t.alignment=TextAlignmentOptions.Center; t.color=ink; }
        void BuildUI()
        {
            MinigameUI.EnsureInput();
            var canvasGo=new GameObject("Episode 2 interface",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvasTransform=canvasGo.transform;
            var c=canvasGo.GetComponent<Canvas>();
            c.renderMode=RenderMode.ScreenSpaceOverlay;
            c.sortingOrder=210;
            var scaler=canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);
            scaler.matchWidthOrHeight=.5f;
            pressure=Panel("Pressure tint",canvasTransform,Vector2.zero,Vector2.one,Color.clear).GetComponent<Image>();
            pressure.raycastTarget=false;
            hud=Panel("Exploration",canvasTransform,Vector2.zero,Vector2.one);

            // Safe corner-anchored objectives panel
            var box=Box("Objectives",hud,new Vector2(36f,-36f),new Vector2(460f,225f),new Vector2(0f,1f),new Vector2(0f,1f),ink);
            Label(box,"CHAPTER 2  /  EPISODE 02",new Vector2(.06f,.81f),new Vector2(.94f,.95f),17).color=gold;
            objective=Label(box,"",new Vector2(.06f,.54f),new Vector2(.94f,.79f),26,true);
            checklist=Label(box,"",new Vector2(.06f,.06f),new Vector2(.94f,.52f),19);

            // Timer anchored top-right
            var timerBox=Box("Timer",hud,new Vector2(-36f,-36f),new Vector2(460f,38f),new Vector2(1f,1f),new Vector2(1f,1f));
            timer=timerBox.gameObject.AddComponent<TextMeshProUGUI>();
            timer.font=MinigameUI.Font(true);
            timer.fontSize=23;
            timer.color=gold;
            timer.alignment=TextAlignmentOptions.Right;
            timer.raycastTarget=false;

            // Controls anchored bottom-center
            var bottom=Box("Controls",hud,new Vector2(0f,28f),new Vector2(960f,86f),new Vector2(.5f,0f),new Vector2(.5f,0f),ink);
            prompt=Label(bottom,"",new Vector2(.025f,.46f),new Vector2(.975f,.92f),22);
            prompt.alignment=TextAlignmentOptions.Center;
            Label(bottom,"WASD  Move   •   Mouse  Look   •   E  Interact   •   H  Hint   •   P  Phone   •   Esc  Pause",new Vector2(.025f,.08f),new Vector2(.975f,.44f),16).alignment=TextAlignmentOptions.Center;

            toast=Box("Toast",hud,new Vector2(0f,140f),new Vector2(760f,50f),new Vector2(.5f,0f),new Vector2(.5f,0f)).gameObject.AddComponent<TextMeshProUGUI>();
            toast.font=MinigameUI.Font(false);
            toast.fontSize=22;
            toast.color=cream;
            toast.alignment=TextAlignmentOptions.Center;
            toast.raycastTarget=false;

            var subRect=Box("Subtitles",canvasTransform,new Vector2(0f,28f),new Vector2(1100f,140f),new Vector2(.5f,0f),new Vector2(.5f,0f),ink);
            subtitles=subRect;
            speaker=Label(subtitles,"",new Vector2(.035f,.72f),new Vector2(.70f,.94f),15);
            speaker.color=gold;
            subtitle=Label(subtitles,"",new Vector2(.035f,.10f),new Vector2(.78f,.70f),23,true);
            Button(subtitles,"Continue [Space]",new Vector2(.80f,.12f),new Vector2(.965f,.42f),Advance);

            var skipRoot=Box("Skip opening",canvasTransform,new Vector2(-36f,-36f),new Vector2(180f,44f),new Vector2(1f,1f),new Vector2(1f,1f));
            Button(skipRoot,"Skip opening",Vector2.zero,Vector2.one,SkipOpening);
            skip=skipRoot.gameObject;

            phone=Box("Phone",canvasTransform,Vector2.zero,new Vector2(560f,600f),new Vector2(.5f,.5f),new Vector2(.5f,.5f),ink);
            Label(phone,"EXAMINATION MORNING",new Vector2(.08f,.84f),new Vector2(.92f,.95f),20).color=gold;
            phoneTime=Label(phone,"",new Vector2(.08f,.23f),new Vector2(.92f,.80f),28,true);
            Button(phone,"Put away [P]",new Vector2(.08f,.06f),new Vector2(.92f,.17f),TogglePhone);
            phone.gameObject.SetActive(false);

            fade=Panel("Fade",canvasTransform,Vector2.zero,Vector2.one,new Color(.035f,.045f,.05f,1)).GetComponent<Image>();
            fade.raycastTarget=false;

            completion=Box("Episode complete",canvasTransform,Vector2.zero,new Vector2(980f,600f),new Vector2(.5f,.5f),new Vector2(.5f,.5f),ink);
            Label(completion,"EPISODE 02 COMPLETE",new Vector2(.07f,.83f),new Vector2(.93f,.94f),21).color=gold;
            Label(completion,"Running out of time",new Vector2(.07f,.62f),new Vector2(.93f,.81f),48,true);
            completionText=Label(completion,"",new Vector2(.07f,.29f),new Vector2(.93f,.60f),25);
            Label(completion,"NEXT: EPISODE 03 • RUNNING OUT OF TIME",new Vector2(.07f,.20f),new Vector2(.93f,.27f),18).color=gold;
            Button(completion,"Replay",new Vector2(.07f,.055f),new Vector2(.30f,.16f),Replay);
            Button(completion,"Main menu",new Vector2(.33f,.055f),new Vector2(.57f,.16f),ReturnToMenu);
            Button(completion,"Continue to Episode 3",new Vector2(.60f,.055f),new Vector2(.93f,.16f),ContinueToNextEpisode);
            completion.gameObject.SetActive(false);

            pause=Box("Pause",canvasTransform,Vector2.zero,new Vector2(560f,380f),new Vector2(.5f,.5f),new Vector2(.5f,.5f),ink);
            Label(pause,"Take a moment",new Vector2(.08f,.67f),new Vector2(.92f,.9f),38,true);
            Button(pause,"Resume [Esc]",new Vector2(.10f,.40f),new Vector2(.9f,.6f),TogglePause);
            Button(pause,"Main menu",new Vector2(.10f,.13f),new Vector2(.9f,.33f),ReturnToMenu);
            pause.gameObject.SetActive(false);
        }
    }
}
