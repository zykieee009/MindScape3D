using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;
using Mindscape.AcademicPressure;
using Mindscape.Minigames;

// Guided breathing: a circle opens on the in-breath, rests, and settles on the out-breath.
// The screen is a UI Toolkit file (Assets/UI Toolkit/Breathing.uxml and .uss, editable in UI Builder) in the game's
// wood / parchment / brass theme. This script fills in the text and animates the circle and the progress bar.
[AddComponentMenu("Mindscape/Minigame/Breathing Minigame")]
public class BreathingMinigame : QuestMinigame
{
    [Header("Breathing Exercise")]
    [Min(1f)] public float durationSeconds = 15f;
    [Min(0f)] public float getReadySeconds = 5f;
    // One full breath fills the 15 seconds exactly (5 + 3 + 7), so the exercise never ends in the middle of an in-breath.
    [Min(0.5f)] public float inhaleSeconds = 5f;
    [Min(0.5f)] public float holdSeconds = 3f;
    [Min(0.5f)] public float exhaleSeconds = 7f;
    [Tooltip("How long the closing line stays on screen before the exercise ends.")]
    [Min(0f)] public float closingSeconds = 2.5f;
    [Tooltip("Disable for a breathing break inside another minigame.")]
    public bool notifyQuest = true;
    public bool allowEarlyClose = true;

    [Header("Events")]
    public UnityEvent onCompleted = new UnityEvent();   // initialised: episodes add this component at runtime
    public UnityEvent onClosed = new UnityEvent();

    [Header("Text")]
    public BreathingTexts texts = new BreathingTexts();
    [Header("Look")]
    public BreathingLook look = new BreathingLook();

    [Tooltip("Sorting order of the screen's UI document: above the story panels and the pause menu.")]
    public int sortingOrder = 10;

    [Serializable]
    public class BreathingTexts
    {
        public string eyebrow = "MINDSCAPE / BREATHING EXERCISE";
        public string title = "Breathe";
        public string getReady = "Get ready";
        public string getReadyHint = "You are about to begin a breathing exercise. Sit comfortably and relax your shoulders.";
        public string inhale = "Breathe in";
        public string inhaleHint = "Let the air arrive.";
        public string hold = "Hold";
        public string holdHint = "Rest here for a moment.";
        public string exhale = "Breathe out";
        public string exhaleHint = "Let it leave on its own.";
        public string closing = "That is the whole exercise.";
        public string closingHint = "The breath is always here.";
        public string closeButton = "Close";
    }

    [Serializable]
    public class BreathingLook
    {
        [Range(0.2f, 1f)] public float smallestScale = 0.55f;
        [Range(0.5f, 1.3f)] public float largestScale = 1f;
        public Color circleSmall = new Color32(122, 170, 212, 255);   // soft blue, deeper when the breath is out
        public Color circleLarge = new Color32(188, 218, 242, 255);   // and lighter when it is full
        public Color ringColor = new Color32(122, 170, 212, 60);
        public Color coreColor = new Color32(23, 36, 49, 235);
        public Color textColor = new Color32(232, 241, 248, 255);
        public Color mutedColor = new Color32(160, 184, 204, 255);
    }

    // The screen's elements (Breathing.uxml); keep the names there.
    VisualElement root, circle, ring, core, progressFill;
    Label titleLabel, phaseLabel, hintLabel, countLabel, readyCountLabel, timerLabel;
    Button closeButton;
    PlayerControls playerControls;
    Quest activeQuest;
    float remaining;
    float getReadyLeft;
    float closingLeft;
    bool running, closing, preparing;

    public override bool IsOpen => running;
    public override string QuestDisplayName => "Breathing Exercise";
    public float RemainingSeconds => Mathf.Max(0f, remaining);

    void Awake()
    {
        playerControls = FindAnyObjectByType<PlayerControls>();
        BuildUI();
        Hide();
    }

    void OnDestroy()
    {
        if (root != null && root.panel != null) root.RemoveFromHierarchy();
    }

    void Update()
    {
        if (!running) return;

        if (closing)
        {
            closingLeft -= Time.unscaledDeltaTime;
            if (closingLeft <= 0f) Complete();
            return;
        }

        if (preparing)
        {
            getReadyLeft -= Time.unscaledDeltaTime;
            if (getReadyLeft > 0f) { UpdatePreparationUI(); return; }
            preparing = false;
            remaining = Mathf.Max(0f, remaining + getReadyLeft);
            UpdateUI();
            return;
        }

        remaining -= Time.unscaledDeltaTime;
        UpdateUI();
        if (remaining <= 0f) BeginClosing();
    }

    public void Open() { Open(null); }

    public override void Open(Quest quest)
    {
        if (running) return;
        if (playerControls == null) playerControls = FindAnyObjectByType<PlayerControls>();

        activeQuest = notifyQuest ? (quest != null ? quest : FindAnyObjectByType<Quest>()) : null;
        remaining = Mathf.Max(1f, durationSeconds);
        getReadyLeft = Mathf.Max(0f, getReadySeconds);
        preparing = getReadyLeft > 0f;
        closing = false;
        running = true;
        if (root != null) root.EnableInClassList("hidden", false);
        if (titleLabel != null) titleLabel.text = texts.title.ToUpperInvariant();
        if (closeButton != null) closeButton.text = texts.closeButton;
        if (closeButton != null) closeButton.EnableInClassList("hidden", !allowEarlyClose);
        if (playerControls != null) playerControls.minigameOpen = true;
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;
        if (preparing) UpdatePreparationUI(); else UpdateUI();
    }

