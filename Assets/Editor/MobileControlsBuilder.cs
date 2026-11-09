using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Builds the on-screen touch HUD: an analog joystick (bottom-left) plus Interact/Journal
// buttons (bottom-right), wired to a MobileControls component, which adds the look stick at runtime. Ships with its own root Canvas
// (rather than hunting for a scene-specific "HUDCanvas") so it drops into any scene the
// same way, whether or not that scene has a legacy uGUI HUD.
public static class MobileControlsBuilder
{
    const string JoystickBackgroundSpritePath = "Assets/Textures/UI/JoystickBackground.png";
    const string JoystickHandleSpritePath = "Assets/Textures/UI/JoystickHandle.png";

    [MenuItem("Tools/Build Mobile Controls")]
    public static void Build()
    {
        GameObject existing = GameObject.Find("Mobile Controls");
        if (existing != null)
            Object.DestroyImmediate(existing);

        GameObject root = new GameObject("Mobile Controls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        Stretch(root.GetComponent<RectTransform>());

        GameObject controlsRoot = new GameObject("Controls Root", typeof(RectTransform));
        controlsRoot.transform.SetParent(root.transform, false);
        Stretch(controlsRoot.GetComponent<RectTransform>());

        GameObject joystick = BuildJoystick(controlsRoot.transform);
        Button interactButton = BuildActionButton(controlsRoot.transform, "InteractButton", "E",
            new Vector2(1f, 0f), MobileControls.InteractPosition, 112);
        Button journalButton = BuildActionButton(controlsRoot.transform, "JournalButton", "JOURNAL",
            new Vector2(1f, 0f), MobileControls.JournalPosition, 84);

        MobileControls controls = root.AddComponent<MobileControls>();
        SerializedObject serialized = new SerializedObject(controls);
        serialized.FindProperty("controlsRoot").objectReferenceValue = controlsRoot;
        serialized.FindProperty("interactButton").objectReferenceValue = interactButton;
        serialized.FindProperty("journalButton").objectReferenceValue = journalButton;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        ApplyTheme(root);

        EditorUtility.SetDirty(controls);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("Mobile Controls built. Auto-shows on Android; tick 'Force Enable On Desktop' on the MobileControls component to test on PC.");
    }

    static GameObject BuildJoystick(Transform parent)
    {
        // The joystick's Image doubles as its own touch/drag region — MobileJoystick lives
        // on this same GameObject so raycasts land directly on it instead of needing to
        // bubble up from a child.
        GameObject joystick = new GameObject("Joystick", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        joystick.transform.SetParent(parent, false);
        RectTransform joystickRT = joystick.GetComponent<RectTransform>();
        joystickRT.anchorMin = new Vector2(0f, 0f);
        joystickRT.anchorMax = new Vector2(0f, 0f);
        joystickRT.pivot = new Vector2(0.5f, 0.5f);
        joystickRT.sizeDelta = new Vector2(220, 220);
        joystickRT.anchoredPosition = new Vector2(140, 140);

        Image background = joystick.GetComponent<Image>();
        background.color = new Color(1f, 1f, 1f, 0.35f);
        background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(JoystickBackgroundSpritePath);

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        handle.transform.SetParent(joystick.transform, false);
        RectTransform handleRT = handle.GetComponent<RectTransform>();
        handleRT.anchorMin = new Vector2(0.5f, 0.5f);
        handleRT.anchorMax = new Vector2(0.5f, 0.5f);
        handleRT.pivot = new Vector2(0.5f, 0.5f);
        handleRT.sizeDelta = new Vector2(90, 90);
        handleRT.anchoredPosition = Vector2.zero;

        Image handleImage = handle.GetComponent<Image>();
        handleImage.color = new Color(1f, 1f, 1f, 0.55f);
        handleImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(JoystickHandleSpritePath);
        handleImage.raycastTarget = false;

        MobileJoystick joystickComp = joystick.AddComponent<MobileJoystick>();
        SerializedObject serializedJoystick = new SerializedObject(joystickComp);
        serializedJoystick.FindProperty("handle").objectReferenceValue = handleRT;
        serializedJoystick.FindProperty("handleRange").floatValue = 70f;
        serializedJoystick.ApplyModifiedPropertiesWithoutUndo();

        return joystick;
    }

    static Button BuildActionButton(Transform parent, string name, string glyph, Vector2 anchor, Vector2 anchoredPosition, float size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = anchoredPosition;

        Image image = go.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.22f);

        Button button = go.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.32f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0.45f);
        button.colors = colors;

        AddGlyph(go.transform, glyph, size >= 90 ? 30 : 18);
        return button;
    }

    static void AddGlyph(Transform parent, string text, float fontSize)
    {
        GameObject labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(parent, false);
        RectTransform rt = labelGO.GetComponent<RectTransform>();
        Stretch(rt);

        TextMeshProUGUI label = labelGO.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = new Color(1f, 1f, 1f, 0.85f);
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
    }

    // Also styles controls already serialized into gameplay scenes without rebuilding their wiring.
    public static void ApplyTheme(GameObject root)
    {
        if (root == null) return;
        var controls = root.transform.Find("Controls Root");
        if (controls == null) return;
        var roundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(JoystickBackgroundSpritePath);
        var handleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(JoystickHandleSpritePath);
        if (roundSprite == null || handleSprite == null)
        {
            Debug.LogError("Mobile control theme needs the two joystick sprites.");
            return;
        }

        var joystick = controls.Find("Joystick");
        if (joystick != null)
        {
            var rect = joystick.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(230, 230);
            rect.anchoredPosition = new Vector2(155, 150);
            StyleCircle(joystick.GetComponent<Image>(), roundSprite, new Color32(189, 164, 119, 225));
            var inset = CircleChild(joystick, "Inset", roundSprite, 204, new Color32(48, 45, 40, 226));
            inset.SetAsFirstSibling();
            var handle = joystick.Find("Handle");
            if (handle != null)
            {
                handle.GetComponent<RectTransform>().sizeDelta = new Vector2(98, 98);
                StyleCircle(handle.GetComponent<Image>(), handleSprite, new Color32(240, 228, 204, 255));
            }
        }

        StyleAction(controls.Find("InteractButton"), roundSprite, 112,
            MobileControls.InteractPosition, "E", 36);
        StyleAction(controls.Find("JournalButton"), roundSprite, 84,
            MobileControls.JournalPosition, "JOURNAL", 14);
    }

    static void StyleAction(Transform target, Sprite circle, float size, Vector2 position, string label, int fontSize)
    {
        if (target == null) return;
        var rect = target.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = position;
        StyleCircle(target.GetComponent<Image>(), circle, new Color32(189, 164, 119, 245));
        var inset = CircleChild(target, "Inset", circle, size - 10, new Color32(48, 45, 40, 245));
        inset.SetAsFirstSibling();

        var button = target.GetComponent<Button>();
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, .95f, .78f, 1f);
        colors.pressedColor = new Color(.83f, .72f, .52f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        var text = target.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = label;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.color = new Color32(240, 228, 204, 255);
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
        }
    }

    static void StyleCircle(Image image, Sprite sprite, Color color)
    {
        if (image == null) return;
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = color;
    }

    static RectTransform CircleChild(Transform parent, string name, Sprite sprite, float size, Color color)
    {
        var child = parent.Find(name);
        if (child == null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            child = go.transform;
        }
        var rect = child.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(size, size);
        var image = child.GetComponent<Image>();
        StyleCircle(image, sprite, color);
        image.raycastTarget = false;
        return rect;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localPosition = Vector3.zero;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }
}
