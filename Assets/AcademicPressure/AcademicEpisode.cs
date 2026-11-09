using System;
using System.Collections;
using System.Collections.Generic;
using Mindscape.Data;
using Mindscape.Minigames;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UIE = UnityEngine.UIElements;
using static UnityEngine.UIElements.UQueryExtensions;

namespace Mindscape.AcademicPressure
{
    // Shared runtime for the story episodes (Chapters 1-3). Each episode is one linear coroutine (Play) written with
    // small story verbs: Say / Think, Choose, Ask, Card, RoamUntil (free exploration with E-to-interact spots),
    // Breathe (the existing breathing minigame) and Complete.
    // When an episode is complete the player writes in the reflection journal, may answer the self-assessment
    // (after agreeing to the privacy notice), and does the stress check-in, before the completion screen.
    // Every episode also has a stress meter (AddStress / EaseStress), a stress check-in once the episode is over,
    // and a time limit that only runs while the player is in control: when it runs out the episode ends and has to
    // be replayed. The check-in, stress and time go to the database.
    public abstract class AcademicEpisode : MonoBehaviour, IPausable
    {
        public Transform player;
        public Camera storyCamera;
        [Tooltip("Optional scene-specific dialogue template. Leave empty to use the shared EpisodeUI dialogue.")]
        public UIE.VisualTreeAsset dialogueOverride;
        [Tooltip("Optional scene-specific choice template. Leave empty to use the shared EpisodeUI choices.")]
        public UIE.VisualTreeAsset choiceOverride;

        public abstract int Episode { get; }
        protected abstract IEnumerator Play();

