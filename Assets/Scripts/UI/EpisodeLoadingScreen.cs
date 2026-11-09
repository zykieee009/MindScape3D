using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// Displays atmospheric location artwork, quest briefings, and responsive progress
/// while an episode scene loads asynchronously.
/// </summary>
public sealed class EpisodeLoadingScreen : MonoBehaviour
{
    static EpisodeLoadingScreen active;

    static readonly string[] Locations =
    {
        "HOUSE",
        "HOME / BUS STOP",
        "CLASSROOM",
        "HOME / FAMILY DINNER",
        "BEDROOM / KEEPSAKES"
    };

    static readonly string[] EpisodeTitles =
    {
        "The Paper on the Desk",
        "Before the Bell",
        "Running Out of Time",
        "The Weight Behind the Worry",
        "What I Left Behind"
    };

    static readonly string[] QuestBriefings =
    {
        "• Explore the bedroom and examine the paper, school bag and notebook\n• Collect three memories in your journal\n• Return to the examination paper to find clarity",
        "• Turn off the alarm and find your student ID and examination materials\n• Pick up your school bag and leave home\n• Follow the street markers to the bus stop and board the bus",
        "• Reach Room 03 and speak with your teacher\n• Find your assigned seat and begin the examination\n• Answer questions in any order · Take a grounding break when needed",
        "• Return to the examination paper and name your worry\n• Join your family for dinner and choose how to respond\n• Return to your bedroom",
        "• Examine an old certificate, sketchbook, and notebook\n• Reflect on progress and expectations\n• Observe the thoughts that return at bedtime"
    };

    Canvas canvas;
    CanvasGroup canvasGroup;
    CanvasScaler scaler;
    RawImage backdrop;
    RawImage progressFill;
    Text caption;
    Text percentText;

    float targetProgress;
    float displayedProgress;
    float shownAt;
    float previousTimeScale;
    bool pausedGameplay;
    int chapter = 2;
    bool isReady;
    int lastScreenWidth;
    int lastScreenHeight;

    static Font titleFont;
    static Font bodyFont;
    static Font boldFont;
    static Texture2D verticalGradient;
    static Texture2D horizontalGradient;

    public static bool IsActive => active != null;

    void Update()
    {
        // Smoothly interpolate progress fill
        displayedProgress = Mathf.MoveTowards(displayedProgress, targetProgress, Time.unscaledDeltaTime * 0.85f);

        if (progressFill != null)
        {
            progressFill.rectTransform.anchorMax = new Vector2(displayedProgress, 1f);
        }

        if (percentText != null && !isReady)
        {
            percentText.text = Mathf.RoundToInt(displayedProgress * 100f) + "%";
        }

        // Pulse prompt when loading is ready
        if (isReady && caption != null)
        {
            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 4.5f);
            caption.color = new Color(0.97f, 0.88f, 0.70f, pulse);
        }

