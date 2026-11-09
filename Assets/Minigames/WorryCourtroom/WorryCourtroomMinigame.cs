using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Mindscape.AcademicPressure;

namespace Mindscape.Minigames
{
    // CBT-style minigame: put a worry on trial, weigh facts, hear a friend, pick a balanced thought and a small plan.
    // The screen is a UI Toolkit file (Assets/UI Toolkit/WorryCourtroom.uxml and .uss, editable in UI Builder). This
    // script fills it in and runs the rules; the texts, colours and numbers below are edited on the component.
    [AddComponentMenu("Mindscape/Minigames/Worry Courtroom")]
    public class WorryCourtroomMinigame : QuestMinigame
    {
        [Header("Cases (add more entries for more trials; one entry hides the case tabs)")]
        public List<WorryCourtroomCase> cases = new List<WorryCourtroomCase>();
        public int startCase;

        [Header("Behaviour")]
        public CourtRules rules = new CourtRules();
        public CourtTexts texts = new CourtTexts();
        public CourtColors colors = new CourtColors();

        [Header("Scene flow")]
        [Tooltip("On: runs by itself when the scene loads (the standalone Worry Courtroom scene). Off: waits for a Quest or script to call Open(), then closes when finished (used inside Episode 1).")]
        public bool startOpen = true;
        [Tooltip("Scene loaded by the Exit button (standalone only). Leave empty to hide the button.")]
        public string exitSceneName = "";
        [Tooltip("Sorting order of the screen's UI document: above the story panels and the pause menu.")]
        public int sortingOrder = 10;
        [Tooltip("Fired when a case is dismissed. Argument is the star rating (1-3).")]
        public UnityEvent<int> onCaseDismissed = new UnityEvent<int>();
        public UnityEvent onExit = new UnityEvent();

        // The screen's elements (WorryCourtroom.uxml); keep the names there.
        VisualElement root, stageBox, figureBox, meterFill, strikesBox, tabsBox, stepsBox;
        VisualElement[] brows = new VisualElement[2], mouths = new VisualElement[2], stepPills = new VisualElement[6], stars = new VisualElement[3];
        readonly List<VisualElement> strikeDots = new List<VisualElement>();
        Label speakerText, speechText, meterLabel, starsText;
        Button exitButton;
        VisualElement panelIntro, panelSort, panelDefence, panelChoice, panelResult, panelRecess;
        Label introHeading, introLead, introWorry, sortHeading, sortLead, sortCounter, sortCard, sortFeedback;
        Label defenceHeading, defenceLead, defenceFeedback, choiceHeading, choiceLead, choiceFeedback, choiceWhen;
        Label resultHeading, resultFile, resultTraps, recessHeading, recessBody;
        VisualElement defenceOptions, choiceOptions, choiceTimes;
        Button introStart, sortFact, sortGuess, sortNext, defenceContinue, choiceContinue, resultNext, resultReplay, resultContinue, recessContinue;

        // --- trial state ---
        int caseIndex, round, card, strikes, totalStrikes, chosen, time, planTime;
        float worry, shownWorry;
        bool recess, hint, cardDone, fbOk;
        string fb, verdictText, planText;
        EvidenceCard[] cardsOrder; AnswerOption[] defenceOrder, friendOrder, verdictOrder, planOrder;
        readonly List<int> picked = new List<int>();
        readonly List<int> bad = new List<int>();
        readonly List<ThinkingTrap> traps = new List<ThinkingTrap>();
        Coroutine shaking;
        GameObject recoveryBreathing;

        WorryCourtroomCase Case => cases[caseIndex];
        int ChoiceIndex => round - 3;
        AnswerOption[] ChoiceOptions => ChoiceIndex == 0 ? friendOrder : ChoiceIndex == 1 ? verdictOrder : planOrder;

        // ================= lifecycle =================
        bool isOpen;
        Quest activeQuest;
        PlayerControls playerControls;
        bool Embedded => !startOpen;

        public override bool IsOpen => isOpen;
        public override string QuestDisplayName => "Worry Courtroom";
        // What the player chose in the current case (empty until they reach that step); story episodes record these.
        public string BalancedThought => verdictText;
        public string SmallStep => planText;

