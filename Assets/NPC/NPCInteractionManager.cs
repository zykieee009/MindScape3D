using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UIE = UnityEngine.UIElements;
using static UnityEngine.UIElements.UQueryExtensions;

// One per scene. Picks the nearby NPC, shows the talk prompt, runs the dialogue UI (shared EpisodeUI template
// unless dialogueOverride is set) and blends the camera. DialogueActive / HasNearbyNPC are read by other systems
// to hide their own prompts while a conversation is on.
[DefaultExecutionOrder(-20)]
public class NPCInteractionManager : MonoBehaviour
{
    public static bool DialogueActive { get; private set; }
    public static bool HasNearbyNPC { get; private set; }

    [SerializeField] Transform player;
    [SerializeField] Camera dialogueCamera;
    [SerializeField] float cameraBlendDuration = 0.45f;
    [Tooltip("Optional scene-specific NPC dialogue UI. Empty keeps the shared EpisodeUI template.")]
    [SerializeField] UIE.VisualTreeAsset dialogueOverride;

    readonly List<NPC> npcs = new List<NPC>();
    PlayerControls playerControls;
    PlayerMovement playerMovement;
    NPC nearbyNPC;
    NPC activeNPC;
    UIE.VisualElement promptBox;
    UIE.Label promptText;
    // The conversation panel is UI Toolkit: Assets/UI Toolkit/NPCDialogue.uxml (+ .uss), found through Resources/EpisodeUI.
    UIE.VisualElement dialoguePanel;
    UIE.Label speakerText;
    UIE.Label dialogueText;
    UIE.VisualElement choicesRoot;
    readonly List<System.Action> replyActions = new List<System.Action>();
    bool redesignedDialogue;
    float repliesShownAt;
    bool panelOpen;
    Vector3 originalCameraPosition;
    Quaternion originalCameraRotation;
    Quaternion originalNPCRotation;
    bool cameraStored;
    bool endingDialogue;
    Coroutine cameraRoutine;
    Coroutine conversationRoutine;

    void Awake()
    {
        if (player == null)
        {
            playerControls = FindAnyObjectByType<PlayerControls>();
            if (playerControls != null) player = playerControls.transform;
        }
        else
        {
            playerControls = player.GetComponent<PlayerControls>();
        }

        if (dialogueCamera == null) dialogueCamera = Camera.main;
        CacheNPCs();
        CreateUI();
    }

    void OnDestroy()
    {
        DialogueActive = false;
        HasNearbyNPC = false;
    }

    void Update()
    {
        RefreshPanel();
        if (DialogueActive)
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                EndDialogue(false);
            else if (redesignedDialogue && panelOpen && !Paused && Keyboard.current != null)
                for (int i = 0; i < replyActions.Count && i < 9; i++)
                    if (Keyboard.current[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame)
                    {
                        SelectReply(i);
                        break; // A branch can rebuild the replies: never select twice in one frame.
                    }
            return;
        }

        // Also stay quiet while a story scene has the player locked (cutscene, story dialogue, pause): starting a
        // conversation then would fight the story for the camera and the dialogue flag.
        bool storyLocked = playerControls != null && (playerControls.dialogueOpen || !PlayerFree());
        if (Paused || (playerControls != null && (playerControls.questOpen || playerControls.minigameOpen || storyLocked)))
        {
            nearbyNPC = null;
            HasNearbyNPC = false;
            SetPrompt(false);
            return;
        }

        // Talking uses [ E ] like doors and story spots; whichever is nearest gets the key and the only prompt (InteractFocus).
        nearbyNPC = FindNearestNPC();
        if (nearbyNPC != null)
        {
            InteractFocus.Offer(nearbyNPC, Vector3.Distance(player.position, nearbyNPC.transform.position));
            if (!InteractFocus.Has(nearbyNPC)) nearbyNPC = null;
        }
        HasNearbyNPC = nearbyNPC != null;

        if (promptText != null)
        {
            SetPrompt(HasNearbyNPC);
            if (HasNearbyNPC) promptText.text = "[ E ]  Talk to " + nearbyNPC.npcName;
        }

        bool interactPressed = (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) || MobileInput.InteractPressed;
        if (nearbyNPC != null && interactPressed)
            BeginDialogue(nearbyNPC);
    }

