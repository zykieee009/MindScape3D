using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using TMPro;

// Adds the GameObject > Cutscene menu items used to build out a cutscene's UI:
//   Slides       — the container that holds every slide (drag its children into
//                   CutscenePlayer's Slides list, in play order)
//   Slide        — one slide: a CanvasGroup (what CutscenePlayer fades) holding a
//                   Slide Image (artwork) and a Slide Text (dialogue)
//   Text         — a standalone Cutscene Text, for use outside the slide system
//   Next Button  — a button with a Target Slide Index field; jumps CutscenePlayer
//                   to that slide when clicked
//   Play Episode Button — a button with a Scene Name field; loads that scene
//                   when clicked
public static class CutsceneTextMenu
{
    [MenuItem("GameObject/Cutscene/Slides", false, 8)]
    static void CreateSlides(MenuCommand menuCommand)
    {
        GameObject existing = GameObject.Find("Slides");
        GameObject go = existing != null
            ? existing
            : CreateSlidesContainer(menuCommand.context as GameObject);
        Selection.activeGameObject = go;
    }

    [MenuItem("GameObject/Cutscene/Slide", false, 9)]
    static void CreateSlide(MenuCommand menuCommand)
    {
        Transform slidesParent = FindOrCreateSlidesParent(menuCommand.context as GameObject);

        GameObject slide = new GameObject("Slide", typeof(RectTransform), typeof(CanvasGroup));
        GameObjectUtility.SetParentAndAlign(slide, slidesParent.gameObject);
        StretchFull(slide.GetComponent<RectTransform>());

        GameObject image = new GameObject("Slide Image", typeof(RectTransform));
        GameObjectUtility.SetParentAndAlign(image, slide);
        StretchFull(image.GetComponent<RectTransform>());
        Image img = image.AddComponent<Image>();
        img.color = Color.white;
        img.preserveAspect = true;

        CreateTextObject("Slide Text", slide.transform, false);

        Undo.RegisterCreatedObjectUndo(slide, "Create Slide");
        Selection.activeGameObject = slide;
    }

    [MenuItem("GameObject/Cutscene/Text", false, 10)]
    static void CreateCutsceneText(MenuCommand menuCommand)
    {
        Canvas canvas = FindOrCreateCanvas(menuCommand.context as GameObject);
        GameObject go = CreateTextObject("Cutscene Text", canvas.transform, false);

        Undo.RegisterCreatedObjectUndo(go, "Create Cutscene Text");
        Selection.activeGameObject = go;
    }

    [MenuItem("GameObject/Cutscene/Next Button", false, 11)]
    static void CreateNextButton(MenuCommand menuCommand)
    {
        GameObject selected = menuCommand.context as GameObject;
        Transform parent = selected != null ? selected.transform : FindOrCreateCanvas(selected).transform;

        GameObject go = CreateButtonObject("Next Button", "Next", parent);
        AnchorBottomRight(go.GetComponent<RectTransform>(), new Vector2(-40, 40));

        go.AddComponent<CutsceneNextButton>();

        Undo.RegisterCreatedObjectUndo(go, "Create Next Button");
        Selection.activeGameObject = go;
    }

    [MenuItem("GameObject/Cutscene/Play Episode Button", false, 12)]
    static void CreatePlayEpisodeButton(MenuCommand menuCommand)
    {
        GameObject selected = menuCommand.context as GameObject;
        Transform parent = selected != null ? selected.transform : FindOrCreateCanvas(selected).transform;

        GameObject go = CreateButtonObject("Play Episode Button", "Play Episode", parent);
        AnchorBottomRight(go.GetComponent<RectTransform>(), new Vector2(-40, 40));

        go.AddComponent<CutscenePlayEpisodeButton>();

        Undo.RegisterCreatedObjectUndo(go, "Create Play Episode Button");
        Selection.activeGameObject = go;
    }

    // Builds a standard UI Button (background Image + Button + a TMP label child).
    static GameObject CreateButtonObject(string name, string label, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        GameObjectUtility.SetParentAndAlign(go, parent.gameObject);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(180, 48);

        Image img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.9f);

        go.AddComponent<Button>();

        GameObject textGO = new GameObject("Text (TMP)", typeof(RectTransform));
        GameObjectUtility.SetParentAndAlign(textGO, go);
        StretchFull(textGO.GetComponent<RectTransform>());

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.black;
        tmp.fontSize = 24;

        return go;
    }

    static void AnchorBottomRight(RectTransform rt, Vector2 anchoredPosition)
    {
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = anchoredPosition;
    }

    // Builds a bottom-anchored caption box (used by both the standalone Text
    // object and each Slide's Slide Text) or, with stretch=true, a full-rect one.
    static GameObject CreateTextObject(string name, Transform parent, bool stretch)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        GameObjectUtility.SetParentAndAlign(go, parent.gameObject);

        RectTransform rt = go.GetComponent<RectTransform>();
        if (stretch)
        {
            StretchFull(rt);
        }
        else
        {
            rt.sizeDelta = new Vector2(600, 160);
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0, 80);
        }

        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.text = "Dialogue text";
        label.fontSize = 36;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;

        go.AddComponent<CutsceneText>();
        return go;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static GameObject CreateSlidesContainer(GameObject selected)
    {
        Canvas canvas = FindOrCreateCanvas(selected);

        GameObject go = new GameObject("Slides", typeof(RectTransform));
        GameObjectUtility.SetParentAndAlign(go, canvas.gameObject);
        StretchFull(go.GetComponent<RectTransform>());

        Undo.RegisterCreatedObjectUndo(go, "Create Slides");
        return go;
    }

    // Walks up from the current selection looking for a "Slides" container;
    // falls back to any "Slides" object in the scene, then creates one.
    static Transform FindOrCreateSlidesParent(GameObject selected)
    {
        if (selected != null)
        {
            for (Transform t = selected.transform; t != null; t = t.parent)
            {
                if (t.name == "Slides") return t;
            }
        }

        GameObject existing = GameObject.Find("Slides");
        if (existing != null) return existing.transform;

        return CreateSlidesContainer(selected).transform;
    }

    // Reuses the selected object's Canvas if there is one, otherwise the first
    // Canvas in the scene, otherwise creates a new Canvas (plus EventSystem if
    // one doesn't exist yet) — matching Unity's own GameObject > UI behavior.
    static Canvas FindOrCreateCanvas(GameObject selected)
    {
        Canvas canvas = selected != null ? selected.GetComponentInParent<Canvas>() : null;
        if (canvas != null) return canvas;

        canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas != null) return canvas;

        GameObject canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");

        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject esGO = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(esGO, "Create EventSystem");
        }

        return canvas;
    }
}