        void Awake() { BuildUI(); }

        void Start()
        {
            if (cases == null || cases.Count == 0 || cases[0] == null) { Debug.LogError("Worry Courtroom: assign at least one case on the component.", this); return; }
            if (startOpen) OpenNow(null);
            else SetScreen(false);
        }

        // Quest / script entry point.
        public override void Open(Quest quest)
        {
            if (isOpen) return;
            OpenNow(quest != null ? quest : FindAnyObjectByType<Quest>());
        }

        void OpenNow(Quest quest)
        {
            if (cases == null || cases.Count == 0) return;
            activeQuest = quest;
            isOpen = true;
            SetScreen(true);
            if (Embedded)
            {
                if (playerControls == null) playerControls = FindAnyObjectByType<PlayerControls>();
                if (playerControls != null) playerControls.minigameOpen = true;
            }
            UnityEngine.Cursor.lockState = CursorLockMode.None; UnityEngine.Cursor.visible = true;
            NewCase(Mathf.Clamp(startCase, 0, cases.Count - 1));
        }

        // Closing before the case is dismissed leaves the quest able to open it again.
        public override void Close()
        {
            if (!isOpen) return;
            StopRecoveryBreathing();
            bool finished = round >= 6;
            isOpen = false;
            SetScreen(false);
            if (playerControls != null) playerControls.minigameOpen = false;
            if (finished) activeQuest?.NotifyMinigameCompleted(this);
            else activeQuest?.NotifyMinigameClosed(this);
            onExit.Invoke();
        }

        void FinishEmbedded() { Close(); }

        void OnExit()
        {
            if (Embedded) { Close(); return; }
            onExit.Invoke();
            if (!string.IsNullOrEmpty(exitSceneName)) SceneManager.LoadScene(exitSceneName);
        }

        void SetScreen(bool visible)
        {
            if (root != null) root.EnableInClassList("hidden", !visible);
            if (exitButton != null) Show(exitButton, visible && (Embedded || !string.IsNullOrEmpty(exitSceneName)));
        }

        void Update()
        {
            if (!isOpen || meterFill == null || cases == null || cases.Count == 0) return;
            shownWorry = Mathf.MoveTowards(shownWorry, worry, rules.meterSpeed * Time.deltaTime);
            ApplyWorryVisuals();
        }

        // ================= visuals =================
        void ApplyWorryVisuals()
        {
            float w = shownWorry, max = Mathf.Max(1f, rules.startingWorry);
            int mood = w > rules.moodWorriedAbove ? 0 : (w > rules.moodNeutralAbove ? 1 : 2);
            // The worry figure keeps its size and stays fully visible; only its face changes with the worry.
            figureBox.style.scale = new Scale(Vector2.one);
            figureBox.style.opacity = 1f;
            const float scowlBrow = 18, calmBrow = -10, scowlMouth = 24, calmMouth = -24;
            float brow = mood == 0 ? scowlBrow : mood == 1 ? 0 : calmBrow;
            float mouth = mood == 0 ? scowlMouth : mood == 1 ? 0 : calmMouth;
            // USS rotates clockwise, the opposite of the old screen's sign.
            Tilt(brows[0], brow); Tilt(brows[1], -brow);
            Tilt(mouths[0], -mouth); Tilt(mouths[1], mouth);
            meterFill.style.width = Length.Percent(Mathf.Clamp01(w / max) * 100f);
            meterLabel.text = texts.meterFormat.Replace("{value}", Mathf.RoundToInt(w / max * 100f).ToString());
        }
        static void Tilt(VisualElement e, float degrees) { e.style.rotate = new Rotate(new Angle(degrees, AngleUnit.Degree)); }

        IEnumerator Shake()
        {
            for (float t = 0; t < rules.shakeSeconds; t += Time.deltaTime)
            {
                stageBox.style.translate = new Translate(Mathf.Sin(t * 60f) * rules.shakeDistance * (1 - t / rules.shakeSeconds), 0);
                yield return null;
            }
            stageBox.style.translate = new Translate(0, 0);
        }