    bool PlayerFree()
    {
        if (playerMovement == null && player != null) playerMovement = player.GetComponent<PlayerMovement>();
        return playerMovement == null || (playerMovement.enabled && playerMovement.gameObject.activeInHierarchy);
    }

    void CacheNPCs()
    {
        npcs.Clear();
        npcs.AddRange(FindObjectsByType<NPC>(FindObjectsInactive.Exclude));
    }

    NPC FindNearestNPC()
    {
        NPC closest = null;
        float closestDistance = float.MaxValue;
        for (int i = npcs.Count - 1; i >= 0; i--)
        {
            NPC npc = npcs[i];
            if (npc == null)
            {
                npcs.RemoveAt(i);
                continue;
            }
            if (!npc.IsInRange(player)) continue;
            float distance = Vector3.Distance(player.position, npc.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = npc;
            }
        }
        return closest;
    }

    public void BeginDialogue(NPC npc)
    {
        if (DialogueActive || npc == null || npc.dialogueNodes.Count == 0 || dialoguePanel == null) return;
        activeNPC = npc;
        originalNPCRotation = npc.transform.rotation;
        endingDialogue = false;
        DialogueActive = true;
        HasNearbyNPC = false;
        SetPrompt(false);
        if (playerControls != null)
        {
            QuestHUDController.Instance?.Close();
            playerControls.CloseTasks();
            playerControls.dialogueOpen = true;
        }

        ShowPanel(false);
        if (dialogueCamera != null)
        {
            originalCameraPosition = dialogueCamera.transform.position;
            originalCameraRotation = dialogueCamera.transform.rotation;
            cameraStored = true;
        }

        if (conversationRoutine != null) StopCoroutine(conversationRoutine);
        conversationRoutine = StartCoroutine(TurnTowardPlayerThenTalk(npc));
    }

    IEnumerator TurnTowardPlayerThenTalk(NPC npc)
    {
        Vector3 direction = player != null ? player.position - npc.transform.position : npc.transform.forward;
        direction.y = 0f;
        Quaternion targetRotation = npc.turnToTalk && direction.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(direction.normalized, Vector3.up)
            : npc.transform.rotation;

        yield return RotateNPC(npc.transform, targetRotation, npc.turnDuration);
        if (!DialogueActive || endingDialogue || activeNPC != npc) yield break;

        ShowPanel(true);
        FocusCamera(npc);
        ShowDialogueNode(0);
    }

    void ShowDialogueNode(int index)
    {
        if (activeNPC == null || index < 0 || index >= activeNPC.dialogueNodes.Count)
        {
            EndDialogue();
            return;
        }

        NPCDialogueNode node = activeNPC.dialogueNodes[index];
        speakerText.text = activeNPC.npcName.ToUpperInvariant();
        dialogueText.text = Mindscape.Data.MindscapeDatabase.Personalize(node.npcDialogue);
        ClearChoiceButtons();

        repliesShownAt = Time.unscaledTime;
        var scroll = dialoguePanel.Q<UIE.ScrollView>("npc-scroll");
        if (scroll != null) scroll.scrollOffset = Vector2.zero;

        if (activeNPC.enableDialogueChaining && node.playerChoices.Count > 0)
        {
            for (int i = 0; i < node.playerChoices.Count; i++)
            {
                NPCDialogueChoice selectedChoice = node.playerChoices[i];
                string stressSuffix = Mathf.Approximately(selectedChoice.stressChange, 0f)
                    ? string.Empty
                    : $"  ({selectedChoice.stressChange:+0.##;-0.##} stress)";
                CreateChoiceButton(Mindscape.Data.MindscapeDatabase.Personalize(selectedChoice.playerChoice) + stressSuffix, () =>
                {
                    if (playerControls != null)
                        playerControls.ChangeStress(selectedChoice.stressChange);

                    if (selectedChoice.endsDialogue || selectedChoice.nextDialogueIndex < 0) EndDialogue();
                    else ShowDialogueNode(selectedChoice.nextDialogueIndex);
                });
            }
        }
        else
        {
            int nextIndex = index + 1;
            bool canContinue = activeNPC.enableDialogueChaining && nextIndex < activeNPC.dialogueNodes.Count;
            CreateChoiceButton(canContinue ? "Continue" : "Goodbye", () =>
            {
                if (canContinue) ShowDialogueNode(nextIndex);
                else EndDialogue();
            });
        }
    }