        public bool Ready { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsComplete { get; private set; }
        public bool IsRoaming => roaming;
        public string CurrentObjective => objectiveText != null ? objectiveText.text : "";
        protected string PlayerName { get; set; }
        // True for the episode whose own opening asks for the player's name.
        protected virtual bool AsksForName => false;
        public virtual int Chapter => AcademicPressureChapter.Number;   // public: the pause menu shows it
        protected virtual string CompletionKey => AcademicPressureChapter.SaveKey(Episode);
        protected virtual string CompletionNext => Episode >= AcademicPressureChapter.EpisodeCount
            ? "CHAPTER 1 COMPLETE  •  NEXT: CHAPTER 2  •  FAMILY EXPECTATIONS"
            : "NEXT: EPISODE " + (Episode + 1).ToString("00") + "  •  " + AcademicPressureChapter.Titles[Episode].ToUpperInvariant();
        protected virtual string ContinueLabel => Episode >= AcademicPressureChapter.EpisodeCount ? "Begin Chapter 2" : "Continue";
        // Seconds of free play (exploring, walking, finding things). Dialogue, choices and minigames do not count.
        protected virtual float TimeLimit => 600;

        public bool IsFailed { get; private set; }
        public float TimeLeft { get; private set; }
        public float Stress => stress;

        protected PlayerMovement movement;
        protected PlayerControls controls;
        protected CharacterController character;
        protected Animator animator;
        protected AudioSource effects;
        protected AudioClip chime, softTick;

        protected static readonly Color Ink = new Color32(29, 43, 47, 248), Paper = new Color32(242, 232, 209, 255),
            Gold = new Color32(219, 179, 111, 255), Teal = new Color32(65, 101, 99, 255), Muted = new Color32(196, 184, 160, 255);

        protected sealed class Spot
        {
            public Transform target;
            public string label;
            public Func<bool> available;
            public Func<IEnumerator> action;
            public float range = 3.2f;
            public bool arrive;               // reached on foot (through a door, into a room): no E, no prompt
            public InteractOutline outline;   // glows around the target instead of the floating marker
            public GameObject marker;
        }

        readonly List<Spot> spots = new List<Spot>();
        UIE.VisualElement fadeLayer;   // the black the story fades through: a UI Toolkit document under every story panel
        float fadeAlpha = 1;           // the story starts black and fades in
        bool hudShown; // exploration HUD (objective, status, [E] prompt) is up while roaming
        // The objective box, time/stress status, dialogue bar, choice panel, story card, complete / out-of-time screens and
        // pause menu are UI Toolkit documents (see BuildScreens).
        UIE.VisualElement objectiveBox, statusBox, stressFill, promptBox, dialogue, choicePanel, choiceList, card, completeScreen, failedScreen;
        UIE.Label objectiveText, checklistText, timerText, stressText, promptText, speakerText, speechText, choiceTitle, choiceQuestion,
            cardText, cardHint, completeHeading, completeTitle, completeBody, completeStats, completeNext;
        float stressShown = .2f; // the meter eases toward the real stress level
        UIE.Button completeContinue;
        UIE.Label dialogueMode;
        UIE.VisualElement dialogueContext, dialogueProgress, dialogueDots;
        UIE.Label dialogueProgressText;
        int dialogueLineNumber = 1, dialogueLineCount = 1;
        UIE.VisualElement finaleScreen, finaleContent; UIE.Label finaleHeading, finaleTitle, finaleBody; UIE.Button finaleMenu;
        bool finaleStarted;
        UIE.VisualElement flashbackRoot, flashbackTint, flashbackBadge;
        UIE.Label flashbackWhen;
        UIE.VisualElement phoneScreen, phoneChat, phoneLock, phoneNotifications, phoneMessages, phoneTyping, phoneHint, phoneReplies, photoScreen, photoImage;
        UIE.Label phoneName, phoneStatus, phoneAvatar, phoneClock, phoneInputText, lockTime, lockDate, lockEmpty, photoCaption;
        UIE.VisualElement entryScreen, entryCard; UIE.Label entryTitle, entryPrompt, entryHint; UIE.TextField entryField; UIE.Button entryConfirm;
        int phoneMinute = 42;   // the phone's clock: 7:42 PM, a minute later for each message
        string flashbackNow; // the flashback label on screen right now; null in the present
        PauseMenu pauseMenu;
        Spot nearest, pending;
        Vector3 lastSafe;
        bool roaming, advance, typing, breathing, inputSubmitted;
        int choiceCount, choiceResult = -1;
        UIE.ScrollView moodScroll;   // the check-in's card carousel while it is open
        UIE.Label choiceHint; string choiceHintText;
        float lineAt, previousTimeScale = 1;
        Material markerMaterial;
        Coroutine story;
        float stress = 20, stressPeak = 20;
        int checkInStart, checkInEnd;   // checkInStart stays 0: the check-in is asked only after the episode

        // ================================================================ lifecycle

        IEnumerator Start()
        {
            movement = player.GetComponent<PlayerMovement>(); controls = player.GetComponent<PlayerControls>();
            character = player.GetComponent<CharacterController>(); animator = player.GetComponentInChildren<Animator>();
            // The episode owns its overlays; the legacy task-panel shortcut is not used here.
            if (controls != null) controls.enabled = false;
            if (movement != null && storyCamera != null) movement.cameraTransform = storyCamera.transform;
            PlayerName = MindscapeDatabase.PlayerName;
            effects = gameObject.AddComponent<AudioSource>(); effects.spatialBlend = 0;
            chime = Tone("Soft chime", 660, .55f); softTick = Tone("Soft tick", 1100, .045f);
            BuildUI(); SetLocked(true); lastSafe = player.position;
            PlayerOutfit.Wear(player, StartingOutfit);
            var resume = MindscapeSaves.TakePending(Chapter, Episode);
            if (MindscapeSaves.Resumes(resume)) BeginReplay(resume);
            while (EpisodeLoadingScreen.IsActive) yield return null;
            // Continue-game convention shared with the menu: episode 1 is the tutorial, story episodes start at 2.
            PlayerPrefs.SetInt(MindscapeProgress.ChapterKey, Chapter); PlayerPrefs.SetInt(MindscapeProgress.EpisodeKey, Episode + 1); PlayerPrefs.Save();
            MindscapeDatabase.EpisodeStarted(Chapter, Episode);
            TimeLeft = TimeLimit;
            Ready = true;
            // Fade in while the episode frames its first shot.
            StartCoroutine(Fade(1, 0, .7f));
            // A player who has not named themselves yet (e.g. an account new to this device) is asked before the story
            // uses a name. Episode 1 asks in its own opening instead.
            if (!AsksForName && !MindscapeDatabase.HasPlayerName && replay == null)
            {
                yield return Ask("Before we begin", "What name should the story use for you?", "Your name", "", false, 24, "Start", n => MindscapeDatabase.PlayerName = n);
                PlayerName = MindscapeDatabase.PlayerName;
            }
            // From here every step the player takes is kept (see "saving where you are"); a loaded save replays them.
            recording = true;
            story = StartCoroutine(Play());
            yield return story;
        }

        // Switching to another window (Alt+Tab, another app, a phone notification) pauses the story.
        void OnApplicationFocus(bool focused) { if (!focused) PauseForFocus(); }
        void OnApplicationPause(bool paused) { if (paused) PauseForFocus(); }
        void PauseForFocus()
        {
            if (!Ready || IsPaused || IsComplete || IsFailed || replay != null || breathing || OverlayOpen || EpisodeLoadingScreen.IsActive) return;
            TogglePause();
        }

        // ================================================================ saving where you are
        // A save keeps every step the story has taken (each spot used, each choice, typed answer and breath, and the
        // episode's own panels through Log), where the player stands, the stress and the time left. Loading it plays the
        // episode again at high speed behind a cover, feeding those steps back in, and puts the player back where they
        // saved. A replay that loses its way (an older save, a changed episode) restarts the episode instead.

        readonly List<string> trail = new List<string>();
        Queue<string> replay;
        MindscapeSaves.Slot resumeSlot;
        bool recording;
        float replayStarted, replayVolume = 1;
        GameObject replayCover;
        const float ReplaySpeed = 100, ReplayTimeout = 40;

        // True while a loaded save is being replayed: episodes skip anything that waits on the player.
        protected bool Replaying => replay != null;
        // A replay with steps still to come (the save was made later in the episode than this point).
        protected bool ReplayAhead => replay != null && replay.Count > 0;

        // The current state for a save slot (PauseMenu).
        public void CaptureSave(MindscapeSaves.Slot slot)
        {
            slot.trail = trail.ToArray();
            slot.roaming = roaming;
            slot.x = player.position.x; slot.y = player.position.y; slot.z = player.position.z; slot.yaw = player.eulerAngles.y;
            slot.stress = stress; slot.stressPeak = stressPeak; slot.timeLeft = TimeLeft;
        }

        public bool CanSave => Ready && recording && replay == null && !IsComplete && !IsFailed;

        // An episode's own input (an exam handed in, a task picked) is kept as one letter and its data.
        protected void Log(char kind, string data = "") { if (recording && replay == null) trail.Add(kind + (data ?? "")); }

        // During a replay: the next kept step of this kind, if there is one. Reaching the end of the steps finishes the
        // replay; a different kind of step means the save no longer matches the episode, so it starts over.
        protected bool ReplayNext(char kind, out string data)
        {
            data = null;
            if (replay == null) return false;
            if (replay.Count == 0) { EndReplay(false); return false; }
            string next = replay.Peek();
            if (next.Length == 0 || next[0] != kind) { AbortReplay(); return false; }
            data = replay.Dequeue().Substring(1);
            return true;
        }

        // At a moment that waits for the player and keeps nothing (a line of dialogue, a card): true while the replay
        // still has steps to go, or the save was made while exploring (it ends at that exploration, not on a line).
        bool SkipWait()
        {
            if (replay == null) return false;
            if (replay.Count > 0 || (resumeSlot != null && resumeSlot.roaming)) return true;
            EndReplay(false);
            return false;
        }

        void BeginReplay(MindscapeSaves.Slot slot)
        {
            resumeSlot = slot;
            replay = new Queue<string>(slot.trail);
            trail.Clear(); trail.AddRange(slot.trail);
            replayStarted = Time.realtimeSinceStartup;
            replayVolume = AudioListener.volume; AudioListener.volume = 0;
            Time.timeScale = ReplaySpeed;
            var ui = EpisodeUI.Load();
            var go = new GameObject("Loading your save");
            go.SetActive(false);
            var document = go.AddComponent<UIE.UIDocument>();
            document.panelSettings = ui.panelSettings; document.sortingOrder = 50;
            go.SetActive(true);
            var cover = new UIE.VisualElement();
            cover.style.position = UIE.Position.Absolute; cover.style.left = cover.style.right = cover.style.top = cover.style.bottom = 0;
            cover.style.backgroundColor = (Color)new Color32(14, 20, 23, 255);
            cover.style.justifyContent = UIE.Justify.Center; cover.style.alignItems = UIE.Align.Center;
            var label = new UIE.Label("Loading your save…");
            label.style.color = Paper; label.style.fontSize = 28; label.style.unityFontStyleAndWeight = FontStyle.Italic;
            cover.Add(label);
            document.rootVisualElement.Add(cover);
            replayCover = go;
        }

        // The replay has caught up. At an exploration the player goes back to where they saved.
        void EndReplay(bool atRoam)
        {
            if (replay == null) return;
            replay = null;
            Time.timeScale = 1; AudioListener.volume = replayVolume;
            stress = resumeSlot.stress; stressPeak = Mathf.Max(resumeSlot.stressPeak, stress); TimeLeft = resumeSlot.timeLeft;
            if (atRoam && resumeSlot.roaming)
            {
                player.gameObject.SetActive(true);
                Teleport(new Vector3(resumeSlot.x, resumeSlot.y, resumeSlot.z), resumeSlot.yaw);
            }
            if (replayCover != null) StartCoroutine(RemoveCover(replayCover));
            replayCover = null;
        }

        static IEnumerator RemoveCover(GameObject cover)
        {
            var root = cover.GetComponent<UIE.UIDocument>().rootVisualElement;
            for (float t = 0; t < .5f; t += Time.unscaledDeltaTime) { root.style.opacity = 1 - t / .5f; yield return null; }
            Destroy(cover);
        }

        // The save does not fit this episode any more: start it from the beginning instead.
        bool replayAborted;
        void AbortReplay()
        {
            if (replay == null) return;
            Debug.LogWarning("Mindscape: the saved game could not be replayed; the episode starts from the beginning.");
            replay = null; replayAborted = true;
            if (story != null) StopCoroutine(story);
            Time.timeScale = 1; AudioListener.volume = replayVolume;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        protected virtual void Update()
        {
            if (replay != null) { if (Time.realtimeSinceStartup - replayStarted > ReplayTimeout) AbortReplay(); return; }
            if (!Ready || EpisodeLoadingScreen.IsActive || breathing || OverlayOpen) return;
            var k = Keyboard.current;
            if (typing)
            {
                if (k != null && k.escapeKey.wasPressedThisFrame) Escape();
                return;
            }
            if (k != null && k.escapeKey.wasPressedThisFrame && !IsComplete) Escape();
            if (IsPaused) return;
            if (k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame)) Advance();
            if (choiceCount > 0 && k != null)
                for (int i = 0; i < choiceCount && i < 9; i++)
                    if (k[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) Pick(i);
            if (moodScroll != null && k != null)
            {
                if (k.leftArrowKey.wasPressedThisFrame) ScrollMoods(-1);
                if (k.rightArrowKey.wasPressedThisFrame) ScrollMoods(1);
            }
            if (!roaming) return;
            // A street conversation owns the player while it is open. Once it closes, clear any lock it left set,
            // so a stale dialogue flag can never keep the camera and movement frozen while you are free to roam.
            if (NPCInteractionManager.DialogueActive) { ShowScreen(promptBox, false); return; }
            TickTimer(Time.deltaTime);
            if (IsFailed) return;
            if (controls != null && (controls.dialogueOpen || (movement != null && !movement.enabled))) SetLocked(false);
            nearest = FindNearest();
            var arrived = FindArrival();
            if (arrived != null) pending = arrived;
            // The bottom prompt only appears when there is something to press E for; the general hint and the
            // controls live in the pause menu.
            ShowScreen(promptBox, nearest != null);
            if (nearest != null) promptText.text = "[ E ]  " + nearest.label;
            if (nearest != null && ((k != null && k.eKey.wasPressedThisFrame) || MobileInput.InteractPressed)) pending = nearest;
            if (character != null && character.isGrounded) lastSafe = player.position;
            if (player.position.y < FallLimit) Teleport(lastSafe, player.eulerAngles.y);
        }

        void LateUpdate()
        {
            DrawStatus();
            foreach (var s in spots)
            {
                bool show = roaming && !IsPaused && SpotAvailable(s);
                if (s.outline != null) { s.outline.Show(show, s == nearest); continue; }
                if (s.marker == null) continue;
                s.marker.SetActive(show);
                if (!show) continue;
                s.marker.transform.position = s.target.position + Vector3.up * (.9f + Mathf.Sin(Time.time * 2.2f) * .08f);
                s.marker.transform.Rotate(0, 60 * Time.deltaTime, 0, Space.World);
            }
        }

        // Episodes with their own full-screen minigame (the Thought Courtroom) block pause and story keys while it is open.
        protected virtual bool OverlayOpen => false;
        protected virtual string RoamHint => "Explore. Look for the gold markers.";
        protected virtual float FallLimit => -8;
        // Set for an episode that takes place in the past (e.g. "Examination morning"): the flashback frame and badge
        // stay on for the whole episode and the title card says so. Null for episodes set in the present.
        protected virtual string FlashbackLabel => null;
        // What Joey is wearing when the episode opens (school uniform on school days, see PlayerOutfit.ForEpisode).
        protected virtual Outfit StartingOutfit => PlayerOutfit.ForEpisode(Chapter, Episode);

        // A step into the bathroom to change: walking in (through its door) is the trigger. The screen dims while he
        // changes, and he steps back out dressed. Returns null when the house has no bathroom door.
        protected Spot AddBathroom(string label, Func<bool> available, Outfit outfit, string thought, Action done)
        {
            if (!PlayerOutfit.Bathroom(out var inside, out var door)) return null;
            var point = Waypoint("In the bathroom", inside + Vector3.up * .1f);
            return AddArrival(point, label, available, () => ChangeClothes(outfit, door, thought, done), 1.6f);
        }

        IEnumerator ChangeClothes(Outfit outfit, Vector3 door, string thought, Action done)
        {
            yield return Fade(0, 1, .5f);
            effects.PlayOneShot(softTick, .25f);
            yield return new WaitForSeconds(.5f);
            effects.PlayOneShot(softTick, .2f);
            PlayerOutfit.Wear(player, outfit);
            // In the middle of the bathroom, turned to the door; the camera just inside the door, on the new clothes.
            Vector3 into = Vector3.ProjectOnPlane(player.position - door, Vector3.up).normalized;
            if (into.sqrMagnitude < .01f) into = Vector3.back;
            Vector3 at = new Vector3(door.x, player.position.y, door.z) + into * 2.7f, side = Vector3.Cross(Vector3.up, into);
            Teleport(at, Quaternion.LookRotation(-into).eulerAngles.y);
            Shot(new Vector3(door.x, player.position.y, door.z) + into * .4f + side * 1.6f + Vector3.up * 3.3f, at + Vector3.up * 2f);
            ShotFocus.Clear();
            yield return Fade(1, 0, .6f);
            if (!string.IsNullOrEmpty(thought)) yield return Think(thought);
            done?.Invoke();
        }
        protected bool IsFlashbackEpisode => !string.IsNullOrEmpty(FlashbackLabel);

        void OnDestroy()
        {
            Time.timeScale = 1; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            foreach (var clip in new[] { chime, softTick }) if (clip != null) Destroy(clip);
            if (markerMaterial != null) Destroy(markerMaterial);
        }

        // ================================================================ story verbs

        public void Advance() { if (!IsPaused && Time.unscaledTime - lineAt > .3f) advance = true; }

        // Group only consecutive dialogue lines, ending before a choice, story card or exploration.
        // The total comes from the supplied lines, so edited chains cannot leave a guessed dot count.
        protected IEnumerator DialogueSequence(params IEnumerator[] lines)
        {
            int previousNumber = dialogueLineNumber, previousCount = dialogueLineCount;
            try
            {
                dialogueLineCount = lines.Length;
                for (int i = 0; i < lines.Length; i++)
                {
                    dialogueLineNumber = i + 1;
                    yield return lines[i];
                }
            }
            finally
            {
                dialogueLineNumber = previousNumber;
                dialogueLineCount = previousCount;
            }
        }

        // A camera move or pause belongs to the following line, not an extra progress dot.
        protected IEnumerator BeforeDialogue(object transition, IEnumerator line)
        {
            if (transition != null) yield return transition;
            yield return line;
        }

        protected IEnumerator Say(string who, string words, Action onComplete = null)
        {
            if (SkipWait()) { yield return null; onComplete?.Invoke(); yield break; }
            who = MindscapeDatabase.Personalize(who); words = MindscapeDatabase.Personalize(words);
            HidePanels(); ShowScreen(dialogue, true);
            speakerText.text = who.ToUpperInvariant(); speechText.text = words;
            // The alternate template separates the name from optional context; legacy templates keep the original label.
            if (dialogueMode != null)
            {
                int separator = who.IndexOf("  /  ", StringComparison.Ordinal);
                string context = separator >= 0 ? who.Substring(separator + 5).Trim() : "";
                bool thought = string.Equals(context, "thinking", StringComparison.OrdinalIgnoreCase);
                speakerText.text = (separator >= 0 ? who.Substring(0, separator) : who).ToUpperInvariant();
                dialogueMode.text = thought ? "INNER THOUGHT" : context.ToUpperInvariant();
                ShowScreen(dialogueContext, context.Length > 0);
            }
            DrawDialogueProgress();
            advance = false; lineAt = Time.unscaledTime;
            while (!advance) yield return null;
            advance = false;
            onComplete?.Invoke();
        }

        void DrawDialogueProgress()
        {
            if (dialogueProgress == null || dialogueDots == null) return;
            dialogueDots.Clear();
            for (int i = 1; i <= dialogueLineCount; i++)
            {
                var dot = new UIE.VisualElement { pickingMode = UIE.PickingMode.Ignore };
                dot.AddToClassList("dialogue-new-dot");
                dot.EnableInClassList("dialogue-new-dot-complete", i < dialogueLineNumber);
                dot.EnableInClassList("dialogue-new-dot-lit", i == dialogueLineNumber);
                dialogueDots.Add(dot);
            }
            dialogueProgress.tooltip = "Dialogue " + dialogueLineNumber + " of " + dialogueLineCount;
            if (dialogueProgressText != null)
                dialogueProgressText.text = dialogueLineNumber + " / " + dialogueLineCount;
        }

        // ================================================================ a phone conversation, a photograph

        // Shows the phone with a conversation open. Messages stay between openings, like a real chat history.
        protected void OpenPhone(string contact)
        {
            if (phoneScreen == null) return;
            if (phoneChat != null) { ShowScreen(phoneChat, true); ShowScreen(phoneLock, false); }
            phoneName.text = contact; phoneAvatar.text = contact.Substring(0, 1).ToUpperInvariant(); phoneStatus.text = "online";
            phoneClock.text = Clock();
            ShowScreen(phoneScreen, true);
        }

        protected void ClosePhone() { if (phoneScreen != null) ShowScreen(phoneScreen, false); }

        // Shows the phone's lock screen: the time, the date (may be empty) and no notifications yet. Dim is a phone
        // left dark, as if seen by its own faint light.
        protected void OpenPhoneLock(string time, string date, bool dim = false)
        {
            if (phoneScreen == null || phoneLock == null) return;
            ShowScreen(phoneChat, false); ShowScreen(phoneLock, true);
            phoneLock.EnableInClassList("dim", dim);
            lockTime.text = time; lockDate.text = date; ShowScreen(lockDate, !string.IsNullOrEmpty(date));
            phoneClock.text = time;
            phoneNotifications.Clear(); ShowScreen(lockEmpty, true);
            ShowScreen(phoneScreen, true);
        }

        // A notification card on the lock screen, e.g. a reminder. Thoughts can follow while it is shown.
        protected void PhoneNotification(string app, string title, string body)
        {
            if (phoneNotifications == null) { return; }
            ShowScreen(lockEmpty, false);
            var card = new UIE.VisualElement(); card.AddToClassList("phone-notification");
            var header = new UIE.VisualElement(); header.AddToClassList("phone-notification-app");
            foreach (var (text, style) in new[] { (app.Substring(0, 1), "phone-notification-icon"), (app.ToUpperInvariant(), "phone-notification-name"), ("now", "phone-notification-when") })
            {
                var label = new UIE.Label(text); label.AddToClassList(style); header.Add(label);
            }
            card.Add(header);
            foreach (var (text, style) in new[] { (title, "phone-notification-title"), (body, "phone-notification-body") })
            {
                if (string.IsNullOrEmpty(text)) continue;
                var label = new UIE.Label(text); label.AddToClassList(style); card.Add(label);
            }
            card.Query().ForEach(e => e.pickingMode = UIE.PickingMode.Ignore);
            card.AddToClassList("arriving");
            phoneNotifications.Add(card);
            card.schedule.Execute(() => card.RemoveFromClassList("arriving")).StartingIn(30);
            effects.PlayOneShot(chime, .18f);
        }

        // A message arrives from the contact (after a moment of "typing…") or is sent by the player. With wait, the
        // story holds until the player presses Continue, as for a dialogue line.
        protected IEnumerator PhoneMessage(bool fromMe, string text, bool wait = true)
        {
            if (phoneScreen == null) { yield return fromMe ? Me(text) : Say(phoneName != null ? phoneName.text : "Message", text); yield break; }
            HidePanels();   // the dialogue bar steps aside while the chat is read
            if (!fromMe)
            {
                phoneStatus.text = "typing…"; ShowScreen(phoneTyping, true);
                yield return new WaitForSeconds(Mathf.Clamp(.5f + text.Length * .012f, .8f, 1.8f));
                ShowScreen(phoneTyping, false); phoneStatus.text = "online";
                effects.PlayOneShot(softTick, .35f);
            }
            phoneMinute++; phoneClock.text = Clock();
            var row = new UIE.VisualElement(); row.AddToClassList("phone-row"); row.AddToClassList(fromMe ? "outgoing" : "incoming");
            var bubble = new UIE.VisualElement(); bubble.AddToClassList("phone-bubble");
            var words = new UIE.Label(text); words.AddToClassList("phone-bubble-text");
            var time = new UIE.Label(Clock() + (fromMe ? "  ✓✓" : "")); time.AddToClassList("phone-bubble-time");
            bubble.Add(words); bubble.Add(time); row.Add(bubble);
            row.Query().ForEach(e => e.pickingMode = UIE.PickingMode.Ignore);
            row.AddToClassList("arriving");   // slides up into place (PhoneChat.uss)
            phoneMessages.Add(row);
            row.schedule.Execute(() => row.RemoveFromClassList("arriving")).StartingIn(30);
            if (!wait || SkipWait()) yield break;
            ShowScreen(phoneHint, true);
            advance = false; lineAt = Time.unscaledTime;
            while (!advance) yield return null;
            advance = false;
            ShowScreen(phoneHint, false);
        }

        // The player picks a reply on the phone itself (click or number key), like Choose; the caller sends it.
        protected IEnumerator PhoneChoose(IList<string> options, Action<int> picked)
        {
            if (phoneReplies == null) { yield return Choose("Reply", "What would you like to say?", options, picked); yield break; }
            if (ReplayNext('c', out var kept)) { picked(int.Parse(kept)); yield break; }
            HidePanels();
            phoneReplies.Clear();
            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                var button = new UIE.Button(() => Pick(index)) { focusable = false };   // Space must not re-click it
                button.AddToClassList("phone-reply");
                foreach (var (text, style) in new[] { (Convert.ToString(i + 1), "phone-reply-key"), (options[i], "phone-reply-text") })
                {
                    var label = new UIE.Label(text) { pickingMode = UIE.PickingMode.Ignore };
                    label.AddToClassList(style);
                    button.Add(label);
                }
                phoneReplies.Add(button);
            }
            ShowScreen(phoneReplies, true);
            if (phoneInputText != null) phoneInputText.text = "Choose a reply";
            choiceCount = options.Count; choiceResult = -1; lineAt = Time.unscaledTime;
            while (choiceResult < 0) yield return null;
            int result = choiceResult;
            choiceCount = 0; choiceResult = -1;
            ShowScreen(phoneReplies, false);
            if (phoneInputText != null) phoneInputText.text = "Message";
            effects.PlayOneShot(chime, .1f);
            Log('c', result.ToString());
            picked(result);
        }

        string Clock() => "7:" + phoneMinute.ToString("00") + " PM";

        // Holds a photograph up close; thoughts can run below it.
        protected void ShowPhoto(Texture2D image, string caption)
        {
            if (photoScreen == null) return;
            photoImage.style.backgroundImage = image != null ? new UIE.StyleBackground(image) : new UIE.StyleBackground(UIE.StyleKeyword.None);
            photoCaption.text = caption;
            ShowScreen(photoScreen, true);
        }

        protected void HidePhoto() { if (photoScreen != null) { ShowScreen(photoScreen, false); photoScreen.style.opacity = 1; } }

        // Fades the held-up photograph or item in and out (it sits above the story fade, so it can show over black).
        protected IEnumerator FadePhoto(float from, float to, float seconds)
        {
            if (photoScreen == null) yield break;
            for (float t = 0; t < seconds; t += Time.deltaTime) { photoScreen.style.opacity = Mathf.Lerp(from, to, t / seconds); yield return null; }
            photoScreen.style.opacity = to;
        }

        // An object held up close (a keepsake from the desk): the same overlay as a photograph, but a flat picture of the
        // item with no sepia tint, tape or tilt (Photograph.uss ".item").
        protected void ShowItem(Texture2D image, string caption)
        {
            if (photoScreen == null) return;
            photoScreen.AddToClassList("item");
            ShowPhoto(image, caption);
        }

        protected void HideItem()
        {
            HidePhoto();
            if (photoScreen != null) photoScreen.RemoveFromClassList("item");
        }

        protected IEnumerator Think(string words, Action onComplete = null) => Say(PlayerName + "  /  thinking", words, onComplete);
        protected IEnumerator Me(string words, Action onComplete = null) => Say(PlayerName, words, onComplete);

        // A full-screen pause in the story, e.g. "Alex feels overwhelmed."
        protected IEnumerator Card(string words, string hint = "Continue  [Space]")
        {
            if (SkipWait()) { yield return null; yield break; }
            HidePanels(); ShowScreen(card, true);
            cardText.text = words; cardHint.text = hint;
            advance = false; lineAt = Time.unscaledTime;
            // The card sits above the screen fade, so it follows the fade itself:
            // a story fading in from black fades the card in with it.
            while (!advance) { card.style.opacity = 1 - fadeAlpha; yield return null; }
            advance = false; ShowScreen(card, false);
        }

        // A card that stays up for a few seconds on its own (a countdown, a title in a sequence), with no key to press.
        protected IEnumerator TimedCard(string words, float seconds)
        {
            HidePanels(); ShowScreen(card, true);
            cardText.text = words; cardHint.text = "";
            for (float t = 0; t < seconds; t += Time.deltaTime) { card.style.opacity = 1 - fadeAlpha; yield return null; }
            ShowScreen(card, false);
        }

        // The story fade in a colour other than its usual black (white for a camera flash or a light filling the view).
        // Null puts the stylesheet's own colour back.
        protected void SetFadeColor(Color? color)
        {
            if (fadeLayer == null) return;
            fadeLayer.style.backgroundColor = color.HasValue ? new UIE.StyleColor(color.Value) : new UIE.StyleColor(UIE.StyleKeyword.Null);
        }

        // A camera flash: the screen goes white for a moment and fades back.
        protected IEnumerator Flash(float seconds = .6f)
        {
            SetFadeColor(Color.white);
            yield return Fade(.95f, 0, seconds);
            SetFadeColor(null);
        }

        // moods: the options are the check-in's stress levels, shown as a sideways carousel of cards with a face each.
        protected IEnumerator Choose(string title, string question, IList<string> options, Action<int> picked, bool moods = false)
        {
            if (ReplayNext('c', out var kept)) { yield return null; picked(int.Parse(kept)); yield break; }
            HidePanels(); ShowScreen(choicePanel, true);
            choicePanel.EnableInClassList("checkin", moods);
            choiceTitle.text = title.ToUpperInvariant(); choiceQuestion.text = question;
            if (choiceHint != null)
                choiceHint.text = !moods ? choiceHintText : MobileControls.Enabled ? "Swipe to see every card, then tap how you feel"
                    : "Scroll or use the arrows to see every card, then click one or press its number";
            choiceList.Clear(); moodScroll = null;
            var holder = moods ? MoodCarousel() : choiceList;
            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                holder.Add(moods ? MoodCard(i + 1, options[i], () => Pick(index)) : ChoiceOption(i + 1, options[i], () => Pick(index), choicePanel.ClassListContains("choice-new")));
            }
            if (moods) DragToScroll(moodScroll);
            choiceCount = options.Count; choiceResult = -1; lineAt = Time.unscaledTime;
            while (choiceResult < 0) yield return null;
            int result = choiceResult;
            choiceCount = 0; choiceResult = -1; moodScroll = null; ShowScreen(choicePanel, false);
            effects.PlayOneShot(chime, .1f);
            Log('c', result.ToString());
            picked(result);
        }