        // ================= state =================
        void NewCase(int index)
        {
            StopRecoveryBreathing();
            caseIndex = index; var c = Case;
            round = 0; worry = shownWorry = rules.startingWorry; strikes = totalStrikes = 0;
            recess = hint = cardDone = false; fb = null; card = 0; chosen = -1; time = -1; planTime = 0;
            verdictText = planText = ""; picked.Clear(); bad.Clear(); traps.Clear();
            cardsOrder = Order(c.evidence, rules.shuffleEvidence); defenceOrder = Order(c.defence, rules.shuffleOptions);
            friendOrder = Order(c.friend, rules.shuffleOptions); verdictOrder = Order(c.verdict, rules.shuffleOptions); planOrder = Order(c.plan, rules.shuffleOptions);
            if (isOpen) SetScreen(true);
            Render();
        }

        void StopRecoveryBreathing()
        {
            if (recoveryBreathing == null) return;
            var screen = recoveryBreathing;
            recoveryBreathing = null;
            Destroy(screen);
        }

        void StartRecoveryBreathing()
        {
            recoveryBreathing = new GameObject("Courtroom Breathing Break");
            recoveryBreathing.SetActive(false);
            recoveryBreathing.transform.SetParent(transform, false);
            var exercise = recoveryBreathing.AddComponent<BreathingMinigame>();
            exercise.durationSeconds = 15f;
            exercise.getReadySeconds = 5f;
            exercise.closingSeconds = 0f;
            exercise.notifyQuest = false;
            // Return goes back to the courtroom early; finishing the breath does the same.
            exercise.allowEarlyClose = true;
            exercise.texts.closeButton = "Return to the courtroom";
            exercise.sortingOrder = sortingOrder + 1;
            exercise.texts.eyebrow = "WORRY COURTROOM / BREATHING BREAK";
            exercise.texts.getReadyHint = "Three strikes. You are about to take a short breathing break before you try the case again.";
            bool resumed = false;
            void BackToCourt()
            {
                if (resumed || !isOpen) return;
                resumed = true;
                NewCase(caseIndex);
                if (playerControls != null) playerControls.minigameOpen = true;
            }
            exercise.onCompleted.AddListener(BackToCourt);
            exercise.onClosed.AddListener(BackToCourt);
            recoveryBreathing.SetActive(true);
            SetScreen(false);
            exercise.Open();
        }

        static T[] Order<T>(List<T> src, bool shuffle)
        {
            var a = src.ToArray();
            if (!shuffle) return a;
            for (int i = a.Length - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); var t = a[i]; a[i] = a[j]; a[j] = t; }
            return a;
        }

        void Strike()
        {
            strikes++; totalStrikes++;
            if (stageBox != null) { if (shaking != null) StopCoroutine(shaking); shaking = StartCoroutine(Shake()); }
            if (strikes >= rules.maxStrikes)
            {
                recess = true;
                StartRecoveryBreathing();
            }
        }
        void Gain(float n) { worry = Mathf.Max(0, worry - n); }
        void ResetRound() { picked.Clear(); bad.Clear(); chosen = -1; time = -1; fb = null; hint = false; }
        void Feedback(bool ok, string text) { fbOk = ok; fb = text; }
        int Stars() { return totalStrikes <= rules.threeStarsMaxStrikes ? 3 : totalStrikes <= rules.twoStarsMaxStrikes ? 2 : 1; }

        // ================= actions =================
        void OnStart() { round = 1; fb = null; Render(); }

        void OnSort(bool saidFact)
        {
            if (recess || cardDone || cardsOrder.Length == 0) return;
            var c = cardsOrder[card];
            if (saidFact == c.isFact)
            {
                Gain(rules.gainPerEvidence); cardDone = true;
                if (c.isFact) Feedback(true, texts.factAdmitted);
                else
                {
                    if (!traps.Contains(c.trap)) traps.Add(c.trap);
                    var info = Case.FindTrap(c.trap);
                    Feedback(true, texts.guessNotAdmitted.Replace("{trap}", info.displayName).Replace("{description}", info.description));
                }
            }
            else { Strike(); Feedback(false, c.isFact ? texts.wrongSaidGuess : texts.wrongSaidFact); }
            Render();
        }