    // Leaving early. The quest keeps the exercise available so it can be opened again.
    public override void Close()
    {
        bool wasRunning = running;
        running = false; closing = preparing = false;
        Hide();
        if (playerControls != null) playerControls.minigameOpen = false;
        if (wasRunning)
        {
            activeQuest?.NotifyMinigameClosed(this);
            onClosed?.Invoke();
        }
    }

    void Hide()
    {
        if (root != null) root.EnableInClassList("hidden", true);
    }

    void BeginClosing()
    {
        closing = true; closingLeft = closingSeconds;
        remaining = 0f;
        if (phaseLabel != null) phaseLabel.text = texts.closing;
        if (hintLabel != null) hintLabel.text = texts.closingHint;
        if (countLabel != null) countLabel.text = "";
        SetProgress(0f);
        if (timerLabel != null) timerLabel.text = "00:00";
        SetCircle(look.smallestScale, 0f);
        if (closingSeconds <= 0f) Complete();
    }

    void Complete()
    {
        running = false; closing = preparing = false;
        Hide();
        if (playerControls != null) playerControls.minigameOpen = false;
        activeQuest?.NotifyMinigameCompleted(this);
        onCompleted?.Invoke();
    }

    // ---------------------------------------------------------------- animation
    void UpdatePreparationUI()
    {
        SetBubbleVisible(false);
        if (readyCountLabel != null)
        {
            readyCountLabel.EnableInClassList("hidden", false);
            readyCountLabel.text = Mathf.CeilToInt(getReadyLeft).ToString();
        }
        if (phaseLabel != null) phaseLabel.text = texts.getReady;
        if (hintLabel != null) hintLabel.text = texts.getReadyHint;
        if (countLabel != null) countLabel.text = "";
        SetProgress(1f);
        if (timerLabel != null)
        {
            int seconds = Mathf.CeilToInt(remaining);
            timerLabel.text = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
        }
    }

    void UpdateUI()
    {
        SetBubbleVisible(true);
        if (readyCountLabel != null) readyCountLabel.EnableInClassList("hidden", true);
        float elapsed = Mathf.Max(0f, durationSeconds - remaining);
        float cycle = Mathf.Max(1f, inhaleSeconds + holdSeconds + exhaleSeconds);
        float t = elapsed % cycle;
        float scale, blend, phaseLeft;
        string phase, hint;

        if (t < inhaleSeconds)
        {
            float k = t / inhaleSeconds;
            phase = texts.inhale; hint = texts.inhaleHint;
            blend = Mathf.SmoothStep(0f, 1f, k); phaseLeft = inhaleSeconds - t;
        }
        else if (t < inhaleSeconds + holdSeconds)
        {
            phase = texts.hold; hint = texts.holdHint;
            blend = 1f; phaseLeft = inhaleSeconds + holdSeconds - t;
        }
        else
        {
            float k = (t - inhaleSeconds - holdSeconds) / exhaleSeconds;
            phase = texts.exhale; hint = texts.exhaleHint;
            blend = 1f - Mathf.SmoothStep(0f, 1f, k); phaseLeft = cycle - t;
        }
        scale = Mathf.Lerp(look.smallestScale, look.largestScale, blend);
        SetCircle(scale, blend);

        if (phaseLabel != null) phaseLabel.text = phase;
        if (hintLabel != null) hintLabel.text = hint;
        if (countLabel != null) countLabel.text = Mathf.Max(1, Mathf.CeilToInt(phaseLeft)).ToString();
        SetProgress(remaining / Mathf.Max(1f, durationSeconds));
        if (timerLabel != null)
        {
            int seconds = Mathf.CeilToInt(remaining);
            timerLabel.text = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
        }
    }

    void SetCircle(float scale, float blend)
    {
        if (circle == null) return;
        circle.style.scale = new Scale(new Vector2(scale, scale));
        circle.style.backgroundColor = Color.Lerp(look.circleSmall, look.circleLarge, blend);
    }

    void SetBubbleVisible(bool visible)
    {
        if (ring != null) ring.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (circle != null) circle.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    void SetProgress(float amount)
    {
        if (progressFill != null) progressFill.style.width = Length.Percent(Mathf.Clamp01(amount) * 100f);
    }

    // ---------------------------------------------------------------- screen
    void BuildUI()
    {
        var ui = EpisodeUI.Load();
        var document = EpisodeUI.Spawn("Breathing Exercise Screen", ui != null ? ui.breathing : null, sortingOrder, transform);
        if (document == null) { Debug.LogWarning("Breathing exercise: Breathing.uxml is missing from Resources/EpisodeUI.", this); return; }
        root = document.rootVisualElement.Q("breathing-root");
        circle = root.Q("breathing-circle"); ring = root.Q("breathing-ring"); core = root.Q("breathing-core");
        progressFill = root.Q("breathing-fill");
        titleLabel = root.Q<Label>("breathing-title"); phaseLabel = root.Q<Label>("breathing-phase"); hintLabel = root.Q<Label>("breathing-hint");
        countLabel = root.Q<Label>("breathing-count"); readyCountLabel = root.Q<Label>("breathing-ready-count");
        timerLabel = root.Q<Label>("breathing-timer");
        closeButton = root.Q<Button>("breathing-close");
        closeButton.clicked += Close;
        closeButton.focusable = false; // Space and Enter belong to the story, not to a focused button
        root.Q<Label>("breathing-eyebrow").text = texts.eyebrow;
        // The colours the Look settings expose; the circle's own colour and size follow the breath.
        ring.style.backgroundColor = look.ringColor;
        core.style.backgroundColor = look.coreColor;
        titleLabel.style.color = look.textColor; phaseLabel.style.color = look.textColor; countLabel.style.color = look.textColor;
        hintLabel.style.color = look.mutedColor; timerLabel.style.color = look.mutedColor;
    }
}