        // Consent is a separate choice before assessment questions. The unchecked toggle must be
        // affirmed before the player can begin; skipping does not collect or save answers.
        protected IEnumerator AssessmentConsent(Action<bool> answered)
        {
            HidePanels(); ShowScreen(choicePanel, true);
            choicePanel.EnableInClassList("checkin", false);
            choiceTitle.text = "DATA PRIVACY NOTICE";
            choiceQuestion.text = "Before your self-reflection assessment";
            if (choiceHint != null) choiceHint.text = "You can continue the story without answering.";
            choiceList.Clear();
            var notice = new UIE.Label("The two answers describe how overwhelmed you felt and which coping strategy helped. They appear in your self-reflection journal. If you are signed in, they are sent to Supabase; offline, they are saved on this device. This is a personal reflection, not a medical diagnosis.");
            notice.AddToClassList("assessment-privacy-notice");
            choiceList.Add(notice);
            var consent = new UIE.Toggle("I agree to save my assessment answers") { value = false };
            consent.AddToClassList("assessment-consent");
            choiceList.Add(consent);
            bool decided = false, agreed = false;
            var begin = new UIE.Button(() => { if (IsPaused || !consent.value) return; agreed = true; decided = true; }) { text = "Begin assessment" };
            begin.AddToClassList("choice-option");
            begin.SetEnabled(false);
            consent.RegisterCallback<UIE.ChangeEvent<bool>>(evt => begin.SetEnabled(evt.newValue));
            choiceList.Add(begin);
            var skip = new UIE.Button(() => { if (!IsPaused) decided = true; }) { text = "Skip assessment and continue" };
            skip.AddToClassList("choice-option");
            choiceList.Add(skip);
            choiceCount = 0;
            while (!decided) yield return null;
            ShowScreen(choicePanel, false);
            answered(agreed);
        }

