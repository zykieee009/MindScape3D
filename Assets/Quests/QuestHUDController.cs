using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

// Quest Journal drawer (UI Toolkit): lists the active quests, their criteria and the episode timer, refreshed
// every refreshInterval seconds. Toggled with the Journal key / mobile button. One per scene via Instance.
[RequireComponent(typeof(UIDocument))]
public class QuestHUDController : MonoBehaviour
{
    public static QuestHUDController Instance { get; private set; }

    [SerializeField] float refreshInterval = 0.15f;
    [SerializeField] bool journalAvailable = true;

    public bool JournalAvailable => journalAvailable;

    UIDocument document;
    VisualElement drawer;
    VisualElement shortcut;
    ScrollView questList;
    Label questSummary;
    Label episodeTimer;
    Label episodeTimerFull;
    Button closeButton;
    VisualElement episodeModal;
    Label episodeModalEyebrow;
    Label episodeModalTitle;
    Label episodeModalMessage;
    Button episodeModalPrimary;
    Button episodeModalSecondary;
    PlayerControls playerControls;
    PlayerInputBindings inputBindings;
    float refreshTimer;

    public bool IsOpen { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        document = GetComponent<UIDocument>();
        playerControls = FindAnyObjectByType<PlayerControls>();
        if (playerControls != null)
            inputBindings = playerControls.GetComponent<PlayerInputBindings>();
    }

    void OnEnable()
    {
        InitializeUI();
    }

    void Start()
    {
        if (drawer == null) InitializeUI();
        Close();
        RefreshQuestList();
    }