        void OnNextCard()
        {
            card++; fb = null; hint = false; cardDone = false;
            if (card >= cardsOrder.Length) { round = 2; ResetRound(); }
            Render();
        }

        void OnDefence(int i)
        {
            if (recess || picked.Contains(i) || bad.Contains(i) || picked.Count >= rules.defencePicksRequired) return;
            var o = defenceOrder[i];
            if (o.isGood) { picked.Add(i); Gain(rules.gainPerDefence); Feedback(true, texts.defenceAdmitted); }
            else { bad.Add(i); Strike(); Feedback(false, o.whyNot); }
            Render();
        }

        void OnChoice(int i)
        {
            if (recess || chosen >= 0 || bad.Contains(i)) return;
            var o = ChoiceOptions[i];
            if (o.isGood)
            {
                chosen = i;
                Gain(ChoiceIndex == 0 ? rules.gainFriend : ChoiceIndex == 1 ? rules.gainVerdict : rules.gainPlan);
                if (ChoiceIndex == 1) verdictText = o.text;
                if (ChoiceIndex == 2) planText = o.text;
                Feedback(true, ChoiceIndex == 0 ? texts.friendCorrect : ChoiceIndex == 1 ? texts.verdictCorrect : texts.planCorrect);
            }
            else { bad.Add(i); Strike(); Feedback(false, o.whyNot); }
            Render();
        }

        void OnTime(int i) { time = i; Render(); }

        void OnContinue()
        {
            if (round == 5) planTime = time;
            round++; ResetRound();
            if (round == 6)
            {
                worry = 0; SaveDone(Case.name);
                onCaseDismissed.Invoke(Stars());
            }
            Render();
        }

        void OnRecessEnd() { recess = false; strikes = 0; hint = true; Render(); }

        static string DoneKey => Mindscape.Data.MindscapeDatabase.ProfileKey("Mindscape.WorryCourtroom.Done");
        static List<string> LoadDone() { var l = new List<string>(); var s = PlayerPrefs.GetString(DoneKey, ""); if (s.Length > 0) l.AddRange(s.Split(',')); return l; }
        static void SaveDone(string id) { var l = LoadDone(); if (!l.Contains(id)) { l.Add(id); PlayerPrefs.SetString(DoneKey, string.Join(",", l)); PlayerPrefs.Save(); } }

        // ================= rendering =================
        void Render()
        {
            RenderTabs(); RenderStage(); RenderSteps();
            exitButton.text = Embedded ? texts.closeEarlyButton : texts.exitButton;
            if (recess) RenderRecess();
            else if (round == 0) RenderIntro();
            else if (round == 1) RenderSort();
            else if (round == 2) RenderDefence();
            else if (round <= 5) RenderChoice();
            else RenderResult();
        }

        void ShowOnly(VisualElement p)
        {
            foreach (var g in new[] { panelIntro, panelSort, panelDefence, panelChoice, panelResult, panelRecess }) Show(g, g == p);
        }

        void RenderTabs()
        {
            tabsBox.Clear();
            Show(tabsBox, cases.Count > 1);
            if (cases.Count <= 1) return;
            var done = LoadDone();
            for (int i = 0; i < cases.Count; i++)
            {
                int id = i; var c = cases[i]; if (c == null) continue;
                var b = MakeButton("btn pale court-btn court-tab", c.chapterLabel + "  " + c.caseName + (done.Contains(c.name) ? texts.doneMark : ""), () => NewCase(id));
                b.style.backgroundColor = i == caseIndex ? colors.tabActive : colors.tabInactive;
                tabsBox.Add(b);
            }
        }