        // Same structure as the sample options in Choice.uxml: the number key, then the answer.
        static UIE.Button ChoiceOption(int number, string words, Action picked, bool redesigned)
        {
            // The new numbered keycaps replace the legacy A./B. presentation, not the story data.
            if (redesigned)
            {
                string prefix = ((char)('A' + number - 1)) + ".";
                if (words.StartsWith(prefix + " ", StringComparison.Ordinal)) words = words.Substring(prefix.Length).Trim();
                if (words.Length >= 2 && words[0] == '"' && words[words.Length - 1] == '"')
                    words = words.Substring(1, words.Length - 2);
            }
            var button = new UIE.Button(picked) { focusable = false }; // Space advances the story; don't let it re-click
            button.AddToClassList("choice-option");
            foreach (var (text, style) in new[] { (number.ToString(), "choice-key"), (words, "choice-text") })
            {
                var label = new UIE.Label(text) { pickingMode = UIE.PickingMode.Ignore };
                label.AddToClassList(style);
                button.Add(label);
            }
            if (redesigned)
            {
                var arrow = new UIE.Label("›") { pickingMode = UIE.PickingMode.Ignore };
                arrow.AddToClassList("choice-arrow");
                button.Add(arrow);
            }
            return button;
        }

        // The check-in's row: arrow buttons either side of a sideways ScrollView (drag or swipe it, or use the arrow keys).
        UIE.VisualElement MoodCarousel()
        {
            var row = new UIE.VisualElement(); row.AddToClassList("mood-carousel");
            moodScroll = new UIE.ScrollView(UIE.ScrollViewMode.Horizontal) { horizontalScrollerVisibility = UIE.ScrollerVisibility.Hidden, touchScrollBehavior = UIE.ScrollView.TouchScrollBehavior.Elastic };
            moodScroll.AddToClassList("mood-scroll");
            foreach (int direction in new[] { -1, 1 })
            {
                var arrow = new UIE.Button(() => ScrollMoods(direction)) { text = direction < 0 ? "‹" : "›", focusable = false };
                arrow.AddToClassList("mood-nav");
                if (direction < 0) row.Add(arrow);
                else { row.Add(moodScroll); row.Add(arrow); }
            }
            choiceList.Add(row);
            return moodScroll.contentContainer;
        }

        // Dragging the row scrolls it, with the mouse as well as touch (ScrollView only drag-scrolls touch). Once the
        // pointer has moved a few pixels it is a drag: the row takes the pointer from the card under it, so that card is
        // not picked. A pressed card holds the pointer until then, so its moves are heard on the cards as well as the row.
        static void DragToScroll(UIE.ScrollView scroll)
        {
            int pointer = -1; bool dragging = false; float startX = 0, startOffset = 0;
            scroll.RegisterCallback<UIE.PointerDownEvent>(e =>
            {
                if (pointer >= 0 || e.button > 0) return;
                pointer = e.pointerId; dragging = false; startX = e.position.x; startOffset = scroll.scrollOffset.x;
            }, UIE.TrickleDown.TrickleDown);
            void Move(UIE.PointerMoveEvent e)
            {
                if (e.pointerId != pointer) return;   // the row and a card may both hear one move; the result is the same
                float dx = e.position.x - startX;
                if (!dragging && Mathf.Abs(dx) > 8) { dragging = true; UIE.PointerCaptureHelper.CapturePointer(scroll, pointer); }
                if (!dragging) return;
                scroll.scrollOffset = new Vector2(startOffset - dx, 0);
                e.StopPropagation();
            }
            void End(int id)
            {
                if (id != pointer) return;
                bool held = dragging && UIE.PointerCaptureHelper.HasPointerCapture(scroll, pointer);
                pointer = -1; dragging = false;
                if (held) UIE.PointerCaptureHelper.ReleasePointer(scroll, id);
            }
            void Up(UIE.PointerUpEvent e) { if (dragging && e.pointerId == pointer) e.StopPropagation(); End(e.pointerId); }
            scroll.RegisterCallback<UIE.PointerMoveEvent>(Move, UIE.TrickleDown.TrickleDown);
            scroll.RegisterCallback<UIE.PointerUpEvent>(Up, UIE.TrickleDown.TrickleDown);
            scroll.RegisterCallback<UIE.PointerCancelEvent>(e => End(e.pointerId), UIE.TrickleDown.TrickleDown);
            foreach (var card in scroll.contentContainer.Children())
            {
                card.RegisterCallback<UIE.PointerMoveEvent>(Move, UIE.TrickleDown.TrickleDown);
                card.RegisterCallback<UIE.PointerUpEvent>(Up, UIE.TrickleDown.TrickleDown);
            }
        }