        // Update crop UV when viewport resolution changes
        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            if (backdrop != null && backdrop.texture != null)
            {
                backdrop.uvRect = CropToFill(backdrop.texture);
            }
        }
    }

    // The in-game shot of an episode (Mindscape > Main Menu > Capture Episode Thumbnails), shared with the main menu's
    // chapter and episode selector. Episode 0 is the tutorial.
    public static string Thumbnail(int chapter, int episode)
    {
        int count = chapter == 3 ? Mindscape.SocialDynamics.SocialDynamicsChapter.EpisodeCount : chapter == 1 ? Mindscape.AcademicPressure.AcademicPressureChapter.EpisodeCount : EpisodeTitles.Length;
        return episode < 1 ? "EpisodeThumbnails/Tutorial" : "EpisodeThumbnails/Chapter" + chapter + "_Episode" + Mathf.Min(episode, count);
    }

    // chapter 1 = Academic Pressure, chapter 2 = Family Expectations; 0 infers it from the scene name.
    public static bool Load(string sceneName, int episodeNumber, int chapter = 0)
    {
        if (chapter == 0) chapter = sceneName != null && sceneName.StartsWith("Chapter 3 ") ? 3 : sceneName != null && sceneName.StartsWith("Chapter 1 ") ? 1 : 2;
        if (active != null || string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            return false;

        var instance = new GameObject("Episode Loading Screen").AddComponent<EpisodeLoadingScreen>();
        active = instance;
        instance.shownAt = Time.realtimeSinceStartup;
        DontDestroyOnLoad(instance.gameObject);
        instance.chapter = chapter;
        instance.Build(Mathf.Clamp(episodeNumber, 1, chapter == 3 ? 8 : chapter == 1 ? Mindscape.AcademicPressure.AcademicPressureChapter.EpisodeCount : EpisodeTitles.Length) - 1);
        instance.StartCoroutine(instance.LoadRoutine(sceneName));
        return true;
    }

    void Build(int index)
    {
        LoadResources();

        // 1. Root Canvas
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;

        // 2. Responsive Canvas Scaler (1920x1080 baseline with 0.5 match width/height). In mobile mode the layout is
        // matched to a 720-high screen instead, so text is 1.5x larger on a phone and still fits top to bottom.
        bool mobile = MobileControls.Enabled;
        scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = mobile ? new Vector2(1280, 720) : new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = mobile ? 1f : 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        // 3. Full-screen Artwork Backdrop
        bool academic = chapter == 1;
        string[] locations = chapter == 3 ? Mindscape.SocialDynamics.SocialDynamicsChapter.Locations : academic ? Mindscape.AcademicPressure.AcademicPressureChapter.Locations : Locations;
        string[] titles = chapter == 3 ? Mindscape.SocialDynamics.SocialDynamicsChapter.Titles : academic ? Mindscape.AcademicPressure.AcademicPressureChapter.Titles : EpisodeTitles;
        string[] briefings = chapter == 3 ? Mindscape.SocialDynamics.SocialDynamicsChapter.Briefings : academic ? Mindscape.AcademicPressure.AcademicPressureChapter.Briefings : QuestBriefings;
        var tex = Resources.Load<Texture2D>(Thumbnail(chapter, index + 1));
        backdrop = CreateImage("Artwork", transform, Color.white, tex);
        StretchFull(backdrop.rectTransform);
        backdrop.uvRect = CropToFill(tex);

        // 4. Subtle Ambient Vignette
        var ambientShade = CreateImage("Ambient Shade", transform, new Color(0.04f, 0.03f, 0.02f, 0.25f));
        StretchFull(ambientShade.rectTransform);

        // 5. Cinematic Vertical Gradient Footer Shade (smoothly fades from bottom up)
        var footerShade = CreateImage("Footer Shade", transform, Color.white, GetVerticalGradient());
        var rtShade = footerShade.rectTransform;
        rtShade.anchorMin = new Vector2(0f, 0f);
        rtShade.anchorMax = new Vector2(1f, 0f);
        rtShade.pivot = new Vector2(0.5f, 0f);
        rtShade.offsetMin = Vector2.zero;
        rtShade.offsetMax = new Vector2(0f, 780f);

        // 6. Responsive Safe Margin Container
        var safe = CreateRect("Safe Container", transform);
        safe.anchorMin = new Vector2(0f, 0f);
        safe.anchorMax = new Vector2(1f, 1f);
        safe.pivot = new Vector2(0.5f, 0.5f);
        safe.offsetMin = mobile ? new Vector2(48f, 22f) : new Vector2(80f, 48f);
        safe.offsetMax = mobile ? new Vector2(-48f, -22f) : new Vector2(-80f, -48f);

        // --- Top Bar (Brand & Chapter Badge) ---
        var topBar = CreateRect("Top Bar", safe);
        topBar.anchorMin = new Vector2(0f, 1f);
        topBar.anchorMax = new Vector2(1f, 1f);
        topBar.pivot = new Vector2(0f, 1f);
        topBar.offsetMin = new Vector2(0f, -70f);
        topBar.offsetMax = Vector2.zero;

        var brand = CreateText("Brand", topBar, "M I N D S C A P E", titleFont, 34, FontStyle.Bold, new Color(0.96f, 0.90f, 0.78f, 0.92f), TextAnchor.MiddleLeft);
        brand.rectTransform.anchorMin = new Vector2(0f, 0f);
        brand.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        brand.rectTransform.offsetMin = Vector2.zero;
        brand.rectTransform.offsetMax = Vector2.zero;

        var chapterBadge = CreateText("Chapter Badge", topBar, chapter == 3 ? "CHAPTER 03  ·  SOCIAL DYNAMICS" : academic ? "CHAPTER 01  ·  ACADEMIC PRESSURE" : "CHAPTER 02  ·  FAMILY EXPECTATIONS", boldFont, 24, FontStyle.Bold, new Color(0.85f, 0.70f, 0.44f, 0.95f), TextAnchor.MiddleRight);
        chapterBadge.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        chapterBadge.rectTransform.anchorMax = new Vector2(1f, 1f);
        chapterBadge.rectTransform.offsetMin = Vector2.zero;
        chapterBadge.rectTransform.offsetMax = Vector2.zero;

        // --- Bottom Content Area ---
        var bottomArea = CreateRect("Bottom Content", safe);
        bottomArea.anchorMin = new Vector2(0f, 0f);
        bottomArea.anchorMax = new Vector2(1f, 0f);
        bottomArea.pivot = new Vector2(0f, 0f);
        bottomArea.offsetMin = Vector2.zero;
        bottomArea.offsetMax = new Vector2(0f, 580f);

        // Episode Tag & Location
        var locationTag = CreateText("Location Tag", bottomArea, "EPISODE " + (index + 1).ToString("00") + "  ·  " + locations[index], boldFont, 24, FontStyle.Bold, new Color(0.86f, 0.71f, 0.45f, 1f));
        SetAnchors(locationTag.rectTransform, 0f, 0f, 1f, 0f, new Vector2(0f, 536f), new Vector2(0f, 572f));

        // Episode Title
        string titleString = index < titles.Length ? titles[index] : "Episode " + (index + 1);
        var title = CreateText("Episode Title", bottomArea, titleString, titleFont, 64, FontStyle.Bold, new Color(0.98f, 0.94f, 0.86f, 1f));
        SetAnchors(title.rectTransform, 0f, 0f, 1f, 0f, new Vector2(0f, 444f), new Vector2(0f, 534f));

        // Refined Horizontal Divider Line
        var divider = CreateImage("Divider", bottomArea, Color.white, GetHorizontalDivider());
        SetAnchors(divider.rectTransform, 0f, 0f, 1f, 0f, new Vector2(0f, 434f), new Vector2(0f, 437f));

        // Objectives Header
        var briefingHeader = CreateText("Briefing Header", bottomArea, "QUEST OBJECTIVES", boldFont, 22, FontStyle.Bold, new Color(0.85f, 0.70f, 0.44f, 0.95f));
        SetAnchors(briefingHeader.rectTransform, 0f, 0f, 1f, 0f, new Vector2(0f, 386f), new Vector2(0f, 420f));

        // Objectives Body
        string briefingString = index < briefings.Length ? briefings[index] : "";
        var briefing = CreateText("Briefing Body", bottomArea, briefingString, bodyFont, 28, FontStyle.Normal, new Color(0.93f, 0.88f, 0.80f, 0.95f), TextAnchor.UpperLeft);
        briefing.lineSpacing = 1.35f;
        SetAnchors(briefing.rectTransform, 0f, 0f, 1f, 0f, new Vector2(0f, 96f), new Vector2(0f, 380f));

        // --- Progress & Status Row ---
        var progressRow = CreateRect("Progress Row", bottomArea);
        SetAnchors(progressRow, 0f, 0f, 1f, 0f, new Vector2(0f, 30f), new Vector2(0f, 76f));

        caption = CreateText("Caption", progressRow, "Loading your story…", bodyFont, 26, FontStyle.Normal, new Color(0.90f, 0.84f, 0.74f, 0.95f));
        caption.rectTransform.anchorMin = new Vector2(0f, 0f);
        caption.rectTransform.anchorMax = new Vector2(0.8f, 1f);
        caption.rectTransform.offsetMin = Vector2.zero;
        caption.rectTransform.offsetMax = Vector2.zero;

        percentText = CreateText("Percent", progressRow, "0%", boldFont, 26, FontStyle.Bold, new Color(0.86f, 0.71f, 0.45f, 1f), TextAnchor.MiddleRight);
        percentText.rectTransform.anchorMin = new Vector2(0.8f, 0f);
        percentText.rectTransform.anchorMax = new Vector2(1f, 1f);
        percentText.rectTransform.offsetMin = Vector2.zero;
        percentText.rectTransform.offsetMax = Vector2.zero;

        // --- Progress Bar Track & Fill ---
        var track = CreateImage("Progress Track", bottomArea, new Color(0.12f, 0.09f, 0.07f, 0.85f));
        SetAnchors(track.rectTransform, 0f, 0f, 1f, 0f, new Vector2(0f, 10f), new Vector2(0f, 20f));

        var fill = CreateImage("Progress Fill", track.rectTransform, new Color(0.90f, 0.73f, 0.42f, 1f));
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;
        progressFill = fill;
    }

    static void SetAnchors(RectTransform rt, float minX, float minY, float maxX, float maxY, Vector2 offMin, Vector2 offMax)
    {
        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.offsetMin = offMin;
        rt.offsetMax = offMax;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    static RawImage CreateImage(string name, Transform parent, Color color, Texture texture = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        var img = go.GetComponent<RawImage>();
        img.color = color;
        img.texture = texture != null ? texture : Texture2D.whiteTexture;
        img.raycastTarget = false;
        return img;
    }

    static Text CreateText(string name, Transform parent, string message, Font font, int size, FontStyle style, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        var label = go.GetComponent<Text>();
        label.font = font;
        label.fontSize = size;
        label.fontStyle = style;
        label.color = color;
        label.text = message;
        label.alignment = alignment;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;   // Truncate hides a line taller than its box (the big serif title)
        label.raycastTarget = false;
        return label;
    }

    static void LoadResources()
    {
        if (titleFont == null)
            titleFont = Resources.Load<Font>("MainMenuNew/StorySerif");
        if (bodyFont == null)
            bodyFont = Resources.Load<Font>("MinigameTheme/ComicNeue-Regular");
        if (boldFont == null)
            boldFont = Resources.Load<Font>("MinigameTheme/ComicNeue-Bold");

        Font fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (titleFont == null) titleFont = fallback;
        if (bodyFont == null) bodyFont = fallback;
        if (boldFont == null) boldFont = fallback;
    }

    static Texture2D GetVerticalGradient()
    {
        if (verticalGradient != null) return verticalGradient;
        const int h = 128;
        verticalGradient = new Texture2D(1, h, TextureFormat.RGBA32, false);
        var pixels = new Color[h];
        Color bottom = new Color(0.06f, 0.045f, 0.035f, 0.96f);
        Color top = new Color(0.06f, 0.045f, 0.035f, 0f);
        for (int i = 0; i < h; i++)
        {
            float t = (float)i / (h - 1);
            float curved = t * t * (3f - 2f * t);
            pixels[i] = Color.Lerp(bottom, top, curved);
        }
        verticalGradient.SetPixels(pixels);
        verticalGradient.wrapMode = TextureWrapMode.Clamp;
        verticalGradient.Apply();
        return verticalGradient;
    }

    static Texture2D GetHorizontalDivider()
    {
        if (horizontalGradient != null) return horizontalGradient;
        const int w = 128;
        horizontalGradient = new Texture2D(w, 1, TextureFormat.RGBA32, false);
        var pixels = new Color[w];
        Color center = new Color(0.86f, 0.71f, 0.45f, 0.5f);
        Color edges = new Color(0.86f, 0.71f, 0.45f, 0f);
        for (int i = 0; i < w; i++)
        {
            float t = (float)i / (w - 1);
            float distFromCenter = Mathf.Abs(t - 0.5f) * 2f;
            pixels[i] = Color.Lerp(center, edges, Mathf.SmoothStep(0f, 1f, distFromCenter));
        }
        horizontalGradient.SetPixels(pixels);
        horizontalGradient.wrapMode = TextureWrapMode.Clamp;
        horizontalGradient.Apply();
        return horizontalGradient;
    }

    static Rect CropToFill(Texture texture)
    {
        if (texture == null || Screen.height == 0) return new Rect(0, 0, 1, 1);
        float imageRatio = (float)texture.width / texture.height;
        float screenRatio = (float)Screen.width / Screen.height;
        if (imageRatio > screenRatio)
        {
            float width = screenRatio / imageRatio;
            return new Rect((1 - width) / 2, 0, width, 1);
        }
        float height = imageRatio / screenRatio;
        return new Rect(0, (1 - height) / 2, 1, height);
    }

    IEnumerator LoadRoutine(string sceneName)
    {
        yield return null;
        var operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null) { Destroy(gameObject); yield break; }
        operation.allowSceneActivation = false;

        while (operation.progress < .9f)
        {
            SetProgress(operation.progress);
            yield return null;
        }

        SetProgress(.9f);
        yield return null;
        operation.allowSceneActivation = true;
        while (!operation.isDone) yield return null;

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        pausedGameplay = true;

        SetProgress(.95f);
        yield return new WaitForEndOfFrame();
        SetProgress(1f);

        while (displayedProgress < .995f) yield return null;
        yield return new WaitForEndOfFrame();

        // Brief breathing room to take in the artwork and briefing (2.5 seconds minimum)
        while (Time.realtimeSinceStartup - shownAt < 2.5f) yield return null;

        isReady = true;
        if (caption != null) caption.text = "Tap the screen or press any key to continue  ›";
        if (percentText != null) percentText.text = "READY";

        yield return null;
        while (!ContinuePressed()) yield return null;

        // Smooth crossfade out into gameplay
        if (canvasGroup != null)
        {
            float elapsed = 0f;
            const float fadeDuration = 0.25f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                yield return null;
            }
        }

        Destroy(gameObject);
    }

    static bool ContinuePressed()
    {
        return (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            || (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
    }

    void SetProgress(float amount)
    {
        targetProgress = Mathf.Clamp01(amount);
    }

    void OnDestroy()
    {
        if (pausedGameplay) Time.timeScale = previousTimeScale;
        if (active == this) active = null;
    }
}