        void RenderStage()
        {
            var c = Case; string who, line;
            switch (round)
            {
                case 0: who = texts.worryName; line = "\"" + c.worry + "\""; break;
                case 1: who = texts.prosecutorName; line = texts.prosecutorLine; break;
                case 2: who = texts.defenderName; line = texts.defenderLine; break;
                case 3: who = texts.friendName; line = texts.friendLine; break;
                case 4: who = texts.judgeName; line = texts.judgeLine; break;
                case 5: who = texts.clerkName; line = texts.clerkLine; break;
                default: who = texts.worryName; line = texts.worryFarewell; break;
            }
            speakerText.text = who.ToUpper();
            speechText.text = line;
            // One dot per allowed strike (the screen starts with none; they are made to match the rules).
            while (strikeDots.Count < rules.maxStrikes)
            {
                var dot = new VisualElement(); dot.AddToClassList("court-strike"); strikesBox.Add(dot); strikeDots.Add(dot);
            }
            for (int i = 0; i < strikeDots.Count; i++)
            {
                Show(strikeDots[i], i < rules.maxStrikes);
                strikeDots[i].style.backgroundColor = i < strikes ? colors.strikeOn : colors.strikeOff;
            }
        }

        void RenderSteps()
        {
            for (int i = 0; i < stepPills.Length; i++)
            {
                stepPills[i].style.backgroundColor = round == i ? colors.stepNowFill : colors.stepIdleFill;
                var label = stepPills[i].Q<Label>();
                label.text = i < texts.stepNames.Length ? texts.stepNames[i] : "";
                label.style.color = round == i ? colors.stepNowText : round > i ? colors.stepDoneText : colors.stepIdleText;
            }
        }

        void RenderRecess()
        {
            ShowOnly(panelRecess);
            recessHeading.text = texts.recessHeading; recessBody.text = texts.recessBody;
            recessContinue.text = texts.recessButton;
        }

        void RenderIntro()
        {
            ShowOnly(panelIntro); var c = Case;
            introHeading.text = texts.introHeading.Replace("{case}", c.caseName);
            introLead.text = Pick(c.introLead, texts.introLead);
            introWorry.text = "\"" + c.worry + "\"";
            introStart.text = texts.introButton;
        }

        void RenderSort()
        {
            ShowOnly(panelSort);
            sortHeading.text = texts.sortHeading; sortLead.text = texts.sortLead;
            sortFact.text = texts.factButton; sortGuess.text = texts.guessButton;
            if (cardsOrder.Length == 0) { sortCard.text = "(This case has no evidence cards.)"; return; }
            var c = cardsOrder[card];
            sortCounter.text = texts.evidenceCounter.Replace("{n}", (card + 1).ToString()).Replace("{total}", cardsOrder.Length.ToString());
            sortCard.text = c.text;
            sortFact.EnableInClassList("hint", hint && !cardDone && c.isFact);
            sortGuess.EnableInClassList("hint", hint && !cardDone && !c.isFact);
            ShowFeedback(sortFeedback);
            Show(sortNext, cardDone);
            sortNext.text = card + 1 < cardsOrder.Length ? texts.nextEvidenceButton : texts.toDefenceButton;
        }

        void RenderDefence()
        {
            ShowOnly(panelDefence); var c = Case; int need = rules.defencePicksRequired;
            defenceHeading.text = texts.defenceHeading;
            defenceLead.text = Pick(c.defenceLead, texts.defenceLead).Replace("{needed}", need.ToString()).Replace("{picked}", picked.Count.ToString());
            Populate(defenceOptions, defenceOrder, i => picked.Contains(i), i => bad.Contains(i), OnDefence, picked.Count >= need);
            ShowFeedback(defenceFeedback);
            bool ready = picked.Count >= need;
            Show(defenceContinue, ready); defenceContinue.text = texts.toFriendButton;
        }