        // One card along, eased.
        void ScrollMoods(int direction)
        {
            var scroll = moodScroll;
            if (scroll == null || scroll.contentContainer.childCount == 0) return;
            var first = scroll.contentContainer[0];
            float step = first.layout.width + first.resolvedStyle.marginLeft + first.resolvedStyle.marginRight;
            float most = Mathf.Max(0, scroll.contentContainer.layout.width - scroll.contentViewport.layout.width);
            float from = scroll.scrollOffset.x, to = Mathf.Clamp(from + direction * step, 0, most), start = Time.unscaledTime;
            scroll.schedule.Execute(() =>
            {
                float t = Mathf.Clamp01((Time.unscaledTime - start) / .22f);
                scroll.scrollOffset = new Vector2(Mathf.Lerp(from, to, 1 - (1 - t) * (1 - t)), 0);
            }).Every(16).Until(() => Time.unscaledTime - start >= .22f && scroll.scrollOffset.x == to);
        }

        // A check-in card: the number key, the level's face (MoodFace), then the words. .mood-N colours its top edge.
        static UIE.Button MoodCard(int level, string words, Action picked)
        {
            var card = new UIE.Button(picked) { focusable = false };
            card.AddToClassList("mood-card"); card.AddToClassList("mood-" + level);
            var key = new UIE.Label(level.ToString()) { pickingMode = UIE.PickingMode.Ignore }; key.AddToClassList("mood-key");
            var face = new MoodFace(level); face.AddToClassList("mood-face");
            var text = new UIE.Label(words) { pickingMode = UIE.PickingMode.Ignore }; text.AddToClassList("mood-text");
            card.Add(key); card.Add(face); card.Add(text);
            return card;
        }

        void Pick(int index)
        {
            if (IsPaused || index < 0 || index >= choiceCount || Time.unscaledTime - lineAt < .3f) return;
            choiceResult = index;
        }

        // Typed answers: the player's name and the reflection journal.
        protected IEnumerator Ask(string title, string prompt, string placeholder, string initial, bool multiline, int limit, string confirm, Action<string> answered, bool allowEmpty = false)
        {
            if (ReplayNext('a', out var kept)) { yield return null; answered(kept); yield break; }
            HidePanels(); ShowScreen(entryScreen, true);
            entryCard.EnableInClassList("multiline", multiline);
            entryTitle.text = title; entryPrompt.text = prompt; ShowScreen(entryHint, false);
            entryField.multiline = multiline; entryField.maxLength = limit;
            entryField.textEdition.placeholder = placeholder;
            entryField.SetValueWithoutNotify(initial ?? "");
            entryConfirm.text = confirm;
            typing = true; inputSubmitted = false;
            FocusEntry();
            while (true)
            {
                while (!inputSubmitted) yield return null;
                inputSubmitted = false;
                if (allowEmpty || entryField.value.Trim().Length > 0) break;
                entryHint.text = multiline ? "Write a few words, in your own way." : "Type a name to continue.";
                ShowScreen(entryHint, true);
                FocusEntry();
            }
            typing = false; ShowScreen(entryScreen, false);
            effects.PlayOneShot(chime, .1f);
            Log('a', entryField.value.Trim());
            answered(entryField.value.Trim());
        }

        void SubmitInput() { if (!IsPaused) inputSubmitted = true; }

        // Puts the caret in the box (a name already there is selected, so typing replaces it).
        void FocusEntry() => entryField.schedule.Execute(() => { entryField.Focus(); if (!entryField.multiline) entryField.SelectAll(); });

        protected Spot AddSpot(Transform target, string label, Func<bool> available, Func<IEnumerator> action, float range = 3.2f)
        {
            var s = new Spot { target = target, label = label, available = available, action = action, range = range };
            s.outline = target.GetComponentInChildren<InteractOutline>(true);
            if (s.outline != null) { spots.Add(s); return s; }
            s.marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            s.marker.name = "Interaction marker · " + label;
            Destroy(s.marker.GetComponent<Collider>());
            s.marker.layer = 2;   // Ignore Raycast: the mirror's reflection camera (SocialMirror) culls this layer, so a marker beside a mirror is not in the reflection
            s.marker.transform.localScale = Vector3.one * .16f;
            s.marker.transform.rotation = Quaternion.Euler(45, 0, 45);
            if (markerMaterial == null)
            {
                markerMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color")) { color = Gold };
                if (markerMaterial.HasProperty("_BaseColor")) markerMaterial.SetColor("_BaseColor", Gold);
            }
            s.marker.GetComponent<Renderer>().sharedMaterial = markerMaterial;
            s.marker.SetActive(false);
            spots.Add(s);
            return s;
        }

        // A spot the player reaches on foot, e.g. the far side of a door: walking there runs its action, with no key and
        // no prompt, so the door's own [ E ] Open Door is the only key at a doorway. The gold marker still shows the way.
        protected Spot AddArrival(Transform target, string label, Func<bool> available, Func<IEnumerator> action, float range = 1.8f)
        {
            var s = AddSpot(target, label, available, action, range);
            s.arrive = true;
            return s;
        }

        // An empty point to arrive at, for places the scene has no object for (just outside the front door).
        protected Transform Waypoint(string name, Vector3 position)
        {
            var point = new GameObject(name).transform;
            point.SetParent(transform, true);
            point.position = position;
            return point;
        }

        Spot FindArrival()
        {
            foreach (var s in spots)
            {
                if (!s.arrive || !SpotAvailable(s)) continue;
                Vector3 d = s.target.position - player.position;
                if (Mathf.Abs(d.y) < 3f && Vector3.ProjectOnPlane(d, Vector3.up).magnitude <= s.range) return s;
            }
            return null;
        }

        // Free exploration until done() is true. Interacting with a spot runs its action, then exploration resumes.
        protected IEnumerator RoamUntil(Func<bool> done)
        {
            while (!done())
            {
                // A replay walks straight to the next spot the player used; with none left, this is where they saved.
                Spot kept = null;
                if (replay != null)
                {
                    if (replay.Count == 0) EndReplay(true);
                    else if (ReplayNext('s', out var index) && int.TryParse(index, out int i) && i >= 0 && i < spots.Count) kept = spots[i];
                    else AbortReplay();
                    if (replayAborted) yield break;
                }
                if (kept == null)
                {
                    SetRoaming(true);
                    while (pending == null && !done()) yield return null;
                    if (pending == null) break;
                    kept = pending;
                }
                pending = null;
                SetRoaming(false);
                Log('s', spots.IndexOf(kept).ToString());
                yield return kept.action();
            }
            SetRoaming(false);
        }

        void SetRoaming(bool on)
        {
            if (IsFailed) on = false;
            roaming = on; pending = null;
            if (on)
            {
                HidePanels(); ShotFocus.Clear();
                if (storyCamera != null && followNearClip >= 0) storyCamera.nearClipPlane = followNearClip;
            }
            hudShown = on;
            SetLocked(!on);
        }

        Spot FindNearest()
        {
            Spot best = null; float bestScore = float.MaxValue;
            Vector3 eye = player.position + Vector3.up * 1.5f;
            foreach (var s in spots)
            {
                if (s.arrive || !SpotAvailable(s)) continue;
                Vector3 d = s.target.position - eye; float distance = d.magnitude;
                if (distance > s.range) continue;
                Vector3 flat = Vector3.ProjectOnPlane(d, Vector3.up);
                // Facing it counts whether the player or the orbiting camera is turned toward it.
                Vector3 view = storyCamera != null ? Vector3.ProjectOnPlane(storyCamera.transform.forward, Vector3.up).normalized : player.forward;
                float facing = flat.sqrMagnitude > .01f ? Mathf.Max(Vector3.Dot(player.forward, flat.normalized), Vector3.Dot(view, flat.normalized)) : 1;
                if (facing < -.15f) continue;
                float score = distance + (1 - facing) * 1.2f;
                if (score < bestScore) { bestScore = score; best = s; }
            }
            // Doors and NPCs share the [ E ] key: the nearest of everything in reach gets it (InteractFocus).
            if (best != null) { InteractFocus.Offer(best, bestScore - InteractFocus.StoryBonus); if (!InteractFocus.Has(best)) best = null; }
            return best;
        }

        static bool SpotAvailable(Spot s) => s.target != null && s.target.gameObject.activeInHierarchy && (s.available == null || s.available());

        // A memory moment inside an episode: shows the flashback frame with its own label ("The night before")
        // until EndFlashback, which returns to the episode's own state (still a flashback, or the present).
        protected void ShowFlashback(string when) => flashbackNow = when;
        protected void EndFlashback() => flashbackNow = FlashbackLabel;

        // A memory fades in and out instead of cutting: to black, swap the scene while it is black (whileBlack), then back
        // up. The frame's tint follows the screen fade, so the memory frame fades with it.
        protected IEnumerator FlashbackIn(string when, Action whileBlack = null, float seconds = .45f)
        {
            if (fadeAlpha < 1) yield return Fade(fadeAlpha, 1, seconds);
            whileBlack?.Invoke();
            ShowFlashback(when);
            yield return Fade(1, 0, seconds);
        }

        protected IEnumerator FlashbackOut(Action whileBlack = null, float seconds = .45f)
        {
            if (fadeAlpha < 1) yield return Fade(fadeAlpha, 1, seconds);
            EndFlashback();
            whileBlack?.Invoke();
            yield return Fade(1, 0, seconds);
        }

        protected void SetObjective(string objective, string checklist = "")
        {
            objectiveText.text = objective; checklistText.text = checklist.TrimEnd('\n');
            ShowScreen(checklistText, checklistText.text.Length > 0);
        }

        // A finished item is ticked, struck through and dimmed; an open one stays bright.
        protected static string Check(bool done, string label) =>
            done ? "<color=#DBB36F>[x]</color>  <color=#F2E8D199><s>" + label + "</s></color>\n" : "<color=#DBB36F>[  ]</color>  " + label + "\n";

