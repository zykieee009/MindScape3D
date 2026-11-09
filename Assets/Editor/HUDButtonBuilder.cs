using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

public static class HUDButtonBuilder
{
    [MenuItem("Tools/Build HUD Buttons")]
    public static void Build()
    {
        SetSprite("Assets/UI/task_icon.png");
        AssetDatabase.Refresh();

        GameObject hudCanvasGO = GameObject.Find("HUDCanvas");
        Canvas canvas = hudCanvasGO != null ? hudCanvasGO.GetComponent<Canvas>() : Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("No Canvas found in scene. Create one first.");
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

        GameObject existing = GameObject.Find("HUD_ButtonGroup");
        if (existing != null) Object.DestroyImmediate(existing);

        GameObject group = new GameObject("HUD_ButtonGroup");
        group.transform.SetParent(canvas.transform, false);

        RectTransform groupRT = group.AddComponent<RectTransform>();
        groupRT.anchorMin = new Vector2(1, 0);
        groupRT.anchorMax = new Vector2(1, 0);
        groupRT.pivot = new Vector2(1, 0);
        groupRT.anchoredPosition = new Vector2(-20, 20);
        groupRT.sizeDelta = new Vector2(130, 60);

        HorizontalLayoutGroup layout = group.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10;
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.padding = new RectOffset(0, 0, 0, 0);

        ContentSizeFitter fitter = group.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CreateIconButton(group.transform, "TaskButton", "Assets/UI/task_icon.png", 60);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("HUD buttons created successfully.");
    }

    static void SetSprite(string path)
    {
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.alphaIsTransparency = true;
        ti.SaveAndReimport();
    }

    static void CreateIconButton(Transform parent, string name, string spritePath, float size)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);

        GameObject btnGO = new GameObject(name);
        btnGO.transform.SetParent(parent, false);

        RectTransform rt = btnGO.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);

        Image img = btnGO.AddComponent<Image>();
        img.sprite = sprite;
        img.color = Color.white;
        img.preserveAspect = true;

        Button btn = btnGO.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        btn.colors = cb;
    }
}
