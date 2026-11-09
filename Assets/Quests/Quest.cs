using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Quest = the win/lose condition of an episode. Criteria (collectibles, stress range, NPCs talked to, minigames
// finished) are checked every frame; the quest succeeds when all are met, or fails when its timer runs out.
// Add one with GameObject > Quest and fill the lists in the Inspector. See docs: Create a Quest.
[Serializable]
public class QuestCollectibleRequirement
{
    [Tooltip("Drag a Collectible from the scene or a collectible prefab here.")]
    public CollectibleItem collectible;

    [Tooltip("Optional stable ID used when no Collectible reference is assigned.")]
    public string collectibleId;

    [Min(1)] public int requiredQuantity = 1;

    public string RequiredId
    {
        get
        {
            if (collectible != null && !string.IsNullOrWhiteSpace(collectible.itemId))
                return collectible.itemId.Trim();
            return string.IsNullOrWhiteSpace(collectibleId) ? string.Empty : collectibleId.Trim();
        }
    }

    public string DisplayName => collectible != null ? collectible.DisplayName : RequiredId;
}

public static class QuestProgress
{
    public static event Action<string> NPCConversationCompleted;

    public static void ReportNPCConversationCompleted(string npcName)
    {
        if (!string.IsNullOrWhiteSpace(npcName))
            NPCConversationCompleted?.Invoke(npcName.Trim());
    }
}

public enum StressMinigameTrigger
{
    AtOrAbove,
    AtOrBelow
}

public enum QuestMinigameTrigger
{
    OnQuestStart,
    AtTimer,
    WhenStress,
    WhenTalkToNPC,
    Manual
}

[Serializable]
public class QuestMinigameRequirement
{
    [Tooltip("The minigame that must be completed.")]
    public QuestMinigame minigame;
    public QuestMinigameTrigger trigger = QuestMinigameTrigger.Manual;

    [Header("Timer Trigger")]
    [Min(0)] public int triggerMinutes;
    [Range(0, 59)] public int triggerSeconds;

    [Header("Stress Trigger")]
    public StressMinigameTrigger stressTrigger = StressMinigameTrigger.AtOrAbove;
    [Min(0f)] public float stressThreshold = 70f;

    [Header("NPC Trigger")]
    public string npcName;

    [SerializeField, HideInInspector] public bool completed;
    [SerializeField, HideInInspector] public bool triggered;

    public float TriggerTimeSeconds => Mathf.Max(0f, triggerMinutes * 60f + triggerSeconds);
    public bool IsConfigured => minigame != null;
}

[AddComponentMenu("Mindscape/Quest")]
public class Quest : MonoBehaviour
{
    [Header("Quest")]
    public string questId = "quest-id";
    public string questName = "New Quest";
    [TextArea(2, 5)] public string description;
    public bool activeOnStart = true;

    [Header("Required Collectibles")]
    public List<QuestCollectibleRequirement> requiredCollectibles = new List<QuestCollectibleRequirement>();

    [Header("Accepted Stress Range")]
    public bool requireStressRange;
    [Min(0f)] public float minimumStress;
    [Min(0f)] public float maximumStress = 100f;

    [Header("NPC Tracker")]
    [Tooltip("NPC names the player must finish talking to. Names are matched without case sensitivity.")]
    public List<string> requiredNPCNames = new List<string>();

    [Header("Minigames")]
    [Tooltip("Every configured minigame in this list is required for the quest to complete.")]
    public List<QuestMinigameRequirement> requiredMinigames = new List<QuestMinigameRequirement>();

    // Kept hidden for scene migration from the original single breathing-exercise setup.
    [SerializeField, HideInInspector] BreathingMinigame breathingMinigame;
    [SerializeField, HideInInspector] bool requireBreathingExercise;
    [SerializeField, HideInInspector] bool triggerBreathingOnStress;
    [SerializeField, HideInInspector] StressMinigameTrigger breathingStressTrigger = StressMinigameTrigger.AtOrAbove;
    [SerializeField, HideInInspector] float breathingStressThreshold = 70f;

    [Header("Episode Timer")]
    [Min(0)] public int episodeDurationMinutes = 5;
    [Range(0, 59)] public int episodeDurationSeconds;

    [Header("Completion")]
    public UnityEvent onQuestCompleted;
    public UnityEvent onEpisodeFailed;

    [SerializeField, HideInInspector] bool questActive;
    [SerializeField, HideInInspector] bool completed;
    [SerializeField, HideInInspector] List<string> talkedToNPCNames = new List<string>();
    PlayerControls playerControls;
    float remainingEpisodeTime;
    bool episodeEnded;

    public bool IsActive => questActive;
    public bool IsCompleted => completed;
    public bool IsEpisodeFailed => episodeEnded && !completed;
    public float EpisodeDuration => Mathf.Max(0f, episodeDurationMinutes * 60f + episodeDurationSeconds);
    public float RemainingEpisodeTime => Mathf.Max(0f, remainingEpisodeTime);
    public string RemainingEpisodeTimeText
    {
        get
        {
            int totalSeconds = Mathf.CeilToInt(RemainingEpisodeTime);
            return (totalSeconds / 60).ToString("00") + ":" + (totalSeconds % 60).ToString("00");
        }
    }
    public IReadOnlyList<string> TalkedToNPCNames => talkedToNPCNames;
    public bool HasCriteria
    {
        get
        {
            if (requireStressRange) return true;

            for (int i = 0; i < requiredCollectibles.Count; i++)
                if (requiredCollectibles[i] != null && !string.IsNullOrEmpty(requiredCollectibles[i].RequiredId))
                    return true;

            for (int i = 0; i < requiredNPCNames.Count; i++)
                if (!string.IsNullOrWhiteSpace(requiredNPCNames[i]))
                    return true;

            for (int i = 0; i < requiredMinigames.Count; i++)
                if (requiredMinigames[i] != null && requiredMinigames[i].IsConfigured)
                    return true;

            return false;
        }
    }

    void Awake()
    {
        playerControls = FindAnyObjectByType<PlayerControls>();
        MigrateLegacyBreathingExercise();
        questActive = activeOnStart;
        ResetEpisodeTimer();
    }

    void OnValidate()
    {
        if (!Application.isPlaying)
            MigrateLegacyBreathingExercise();
    }

    void OnEnable()
    {
        QuestProgress.NPCConversationCompleted += RecordNPCConversation;
    }

    void OnDisable()
    {
        QuestProgress.NPCConversationCompleted -= RecordNPCConversation;
    }

    void Update()
    {
        if (EpisodeLoadingScreen.IsActive) return;
        if (questActive && !completed)
        {
            CheckMinigameTriggers();
            EvaluateNow();

            bool minigameRunning = playerControls != null && playerControls.minigameOpen;
            if (!completed && EpisodeDuration > 0f && !minigameRunning)
            {
                remainingEpisodeTime -= Time.unscaledDeltaTime;
                if (remainingEpisodeTime <= 0f)
                    FailEpisode();
            }
        }
    }

    public void ActivateQuest()
    {
        questActive = true;
        if (!episodeEnded) ResetEpisodeTimer();
        EvaluateNow();
    }

    public void DeactivateQuest()
    {
        questActive = false;
    }

    public void ResetQuest()
    {
        completed = false;
        episodeEnded = false;
        for (int i = 0; i < requiredMinigames.Count; i++)
        {
            if (requiredMinigames[i] == null) continue;
            requiredMinigames[i].completed = false;
            requiredMinigames[i].triggered = false;
        }
        questActive = activeOnStart;
        talkedToNPCNames.Clear();
        ResetEpisodeTimer();
    }

    public bool EvaluateNow()
    {
        if (!questActive || completed || !HasCriteria)
            return completed;

        if (!AreCollectiblesComplete() || !IsStressAccepted() || !AreNPCsComplete() || !AreMinigamesComplete())
            return false;

        completed = true;
        episodeEnded = true;
        PlayerPrefs.SetInt(Mindscape.Data.MindscapeProgress.ChapterUnlockedKey, Mathf.Max(2, PlayerPrefs.GetInt(Mindscape.Data.MindscapeProgress.ChapterUnlockedKey, 1)));
        PlayerPrefs.Save();
        onQuestCompleted?.Invoke();
        Time.timeScale = 0f;
        QuestHUDController.Instance?.ShowEpisodeComplete(this);
        return true;
    }

    void MigrateLegacyBreathingExercise()
    {
        if (breathingMinigame == null || requiredMinigames.Count > 0 ||
            (!requireBreathingExercise && !triggerBreathingOnStress)) return;
        QuestMinigameRequirement legacy = new QuestMinigameRequirement
        {
            minigame = breathingMinigame,
            trigger = triggerBreathingOnStress ? QuestMinigameTrigger.WhenStress : QuestMinigameTrigger.Manual,
            stressTrigger = breathingStressTrigger,
            stressThreshold = breathingStressThreshold
        };
        requiredMinigames.Add(legacy);
        requireBreathingExercise = false;
    }

    void CheckMinigameTriggers()
    {
        for (int i = 0; i < requiredMinigames.Count; i++)
        {
            QuestMinigameRequirement requirement = requiredMinigames[i];
            if (requirement == null || !requirement.IsConfigured || requirement.triggered || requirement.completed)
                continue;

            bool shouldTrigger = false;
            switch (requirement.trigger)
            {
                case QuestMinigameTrigger.OnQuestStart:
                    shouldTrigger = true;
                    break;
                case QuestMinigameTrigger.AtTimer:
                    shouldTrigger = EpisodeDuration <= 0f
                        ? false
                        : EpisodeDuration - remainingEpisodeTime >= requirement.TriggerTimeSeconds;
                    break;
                case QuestMinigameTrigger.WhenStress:
                    if (playerControls == null) playerControls = FindAnyObjectByType<PlayerControls>();
                    if (playerControls != null)
                        shouldTrigger = requirement.stressTrigger == StressMinigameTrigger.AtOrAbove
                            ? playerControls.stressLevel >= requirement.stressThreshold
                            : playerControls.stressLevel <= requirement.stressThreshold;
                    break;
            }

            if (shouldTrigger) TriggerMinigame(requirement);
        }
    }

    void TriggerMinigame(QuestMinigameRequirement requirement)
    {
        requirement.triggered = true;
        requirement.minigame.Open(this);
    }

    public void TriggerMinigame(int index)
    {
        if (index < 0 || index >= requiredMinigames.Count) return;
        QuestMinigameRequirement requirement = requiredMinigames[index];
        if (requirement == null || !requirement.IsConfigured || requirement.completed) return;
        TriggerMinigame(requirement);
    }

    public void NotifyMinigameCompleted(QuestMinigame minigame)
    {
        for (int i = 0; i < requiredMinigames.Count; i++)
        {
            QuestMinigameRequirement requirement = requiredMinigames[i];
            if (requirement != null && requirement.minigame == minigame)
                requirement.completed = true;
        }
        EvaluateNow();
    }

    // The player left before finishing: let the same trigger open it again (for example, talking to the NPC once more).
    public void NotifyMinigameClosed(QuestMinigame minigame)
    {
        for (int i = 0; i < requiredMinigames.Count; i++)
        {
            QuestMinigameRequirement requirement = requiredMinigames[i];
            if (requirement != null && requirement.minigame == minigame && !requirement.completed)
                requirement.triggered = false;
        }
    }

    public bool AreMinigamesComplete()
    {
        for (int i = 0; i < requiredMinigames.Count; i++)
        {
            QuestMinigameRequirement requirement = requiredMinigames[i];
            if (requirement != null && requirement.IsConfigured && !requirement.completed)
                return false;
        }
        return true;
    }

    public bool IsMinigameCompleted(int index)
    {
        return index >= 0 && index < requiredMinigames.Count && requiredMinigames[index] != null && requiredMinigames[index].completed;
    }


    void ResetEpisodeTimer()
    {
        remainingEpisodeTime = EpisodeDuration;
    }

    void FailEpisode()
    {
        if (episodeEnded || completed) return;
        episodeEnded = true;
        questActive = false;
        onEpisodeFailed?.Invoke();
        Time.timeScale = 0f;
        QuestHUDController.Instance?.ShowGameOver(this);
    }

    public bool AreCollectiblesComplete()
    {
        if (requiredCollectibles.Count == 0) return true;
        CollectiblesManager collectibles = FindAnyObjectByType<CollectiblesManager>();
        if (collectibles == null) return false;

        for (int i = 0; i < requiredCollectibles.Count; i++)
        {
            QuestCollectibleRequirement requirement = requiredCollectibles[i];
            if (requirement == null || string.IsNullOrEmpty(requirement.RequiredId)) return false;
            if (collectibles.GetItemCount(requirement.RequiredId) < Mathf.Max(1, requirement.requiredQuantity)) return false;
        }

        return true;
    }

    public bool IsStressAccepted()
    {
        if (!requireStressRange) return true;
        if (playerControls == null) playerControls = FindAnyObjectByType<PlayerControls>();
        if (playerControls == null) return false;

        float minimum = Mathf.Min(minimumStress, maximumStress);
        float maximum = Mathf.Max(minimumStress, maximumStress);
        return playerControls.stressLevel >= minimum && playerControls.stressLevel <= maximum;
    }

    public bool AreNPCsComplete()
    {
        for (int i = 0; i < requiredNPCNames.Count; i++)
        {
            string requiredName = requiredNPCNames[i];
            if (string.IsNullOrWhiteSpace(requiredName)) continue;

            bool found = false;
            for (int talkedIndex = 0; talkedIndex < talkedToNPCNames.Count; talkedIndex++)
            {
                if (string.Equals(requiredName.Trim(), talkedToNPCNames[talkedIndex], StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }

            if (!found) return false;
        }

        return true;
    }

    public int GetCollectibleProgress(QuestCollectibleRequirement requirement)
    {
        CollectiblesManager collectibles = FindAnyObjectByType<CollectiblesManager>();
        return requirement == null || collectibles == null ? 0 : collectibles.GetItemCount(requirement.RequiredId);
    }

    public bool HasTalkedToNPC(string npcName)
    {
        if (string.IsNullOrWhiteSpace(npcName)) return false;
        for (int i = 0; i < talkedToNPCNames.Count; i++)
            if (string.Equals(npcName.Trim(), talkedToNPCNames[i], StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    void RecordNPCConversation(string npcName)
    {
        if (!questActive || completed) return;
        if (HasTalkedToNPC(npcName)) { CheckMinigameTriggersForNPC(npcName); return; }
        talkedToNPCNames.Add(npcName.Trim());
        CheckMinigameTriggersForNPC(npcName);
        EvaluateNow();
    }

    void CheckMinigameTriggersForNPC(string npcName)
    {
        for (int i = 0; i < requiredMinigames.Count; i++)
        {
            QuestMinigameRequirement requirement = requiredMinigames[i];
            if (requirement == null || !requirement.IsConfigured || requirement.triggered || requirement.completed ||
                requirement.trigger != QuestMinigameTrigger.WhenTalkToNPC ||
                string.IsNullOrWhiteSpace(requirement.npcName)) continue;

            if (string.Equals(requirement.npcName.Trim(), npcName.Trim(), StringComparison.OrdinalIgnoreCase))
                TriggerMinigame(requirement);
        }
    }
}