        // The existing breathing exercise, opened directly (no Quest in these scenes).
        protected IEnumerator Breathe(Action<bool> finished, float seconds = 15)
        {
            if (ReplayNext('b', out var kept)) { yield return null; EaseStress(kept == "1" ? 30 : 15); finished(kept == "1"); yield break; }
            var go = new GameObject("Breathing Exercise");
            go.SetActive(false); // configure before Awake builds the screen
            go.transform.SetParent(transform, false);
            var exercise = go.AddComponent<BreathingMinigame>();
            exercise.durationSeconds = seconds;
            exercise.notifyQuest = false;
            exercise.texts.eyebrow = "CHAPTER " + Chapter + "  /  TAKE A MOMENT";
            exercise.texts.closing = "I feel a little calmer.";
            exercise.texts.closingHint = "You can come back to the breath at any time.";
            exercise.texts.closeButton = "Return";
            bool completed = false;
            exercise.onCompleted.AddListener(() => completed = true);
            go.SetActive(true);
            HidePanels(); SetLocked(true); breathing = true;
            exercise.Open();
            while (exercise.IsOpen) yield return null;
            breathing = false;
            if (controls != null) controls.minigameOpen = false;
            Destroy(go);
            SetLocked(true);
            EaseStress(completed ? 30 : 15);
            Log('b', completed ? "1" : "0");
            finished(completed);
        }

        // A replayed save does not write the choices again: they were kept when the player first made them.
        protected void Record(string key, string value) { if (replay == null) MindscapeDatabase.RecordChoice(Chapter, Episode, key, value); }
        protected string Recall(string key, string fallback = "") => MindscapeDatabase.GetChoice(Chapter, key, fallback);

        protected void Complete(string title, string body)
        {
            if (IsComplete || IsFailed) return;
            IsComplete = true; roaming = false; HidePanels(); hudShown = false; SetLocked(true);
            PlayerPrefs.SetInt(CompletionKey, 1); PlayerPrefs.Save();
            MindscapeDatabase.EpisodeCompleted(Chapter, Episode);
            StartCoroutine(Finish(title, body));
        }

        IEnumerator Finish(string title, string body)
        {
            string reflection = "";
            yield return Reflect(summary => reflection = summary);
            yield return CheckIn(level => checkInEnd = level);
            SaveRecord("completed");
            completeHeading.text = "CHAPTER " + Chapter + "  /  EPISODE " + Episode.ToString("00") + " COMPLETE";
            completeTitle.text = title;
            completeBody.text = body + "\n\n" + reflection;
            completeStats.text = "Stress check-in  " + (checkInStart > 0 ? checkInStart + " / 5  →  " : "") + checkInEnd + " / 5      Time left  " + Clock(TimeLeft);
            completeNext.text = CompletionNext;
            completeContinue.text = NextAvailable || HasFinale ? ContinueLabel : "Main menu";
            ShowScreen(completeScreen, true);
            effects.PlayOneShot(chime, .14f);
        }

        // The reflection journal (it can be left empty), then the optional self-assessment. The summary goes on the
        // completion screen.
        IEnumerator Reflect(Action<string> summary)
        {
            string prompt = EpisodeReflection.Prompt(Chapter, Episode), entry = "";
            yield return Ask("Reflection Journal", prompt, "Write a few words, in your own way, or leave it empty.",
                MindscapeDatabase.LatestJournal(Chapter, Episode), true, 1000, "Save and continue", e => entry = e, allowEmpty: true);
            if (entry.Length > 0) MindscapeDatabase.SaveJournal(Chapter, Episode, prompt, entry);

            bool consented = false;
            yield return AssessmentConsent(value => consented = value);
            string result = entry.Length > 0 ? "Your journal entry is saved." : "You left the journal empty this time.";
            if (consented)
            {
                int level = 0, strategy = 0;
                yield return Choose("Self-reflection assessment", "How overwhelmed did you feel during this episode?", EpisodeReflection.OverwhelmLevels, i => level = i);
                yield return Choose("Self-reflection assessment", "What coping strategy did you find most useful?", EpisodeReflection.Strategies, i => strategy = i);
                MindscapeDatabase.SaveAssessment(Chapter, Episode, level + 1, EpisodeReflection.Strategies[strategy]);
                result += " Most useful this time: " + EpisodeReflection.Strategies[strategy] + ".";
            }
            else result += " You skipped the assessment; no answers were recorded.";
            summary(result);
        }

        // ================================================================ stress, check-ins and the time limit

        // The check-in cards show each level's number and face (MoodFace), so the levels are words only.
        static readonly string[] CheckInLevels = { "Calm", "A little tense", "Stressed", "Very stressed", "Overwhelmed" };

        // A story moment that weighs on the player (an alarm, being late, a comparison). Never ends the episode.
        protected void AddStress(float amount)
        {
            stress = Mathf.Clamp(stress + amount, 0, 100);
            stressPeak = Mathf.Max(stressPeak, stress);
        }

        // A coping moment (a breath, grounding, talking with someone).
        protected void EaseStress(float amount) => AddStress(-amount);

        IEnumerator CheckIn(Action<int> answered)
        {
            // The choice panel sits above any fade the story left on screen.
            int level = 3;
            yield return Choose("Stress check-in", "The episode is over. How stressed do you feel now?",
                CheckInLevels, i => level = i + 1, moods: true);
            Record("checkin_end", level.ToString());
            answered(level);
        }

        void TickTimer(float delta)
        {
            if (IsComplete || IsFailed || IsPaused) return;
            TimeLeft = Mathf.Max(0, TimeLeft - delta);
            if (TimeLeft < 60) AddStress(delta * .5f);   // the last minute weighs a little more
            if (TimeLeft <= 0) StartCoroutine(OutOfTime());
        }

        IEnumerator OutOfTime()
        {
            IsFailed = true;
            if (story != null) StopCoroutine(story);
            roaming = false; pending = null; HidePanels(); hudShown = false; SetLocked(true);
            SaveRecord("time_up");
            effects.PlayOneShot(softTick, .3f);
            yield return Fade(fadeAlpha, .85f, .6f);
            ShowScreen(failedScreen, true);
        }

        void SaveRecord(string result) => MindscapeDatabase.SaveEpisodeRecord(new EpisodeRecord
        {
            chapter = Chapter, episode = Episode, result = result, time_limit = Mathf.RoundToInt(TimeLimit), seconds_left = Mathf.RoundToInt(TimeLeft),
            checkin_start = checkInStart, checkin_end = checkInEnd, stress_peak = Mathf.RoundToInt(stressPeak), stress_final = Mathf.RoundToInt(stress)
        });

        static string Clock(float seconds) { int s = Mathf.CeilToInt(seconds); return (s / 60).ToString("00") + ":" + (s % 60).ToString("00"); }

        void DrawFlashback()
        {
            if (flashbackRoot == null) return;
            bool show = !string.IsNullOrEmpty(flashbackNow) && !IsComplete && !IsFailed;
            ShowScreen(flashbackRoot, show);
            if (!show) return;
            flashbackWhen.text = flashbackNow;
            // The tint follows the screen fade (the frame sits above it); the badge stays through fades
            // and steps aside for an episode's own full-screen panel (the examination paper's clock is top-right).
            flashbackTint.style.opacity = 1 - fadeAlpha;
            ShowScreen(flashbackBadge, !OverlayOpen);
        }

        void DrawStatus()
        {
            DrawFlashback();
            if (statusBox == null) return;
            // The HUD shows while roaming, and steps aside for full-screen overlays (courtroom, journal, exam), which
            // are uGUI and would otherwise sit under these UI Toolkit boxes.
            bool hudOn = hudShown && !OverlayOpen && !IsPaused;
            ShowScreen(objectiveBox, hudOn);
            if (!hudOn) ShowScreen(promptBox, false); // Update shows it while roaming, near something to press E for
            bool show = hudOn && !IsFailed;
            ShowScreen(statusBox, show);
            if (!show) return;
            timerText.text = "TIME LEFT  " + Clock(TimeLeft);
            timerText.EnableInClassList("urgent", TimeLeft < 60 && Mathf.Repeat(Time.unscaledTime, 1) < .5f);
            stressShown = Mathf.Lerp(stressShown, stress / 100f, Time.unscaledDeltaTime * 4);
            stressFill.style.width = UIE.Length.Percent(stressShown * 100);
            stressFill.style.backgroundColor = Color.Lerp(Teal, new Color32(196, 112, 92, 255), stress / 100f);
            stressText.text = "STRESS  " + Mathf.RoundToInt(stress);
        }

        bool NextAvailable => !string.IsNullOrEmpty(NextScene) && Application.CanStreamedLevelBeLoaded(NextScene);

        // The last episode of the story ends with a closing screen after the Episode Complete panel's Continue.
        protected virtual bool HasFinale => false;
        protected virtual string FinaleHeading => "THE END";
        protected virtual string FinaleTitle => "Thanks for playing with Mindscape";
        protected virtual string FinaleBody => "";

        // The scene fades to black, then the title and the Main menu button fade in.
        IEnumerator Finale()
        {
            finaleHeading.text = FinaleHeading; finaleTitle.text = FinaleTitle; finaleBody.text = FinaleBody;
            finaleBody.style.display = string.IsNullOrEmpty(FinaleBody) ? UIE.DisplayStyle.None : UIE.DisplayStyle.Flex;
            finaleScreen.style.opacity = 0; finaleContent.style.opacity = 0; finaleMenu.SetEnabled(false);
            ShowScreen(finaleScreen, true);
            yield return FadeElement(finaleScreen, 0, 1, 1.4f);
            ShowScreen(completeScreen, false);
            yield return new WaitForSecondsRealtime(.5f);
            yield return FadeElement(finaleContent, 0, 1, 1.6f);
            finaleMenu.SetEnabled(true);
        }

        static IEnumerator FadeElement(UIE.VisualElement element, float from, float to, float seconds)
        {
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime) { element.style.opacity = Mathf.Lerp(from, to, t / seconds); yield return null; }
            element.style.opacity = to;
        }
        protected virtual string NextScene => Episode >= AcademicPressureChapter.EpisodeCount ? Mindscape.EpisodeOne.EpisodeOneStory.SceneName : AcademicPressureChapter.Scenes[Episode];

