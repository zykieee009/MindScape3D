using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// First scene of the game: a short loading screen while the story video gets ready, a content disclaimer
/// (Proceed, or Exit to quit), the story video (skippable), then the game title fading in over black until the
/// player presses any button, then the main menu, which asks to log in or play offline.
/// </summary>
public sealed class IntroSequence : MonoBehaviour
{
    [Tooltip("Story video, relative to Assets/StreamingAssets.")]
    public string videoFile = "video-introduction.mp4";
    public string menuScene = "main_menu_new";
    public float fadeTime = 1.2f;
    [Tooltip("The loading screen stays at least this long, so it never just flickers.")]
    public float minLoadingTime = 1.5f;
    [Tooltip("The 3D title (MindScape3D_IntroScreen). Empty: the flat MenuArt/title picture is used.")]
    public TitleModelView titleModel;

    VideoPlayer player;
    AudioSource videoAudio;
    RenderTexture videoTarget;
    RenderTexture[] glowSteps;   // the title, halved three times: stretched back up it is a soft blur of the logo
    Texture glowSource;
    CanvasGroup videoGroup, skipGroup, titleGroup, promptGroup, loadingGroup, disclaimerGroup;
    RawImage videoFrame;
    RectTransform loadingFill;
    Text prompt, loadingLabel, disclaimerNote;
    Button proceedButton, exitButton;
    bool skipRequested, videoFailed;
    float videoGiveUpAt;

    const string DisclaimerTitle = "Before you begin";
    const string DisclaimerBody =
        "Mindscape is a story about everyday stress: school deadlines, family expectations, and comparing yourself with others. " +
        "Some scenes show a character feeling worried, overwhelmed, or left behind.\n\n" +
        "The game includes breathing and reflection activities, but it is not therapy or a medical tool, and it cannot diagnose anything. " +
        "You can pause or stop at any time.\n\n" +
        "If you feel pressure or stress while playing, we advise you to stop and rest. The game will wait for you. " +
        "If the story starts to feel like too much, take a break and talk to someone you trust. " +
        "Support contacts are listed under Resources in the main menu.";

    static Font bodyFont, boldFont, titleFont;
    // Shared story UI palette (Dialogue.uss, Choice.uss, and EpisodeScreens.uss).
    static readonly Color Ink = new Color32(29, 43, 47, 255);
    static readonly Color InkPanel = new Color32(29, 43, 47, 248);
    static readonly Color Paper = new Color32(242, 232, 209, 255);
    static readonly Color Gold = new Color32(219, 179, 111, 255);
    static readonly Color Teal = new Color32(65, 101, 99, 255);

    void Awake()
    {
        Time.timeScale = 1;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat("Mindscape.Master", 0.8f));
        MusicManager.SetPaused(true);
        EnsureCameraAndEventSystem();
        if (titleModel != null) titleModel.Build();   // this Awake may run before the model's own
        BuildUI();
    }

    IEnumerator Start()
    {
        PrepareVideo();
        yield return ShowLoading();
        // Proceed is also the click browsers need before the video's audio may play (web build).
        yield return ShowDisclaimer();
        yield return PlayVideo();

        MusicManager.SetPaused(false);
        var menu = SceneManager.LoadSceneAsync(menuScene);
        menu.allowSceneActivation = false;

        prompt.text = Application.isMobilePlatform || MobileControls.Enabled ? "Tap anywhere to enter" : "Press any button to enter";
        if (titleModel != null && titleModel.Ready) titleModel.Restart();   // the logo builds up as it fades in
        yield return Fade(titleGroup, 1, fadeTime);
        yield return Fade(promptGroup, 1, 0.6f);
        yield return WaitForAnyPress(() => promptGroup.alpha = 0.55f + 0.45f * Mathf.Cos(Time.unscaledTime * 2.4f));
        yield return Fade(titleGroup, 0, fadeTime, promptGroup);

        menu.allowSceneActivation = true;
    }

    // The video starts loading behind the loading screen and the disclaimer, so it is usually ready by Proceed.
    void PrepareVideo()
    {
        player.url = Path.Combine(Application.streamingAssetsPath, videoFile);
        player.errorReceived += (_, message) => { videoFailed = true; Debug.LogWarning("IntroSequence: " + message); };
        player.Prepare();
        videoGiveUpAt = Time.unscaledTime + 15f;
    }

    // At least minLoadingTime, and until the video is ready (or has failed, or is taking too long: the bar never hangs).
    IEnumerator ShowLoading()
    {
        yield return Fade(loadingGroup, 1, 0.4f);
        float start = Time.unscaledTime, shown = 0;
        while (shown < 1)
        {
            bool ready = player.isPrepared || videoFailed || Time.unscaledTime > start + 8f;
            float target = Mathf.Min(Mathf.Clamp01((Time.unscaledTime - start) / minLoadingTime), ready ? 1 : 0.9f);
            shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * 1.6f);
            loadingFill.anchorMax = new Vector2(shown, 1);
            loadingLabel.text = "Loading" + new string('.', 1 + (int)(Time.unscaledTime * 2.5f) % 3);
            yield return null;
        }
        yield return Fade(loadingGroup, 0, 0.4f);
    }

    // The content notice. Proceed goes on to the story; Exit closes the game.
    IEnumerator ShowDisclaimer()
    {
        bool proceed = false;
        proceedButton.onClick.AddListener(() => proceed = true);
        exitButton.onClick.AddListener(ExitGame);
        disclaimerGroup.blocksRaycasts = true;
        yield return Fade(disclaimerGroup, 1, 0.5f);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(proceedButton.gameObject);   // keyboard and gamepad
        while (!proceed) yield return null;
        disclaimerGroup.blocksRaycasts = false;
        yield return Fade(disclaimerGroup, 0, 0.5f);
    }

    void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
        disclaimerNote.text = "You can close this browser tab now.";