    void FocusCamera(NPC npc)
    {
        if (dialogueCamera == null) return;
        Vector3 face = npc.FacePosition;
        Vector3 targetPosition = face + npc.transform.forward * npc.cameraDistance + Vector3.up * npc.cameraHeightOffset;
        Quaternion targetRotation = Quaternion.LookRotation(face - targetPosition, Vector3.up);
        ShotFocus.Set(face);   // your own character steps out of the shot while you talk
        if (cameraRoutine != null) StopCoroutine(cameraRoutine);
        cameraRoutine = StartCoroutine(BlendCamera(targetPosition, targetRotation));
    }

    IEnumerator BlendCamera(Vector3 targetPosition, Quaternion targetRotation)
    {
        if (dialogueCamera == null) yield break;
        Vector3 startPosition = dialogueCamera.transform.position;
        Quaternion startRotation = dialogueCamera.transform.rotation;
        float elapsed = 0f;
        while (elapsed < cameraBlendDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / cameraBlendDuration));
            dialogueCamera.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            dialogueCamera.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }
        dialogueCamera.transform.SetPositionAndRotation(targetPosition, targetRotation);
    }

    public void EndDialogue(bool completedConversation = true)
    {
        if (!DialogueActive || endingDialogue) return;
        endingDialogue = true;
        ShowPanel(false);
        ClearChoiceButtons();
        if (conversationRoutine != null) StopCoroutine(conversationRoutine);
        if (cameraRoutine != null) StopCoroutine(cameraRoutine);
        if (dialogueCamera != null && cameraStored)
            dialogueCamera.transform.SetPositionAndRotation(originalCameraPosition, originalCameraRotation);
        ShotFocus.Clear();

        conversationRoutine = StartCoroutine(FinishDialogue(activeNPC, completedConversation));
    }

    public void ForceCloseDialogue()
    {
        ShowPanel(false);
        ClearChoiceButtons();
        if (conversationRoutine != null) StopCoroutine(conversationRoutine);
        if (cameraRoutine != null) StopCoroutine(cameraRoutine);
        if (dialogueCamera != null && cameraStored)
            dialogueCamera.transform.SetPositionAndRotation(originalCameraPosition, originalCameraRotation);
        ShotFocus.Clear();
        if (activeNPC != null)
            activeNPC.transform.rotation = originalNPCRotation;

        DialogueActive = false;
        HasNearbyNPC = false;
        cameraStored = false;
        endingDialogue = false;
        if (playerControls != null) playerControls.dialogueOpen = false;
        activeNPC = null;
        conversationRoutine = null;
        cameraRoutine = null;
    }

    IEnumerator FinishDialogue(NPC npc, bool completedConversation)
    {
        if (npc != null)
            yield return RotateNPC(npc.transform, originalNPCRotation, npc.turnDuration);

        DialogueActive = false;
        HasNearbyNPC = false;
        cameraStored = false;
        endingDialogue = false;
        if (playerControls != null) playerControls.dialogueOpen = false;
        if (completedConversation && npc != null)
            QuestProgress.ReportNPCConversationCompleted(npc.npcName);
        activeNPC = null;
        conversationRoutine = null;
    }

    IEnumerator RotateNPC(Transform npcTransform, Quaternion targetRotation, float duration)
    {
        if (npcTransform == null) yield break;
        Quaternion startRotation = npcTransform.rotation;
        if (duration <= 0f)
        {
            npcTransform.rotation = targetRotation;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration && npcTransform != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            npcTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        if (npcTransform != null) npcTransform.rotation = targetRotation;
    }

    void SetPrompt(bool visible)
    {
        if (promptBox != null) promptBox.EnableInClassList("hidden", !visible);
    }

    void ShowPanel(bool open) { panelOpen = open; RefreshPanel(); }

    // The panel steps aside while a pause menu is open: the Tutorial's pause menu is on a lower UI Toolkit panel
    // and would otherwise sit under it (in the story episodes the pause menu is above it anyway).
    void RefreshPanel()
    {
        if (dialoguePanel == null) return;
        dialoguePanel.EnableInClassList("hidden", !panelOpen || Paused);
    }

    static bool Paused => PauseMenu.Instance != null && PauseMenu.Instance.IsPaused;

    // Both pieces are UI Toolkit files in Assets/UI Toolkit, found through Resources/EpisodeUI: the "[ E ]  Talk to"
    // prompt (InteractPrompt.uxml, shared with the episodes' "[ E ]" prompt) and the conversation panel (NPCDialogue.uxml).
    // This only finds the named elements.
    void CreateUI()
    {
        var ui = Mindscape.AcademicPressure.EpisodeUI.Load();
        if (ui == null || (dialogueOverride == null && ui.npcDialogue == null) || ui.interactPrompt == null)
        {
            Debug.LogWarning("NPCInteractionManager: NPC dialogue / interact prompt UXML missing from Resources/EpisodeUI.");
            return;
        }

        // Sorting order: prompt with the episode HUD (0), panel with the story dialogue bar (1), under story choices,
        // cards, end screens and the pause menu.
        var promptRoot = Document("NPC Talk Prompt", ui, ui.interactPrompt, 0);
        promptRoot.Query().ForEach(e => e.pickingMode = UIE.PickingMode.Ignore);
        promptBox = promptRoot.Q("prompt"); promptText = promptRoot.Q<UIE.Label>("prompt-text");
        promptBox.AddToClassList("raised"); // a row above the episodes' [ E ] prompt, so both can show at once
        SetPrompt(false);

        var root = Document("NPC Dialogue", ui, dialogueOverride != null ? dialogueOverride : ui.npcDialogue, 1);
        dialoguePanel = root.Q("npc-dialogue");
        speakerText = root.Q<UIE.Label>("npc-speaker"); dialogueText = root.Q<UIE.Label>("npc-text");
        choicesRoot = root.Q("npc-choices");
        redesignedDialogue = dialoguePanel.ClassListContains("npc-dialogue-new");
        if (redesignedDialogue)
        {
            root.RegisterCallback<UIE.GeometryChangedEvent>(e =>
                dialoguePanel.EnableInClassList("compact", e.newRect.width < 800 || e.newRect.height < 540));
            var hint = root.Q<UIE.Label>("npc-hint");
            if (hint != null && MobileControls.Enabled) hint.text = "Tap a reply";
        }
        ShowPanel(false);
    }

    UIE.VisualElement Document(string objectName, Mindscape.AcademicPressure.EpisodeUI ui, UIE.VisualTreeAsset tree, int order)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        go.SetActive(false); // assign both assets before UIDocument.OnEnable builds the tree
        var document = go.AddComponent<UIE.UIDocument>();
        document.panelSettings = ui.panelSettings; document.visualTreeAsset = tree; document.sortingOrder = order;
        go.SetActive(true);
        var root = document.rootVisualElement;
        root.pickingMode = UIE.PickingMode.Ignore; // clicks pass through while nothing is open
        return root;
    }

    // Same structure as the sample replies in NPCDialogue.uxml.
    void CreateChoiceButton(string label, System.Action action)
    {
        if (choicesRoot == null) return;
        int index = replyActions.Count;
        replyActions.Add(action);
        var button = new UIE.Button(redesignedDialogue ? () => SelectReply(index) : action) { focusable = false };
        if (redesignedDialogue)
        {
            foreach (var (text, style) in new[] { ((index + 1).ToString(), "npc-key"), (label, "npc-reply-text"), ("›", "npc-arrow") })
            {
                var item = new UIE.Label(text) { pickingMode = UIE.PickingMode.Ignore };
                item.AddToClassList(style);
                button.Add(item);
            }
        }
        else button.text = label;
        button.AddToClassList("npc-choice");
        choicesRoot.Add(button);
    }

    void SelectReply(int index)
    {
        if (!DialogueActive || !panelOpen || endingDialogue || Paused ||
            Time.unscaledTime - repliesShownAt < .2f || index < 0 || index >= replyActions.Count) return;
        repliesShownAt = Time.unscaledTime;
        replyActions[index]();
    }

    void ClearChoiceButtons()
    {
        choicesRoot?.Clear();
        replyActions.Clear();
    }
}