        public void ContinueToNext()
        {
            if (!IsComplete) return;
            Time.timeScale = 1;
            if (HasFinale && finaleScreen != null) { if (!finaleStarted) { finaleStarted = true; StartCoroutine(Finale()); } return; }
            if (!NextAvailable) { ReturnToMenu(); return; }
            if (Chapter == 2 && NextScene.StartsWith("Chapter 3 ")) EpisodeLoadingScreen.Load(NextScene, 1, 3);
            else if (Chapter != 1) EpisodeLoadingScreen.Load(NextScene, Episode + 1, Chapter);
            else if (Episode >= AcademicPressureChapter.EpisodeCount) EpisodeLoadingScreen.Load(NextScene, 1, 2);
            else AcademicPressureChapter.Load(Episode + 1);
        }

        public void Replay() { Time.timeScale = 1; SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
        public void ReturnToMenu() { Time.timeScale = 1; MusicManager.LoadSceneFaded("main_menu_new"); }

        public void TogglePause()
        {
            if (!Ready || IsComplete || breathing || OverlayOpen || replay != null) return;
            IsPaused = !IsPaused;
            if (IsPaused) { previousTimeScale = Time.timeScale; Time.timeScale = 0; pauseMenu.SetHint(RoamHint); } else Time.timeScale = previousTimeScale;
            if (IsPaused)
            {
                ShowScreen(objectiveBox, false);
                ShowScreen(statusBox, false);
                ShowScreen(promptBox, false);
            }
            else DrawStatus();
            pauseMenu.SetVisible(IsPaused);
            SetLocked(IsPaused || !roaming); effects.mute = IsPaused;
            if (!IsPaused && typing) FocusEntry();
        }

        // Esc steps back out of the pause menu's settings page before it unpauses.
        void Escape()
        {
            if (IsPaused && pauseMenu.SettingsOpen) pauseMenu.BackToPauseMenu();
            else TogglePause();
        }

        // ================================================================ camera & player

        protected void SetLocked(bool locked)
        {
            if (movement != null) movement.enabled = !locked;
            if (controls != null)
            {
                controls.dialogueOpen = locked;
                controls.tasksOpen = controls.pauseOpen = controls.questOpen = false;
                if (!locked) controls.minigameOpen = false;
            }
            if (locked && animator != null) animator.SetFloat("Speed", 0);
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = locked;
        }

        protected void Teleport(Vector3 position, float yaw)
        {
            if (character != null) character.enabled = false;
            player.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            if (character != null) character.enabled = true;
            if (movement != null) movement.ResetView(yaw);
            lastSafe = position;
        }

        // Story shots sit close to things; the default near plane would slice through anything the camera brushes past.
        const float ShotNearClip = .1f;
        float followNearClip = -1;

        // A shot that backs off toward the player (ShotOver) can land inside their head: keep the camera clear of the body.
        Vector3 ClearOfPlayer(Vector3 position, Vector3 lookAt) =>
            player == null ? position : ShotFocus.ClearOfBody(position, lookAt, player, character);

        void UseShotNearClip()
        {
            if (storyCamera == null) return;
            if (followNearClip < 0) followNearClip = storyCamera.nearClipPlane;
            storyCamera.nearClipPlane = Mathf.Min(followNearClip, ShotNearClip);
        }

        // A close story shot (a person, an object, at most 7.5 m away) never looks through a wall: if solid scenery
        // stands between what the shot looks at and the camera, the camera comes forward to just in front of it.
        // Establishing shots, which look at a point far ahead, are left as framed. People (the player, NPCs) and
        // triggers do not count. Any shot whose camera sits inside a wall is nudged toward what it looks at.
        Vector3 ClearOfWalls(Vector3 position, Vector3 lookAt)
        {
            Vector3 toCamera = position - lookAt;
            float distance = toCamera.magnitude;
            if (distance < .3f) return position;
            Vector3 direction = toCamera / distance;
            if (distance > 7.5f)
            {
                for (int i = 0; i < 12 && Embedded(position); i++) position -= direction * .3f;
                return position;
            }
            bool backfaces = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            var hits = Physics.SphereCastAll(lookAt, .2f, direction, distance, ~(1 << 2), QueryTriggerInteraction.Ignore);
            Physics.queriesHitBackfaces = backfaces;
            float nearest = distance;
            foreach (var hit in hits)
            {
                if (hit.distance <= 0f || hit.distance >= nearest) continue;
                var c = hit.collider;
                if (IsPerson(c)) continue;
                if (player != null && c.transform.IsChildOf(player)) continue;
                nearest = hit.distance;
            }
            return nearest < distance ? lookAt + direction * Mathf.Max(.6f, nearest - .15f) : position;
        }

        // The player, NPCs (an Animator on the collider's object, above or below it) and anything with physics.
        static bool IsPerson(Collider c) => c is CharacterController || c.attachedRigidbody != null
            || c.GetComponentInParent<Animator>() != null || c.GetComponentInChildren<Animator>() != null;

        bool Embedded(Vector3 at)
        {
            foreach (var c in Physics.OverlapSphere(at, .15f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                if (!IsPerson(c) && (player == null || !c.transform.IsChildOf(player))) return true;
            return false;
        }

        protected void Shot(Vector3 position, Vector3 lookAt)
        {
            position = ClearOfWalls(position, lookAt);
            position = ClearOfPlayer(position, lookAt);
            UseShotNearClip();
            storyCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
            ShotFocus.Set(lookAt);
        }

        protected IEnumerator Glide(Vector3 position, Vector3 lookAt, float seconds)
        {
            position = ClearOfWalls(position, lookAt);
            position = ClearOfPlayer(position, lookAt);
            UseShotNearClip();
            Vector3 p0 = storyCamera.transform.position; Quaternion r0 = storyCamera.transform.rotation;
            Quaternion r1 = Quaternion.LookRotation(lookAt - position);
            ShotFocus.Set(lookAt);
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / seconds);
                storyCamera.transform.SetPositionAndRotation(Vector3.Lerp(p0, position, k), Quaternion.Slerp(r0, r1, k));
                yield return null;
            }
            storyCamera.transform.SetPositionAndRotation(position, r1);
        }

        // A camera framing a target from roughly where the player stands.
        protected void ShotOver(Transform target, float back = 2.4f, float up = 1.6f)
        {
            Vector3 from = Vector3.ProjectOnPlane(player.position - target.position, Vector3.up);
            if (from.sqrMagnitude < .01f) from = -player.forward;
            Shot(target.position + from.normalized * back + Vector3.up * up, target.position);
        }

        protected IEnumerator Fade(float from, float to, float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime) { SetFade(Mathf.Lerp(from, to, t / seconds)); yield return null; }
            SetFade(to);
        }

        void SetFade(float alpha)
        {
            fadeAlpha = alpha;
            if (fadeLayer != null) fadeLayer.style.opacity = alpha;
        }

