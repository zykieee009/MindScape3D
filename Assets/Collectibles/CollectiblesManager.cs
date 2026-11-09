using System.Collections;
using System.Collections.Generic;
using Mindscape.Minigames;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// One per scene. Finds the nearest CollectibleItem in range of the player, shows the "F to collect" prompt and
// records pickups (quests read the counts). Swap the prefabs in "Prefab slots" to change how items look.
public class CollectiblesManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform player;
    [SerializeField] Canvas promptCanvas;

    [Header("Prefab slots — swap these to change the visual")]
    [SerializeField] List<CollectibleItem> collectiblePrefabs = new List<CollectibleItem>();

    [Header("Prompt")]
    [SerializeField] string collectPrompt = "F to collect";
    [SerializeField] Color promptColor = new Color(1f, 0.92f, 0.55f, 1f);

    readonly List<CollectibleItem> items = new List<CollectibleItem>();
    TextMeshProUGUI promptText;
    GameObject promptRoot;   // the panel that holds the prompt text
    CollectibleItem nearbyItem;
    int collectedCount;
    readonly Dictionary<string, int> itemCounts = new Dictionary<string, int>();

    public int CollectedCount => collectedCount;
    public int GetItemCount(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return 0;
        return itemCounts.TryGetValue(itemId, out int count) ? count : 0;
    }
    public IReadOnlyList<CollectibleItem> CollectiblePrefabs => collectiblePrefabs;

    void Awake()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.Find("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        if (promptCanvas == null)
            promptCanvas = FindAnyObjectByType<Canvas>();

        CreatePrompt();
        CacheSceneItems();
    }

    void Update()
    {
        if (NPCInteractionManager.DialogueActive || NPCInteractionManager.HasNearbyNPC ||
            (QuestHUDController.Instance != null && QuestHUDController.Instance.IsOpen))
        {
            nearbyItem = null;
            if (promptRoot != null) promptRoot.SetActive(false);
            return;
        }

        nearbyItem = FindNearbyItem();

        if (promptText != null)
        {
            bool showPrompt = nearbyItem != null;
            promptRoot.SetActive(showPrompt);
            if (showPrompt)
                promptText.text = "F to collect  •  " + nearbyItem.DisplayName;
        }

        bool interactPressed = (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) || MobileInput.InteractPressed;
        if (nearbyItem != null && interactPressed)
            nearbyItem.Collect();
    }

    void CacheSceneItems()
    {
        items.Clear();
        CollectibleItem[] sceneItems = FindObjectsByType<CollectibleItem>(FindObjectsInactive.Include);
        for (int i = 0; i < sceneItems.Length; i++)
        {
            if (sceneItems[i] == null) continue;
            sceneItems[i].manager = this;
            items.Add(sceneItems[i]);
        }
    }

    CollectibleItem FindNearbyItem()
    {
        CollectibleItem closest = null;
        float closestDistance = float.MaxValue;

        for (int i = items.Count - 1; i >= 0; i--)
        {
            CollectibleItem item = items[i];
            if (item == null)
            {
                items.RemoveAt(i);
                continue;
            }

            if (!item.CanCollect(player)) continue;

            float distance = Vector3.Distance(item.transform.position, player.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = item;
            }
        }

        return closest;
    }

    public void Collect(CollectibleItem item)
    {
        if (item == null) return;

        collectedCount++;
        string id = item.itemId;
        if (!string.IsNullOrEmpty(id)) itemCounts[id] = GetItemCount(id) + 1;
        StartCoroutine(FinishCollection(item));
    }

    IEnumerator FinishCollection(CollectibleItem item)
    {
        Transform itemTransform = item.transform;
        Vector3 startScale = itemTransform.lossyScale;
        float elapsed = 0f;
        const float duration = 0.55f;

        Collider[] colliders = item.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            itemTransform.localScale = Vector3.Lerp(startScale, Vector3.one * 0.08f, eased);
            yield return null;
        }

        item.gameObject.SetActive(false);
    }

    // A dark panel with cream text at the bottom centre, matching the other interact prompts.
    void CreatePrompt()
    {
        if (promptCanvas == null) return;

        promptRoot = new GameObject("Collect Prompt", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        promptRoot.transform.SetParent(promptCanvas.transform, false);
        RectTransform rect = promptRoot.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 48f);
        rect.sizeDelta = new Vector2(620f, 70f);
        var panel = promptRoot.GetComponent<UnityEngine.UI.Image>();
        panel.color = new Color32(34, 45, 49, 235);
        panel.raycastTarget = false;

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(promptRoot.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(24f, 6f);
        textRect.offsetMax = new Vector2(-24f, -6f);

        promptText = textObject.GetComponent<TextMeshProUGUI>();
        promptText.font = MinigameUI.Font(true);
        promptText.text = collectPrompt;
        promptText.fontSize = 30f;
        promptText.enableAutoSizing = true;
        promptText.fontSizeMin = 20f;
        promptText.fontSizeMax = 30f;
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.color = promptColor;
        promptText.raycastTarget = false;
        promptRoot.SetActive(false);
    }
}