#else
        Application.Quit();
#endif
    }

    IEnumerator PlayVideo()
    {
        while (!player.isPrepared && !videoFailed && !skipRequested && Time.unscaledTime < videoGiveUpAt) yield return null;
        if (!player.isPrepared || videoFailed || skipRequested) { player.Stop(); yield break; }

        videoTarget = new RenderTexture((int)player.width, (int)player.height, 0);
        player.targetTexture = videoTarget;
        videoFrame.texture = videoTarget;
        videoFrame.GetComponent<AspectRatioFitter>().aspectRatio = (float)player.width / player.height;

        bool finished = false;
        player.loopPointReached += _ => finished = true;
        player.Play();
        StartCoroutine(Fade(videoGroup, 1, 0.6f));
        var showSkip = StartCoroutine(ShowSkip());
        while (!finished && !skipRequested && !videoFailed)
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) skipRequested = true;
            yield return null;
        }

        StopCoroutine(showSkip);
        skipGroup.blocksRaycasts = false;
        StartCoroutine(Fade(skipGroup, 0, 0.3f));
        float startVolume = videoAudio.volume;
        for (float t = 0; t < 1; t += Time.unscaledDeltaTime / 0.8f)
        {
            videoGroup.alpha = 1 - t;
            videoAudio.volume = startVolume * (1 - t);
            yield return null;
        }
        videoGroup.alpha = 0;
        player.Stop();
        videoFrame.texture = null;
        videoTarget.Release();
    }

    // The skip button appears a second into the video and only takes clicks once it is visible.
    IEnumerator ShowSkip()
    {
        for (float t = 0; t < 1; t += Time.unscaledDeltaTime) yield return null;
        skipGroup.blocksRaycasts = true;
        yield return Fade(skipGroup, 1, 0.6f);
    }

    void LateUpdate()
    {
        if (glowSteps == null || glowSource == null || titleGroup.alpha <= 0) return;
        Graphics.Blit(glowSource, glowSteps[0]); Graphics.Blit(glowSteps[0], glowSteps[1]); Graphics.Blit(glowSteps[1], glowSteps[2]);
    }

    void OnDestroy()
    {
        if (videoTarget != null) Destroy(videoTarget);
        if (glowSteps != null) foreach (var step in glowSteps) if (step != null) { step.Release(); Destroy(step); }
        MusicManager.SetPaused(false);
    }

    // Any key, mouse button, gamepad button, or touch. Listening starts only once the title is shown,
    // so the click that pressed Skip never carries over.
    static IEnumerator WaitForAnyPress(System.Action whileWaiting)
    {
        bool pressed = false;
        using (InputSystem.onAnyButtonPress.CallOnce(_ => pressed = true))
            while (!pressed && !(Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame))
            {
                whileWaiting?.Invoke();
                yield return null;
            }
    }

    static IEnumerator Fade(CanvasGroup group, float to, float duration, CanvasGroup alongside = null)
    {
        float from = group.alpha, alongsideFrom = alongside != null ? alongside.alpha : 0;
        for (float t = 0; t < 1; t += Time.unscaledDeltaTime / duration)
        {
            float eased = Mathf.SmoothStep(0, 1, t);
            group.alpha = Mathf.Lerp(from, to, eased);
            if (alongside != null) alongside.alpha = Mathf.Lerp(alongsideFrom, to, eased);
            yield return null;
        }
        group.alpha = to;
        if (alongside != null) alongside.alpha = to;
    }

    void EnsureCameraAndEventSystem()
    {
        if (Camera.main == null)
        {
            var cameraObject = new GameObject("Intro Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;
        }
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    void BuildUI()
    {
        bodyFont = bodyFont != null ? bodyFont : Resources.Load<Font>("MinigameTheme/ComicNeue-Regular");
        boldFont = boldFont != null ? boldFont : Resources.Load<Font>("MinigameTheme/ComicNeue-Bold");
        titleFont = titleFont != null ? titleFont : Resources.Load<Font>("MainMenuNew/StorySerif");
        var fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (bodyFont == null) bodyFont = fallback;
        if (boldFont == null) boldFont = fallback;
        if (titleFont == null) titleFont = boldFont;

        // Same 1920x1080 baseline as the loading screen; mobile mode matches a 720-high screen so text reads larger.
        bool mobile = MobileControls.Enabled;
        var canvasObject = new GameObject("Intro Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = mobile ? new Vector2(1280, 720) : new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = mobile ? 1f : 0.5f;
        var canvas = canvasObject.transform;

        Stretch(Image("Black", canvas, Color.black).rectTransform);
        var art = Resources.Load<Texture2D>("MenuArt/Background3D");

        // Loading screen: the menu artwork, dimmed, with "Loading..." over a thin gold bar.
        loadingGroup = Group("Loading", canvas);
        Backdrop(loadingGroup.transform, art, new Color(0.5f, 0.48f, 0.46f));
        loadingLabel = Label("Label", loadingGroup.transform, "Loading.", boldFont, mobile ? 38 : 36, new Color(0.96f, 0.90f, 0.78f), TextAnchor.MiddleCenter);
        var loadingRect = loadingLabel.rectTransform;
        loadingRect.anchorMin = loadingRect.anchorMax = new Vector2(0.5f, 0.5f);
        loadingRect.sizeDelta = new Vector2(600, 60); loadingRect.anchoredPosition = new Vector2(0, 40);
        var track = Image("Bar", loadingGroup.transform, new Color(0.10f, 0.09f, 0.07f, 0.85f));
        track.rectTransform.anchorMin = track.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        track.rectTransform.sizeDelta = new Vector2(mobile ? 560 : 520, 10); track.rectTransform.anchoredPosition = new Vector2(0, -10);
        var fill = Image("Fill", track.transform, new Color(0.83f, 0.67f, 0.37f));
        loadingFill = fill.rectTransform;
        loadingFill.anchorMin = Vector2.zero; loadingFill.anchorMax = new Vector2(0, 1); loadingFill.offsetMin = loadingFill.offsetMax = Vector2.zero;

        // Content notice: the same ink panel, paper text, and gold accents as story dialogue and choices.
        disclaimerGroup = Group("Disclaimer", canvas);
        Backdrop(disclaimerGroup.transform, art, new Color(0.32f, 0.40f, 0.41f));
        var cardEdge = Image("Card", disclaimerGroup.transform, Gold);
        cardEdge.rectTransform.anchorMin = cardEdge.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        cardEdge.rectTransform.sizeDelta = mobile ? new Vector2(1120, 640) : new Vector2(1180, 700);
        var card = Image("Fill", cardEdge.transform, InkPanel);
        Stretch(card.rectTransform); card.rectTransform.offsetMin = new Vector2(2, 2); card.rectTransform.offsetMax = new Vector2(-2, -2);
        var heading = Label("Title", card.transform, DisclaimerTitle, titleFont, mobile ? 44 : 46, Gold, TextAnchor.UpperLeft);
        Pin(heading.rectTransform, 60, 50, 60, -1, 60);
        var body = Label("Body", card.transform, DisclaimerBody, bodyFont, mobile ? 30 : 30, Paper, TextAnchor.UpperLeft);
        body.horizontalOverflow = HorizontalWrapMode.Wrap; body.verticalOverflow = VerticalWrapMode.Truncate;
        body.resizeTextForBestFit = true; body.resizeTextMinSize = 20; body.resizeTextMaxSize = body.fontSize; body.lineSpacing = 1.1f;
        Pin(body.rectTransform, 60, 130, 60, mobile ? 140 : 150);
        disclaimerNote = Label("Note", card.transform, "", bodyFont, 24, Gold, TextAnchor.MiddleLeft);
        Pin(disclaimerNote.rectTransform, 60, -1, 480, 50, 60);
        proceedButton = CardButton(card.transform, "Proceed  ›", true, mobile);
        ((RectTransform)proceedButton.transform).anchoredPosition = mobile ? new Vector2(-48, 40) : new Vector2(-56, 44);
        exitButton = CardButton(card.transform, "Exit", false, mobile);
        ((RectTransform)exitButton.transform).anchoredPosition = mobile ? new Vector2(-288, 40) : new Vector2(-276, 44);

        // Story video, letterboxed to its own aspect ratio.
        videoGroup = Group("Video", canvas);
        videoFrame = Image("Frame", videoGroup.transform, Color.white);
        videoFrame.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        player = gameObject.AddComponent<VideoPlayer>();
        videoAudio = gameObject.AddComponent<AudioSource>();
        player.playOnAwake = false;
        player.isLooping = false;
        player.source = VideoSource.Url;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.audioOutputMode = VideoAudioOutputMode.AudioSource;
        player.SetTargetAudioSource(0, videoAudio);

        // Game title over black, with the "press any button" prompt beneath it.
        // The main menu's artwork (MenuArt/Background3D, the sunset hill) sits behind it, so the menu follows on naturally.
        titleGroup = Group("Title", canvas);
        if (art != null)
        {
            // The 3D title has dark navy letters, so the artwork is left almost as bright as it is in the menu.
            bool brightBackdrop = titleModel != null && titleModel.Ready;
            var backdrop = Image("Backdrop", titleGroup.transform, brightBackdrop ? new Color(0.94f, 0.92f, 0.9f) : new Color(0.62f, 0.6f, 0.58f), art);
            var fitter = backdrop.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = (float)art.width / art.height;
        }
        bool model = titleModel != null && titleModel.Ready;
        Texture logo = model ? titleModel.Texture : Resources.Load<Texture2D>("MenuArt/title");
        float logoHeight = mobile ? 322 : 412;
        var logoSize = new Vector2(model ? logoHeight * titleModel.Aspect : logoHeight * 1100f / 412f, logoHeight);
        // A soft dark halo behind the title, as on the main menu (.logo-glow), so it stands off the bright sky. uGUI cannot
        // blur, so the logo is shrunk to an eighth in three halving steps (LateUpdate, as the 3D title animates) and drawn
        // back at full size in black: the stretch is the blur.
        glowSteps = new RenderTexture[3];
        for (int i = 0; i < glowSteps.Length; i++)
            glowSteps[i] = new RenderTexture(Mathf.Max(8, logo.width >> (i + 1)), Mathf.Max(8, logo.height >> (i + 1)), 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Bilinear };
        Graphics.Blit(logo, glowSteps[0]); Graphics.Blit(glowSteps[0], glowSteps[1]); Graphics.Blit(glowSteps[1], glowSteps[2]);
        var glow = Image("Logo glow", titleGroup.transform, new Color(0, 0, 0, 0.85f), glowSteps[2]);
        glow.rectTransform.sizeDelta = logoSize * 1.04f;
        glow.rectTransform.anchoredPosition = new Vector2(0, 50);
        glowSource = logo;
        var title = Image("Logo", titleGroup.transform, Color.white, logo);
        title.rectTransform.sizeDelta = logoSize;
        title.rectTransform.anchoredPosition = new Vector2(0, 50);

        promptGroup = Group("Prompt", canvas);
        prompt = Label("Text", promptGroup.transform, "", bodyFont, mobile ? 30 : 32, new Color(0.96f, 0.90f, 0.78f), TextAnchor.MiddleCenter);
        // A dark edge as well as the shadow: the prompt sits over the light stone path of the menu artwork.
        var outline = prompt.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0, 0, 0, 0.75f); outline.effectDistance = new Vector2(1.5f, -1.5f);
        var shadow = prompt.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.85f); shadow.effectDistance = new Vector2(2, -2);
        var promptRect = prompt.rectTransform;
        promptRect.anchorMin = new Vector2(0, 0.5f); promptRect.anchorMax = new Vector2(1, 0.5f);
        promptRect.sizeDelta = new Vector2(0, 60);
        promptRect.anchoredPosition = new Vector2(0, mobile ? -170 : -210);

        // Skip button in the bottom-right corner: a gold border around a dark fill, like the menu buttons.
        skipGroup = Group("Skip", canvas);
        var border = Image("Button", skipGroup.transform, new Color(0.78f, 0.67f, 0.47f, 0.9f));
        border.raycastTarget = true;
        var skipRect = border.rectTransform;
        skipRect.anchorMin = skipRect.anchorMax = skipRect.pivot = new Vector2(1, 0);
        skipRect.sizeDelta = mobile ? new Vector2(200, 72) : new Vector2(180, 60);
        skipRect.anchoredPosition = mobile ? new Vector2(-40, 32) : new Vector2(-56, 48);
        var skip = Image("Fill", border.transform, new Color(0.16f, 0.14f, 0.11f, 0.92f));
        Stretch(skip.rectTransform);
        skip.rectTransform.offsetMin = new Vector2(2, 2); skip.rectTransform.offsetMax = new Vector2(-2, -2);
        var button = border.gameObject.AddComponent<Button>();
        button.targetGraphic = skip;
        var colors = button.colors; colors.highlightedColor = new Color(1.25f, 1.2f, 1.1f); colors.pressedColor = new Color(0.8f, 0.8f, 0.8f); button.colors = colors;
        button.onClick.AddListener(() => skipRequested = true);
        Stretch(Label("Label", skip.transform, "Skip  ›", boldFont, mobile ? 30 : 26, new Color(0.96f, 0.90f, 0.78f), TextAnchor.MiddleCenter).rectTransform);
    }

    // The menu artwork filling the screen, tinted (darker for text to sit on).
    static void Backdrop(Transform parent, Texture2D art, Color tint)
    {
        if (art == null) return;
        var backdrop = Image("Backdrop", parent, tint, art);
        var fitter = backdrop.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = (float)art.width / art.height;
    }

    // A button in the disclaimer card's bottom-right corner: gold for the main choice, teal for the secondary choice.
    Button CardButton(Transform card, string text, bool primary, bool mobile)
    {
        var edge = Image(text, card, Gold);
        edge.raycastTarget = true;
        var rect = edge.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0);
        rect.sizeDelta = mobile ? new Vector2(220, 76) : new Vector2(200, 64);
        var face = Image("Fill", edge.transform, primary ? Gold : Teal);
        Stretch(face.rectTransform); face.rectTransform.offsetMin = new Vector2(2, 2); face.rectTransform.offsetMax = new Vector2(-2, -2);
        var button = edge.gameObject.AddComponent<Button>();
        button.targetGraphic = face;
        var colors = button.colors; colors.highlightedColor = new Color(1.18f, 1.14f, 1.08f); colors.selectedColor = colors.highlightedColor; colors.pressedColor = new Color(0.8f, 0.8f, 0.8f); button.colors = colors;
        Stretch(Label("Label", face.transform, text, boldFont, mobile ? 30 : 28, primary ? Ink : Paper, TextAnchor.MiddleCenter).rectTransform);
        return button;
    }

    // Pins a rect inside its parent by edge margins; -1 leaves that edge free, using the given height instead.
    static void Pin(RectTransform rect, float left, float top, float right, float bottom, float height = 0)
    {
        rect.anchorMin = new Vector2(0, top < 0 ? 0 : bottom < 0 ? 1 : 0);
        rect.anchorMax = new Vector2(1, top < 0 ? 0 : 1);
        rect.pivot = new Vector2(0.5f, top < 0 ? 0 : 1);
        if (bottom < 0) { rect.offsetMin = new Vector2(left, -top - height); rect.offsetMax = new Vector2(-right, -top); }
        else if (top < 0) { rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, bottom + height); }
        else { rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top); }
    }

    static CanvasGroup Group(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Stretch(rect);
        var group = rect.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0;
        group.blocksRaycasts = false;
        return group;
    }

    static RawImage Image(string name, Transform parent, Color color, Texture texture = null)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        image.rectTransform.SetParent(parent, false);
        image.color = color;
        image.texture = texture;
        image.raycastTarget = false;
        return image;
    }

    static Text Label(string name, Transform parent, string message, Font font, int size, Color color, TextAnchor alignment)
    {
        var label = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        label.rectTransform.SetParent(parent, false);
        label.font = font;
        label.fontSize = size;
        label.color = color;
        label.text = message;
        label.alignment = alignment;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        return label;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