        protected AudioClip Tone(string name, float hz, float seconds, bool repeating = false)
        {
            const int rate = 22050; var samples = new float[(int)(rate * seconds)];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate;
                float envelope = repeating ? (Mathf.Repeat(t, .28f) < .13f ? .25f : 0) : Mathf.Exp(-t * 6) * .35f;
                samples[i] = (Mathf.Sin(t * hz * Mathf.PI * 2) + .25f * Mathf.Sin(t * hz * 2.01f * Mathf.PI * 2)) * envelope;
            }
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false); clip.SetData(samples, 0); return clip;
        }

        // ================================================================ UI helpers (also used by episode panels)

        // Clears the dialogue, choice, card and text-entry panels (e.g. before an episode opens its own full-screen panel).
        protected void HidePanels()
        {
            if (entryScreen != null && !typing) ShowScreen(entryScreen, false);
            foreach (var p in new[] { dialogue, choicePanel, card }) if (p != null) ShowScreen(p, false);
        }

        void BuildUI()
        {
            MinigameUI.EnsureInput(); // UI Toolkit reads pointer input through the scene's EventSystem
            HidePanels();
            BuildScreens();
        }

        // A UI Toolkit file as an episode's own full-screen panel (the examination paper, the thought journal, the laptop's
        // task list). Sorted under the screen fade (-2) and every story panel, as the old canvas screens were.
        protected UIE.VisualElement Overlay(UIE.VisualTreeAsset tree, string name, int order = -3)
        {
            var ui = EpisodeUI.Load();
            return Document("Chapter " + Chapter + " · Episode " + Episode.ToString("00") + " " + name, ui.panelSettings, tree, order);
        }

        // UI Toolkit documents, each its own file in Assets/UI Toolkit (editable in UI Builder): EpisodeObjective.uxml,
        // EpisodeStatus.uxml, Dialogue.uxml, Choice.uxml, EpisodeComplete.uxml and the shared PauseMenu.uxml, found
        // through Resources/EpisodeUI.asset. This only finds the named elements and hooks up the buttons.
        void BuildScreens()
        {
            var ui = EpisodeUI.Load();
            string prefix = "Chapter " + Chapter + " · Episode " + Episode.ToString("00") + " ";
            if (ui.screenFade != null)
            {
                fadeLayer = Document(prefix + "fade", ui.panelSettings, ui.screenFade, -2).Q("fade");
                fadeLayer.style.opacity = fadeAlpha;
            }
            // Sorting order: flashback frame < HUD < dialogue < choices < story card < end screens < pause menu (over everything).
            if (ui.flashback != null)
            {
                var flashbackDoc = Document(prefix + "flashback", ui.panelSettings, ui.flashback, -1);
                flashbackDoc.Query().ForEach(e => e.pickingMode = UIE.PickingMode.Ignore);
                flashbackRoot = flashbackDoc.Q("flashback"); flashbackTint = flashbackDoc.Q("flashback-tint");
                flashbackBadge = flashbackDoc.Q("flashback-badge"); flashbackWhen = flashbackDoc.Q<UIE.Label>("flashback-when");
                flashbackBadge.EnableInClassList("mobile", MobileControls.Enabled);
                flashbackNow = FlashbackLabel;
                ShowScreen(flashbackRoot, false);
            }
            var objectiveRoot = Document(prefix + "objective", ui.panelSettings, ui.objective, 0);
            var statusRoot = Document(prefix + "status", ui.panelSettings, ui.status, 0);
            var promptRoot = Document(prefix + "interact prompt", ui.panelSettings, ui.interactPrompt, 0);
            var dialogueRoot = Document(prefix + "dialogue", ui.panelSettings, dialogueOverride != null ? dialogueOverride : ui.dialogue, 1);
            var choiceRoot = Document(prefix + "choice", ui.panelSettings, choiceOverride != null ? choiceOverride : ui.choice, 2);
            if (ui.textEntry != null)
            {
                var entryRoot = Document(prefix + "text entry", ui.panelSettings, ui.textEntry, 2);
                entryScreen = entryRoot.Q("entry-screen"); entryCard = entryRoot.Q("entry");
                entryTitle = entryRoot.Q<UIE.Label>("entry-title"); entryPrompt = entryRoot.Q<UIE.Label>("entry-prompt");
                entryHint = entryRoot.Q<UIE.Label>("entry-hint"); entryField = entryRoot.Q<UIE.TextField>("entry-field");
                entryConfirm = entryRoot.Q<UIE.Button>("entry-confirm");
                entryConfirm.focusable = false; entryConfirm.clicked += SubmitInput;
                // Enter submits a one-line answer; in the journal it starts a new line.
                entryField.RegisterCallback<UIE.KeyDownEvent>(e =>
                {
                    if (entryField.multiline || (e.keyCode != KeyCode.Return && e.keyCode != KeyCode.KeypadEnter)) return;
                    SubmitInput(); e.StopPropagation();
                }, UIE.TrickleDown.TrickleDown);
                ShowScreen(entryScreen, false);
            }
            var cardRoot = Document(prefix + "story card", ui.panelSettings, ui.storyCard, 3);
            var root = Document(prefix + "complete", ui.panelSettings, ui.complete, 4);
            Document(prefix + "pause menu", ui.panelSettings, ui.pauseMenu, 5, go =>
            {
                pauseMenu = go.AddComponent<PauseMenu>();
                pauseMenu.owner = this; // the episode keeps Esc, time, sound and the cursor
            });

            // The HUD never takes clicks: clicking the game is how the player gets the cursor back.
            foreach (var hudRoot in new[] { objectiveRoot, statusRoot, promptRoot }) hudRoot.Query().ForEach(e => e.pickingMode = UIE.PickingMode.Ignore);
            objectiveBox = objectiveRoot.Q("objective"); statusBox = statusRoot.Q("status");
            foreach (var box in new[] { objectiveBox, statusBox }) { box.EnableInClassList("mobile", MobileControls.Enabled); ShowScreen(box, false); }
            promptBox = promptRoot.Q("prompt"); promptText = promptRoot.Q<UIE.Label>("prompt-text");
            ShowScreen(promptBox, false);
            objectiveRoot.Q<UIE.Label>("objective-chapter").text = "CHAPTER " + Chapter + "  /  EPISODE " + Episode.ToString("00");
            objectiveText = objectiveRoot.Q<UIE.Label>("objective-title"); checklistText = objectiveRoot.Q<UIE.Label>("objective-checklist");
            objectiveText.text = checklistText.text = "";
            timerText = statusRoot.Q<UIE.Label>("status-timer"); stressText = statusRoot.Q<UIE.Label>("status-stress");
            stressFill = statusRoot.Q("status-fill");

            dialogue = dialogueRoot.Q("dialogue");
            speakerText = dialogueRoot.Q<UIE.Label>("dialogue-speaker"); speechText = dialogueRoot.Q<UIE.Label>("dialogue-speech");
            dialogueRoot.Q<UIE.Button>("dialogue-continue").clicked += Advance;
            dialogueMode = dialogueRoot.Q<UIE.Label>("dialogue-mode");
            dialogueContext = dialogueRoot.Q("dialogue-context");
            dialogueProgress = dialogueRoot.Q("dialogue-progress");
            dialogueDots = dialogueRoot.Q("dialogue-progress-dots");
            dialogueProgressText = dialogueRoot.Q<UIE.Label>("dialogue-progress-text");
            if (dialogueMode != null)
            {
                dialogue.EnableInClassList("touch", MobileControls.Enabled);
                dialogueRoot.RegisterCallback<UIE.GeometryChangedEvent>(e =>
                    dialogue.EnableInClassList("compact", e.newRect.width < 850));
            }
            ShowScreen(dialogue, false);

            // Story props shown up close, under the dialogue bar (thoughts appear below them).
            if (ui.phoneChat != null)
            {
                var phoneRoot = Document(prefix + "phone", ui.panelSettings, ui.phoneChat, 0);
                phoneRoot.Query().ForEach(e => e.pickingMode = UIE.PickingMode.Ignore);
                phoneScreen = phoneRoot.Q("phone-screen"); phoneMessages = phoneRoot.Q("phone-messages");
                phoneTyping = phoneRoot.Q("phone-typing"); phoneHint = phoneRoot.Q("phone-hint");
                phoneName = phoneRoot.Q<UIE.Label>("phone-name"); phoneStatus = phoneRoot.Q<UIE.Label>("phone-status");
                phoneAvatar = phoneRoot.Q<UIE.Label>("phone-avatar"); phoneClock = phoneRoot.Q<UIE.Label>("phone-clock");
                phoneReplies = phoneRoot.Q("phone-replies"); phoneInputText = phoneRoot.Q<UIE.Label>("phone-input-text");
                phoneChat = phoneRoot.Q("phone-chat"); phoneLock = phoneRoot.Q("phone-lock"); phoneNotifications = phoneRoot.Q("phone-notifications");
                lockTime = phoneRoot.Q<UIE.Label>("phone-lock-time"); lockDate = phoneRoot.Q<UIE.Label>("phone-lock-date"); lockEmpty = phoneRoot.Q<UIE.Label>("phone-lock-empty");
                foreach (var e in new[] { phoneScreen, phoneTyping, phoneHint, phoneReplies }) if (e != null) ShowScreen(e, false);
            }
            if (ui.photograph != null)
            {
                var photoRoot = Document(prefix + "photograph", ui.panelSettings, ui.photograph, 0);
                photoRoot.Query().ForEach(e => e.pickingMode = UIE.PickingMode.Ignore);
                photoScreen = photoRoot.Q("photo-screen"); photoImage = photoRoot.Q("photo-image");
                photoCaption = photoRoot.Q<UIE.Label>("photo-caption");
                ShowScreen(photoScreen, false);
            }

            choicePanel = choiceRoot.Q("choice-screen"); choiceList = choiceRoot.Q("choice-options");
            choiceTitle = choiceRoot.Q<UIE.Label>("choice-title"); choiceQuestion = choiceRoot.Q<UIE.Label>("choice-question");
            choiceHint = choiceRoot.Q<UIE.Label>("choice-hint");
            if (choiceHint != null && MobileControls.Enabled) choiceHint.text = "Tap an option to choose";
            choiceHintText = choiceHint != null ? choiceHint.text : null;
            if (choicePanel.ClassListContains("choice-new"))
                choicePanel.RegisterCallback<UIE.GeometryChangedEvent>(e =>
                    choicePanel.EnableInClassList("compact", e.newRect.width < 800 || e.newRect.height < 540));
            ShowScreen(choicePanel, false);

            card = cardRoot.Q("story-card");
            cardText = cardRoot.Q<UIE.Label>("card-text"); cardHint = cardRoot.Q<UIE.Label>("card-hint");
            card.RegisterCallback<UIE.ClickEvent>(_ => Advance()); // click anywhere to continue
            ShowScreen(card, false);

            completeScreen = root.Q("complete-screen"); failedScreen = root.Q("failed-screen");
            ShowScreen(completeScreen, false); ShowScreen(failedScreen, false);
            completeHeading = root.Q<UIE.Label>("complete-heading"); completeTitle = root.Q<UIE.Label>("complete-title");
            completeBody = root.Q<UIE.Label>("complete-body"); completeStats = root.Q<UIE.Label>("complete-stats");
            completeNext = root.Q<UIE.Label>("complete-next");
            completeContinue = root.Q<UIE.Button>("complete-continue");
            finaleScreen = root.Q("finale-screen"); finaleContent = root.Q("finale-content");
            finaleHeading = root.Q<UIE.Label>("finale-heading"); finaleTitle = root.Q<UIE.Label>("finale-title");
            finaleBody = root.Q<UIE.Label>("finale-body"); finaleMenu = root.Q<UIE.Button>("finale-menu");
            if (finaleScreen != null) { ShowScreen(finaleScreen, false); finaleMenu.clicked += ReturnToMenu; }
            root.Q<UIE.Label>("failed-heading").text = "CHAPTER " + Chapter + "  /  EPISODE " + Episode.ToString("00");

            root.Q<UIE.Button>("complete-replay").clicked += Replay;
            root.Q<UIE.Button>("complete-menu").clicked += ReturnToMenu;
            completeContinue.clicked += ContinueToNext;
            root.Q<UIE.Button>("failed-replay").clicked += Replay;
            root.Q<UIE.Button>("failed-menu").clicked += ReturnToMenu;
            // Space and Enter advance the story; a focused button would also "click" on them.
            foreach (var screen in new[] { dialogue, completeScreen, failedScreen, finaleScreen }) if (screen != null) screen.Query<UIE.Button>().ForEach(b => b.focusable = false);
        }

        static UIE.VisualElement Document(string name, UIE.PanelSettings panel, UIE.VisualTreeAsset tree, int order, Action<GameObject> configure = null)
        {
            var go = new GameObject(name);
            go.SetActive(false); // set everything up before UIDocument (and PauseMenu) run OnEnable
            var document = go.AddComponent<UIE.UIDocument>();
            document.panelSettings = panel; document.visualTreeAsset = tree; document.sortingOrder = order;
            configure?.Invoke(go);
            go.SetActive(true);
            var root = document.rootVisualElement;
            root.pickingMode = UIE.PickingMode.Ignore; // clicks pass through to the story controls while nothing is open
            return root;
        }

        static void ShowScreen(UIE.VisualElement screen, bool show) => screen.EnableInClassList("hidden", !show);
    }
}
