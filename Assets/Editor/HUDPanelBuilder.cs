using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class HUDPanelBuilder
{
    const string TaskPanelPath = "Assets/Resources/UI/task_panel.png";

    [MenuItem("Tools/Build HUD Panels")]
    public static void Build()
    {
        SetSprite(TaskPanelPath);
        AssetDatabase.Refresh();

        GameObject hudCanvasGO = GameObject.Find("HUDCanvas");
        Canvas canvas = hudCanvasGO != null ? hudCanvasGO.GetComponent<Canvas>() : Object.FindFirstObjectByType<Canvas>();
        PlayerControls player = Object.FindFirstObjectByType<PlayerControls>(FindObjectsInactive.Include);
        if (canvas == null || player == null)
        {
            Debug.LogError("HUDPanelBuilder needs a Canvas and a PlayerControls object in the active scene.");
            return;
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        GameObject existing = FindSceneObject("HUDPanels");
        if (existing != null)
            Object.DestroyImmediate(existing);

        GameObject panelRoot = new GameObject("HUDPanels", typeof(RectTransform));
        panelRoot.transform.SetParent(canvas.transform, false);
        Stretch(panelRoot.GetComponent<RectTransform>());

        GameObject overlay = CreateOverlay(panelRoot.transform);
        GameObject taskPanel = CreateTaskPanel(panelRoot.transform);

        SerializedObject serializedPlayer = new SerializedObject(player);
        serializedPlayer.FindProperty("tasksPanelRoot").objectReferenceValue = taskPanel;
        serializedPlayer.FindProperty("hudPanelOverlay").objectReferenceValue = overlay;
        serializedPlayer.FindProperty("tasksCloseButton").objectReferenceValue = FindSceneObject("Tasks Close Hit Area").GetComponent<Button>();
        serializedPlayer.FindProperty("tasksTextRoot").objectReferenceValue = FindSceneObject("Task Text Display Area").transform;
        serializedPlayer.ApplyModifiedPropertiesWithoutUndo();

        overlay.SetActive(false);
        taskPanel.SetActive(false);

        EnsureEventSystem();
        EditorUtility.SetDirty(player);
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        EditorSceneManager.SaveScene(player.gameObject.scene);

        Debug.Log("HUD task panel built from Resources/UI assets.");
    }

    static GameObject CreateOverlay(Transform parent)
    {
        GameObject overlay = new GameObject("HUD Panel Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.transform.SetParent(parent, false);
        Stretch(overlay.GetComponent<RectTransform>());

        Image image = overlay.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.45f);
        image.raycastTarget = false;
        return overlay;
    }

    static GameObject CreateTaskPanel(Transform parent)
    {
        Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TaskPanelPath);

        GameObject panel = ImageObject(parent, "Tasks Panel", panelSprite, new Vector2(0.5f, 0.5f), new Vector2(505, 606), Vector2.zero);
        panel.AddComponent<CanvasGroup>();

        RectTransform taskArea = Empty(panel.transform, "Task Text Display Area", new Vector2(0.5f, 0.5f), new Vector2(330, 310), new Vector2(5, -45));
        VerticalLayoutGroup layout = taskArea.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 22;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandHeight = false;

        CreateTaskText(taskArea, "Task 1 Text", "Explore the house");
        CreateTaskText(taskArea, "Task 2 Text", "Find supplies");
        CreateTaskText(taskArea, "Task 3 Text", "Return to camp");

        CreateCloseHitArea(panel.transform, "Tasks Close Hit Area", new Vector2(186, 239), new Vector2(82, 82));
        return panel;
    }

    static void CreateTaskText(Transform parent, string name, string text)
    {
        GameObject row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        RectTransform rt = row.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(330, 48);

        TextMeshProUGUI label = row.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 26;
        label.color = new Color(0.25f, 0.18f, 0.12f, 1f);
        label.alignment = TextAlignmentOptions.Left;
        label.enableWordWrapping = true;
        label.raycastTarget = false;
    }

    static void CreateCloseHitArea(Transform parent, string name, Vector2 position, Vector2 size)
    {
        GameObject close = ImageObject(parent, name, null, new Vector2(0.5f, 0.5f), size, position);
        Image image = close.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.001f);
        image.raycastTarget = true;
        close.AddComponent<Button>();
    }

    static GameObject ImageObject(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 size, Vector2 position)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = sprite == null;
        return go;
    }

    static RectTransform Empty(Transform parent, string name, Vector2 anchor, Vector2 size, Vector2 position)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
        return rt;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }

    static void SetSprite(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }

    static GameObject FindSceneObject(string objectName)
    {
        GameObject[] objects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject obj in objects)
            if (obj.name == objectName)
                return obj;

        return null;
    }

    static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        EditorUtility.SetDirty(eventSystem);
    }
}
