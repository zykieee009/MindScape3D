using System.Collections;
using System.Linq;
using Mindscape.Minigames;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Mindscape.EpisodeThree
{
    // Chapter 2 · Episode 3 (exam day): bus arrival, corridor, finding a seat, the timed examination and the
    // grounding exercise, then submitting. The Phase enum is the story order; each phase is one coroutine step.
    // Text lines live in this file; edit them here. Progress is saved under SaveKey.
    public sealed class EpisodeThreeStory : MonoBehaviour, IPausable
    {
        public const string SceneName = "Chapter 2 - Episode 3";
        public static string SaveKey => Mindscape.Data.MindscapeDatabase.ProfileKey("Mindscape.FamilyExpectations.Episode3");
        public enum Phase { Opening, Corridor, Admission, FindingSeat, Sitting, Examination, Grounding, Submitting, Complete }
        public Transform player, entrance, seat, seatView, teacherView, openingView, spawn, bus;
        public Camera storyCamera;
        public GameObject seatMarker;
        [Min(30)] public float examinationSeconds = 180;
        public Phase CurrentPhase { get; private set; }
        public bool IsPaused { get; private set; }
        public bool TimeExpired { get; private set; }
        public bool HasGrounded { get; private set; }
        public float RemainingSeconds { get; private set; }
        public int AnsweredCount => answers.Count(x => x >= 0);
        public int SelectedQuestion { get; private set; }
        public int GroundingStep { get; private set; }
        public bool Ready { get; private set; }
        readonly int[] answers = { -1, -1, -1, -1, -1 };
        readonly bool[] flagged = new bool[5];
        readonly string[] questions = {
            "A notebook costs 24 pesos. How much do three notebooks cost?",
            "Read: ‘Even though it rained, Ana brought the seedlings outside after the clouds cleared.’\n\nWhen did Ana bring the seedlings outside?",
            "A bus travels 12 kilometers in 30 minutes. At the same speed, how far will it travel in one hour?",
            "Which sentence is an observation rather than a prediction?",
            "A class plants 40 seeds. Three-quarters of them sprout. How many seeds sprout?"
        };
        readonly string[][] options = {
            new[]{"48 pesos", "72 pesos", "96 pesos"},
            new[]{"Before the rain", "While it was raining", "After the clouds cleared"},
            new[]{"18 kilometers", "24 kilometers", "36 kilometers"},
            new[]{"The clock shows 8:15.", "Everyone will finish before me.", "This will be my worst examination."},
            new[]{"10 seeds", "20 seeds", "30 seeds"}
        };
        readonly string[] groundingWords = { "Find the edge of your desk.", "Feel your feet resting on the floor.", "Notice one steady sound in the room." };
        readonly string[] groundingActions = { "I can see the desk", "My feet are on the floor", "I can hear the clock" };
        readonly Color ink = new Color32(29, 43, 47, 248), paper = new Color32(242, 232, 209, 255), gold = new Color32(219, 179, 111, 255), teal = new Color32(65, 101, 99, 255);
        PlayerMovement movement;
        PlayerControls controls;
        CharacterController character;
        Animator animator;
        Transform hud, dialogue, exam, grounding, pause, complete, confirmation;
        TMP_Text objective, prompt, speaker, speech, clock, question, progress, thought, groundingText, groundingCount, completionText, confirmationText;
        Button[] questionButtons, answerButtons;
        Button groundingButton, flagButton;
        CanvasGroup mainGroup;
        Image fade, pressure;
        GameObject skip;
        Coroutine sequence;
        AudioSource effects;
        AudioClip tick, pageTurn, bell;
        bool advance;
        float lineAt, tickAt, groundingStarted;
        float previousTimeScale = 1;

        IEnumerator Start()
        {
            movement = player.GetComponent<PlayerMovement>(); controls = player.GetComponent<PlayerControls>();
            character = player.GetComponent<CharacterController>(); animator = player.GetComponentInChildren<Animator>();
            // This episode owns overlays; the legacy task-panel shortcut is not used here.
            controls.enabled = false;
            RemainingSeconds = examinationSeconds;
            BuildUI(); SetLocked(true); seatMarker.SetActive(false);
            effects = gameObject.AddComponent<AudioSource>();
            tick = Tone("Classroom clock", 1100, .045f); pageTurn = Tone("Paper rustle", 180, .16f, true); bell = Tone("School bell", 660, 1.3f);
            while (EpisodeLoadingScreen.IsActive) yield return null;
            PlayerPrefs.SetInt(Mindscape.Data.MindscapeProgress.ChapterKey, 2); PlayerPrefs.SetInt(Mindscape.Data.MindscapeProgress.EpisodeKey, 4); PlayerPrefs.Save(); Mindscape.Data.MindscapeDatabase.EpisodeStarted(2, 3);
            Ready = true; sequence = StartCoroutine(Opening());
        }
        void Update()
        {
            if (!Ready || EpisodeLoadingScreen.IsActive) return;
            var keys = Keyboard.current;
            if (keys != null && keys.escapeKey.wasPressedThisFrame) { if (confirmation.gameObject.activeSelf) CancelSubmit(); else TogglePause(); }
            if (IsPaused) return;
            if (keys != null && (keys.spaceKey.wasPressedThisFrame || keys.enterKey.wasPressedThisFrame)) Advance();
            if (CurrentPhase == Phase.Corridor || CurrentPhase == Phase.FindingSeat)
            {
                bool corridor = CurrentPhase == Phase.Corridor;
                prompt.text = InRange(corridor ? entrance : seat) ? (corridor ? "[ E ] Knock and speak to the teacher" : "[ E ] Sit down and begin") : (corridor ? "Follow the corridor to Room 03" : "Find the empty seat with the gold marker");
                if (keys != null && keys.eKey.wasPressedThisFrame) { if (corridor) TryEnter(); else TrySit(); }
                if (player.position.y < -5) Teleport(spawn.position, spawn.eulerAngles.y);
            }
            if (CurrentPhase == Phase.Examination)
            {
                TickExamination(Time.deltaTime);
                if (keys != null && !confirmation.gameObject.activeSelf)
                {
                    if (keys.digit1Key.wasPressedThisFrame) Answer(0);
                    if (keys.digit2Key.wasPressedThisFrame) Answer(1);
                    if (keys.digit3Key.wasPressedThisFrame) Answer(2);
                    if (keys.leftArrowKey.wasPressedThisFrame) SelectQuestion((SelectedQuestion + 4) % 5);
                    if (keys.rightArrowKey.wasPressedThisFrame) SelectQuestion((SelectedQuestion + 1) % 5);
                    if (keys.bKey.wasPressedThisFrame) BeginGrounding();
                }
                if (Time.time > tickAt && !confirmation.gameObject.activeSelf) { effects.PlayOneShot(tick, .035f); tickAt = Time.time + 1; }
            }
            if (CurrentPhase == Phase.Grounding)
            {
                groundingButton.interactable = Time.time - groundingStarted >= .75f;
                if (keys != null && keys.spaceKey.wasPressedThisFrame) Ground();
            }
            pressure.color = new Color(.26f, .13f, .08f, CurrentPhase == Phase.Examination && RemainingSeconds < 60 && !HasGrounded ? .045f + .015f * Mathf.Sin(Time.time * 2) : 0);
        }
        bool InRange(Transform target) => Vector3.Distance(player.position + Vector3.up * 1.5f, target.position) < 3;
        void SetLocked(bool locked)
        {
            movement.enabled = !locked; controls.dialogueOpen = locked;
            controls.tasksOpen = controls.pauseOpen = controls.questOpen = controls.minigameOpen = false;
            if (locked && animator != null) animator.SetFloat("Speed", 0);
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = locked;
        }
        void Teleport(Vector3 position, float yaw)
        { character.enabled = false; player.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0)); character.enabled = true; movement.ResetView(yaw); }
        public void Advance() { if (!IsPaused && Time.unscaledTime - lineAt > .25f) advance = true; }
        public void SkipOpening()
        {
            if (!Ready || IsPaused || CurrentPhase != Phase.Opening) return;
            if (sequence != null) StopCoroutine(sequence);
            FinishOpening();
        }
        IEnumerator Opening()
        {
            CurrentPhase = Phase.Opening; SetLocked(true); hud.gameObject.SetActive(false); skip.SetActive(true);
            storyCamera.transform.SetPositionAndRotation(openingView.position, openingView.rotation);
            yield return Fade(1, 0, .7f);
            yield return Line("CHAPTER 2 / EPISODE 03", "Running out of time\nThe examination has already begun.");
            if (bus != null)
                for (float elapsed = 0; elapsed < 2; elapsed += Time.deltaTime) { bus.position += Vector3.right * Time.deltaTime * 7; yield return null; }
            yield return Line("JOEY", "I'm here. I can't get those minutes back.\nI just need to reach my classroom.");
            FinishOpening();
        }
        void FinishOpening()
        {
            if (bus != null) bus.gameObject.SetActive(false);
            Teleport(spawn.position, spawn.eulerAngles.y); fade.color = Color.clear; skip.SetActive(false); dialogue.gameObject.SetActive(false);
            CurrentPhase = Phase.Corridor; hud.gameObject.SetActive(true); objective.text = "Reach Room 03"; SetLocked(false);
        }
        public bool TryEnter()
        {
            if (CurrentPhase != Phase.Corridor || IsPaused || !InRange(entrance)) return false;
            CurrentPhase = Phase.Admission; sequence = StartCoroutine(Admission()); return true;
        }
        IEnumerator Admission()
        {
            SetLocked(true); hud.gameObject.SetActive(false); storyCamera.transform.SetPositionAndRotation(teacherView.position, teacherView.rotation);
            yield return Line("JOEY", "I'm sorry I'm late. May I still take the examination?");
            yield return Line("TEACHER", "Come in quietly, Joey. Take the empty seat.\nStart with what you know and use the time that remains.");
            yield return Line("JOEY / THINKING", "It feels like everyone noticed. They're looking at their papers now.\nOne step. Find my seat.");
            dialogue.gameObject.SetActive(false); CurrentPhase = Phase.FindingSeat; seatMarker.SetActive(true); hud.gameObject.SetActive(true);
            objective.text = "Find your empty seat"; SetLocked(false);
        }
        public bool TrySit()
        {
            if (CurrentPhase != Phase.FindingSeat || IsPaused || !InRange(seat)) return false;
            CurrentPhase = Phase.Sitting; sequence = StartCoroutine(Sit()); return true;
        }
        IEnumerator Sit()
        {
            SetLocked(true); seatMarker.SetActive(false); hud.gameObject.SetActive(false);
            yield return Fade(0, 1, .3f); player.gameObject.SetActive(false);
            storyCamera.transform.SetPositionAndRotation(seatView.position, seatView.rotation);
            yield return Fade(1, 0, .4f);
            yield return Line("JOEY / THINKING", "The words look familiar, but my thoughts keep racing.\nI'll begin with one question I understand.");
            dialogue.gameObject.SetActive(false); CurrentPhase = Phase.Examination; exam.gameObject.SetActive(true); DrawExam();
        }
        public void TickExamination(float delta)
        {
            if (CurrentPhase != Phase.Examination || IsPaused || confirmation.gameObject.activeSelf || TimeExpired) return;
            RemainingSeconds = Mathf.Max(0, RemainingSeconds - Mathf.Max(0, delta));
            if (RemainingSeconds <= 0) { TimeExpired = true; effects.PlayOneShot(bell, .12f); }
            DrawClock();
        }
        public bool SelectQuestion(int index)
        {
            if (CurrentPhase != Phase.Examination || IsPaused || confirmation.gameObject.activeSelf || index < 0 || index >= 5) return false;
            SelectedQuestion = index; effects.PlayOneShot(pageTurn, .06f); DrawExam(); return true;
        }
        public bool Answer(int index)
        {
            if (CurrentPhase != Phase.Examination || IsPaused || confirmation.gameObject.activeSelf || index < 0 || index > 2) return false;
            answers[SelectedQuestion] = index; DrawExam();
            if (AnsweredCount >= 2 && !HasGrounded) BeginGrounding();
            return true;
        }
        public int AnswerFor(int index) => index >= 0 && index < 5 ? answers[index] : -1;
        public void FlagQuestion()
        {
            if (CurrentPhase != Phase.Examination || IsPaused || confirmation.gameObject.activeSelf) return;
            flagged[SelectedQuestion] = !flagged[SelectedQuestion]; DrawExam();
        }
        public bool BeginGrounding()
        {
            if (CurrentPhase != Phase.Examination || IsPaused || confirmation.gameObject.activeSelf) return false;
            CurrentPhase = Phase.Grounding; GroundingStep = 0; groundingStarted = Time.time;
            exam.gameObject.SetActive(false); grounding.gameObject.SetActive(true); DrawGrounding(); return true;
        }
        public bool Ground()
        {
            if (CurrentPhase != Phase.Grounding || IsPaused || Time.time - groundingStarted < .75f) return false;
            GroundingStep++; groundingStarted = Time.time;
            if (GroundingStep >= 3)
            { HasGrounded = true; grounding.gameObject.SetActive(false); CurrentPhase = Phase.Examination; exam.gameObject.SetActive(true); DrawExam(); }
            else DrawGrounding();
            return true;
        }
        void DrawGrounding()
        {
            groundingCount.text = "A MOMENT TO RETURN  /  " + (GroundingStep + 1) + " OF 3";
            groundingText.text = groundingWords[GroundingStep] + "\n\nLet your next breath come naturally.\nYou can return to one question at a time.";
            groundingButton.GetComponentInChildren<TMP_Text>().text = groundingActions[GroundingStep] + "  [Space]";
            groundingButton.interactable = false;
        }
        public void RequestSubmit()
        {
            if (CurrentPhase != Phase.Examination || IsPaused) return;
            confirmation.gameObject.SetActive(true); exam.GetComponent<CanvasGroup>().interactable = false;
            confirmationText.text = AnsweredCount + " of 5 questions answered.\nYou may hand in your work as it is, or return to the paper.";
        }
        public void CancelSubmit()
        { if (IsPaused) return; confirmation.gameObject.SetActive(false); exam.GetComponent<CanvasGroup>().interactable = true; }
        public bool Submit()
        {
            if (CurrentPhase != Phase.Examination || IsPaused || !confirmation.gameObject.activeSelf) return false;
            confirmation.gameObject.SetActive(false); exam.gameObject.SetActive(false); CurrentPhase = Phase.Submitting;
            sequence = StartCoroutine(Ending()); return true;
        }
        IEnumerator Ending()
        {
            effects.PlayOneShot(pageTurn, .15f);
            yield return Line("TEACHER", "Thank you, Joey. You can leave your paper here.");
            yield return Line("JOEY / THINKING", "I did what I could with the time I had.\nI don't know how it will turn out yet.");
            yield return Line("JOEY / THINKING", "But the thought follows me out of the room.\nWhat will my family say if it isn't enough?");
            dialogue.gameObject.SetActive(false); yield return Fade(0, 1, .7f);
            CurrentPhase = Phase.Complete; PlayerPrefs.SetInt(SaveKey, 1); PlayerPrefs.Save(); Mindscape.Data.MindscapeDatabase.EpisodeCompleted(2, 3);
            completionText.text = "You arrived, found your place, and handed in your examination.\n\n" + AnsweredCount + " of 5 questions answered. " + (HasGrounded ? "You took a moment to steady yourself." : "You kept going at your own pace.") + "\n\nYour grade is still unknown. Your completion is saved.";
            complete.gameObject.SetActive(true);
        }
        void DrawClock()
        {
            int seconds = Mathf.CeilToInt(RemainingSeconds);
            clock.text = TimeExpired ? "TIME'S UP  /  WRAP UP" : (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00") + "  REMAINING";
            thought.text = TimeExpired ? "The bell has rung. Finish your thought, then hand in your paper. There is no restart." : HasGrounded ? "My feet are on the floor. I can return to the next question." : "What if I can't finish?  •  Press B to take a grounding break.";
        }
        void DrawExam()
        {
            DrawClock(); question.text = questions[SelectedQuestion]; progress.text = "QUESTION " + (SelectedQuestion + 1).ToString("00") + "   /   " + AnsweredCount + " OF 5 ANSWERED";
            for (int i = 0; i < 5; i++)
            {
                questionButtons[i].GetComponent<Image>().color = i == SelectedQuestion ? gold : teal;
                var label = questionButtons[i].GetComponentInChildren<TMP_Text>();
                label.color = i == SelectedQuestion ? ink : paper;
                label.text = (i + 1).ToString("00") + (answers[i] >= 0 ? "  DONE" : "  —") + (flagged[i] ? "  •" : "");
            }
            for (int i = 0; i < 3; i++)
            {
                answerButtons[i].GetComponent<Image>().color = answers[SelectedQuestion] == i ? gold : new Color32(226, 214, 189, 255);
                answerButtons[i].GetComponentInChildren<TMP_Text>().text = (i + 1) + "   " + options[SelectedQuestion][i] + (answers[SelectedQuestion] == i ? "  [selected]" : "");
            }
            flagButton.GetComponentInChildren<TMP_Text>().text = flagged[SelectedQuestion] ? "Remove review mark" : "Mark for review";
        }
        public void TogglePause()
        {
            if (!Ready || CurrentPhase == Phase.Complete) return;
            IsPaused = !IsPaused;
            if (IsPaused) { previousTimeScale = Time.timeScale; Time.timeScale = 0; } else Time.timeScale = previousTimeScale;
            pause.gameObject.SetActive(IsPaused); mainGroup.interactable = !IsPaused; mainGroup.blocksRaycasts = !IsPaused;
            SetLocked(IsPaused || (CurrentPhase != Phase.Corridor && CurrentPhase != Phase.FindingSeat)); effects.mute = IsPaused;
        }
        public void Replay() { Time.timeScale = 1; SceneManager.LoadScene(SceneName); }
        public void ContinueToNextEpisode()
        {
            if (CurrentPhase != Phase.Complete) return;
            EpisodeLoadingScreen.Load(Mindscape.FamilyFinale.FamilyFinaleEpisode.Scene(4), 4, 2);
        }
        public void ReturnToMenu() { Time.timeScale = 1; SceneManager.LoadScene("main_menu_new"); }
        void OnDestroy()
        { Time.timeScale = 1; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; foreach (var clip in new[]{tick, pageTurn, bell}) if (clip != null) Destroy(clip); }
        IEnumerator Line(string who, string words)
        {
            dialogue.gameObject.SetActive(true); speaker.text = Mindscape.Data.MindscapeDatabase.Personalize(who); speech.text = Mindscape.Data.MindscapeDatabase.Personalize(words); advance = false; lineAt = Time.unscaledTime;
            while (!advance || Time.unscaledTime - lineAt < .35f) yield return null;
        }
        IEnumerator Fade(float from, float to, float duration)
        { for (float t = 0; t < duration; t += Time.deltaTime) { fade.color = new Color(.04f, .06f, .065f, Mathf.Lerp(from, to, t / duration)); yield return null; } fade.color = new Color(.04f, .06f, .065f, to); }
        AudioClip Tone(string name, float hz, float seconds, bool noise = false)
        {
            const int rate = 22050; var samples = new float[(int)(rate * seconds)]; var random = new System.Random(31);
            for (int i = 0; i < samples.Length; i++) { float t = (float)i / rate; samples[i] = (noise ? (float)random.NextDouble() * 2 - 1 : Mathf.Sin(t * hz * Mathf.PI * 2) + .2f * Mathf.Sin(t * hz * 2.76f * Mathf.PI * 2)) * Mathf.Exp(-t * (noise ? 24 : 8)) * .3f; }
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false); clip.SetData(samples, 0); return clip;
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
        RectTransform Panel(string name, Transform parent, Vector2 a, Vector2 b, Color? color = null)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); rect.anchorMin = a; rect.anchorMax = b; rect.offsetMin = rect.offsetMax = Vector2.zero;
            if (color.HasValue) rect.gameObject.AddComponent<Image>().color = color.Value;
            return rect;
        }
        TMP_Text Label(Transform parent, string words, Vector2 a, Vector2 b, float size, bool heading = false, bool dark = false)
        {
            var text = Panel("Text", parent, a, b).gameObject.AddComponent<TextMeshProUGUI>(); text.font = heading ? MinigameUI.SerifFont() : MinigameUI.Font(false);
            text.text = words; text.fontSize = size; text.enableAutoSizing = true; text.fontSizeMin = size * .72f; text.fontSizeMax = size; text.color = dark ? ink : paper; text.raycastTarget = false; return text;
        }
        Button Button(Transform parent, string words, Vector2 a, Vector2 b, UnityEngine.Events.UnityAction action)
        {
            var rect = Panel(words, parent, a, b, gold); var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>(); button.onClick.AddListener(action);
            var text = Label(rect, words, new Vector2(.045f, .08f), new Vector2(.955f, .92f), 24, false, true); text.alignment = TextAlignmentOptions.Center; return button;
        }
        void BuildUI()
        {
            MinigameUI.EnsureInput();
            var canvasGo = new GameObject("Episode 3 interface", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.transform;
            var c = canvasGo.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 210;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            pressure = Panel("Pressure", canvas, Vector2.zero, Vector2.one, Color.clear).GetComponent<Image>(); pressure.raycastTarget = false;
            var main = Panel("Story controls", canvas, Vector2.zero, Vector2.one); mainGroup = main.gameObject.AddComponent<CanvasGroup>();
            hud = Panel("Exploration", main, Vector2.zero, Vector2.one);
            var objectives = Box("Objective", hud, new Vector2(36f, -36f), new Vector2(460f, 215f), new Vector2(0f, 1f), new Vector2(0f, 1f), ink);
            Label(objectives, "CHAPTER 2 / EPISODE 03", new Vector2(.06f, .7f), new Vector2(.94f, .94f), 18).color = gold;
            objective = Label(objectives, "", new Vector2(.06f, .14f), new Vector2(.94f, .68f), 30, true);
            var bottom = Box("Controls", hud, new Vector2(0f, 28f), new Vector2(960f, 86f), new Vector2(.5f, 0f), new Vector2(.5f, 0f), ink);
            prompt = Label(bottom, "", new Vector2(.03f, .46f), new Vector2(.97f, .94f), 25); prompt.alignment = TextAlignmentOptions.Center;
            Label(bottom, "WASD  Move   •   Mouse  Look   •   E  Interact   •   Esc  Pause", new Vector2(.03f, .05f), new Vector2(.97f, .42f), 18).alignment = TextAlignmentOptions.Center;
            dialogue = Panel("Dialogue", main, new Vector2(.07f, .03f), new Vector2(.93f, .22f), ink);
            speaker = Label(dialogue, "", new Vector2(.035f, .74f), new Vector2(.76f, .95f), 17); speaker.color = gold;
            speech = Label(dialogue, "", new Vector2(.035f, .1f), new Vector2(.78f, .71f), 27, true);
            Button(dialogue, "Continue [Space]", new Vector2(.80f, .12f), new Vector2(.965f, .43f), Advance);
            skip = Button(main, "Skip opening", new Vector2(.84f, .89f), new Vector2(.97f, .96f), SkipOpening).gameObject;
            exam = Panel("Examination", main, Vector2.zero, Vector2.one); exam.gameObject.AddComponent<CanvasGroup>();
            Panel("Paper backdrop", exam, Vector2.zero, Vector2.one, new Color(.04f, .06f, .065f, .35f));
            Panel("Examination status", exam, new Vector2(.06f, .88f), new Vector2(.95f, .98f), ink);
            var sheet = Panel("Examination paper", exam, new Vector2(.27f, .12f), new Vector2(.88f, .87f), paper);
            Label(sheet, "GENERAL STUDIES", new Vector2(.055f, .86f), new Vector2(.94f, .95f), 37, true, true);
            progress = Label(sheet, "", new Vector2(.055f, .78f), new Vector2(.94f, .84f), 20, false, true);
            question = Label(sheet, "", new Vector2(.055f, .52f), new Vector2(.94f, .76f), 29, true, true);
            answerButtons = new Button[3];
            for (int i = 0; i < 3; i++) { int choice = i; float y = .36f - i * .12f; answerButtons[i] = Button(sheet, "", new Vector2(.055f, y), new Vector2(.94f, y + .10f), () => Answer(choice)); }
            Label(sheet, "Choose 1–3  •  ← → change question  •  Answers can be revised", new Vector2(.055f, .015f), new Vector2(.94f, .075f), 18, false, true);
            var rail = Panel("Question navigation", exam, new Vector2(.07f, .12f), new Vector2(.245f, .87f), ink);
            Label(rail, "YOUR PAPER", new Vector2(.08f, .87f), new Vector2(.92f, .95f), 22).color = gold;
            questionButtons = new Button[5];
            for (int i = 0; i < 5; i++) { int q = i; float y = .72f - i * .12f; questionButtons[i] = Button(rail, "", new Vector2(.08f, y), new Vector2(.92f, y + .09f), () => SelectQuestion(q)); }
            Label(rail, "DONE  Answered\n• Review later\n\nStart anywhere.", new Vector2(.08f, .025f), new Vector2(.92f, .225f), 20);
            clock = Label(exam, "", new Vector2(.58f, .90f), new Vector2(.94f, .965f), 29); clock.alignment = TextAlignmentOptions.Right;
            flagButton = Button(exam, "Mark for review", new Vector2(.27f, .035f), new Vector2(.45f, .095f), FlagQuestion);
            Button(exam, "Grounding [B]", new Vector2(.47f, .035f), new Vector2(.65f, .095f), () => BeginGrounding());
            Button(exam, "Hand in paper", new Vector2(.67f, .035f), new Vector2(.88f, .095f), RequestSubmit);
            thought = Label(exam, "", new Vector2(.07f, .88f), new Vector2(.55f, .97f), 20);
            exam.gameObject.SetActive(false);
            grounding = Panel("Grounding", main, new Vector2(.25f, .26f), new Vector2(.75f, .81f), ink);
            groundingCount = Label(grounding, "", new Vector2(.07f, .82f), new Vector2(.93f, .94f), 20); groundingCount.color = gold;
            groundingText = Label(grounding, "", new Vector2(.07f, .31f), new Vector2(.93f, .77f), 32, true);
            groundingButton = Button(grounding, "", new Vector2(.07f, .12f), new Vector2(.93f, .25f), () => Ground());
            Label(grounding, "The examination clock pauses while you take this moment.", new Vector2(.07f, .02f), new Vector2(.93f, .09f), 18); grounding.gameObject.SetActive(false);
            confirmation = Panel("Submit confirmation", main, new Vector2(.28f, .28f), new Vector2(.72f, .75f), ink);
            Label(confirmation, "Ready to hand it in?", new Vector2(.08f, .71f), new Vector2(.92f, .93f), 37, true);
            confirmationText = Label(confirmation, "", new Vector2(.08f, .34f), new Vector2(.92f, .68f), 26);
            Button(confirmation, "Return to paper", new Vector2(.06f, .10f), new Vector2(.49f, .27f), CancelSubmit);
            Button(confirmation, "Hand it in", new Vector2(.52f, .10f), new Vector2(.94f, .27f), () => Submit()); confirmation.gameObject.SetActive(false);
            fade = Panel("Fade", canvas, Vector2.zero, Vector2.one, Color.black).GetComponent<Image>(); fade.raycastTarget = false;
            complete = Panel("Complete", canvas, new Vector2(.18f, .13f), new Vector2(.82f, .87f), ink);
            Label(complete, "EPISODE 03 COMPLETE", new Vector2(.07f, .86f), new Vector2(.93f, .95f), 21).color = gold;
            Label(complete, "I used the time I had.", new Vector2(.07f, .68f), new Vector2(.93f, .83f), 49, true);
            completionText = Label(complete, "", new Vector2(.07f, .29f), new Vector2(.93f, .66f), 27);
            Label(complete, "NEXT: THE WEIGHT BEHIND THE WORRY", new Vector2(.07f, .19f), new Vector2(.93f, .26f), 18).color = gold;
            Button(complete, "Replay", new Vector2(.05f, .055f), new Vector2(.28f, .16f), Replay);
            Button(complete, "Main menu", new Vector2(.31f, .055f), new Vector2(.55f, .16f), ReturnToMenu);
            Button(complete, "Continue", new Vector2(.58f, .055f), new Vector2(.95f, .16f), ContinueToNextEpisode); complete.gameObject.SetActive(false);
            pause = Panel("Pause", canvas, Vector2.zero, Vector2.one, new Color(0, 0, 0, .65f));
            var pauseBox = Panel("Pause menu", pause, new Vector2(.32f, .29f), new Vector2(.68f, .71f), ink);
            Label(pauseBox, "Take a moment", new Vector2(.08f, .67f), new Vector2(.92f, .9f), 39, true);
            Button(pauseBox, "Resume [Esc]", new Vector2(.1f, .4f), new Vector2(.9f, .6f), TogglePause); Button(pauseBox, "Main menu", new Vector2(.1f, .13f), new Vector2(.9f, .33f), ReturnToMenu); pause.gameObject.SetActive(false);
        }
    }
}