        void RenderChoice()
        {
            ShowOnly(panelChoice); var c = Case; int k = ChoiceIndex;
            choiceHeading.text = k == 0 ? texts.friendHeading : k == 1 ? texts.verdictHeading : texts.planHeading;
            choiceLead.text = k == 0 ? Pick(c.friendPrompt, texts.friendPrompt) : k == 1 ? Pick(c.verdictPrompt, texts.verdictPrompt) : Pick(c.planPrompt, texts.planPrompt);
            Populate(choiceOptions, ChoiceOptions, i => chosen == i, i => bad.Contains(i), OnChoice, chosen >= 0);
            ShowFeedback(choiceFeedback);
            bool isPlan = k == 2;
            Show(choiceWhen, isPlan && chosen >= 0); choiceWhen.text = texts.whenLabel;
            choiceTimes.Clear();
            Show(choiceTimes, isPlan && chosen >= 0);
            if (isPlan && chosen >= 0)
                for (int i = 0; i < texts.whenChoices.Length; i++)
                {
                    int id = i;
                    var b = MakeButton("btn pale court-btn court-chip", texts.whenChoices[i], () => OnTime(id));
                    b.style.backgroundColor = time == i ? colors.chipActive : colors.chipInactive;
                    choiceTimes.Add(b);
                }
            bool ready = chosen >= 0 && (!isPlan || time >= 0);
            Show(choiceContinue, ready);
            choiceContinue.text = k == 0 ? texts.toVerdictButton : k == 1 ? texts.toPlanButton : texts.closeCaseButton;
        }

        void RenderResult()
        {
            ShowOnly(panelResult); var c = Case; int count = Stars();
            resultHeading.text = texts.resultHeading;
            starsText.text = texts.starsFormat.Replace("{n}", count.ToString());
            for (int i = 0; i < stars.Length; i++) stars[i].style.backgroundColor = i < count ? colors.starOn : colors.starOff;
            string when = texts.whenChoices.Length > 0 ? texts.whenChoices[Mathf.Clamp(planTime, 0, texts.whenChoices.Length - 1)] : "";
            resultFile.text = texts.caseFileFormat.Replace("{worry}", c.worry).Replace("{balanced}", verdictText).Replace("{plan}", planText).Replace("{when}", when);
            var sb = new System.Text.StringBuilder(texts.trapsHeading).Append('\n');
            foreach (var t in traps) { var info = c.FindTrap(t); sb.Append(texts.trapLineFormat.Replace("{trap}", info.displayName).Replace("{description}", info.description)).Append('\n'); }
            sb.Append('\n').Append(texts.strikesLabel).Append(' ').Append(totalStrikes);
            resultTraps.text = sb.ToString();
            resultNext.text = texts.nextCaseButton; resultReplay.text = texts.replayButton; resultContinue.text = texts.continueButton;
            Show(resultNext, cases.Count > 1 && !Embedded);
            Show(resultReplay, !Embedded);
            Show(resultContinue, Embedded);
        }

        // ================= helpers =================
        static string Pick(string over, string fallback) { return string.IsNullOrWhiteSpace(over) ? fallback : over; }
        static void Show(VisualElement e, bool on) { if (e != null) e.EnableInClassList("hidden", !on); }

        static Button MakeButton(string classes, string words, Action action)
        {
            var b = new Button(action) { text = words, focusable = false }; // Space and Enter belong to the story
            foreach (var c in classes.Split(' ')) b.AddToClassList(c);
            return b;
        }

        void ShowFeedback(Label t)
        {
            Show(t, !string.IsNullOrEmpty(fb));
            t.text = fb ?? ""; t.style.color = fbOk ? colors.good : colors.wrong;
        }

        void Populate(VisualElement container, AnswerOption[] opts, Func<int, bool> isOk, Func<int, bool> isBad, Action<int> pick, bool locked)
        {
            container.Clear();
            string width = opts.Length >= rules.twoColumnThreshold ? "two" : "one";
            for (int i = 0; i < opts.Length; i++)
            {
                int id = i;
                var b = MakeButton("court-opt " + width, opts[i].text, () => pick(id));
                b.style.backgroundColor = isOk(i) ? colors.optionCorrect : isBad(i) ? colors.optionWrong : colors.optionNormal;
                b.EnableInClassList("hint", hint && opts[i].isGood && !isOk(i) && !isBad(i) && !locked);
                container.Add(b);
            }
        }