    void OnDisable()
    {
        if (closeButton != null) closeButton.clicked -= Close;
        if (shortcut != null) shortcut.UnregisterCallback<ClickEvent>(OnShortcutClicked);
        if (playerControls != null) playerControls.questOpen = false;
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        bool togglePressed = inputBindings != null && inputBindings.OpenQuest != null
            ? inputBindings.OpenQuest.WasPressedThisFrame()
            : Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;

        if (journalAvailable && togglePressed && (playerControls == null || !playerControls.pauseOpen) && !NPCInteractionManager.DialogueActive)
            Toggle();

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = refreshInterval;
            RefreshQuestList();
        }
    }

    void InitializeUI()
    {
        if (document == null) document = GetComponent<UIDocument>();
        VisualElement root = document != null ? document.rootVisualElement : null;
        if (root == null) return;

        drawer = root.Q<VisualElement>("quest-drawer");
        shortcut = root.Q<VisualElement>("quest-shortcut");
        if (!journalAvailable && shortcut != null) shortcut.style.display = DisplayStyle.None;
        if (shortcut != null)
        {
            shortcut.UnregisterCallback<ClickEvent>(OnShortcutClicked);
            shortcut.RegisterCallback<ClickEvent>(OnShortcutClicked);
        }
        questList = root.Q<ScrollView>("quest-list");
        questSummary = root.Q<Label>("quest-summary");
        episodeTimer = root.Q<Label>("episode-timer");
        episodeTimerFull = root.Q<Label>("episode-timer-full");
        closeButton = root.Q<Button>("quest-close");
        episodeModal = root.Q<VisualElement>("episode-modal");
        episodeModalEyebrow = root.Q<Label>("episode-modal-eyebrow");
        episodeModalTitle = root.Q<Label>("episode-modal-title");
        episodeModalMessage = root.Q<Label>("episode-modal-message");
        episodeModalPrimary = root.Q<Button>("episode-modal-primary");
        episodeModalSecondary = root.Q<Button>("episode-modal-secondary");
        if (closeButton != null)
        {
            closeButton.clicked -= Close;
            closeButton.clicked += Close;
        }
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    void OnShortcutClicked(ClickEvent evt)
    {
        if (journalAvailable && !IsOpen && (playerControls == null || !playerControls.pauseOpen) && !NPCInteractionManager.DialogueActive)
            Open();
    }

    public void Open()
    {
        if (!journalAvailable) return;
        if (drawer == null) InitializeUI();
        if (drawer == null) return;

        if (playerControls == null) playerControls = FindAnyObjectByType<PlayerControls>();
        if (playerControls != null)
        {
            playerControls.CloseTasks();
            playerControls.questOpen = true;
        }

        IsOpen = true;
        drawer.RemoveFromClassList("is-hidden");
        shortcut?.AddToClassList("is-hidden");
        RefreshQuestList();
    }

    public void Close()
    {
        IsOpen = false;
        drawer?.AddToClassList("is-hidden");
        shortcut?.RemoveFromClassList("is-hidden");
        if (playerControls != null) playerControls.questOpen = false;
    }

    void RefreshQuestList()
    {
        if (questList == null) return;

        Quest[] sceneQuests = FindObjectsByType<Quest>(FindObjectsInactive.Exclude);
        List<Quest> visibleQuests = new List<Quest>();
        for (int i = 0; i < sceneQuests.Length; i++)
            if (sceneQuests[i].IsActive || sceneQuests[i].IsCompleted)
                visibleQuests.Add(sceneQuests[i]);

        int completedCount = 0;
        for (int i = 0; i < visibleQuests.Count; i++)
            if (visibleQuests[i].IsCompleted) completedCount++;

        if (questSummary != null)
            questSummary.text = visibleQuests.Count == 0
                ? "No active quests"
                : completedCount + " of " + visibleQuests.Count + " completed";

        Quest timedQuest = null;
        for (int i = 0; i < sceneQuests.Length; i++)
            if (sceneQuests[i].IsActive && sceneQuests[i].EpisodeDuration > 0f) { timedQuest = sceneQuests[i]; break; }
        if (episodeTimer != null || episodeTimerFull != null)
        {
            string timeText = timedQuest == null ? string.Empty : "TIME  " + timedQuest.RemainingEpisodeTimeText;
            bool warning = timedQuest != null && timedQuest.RemainingEpisodeTime <= 10f;
            if (episodeTimer != null)
            {
                episodeTimer.text = timeText;
                episodeTimer.EnableInClassList("is-warning", warning);
            }
            if (episodeTimerFull != null)
            {
                episodeTimerFull.text = timeText;
                episodeTimerFull.EnableInClassList("is-warning", warning);
            }
        }

        questList.Clear();
        if (visibleQuests.Count == 0)
        {
            Label empty = new Label("Your journal is quiet. New objectives will appear here.");
            empty.AddToClassList("empty-state");
            questList.Add(empty);
            return;
        }

        for (int i = 0; i < visibleQuests.Count; i++)
            questList.Add(CreateQuestBlock(visibleQuests[i]));
    }

    VisualElement CreateQuestBlock(Quest quest)
    {
        VisualElement block = new VisualElement();
        block.AddToClassList("quest-block");

        VisualElement header = new VisualElement();
        header.AddToClassList("quest-block-header");
        Label title = new Label(string.IsNullOrWhiteSpace(quest.questName) ? "Untitled Quest" : quest.questName);
        title.AddToClassList("quest-name");
        Label status = new Label(quest.IsCompleted ? "COMPLETE" : quest.HasCriteria ? "ACTIVE" : "NOT CONFIGURED");
        status.AddToClassList("quest-status");
        if (quest.IsCompleted) status.AddToClassList("complete");
        header.Add(title);
        header.Add(status);
        block.Add(header);

        if (!string.IsNullOrWhiteSpace(quest.description))
        {
            Label description = new Label(quest.description);
            description.AddToClassList("quest-description");
            block.Add(description);
        }

        int criterionCount = 0;
        for (int i = 0; i < quest.requiredCollectibles.Count; i++)
        {
            QuestCollectibleRequirement requirement = quest.requiredCollectibles[i];
            if (requirement == null || string.IsNullOrEmpty(requirement.RequiredId)) continue;
            int current = quest.GetCollectibleProgress(requirement);
            int required = Mathf.Max(1, requirement.requiredQuantity);
            bool done = quest.IsCompleted || current >= required;
            string itemName = string.IsNullOrWhiteSpace(requirement.DisplayName) ? requirement.RequiredId : requirement.DisplayName;
            block.Add(CreateCriterionRow("ITEM", itemName, current + " / " + required, done));
            criterionCount++;
        }

        if (quest.requireStressRange)
        {
            float minimum = Mathf.Min(quest.minimumStress, quest.maximumStress);
            float maximum = Mathf.Max(quest.minimumStress, quest.maximumStress);
            float current = playerControls != null ? playerControls.stressLevel : 0f;
            bool done = quest.IsCompleted || quest.IsStressAccepted();
            block.Add(CreateCriterionRow(
                "STRESS",
                "Stay within the accepted range",
                Mathf.RoundToInt(current) + "  /  " + Mathf.RoundToInt(minimum) + "–" + Mathf.RoundToInt(maximum),
                done));
            criterionCount++;
        }

        for (int i = 0; i < quest.requiredNPCNames.Count; i++)
        {
            string npcName = quest.requiredNPCNames[i];
            if (string.IsNullOrWhiteSpace(npcName)) continue;
            bool done = quest.IsCompleted || quest.HasTalkedToNPC(npcName);
            block.Add(CreateCriterionRow("PERSON", "Talk to " + npcName.Trim(), done ? "DONE" : "PENDING", done));
            criterionCount++;
        }

        for (int i = 0; i < quest.requiredMinigames.Count; i++)
        {
            QuestMinigameRequirement requirement = quest.requiredMinigames[i];
            if (requirement == null || !requirement.IsConfigured) continue;
            string minigameName = requirement.minigame.QuestDisplayName;
            block.Add(CreateCriterionRow("MINIGAME", minigameName, quest.IsMinigameCompleted(i) ? "DONE" : "PENDING", quest.IsMinigameCompleted(i)));
            criterionCount++;
        }

        if (criterionCount == 0)
        {
            Label empty = new Label("Configure criteria on this Quest component in the Inspector.");
            empty.AddToClassList("empty-state");
            block.Add(empty);
        }

        return block;
    }

    VisualElement CreateCriterionRow(string type, string label, string progress, bool complete)
    {
        VisualElement row = new VisualElement();
        row.AddToClassList("criterion-row");
        if (complete) row.AddToClassList("complete");

        Label mark = new Label(complete ? "✓" : string.Empty);
        mark.AddToClassList("criterion-mark");

        VisualElement copy = new VisualElement();
        copy.AddToClassList("criterion-copy");
        Label typeLabel = new Label(type);
        typeLabel.AddToClassList("criterion-type");
        Label mainLabel = new Label(label);
        mainLabel.AddToClassList("criterion-label");
        copy.Add(typeLabel);
        copy.Add(mainLabel);

        Label progressLabel = new Label(progress);
        progressLabel.AddToClassList("criterion-progress");

        row.Add(mark);
        row.Add(copy);
        row.Add(progressLabel);
        return row;
    }

    public void ShowEpisodeComplete(Quest quest)
    {
        ShowEpisodeModal(
            "EPISODE COMPLETE",
            "ROUND CLEARED",
            "Every requirement was satisfied in time. Chapter 2 is now unlocked.",
            "PLAY NEXT CHAPTER",
            "RETURN TO MENU",
            GoToNextChapter,
            GoToMainMenu);
    }

    public void ShowGameOver(Quest quest)
    {
        ShowEpisodeModal(
            "TIME UP",
            "GAME OVER",
            "The episode timer reached 00:00 before every requirement was complete.",
            "RETRY EPISODE",
            "RETURN TO MENU",
            RetryEpisode,
            GoToMainMenu);
    }

    public void ShowStressGameOver()
    {
        ShowEpisodeModal(
            "STRESS CRITICAL",
            "GAME OVER",
            "Stress stayed at maximum for 10 seconds. Take a breath and try again.",
            "RETRY EPISODE",
            "RETURN TO MENU",
            RetryEpisode,
            GoToMainMenu);
    }

    void ShowEpisodeModal(string eyebrow, string title, string message, string primary, string secondary,
        System.Action primaryAction, System.Action secondaryAction)
    {
        if (episodeModal == null) return;

        // Game-over and completion are terminal states for the current run:
        // dismiss every gameplay overlay before showing the result modal.
        FindAnyObjectByType<NPCInteractionManager>()?.ForceCloseDialogue();
        foreach (QuestMinigame openMinigame in FindObjectsByType<QuestMinigame>(FindObjectsInactive.Exclude))
            if (openMinigame.IsOpen) openMinigame.Close();
        PauseMenu.Instance?.SetVisible(false);
        FindAnyObjectByType<MobileControls>()?.HideForGameOver();
        FindAnyObjectByType<StressLevelUIController>()?.HideForGameOver();

        drawer?.AddToClassList("is-hidden");
        shortcut?.AddToClassList("is-hidden");
        if (playerControls != null)
        {
            playerControls.tasksOpen = false;
            playerControls.questOpen = false;
            playerControls.dialogueOpen = false;
            playerControls.minigameOpen = false;
        }

        if (episodeModalPrimary != null)
        {
            episodeModalPrimary.clicked -= RetryEpisode;
            episodeModalPrimary.clicked -= GoToNextChapter;
            episodeModalPrimary.clicked += () => primaryAction();
            episodeModalPrimary.text = primary;
        }
        if (episodeModalSecondary != null)
        {
            episodeModalSecondary.clicked -= GoToMainMenu;
            episodeModalSecondary.clicked += () => secondaryAction();
            episodeModalSecondary.text = secondary;
        }
        if (episodeModalEyebrow != null) episodeModalEyebrow.text = eyebrow;
        if (episodeModalTitle != null) episodeModalTitle.text = title;
        if (episodeModalMessage != null) episodeModalMessage.text = message;
        episodeModal.RemoveFromClassList("is-hidden");
        if (playerControls != null) playerControls.pauseOpen = true;
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;
        Time.timeScale = 0f;
    }

    void RetryEpisode()
    {
        Time.timeScale = 1f;
        EpisodeLoadingScreen.Load(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
            Mathf.Max(1, PlayerPrefs.GetInt(Mindscape.Data.MindscapeProgress.EpisodeKey, 2) - 1));
    }

    void GoToNextChapter()
    {
        PlayerPrefs.SetInt(Mindscape.Data.MindscapeProgress.ChapterUnlockedKey, 2);
        PlayerPrefs.SetInt("Mindscape.StartPage", 2);
        PlayerPrefs.SetInt("Mindscape.SelectedChapter", 1);
        PlayerPrefs.Save();
        Time.timeScale = 1f;
        MusicManager.LoadSceneFaded("main_menu_new");
    }

    void GoToMainMenu()
    {
        PlayerPrefs.SetInt("Mindscape.StartPage", 0);
        PlayerPrefs.Save();
        Time.timeScale = 1f;
        MusicManager.LoadSceneFaded("main_menu_new");
    }
}