        // ================= screen =================
        void BuildUI()
        {
            var ui = EpisodeUI.Load();
            var document = EpisodeUI.Spawn("Worry Courtroom Screen", ui != null ? ui.worryCourtroom : null, sortingOrder, transform);
            if (document == null) { Debug.LogWarning("Worry Courtroom: WorryCourtroom.uxml is missing from Resources/EpisodeUI.", this); return; }
            root = document.rootVisualElement.Q("court-root");
            var art = Resources.Load<Texture2D>("MenuArt/Background");
            if (art != null) root.Q("court-art").style.backgroundImage = new StyleBackground(art);

            stageBox = root.Q("court-stage"); figureBox = root.Q("court-figure");
            brows[0] = root.Q("court-brow-l"); brows[1] = root.Q("court-brow-r"); mouths[0] = root.Q("court-mouth-l"); mouths[1] = root.Q("court-mouth-r");
            speakerText = root.Q<Label>("court-speaker"); speechText = root.Q<Label>("court-speech");
            meterLabel = root.Q<Label>("court-meter-label"); meterFill = root.Q("court-meter-fill");
            strikesBox = root.Q("court-strikes"); strikesBox.Clear();
            tabsBox = root.Q("court-tabs"); stepsBox = root.Q("court-steps");
            for (int i = 0; i < stepPills.Length; i++) stepPills[i] = root.Q("court-step-" + i);
            exitButton = root.Q<Button>("court-exit"); exitButton.clicked += OnExit;

            panelIntro = root.Q("p-intro"); panelSort = root.Q("p-sort"); panelDefence = root.Q("p-defence");
            panelChoice = root.Q("p-choice"); panelResult = root.Q("p-result"); panelRecess = root.Q("p-recess");
            introHeading = root.Q<Label>("intro-heading"); introLead = root.Q<Label>("intro-lead"); introWorry = root.Q<Label>("intro-worry");
            introStart = root.Q<Button>("intro-start"); introStart.clicked += OnStart;
            sortHeading = root.Q<Label>("sort-heading"); sortLead = root.Q<Label>("sort-lead"); sortCounter = root.Q<Label>("sort-counter");
            sortCard = root.Q<Label>("sort-card"); sortFeedback = root.Q<Label>("sort-feedback");
            sortFact = root.Q<Button>("sort-fact"); sortFact.clicked += () => OnSort(true);
            sortGuess = root.Q<Button>("sort-guess"); sortGuess.clicked += () => OnSort(false);
            sortNext = root.Q<Button>("sort-next"); sortNext.clicked += OnNextCard;
            defenceHeading = root.Q<Label>("defence-heading"); defenceLead = root.Q<Label>("defence-lead"); defenceFeedback = root.Q<Label>("defence-feedback");
            defenceOptions = root.Q("defence-options");
            defenceContinue = root.Q<Button>("defence-continue"); defenceContinue.clicked += OnContinue;
            choiceHeading = root.Q<Label>("choice-heading"); choiceLead = root.Q<Label>("choice-lead"); choiceFeedback = root.Q<Label>("choice-feedback");
            choiceWhen = root.Q<Label>("choice-when"); choiceOptions = root.Q("choice-options"); choiceTimes = root.Q("choice-times");
            choiceContinue = root.Q<Button>("choice-continue"); choiceContinue.clicked += OnContinue;
            resultHeading = root.Q<Label>("result-heading"); starsText = root.Q<Label>("result-stars-text");
            for (int i = 0; i < stars.Length; i++) stars[i] = root.Q("result-star-" + i);
            resultFile = root.Q<Label>("result-file"); resultTraps = root.Q<Label>("result-traps");
            resultNext = root.Q<Button>("result-next"); resultNext.clicked += () => NewCase((caseIndex + 1) % cases.Count);
            resultReplay = root.Q<Button>("result-replay"); resultReplay.clicked += () => NewCase(caseIndex);
            resultContinue = root.Q<Button>("result-continue"); resultContinue.clicked += FinishEmbedded;
            recessHeading = root.Q<Label>("recess-heading"); recessBody = root.Q<Label>("recess-body");
            recessContinue = root.Q<Button>("recess-continue"); recessContinue.clicked += OnRecessEnd;
            // Space and Enter belong to the story; a focused button would also "click" on them.
            root.Query<Button>().ForEach(b => b.focusable = false);
            root.EnableInClassList("hidden", true);
        }
    }
}
