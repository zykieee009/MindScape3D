using Mindscape.Minigames;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// On-screen move stick (bottom-left), look stick + Interact/Journal buttons (bottom-right), and a pause button in the
// top-right corner, for touch platforms.
// Visibility is controlled by the persisted Game Settings checkbox so it works in WebGL previews, desktop testing,
// and mobile builds consistently. The Tutorial and Chapter 2 Episode 1 carry a hand-placed overlay
// (Tools > Build Mobile Controls); every other gameplay scene gets the same overlay built at runtime.
public class MobileControls : MonoBehaviour
{
    const string MobileControlsKey = "Mindscape.MobileControls";

    // Until the player changes the Game Settings checkbox, the overlay is on only where there is no keyboard and
    // mouse by default: phones and tablets, including mobile browsers running the web build.
    public static bool DefaultOn => Application.isMobilePlatform;

    // Whether the touch overlay is wanted (the saved choice, or the platform default).
    public static bool Enabled => PlayerPrefs.GetInt(MobileControlsKey, DefaultOn ? 1 : 0) == 1;

    [Header("Testing / Experimental")]
    [Tooltip("Show the mobile control overlay even when not running on Android. For testing on PC only.")]
    [SerializeField] bool forceEnableOnDesktop = false;

    [Header("References")]
    [SerializeField] GameObject controlsRoot;
    [SerializeField] Button interactButton;
    [SerializeField] Button journalButton;

    static readonly Color Brass = new Color32(189, 164, 119, 245), Dark = new Color32(48, 45, 40, 245), Cream = new Color32(240, 228, 204, 255);

    // Bottom-right cluster, from the bottom-right corner at the 1920x1080 reference: the look stick mirrors the move
    // stick, with Interact and Journal to its left where the right thumb reaches them.
    public static readonly Vector2 LookStickPosition = new Vector2(-155, 150);
    public static readonly Vector2 InteractPosition = new Vector2(-375, 110);
    public static readonly Vector2 JournalPosition = new Vector2(-375, 240);
    const string LookStickName = "Look Joystick";
    const string ViewButtonName = "ViewButton";
    public static readonly Vector2 ViewPosition = new Vector2(-155, 335);

    PlayerControls _playerControls;
    bool _hiddenForGameOver;
    GameObject _pauseRoot;
    IPausable _pausable;
    float _nextPausableSearch;

    // ---------------------------------------------------------------- automatic overlay

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureOverlay();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureOverlay();

    // Gameplay scenes are the ones with a walking player; menus, cutscenes and minigame screens are left alone.
    static void EnsureOverlay()
    {
        if (!Enabled || FindAnyObjectByType<MobileControls>() != null || FindAnyObjectByType<PlayerMovement>() == null) return;
        MinigameUI.EnsureInput();
        var root = new GameObject("Mobile Controls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.SetActive(false);   // Awake runs once the references below are set.
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 50;
        Scale(root);
        var controls = Rect("Controls Root", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        Stick("Joystick", controls, Vector2.zero, new Vector2(155, 150), false);

        var component = root.AddComponent<MobileControls>();
        component.controlsRoot = controls.gameObject;
        component.interactButton = RoundButton("InteractButton", controls, new Vector2(1, 0), InteractPosition, 112, "E", 36);
        component.journalButton = RoundButton("JournalButton", controls, new Vector2(1, 0), JournalPosition, 84, "JOURNAL", 14);
        root.SetActive(true);
    }

    // ---------------------------------------------------------------- lifecycle

    void Awake()
    {
        _playerControls = FindAnyObjectByType<PlayerControls>();

        if (interactButton != null) interactButton.onClick.AddListener(MobileInput.TriggerInteract);
        if (journalButton != null) journalButton.onClick.AddListener(OnJournalPressed);
        EnsureLookStick();
        BuildPauseButton();
    }

    // The hand-placed overlays (Tutorial, Chapter 2 Episode 1) predate the look stick, so it is added here for every
    // overlay, and the buttons move left to make room for it.
    void EnsureLookStick()
    {
        if (controlsRoot == null || controlsRoot.transform.Find(LookStickName) != null) return;
        Stick(LookStickName, controlsRoot.transform, new Vector2(1, 0), LookStickPosition, true);
        if (interactButton != null) ((RectTransform)interactButton.transform).anchoredPosition = InteractPosition;
        if (journalButton != null) ((RectTransform)journalButton.transform).anchoredPosition = JournalPosition;
        EnsureViewButton();
    }

    // Phones have no scroll wheel, so a button above the look stick switches between third and first person.
    void EnsureViewButton()
    {
        if (controlsRoot == null || controlsRoot.transform.Find(ViewButtonName) != null) return;
        RoundButton(ViewButtonName, controlsRoot.transform, new Vector2(1, 0), ViewPosition, 84, "VIEW", 16).onClick.AddListener(MobileInput.TriggerViewToggle);
    }

    void Update()
    {
        if (_hiddenForGameOver)
        {
            if (controlsRoot != null) controlsRoot.SetActive(false);
            if (_pauseRoot != null) _pauseRoot.SetActive(false);
            return;
        }

        // The Game Settings checkbox is persisted by MainMenuController. Read it
        // here so the preference applies after loading any gameplay scene, including
        // WebGL and desktop previews where UNITY_ANDROID is false.
        bool enabledInSettings = Enabled;
        bool journalOpen = QuestHUDController.Instance != null && QuestHUDController.Instance.IsOpen;
        bool shouldShow = (enabledInSettings || forceEnableOnDesktop) && !journalOpen;
        if (controlsRoot != null && controlsRoot.activeSelf != shouldShow)
            controlsRoot.SetActive(shouldShow);
        // Scenes without the task journal (most story episodes) have nothing for this button to open.
        bool showJournal = QuestHUDController.Instance != null && QuestHUDController.Instance.JournalAvailable;
        if (journalButton != null && journalButton.gameObject.activeSelf != showJournal)
            journalButton.gameObject.SetActive(showJournal);

        // The pause menu has its own Resume button, so the corner button hides while the game is paused.
        var pausable = FindPausable();
        bool showPause = shouldShow && pausable != null && !pausable.IsPaused && !EpisodeLoadingScreen.IsActive;
        if (_pauseRoot != null && _pauseRoot.activeSelf != showPause)
            _pauseRoot.SetActive(showPause);
    }

    public void HideForGameOver()
    {
        _hiddenForGameOver = true;
        if (controlsRoot != null) controlsRoot.SetActive(false);
        if (_pauseRoot != null) _pauseRoot.SetActive(false);
    }

    void LateUpdate()
    {
        MobileInput.ClearFrameState();
    }

    static bool IsMobilePlatform()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return true;
#else
        return false;
#endif
    }

    void OnJournalPressed()
    {
        QuestHUDController.Instance?.Toggle();
    }

    // ---------------------------------------------------------------- pause

    // Story episodes run their own pause menu; the UI Toolkit PauseMenu is the fallback (Tutorial).
    IPausable FindPausable()
    {
        if (_pausable is Object found && found == null) _pausable = null;
        if (_pausable != null || Time.unscaledTime < _nextPausableSearch) return _pausable;
        _nextPausableSearch = Time.unscaledTime + 1f;
        IPausable fallback = null;
        foreach (var behaviour in FindObjectsByType<MonoBehaviour>())
        {
            if (!(behaviour is IPausable pausable) || !behaviour.isActiveAndEnabled) continue;
            if (!(pausable is PauseMenu)) return _pausable = pausable;
            fallback = pausable;
        }
        return _pausable = fallback;
    }

    // Lives on its own canvas above the episode HUDs (sorting 200-210) so nothing in the corner can cover it,
    // and below minigames (250+) and the loading screen.
    void BuildPauseButton()
    {
        _pauseRoot = new GameObject("Pause Button", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        _pauseRoot.transform.SetParent(transform, false);
        var rect = (RectTransform)_pauseRoot.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var canvas = _pauseRoot.GetComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = 230;
        // Chapter 2 Episode 1 keeps its Journal [J] button in the top-right corner, so pause sits just below it.
        float top = FindAnyObjectByType<Mindscape.EpisodeOne.EpisodeOneStory>() != null ? -150 : -72;
        var button = RoundButton("PauseButton", rect, new Vector2(1, 1), new Vector2(-72, top), 92, "", 0);
        for (int i = 0; i < 2; i++)
        {
            var bar = Rect("Bar", button.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(i == 0 ? -11 : 11, 0), new Vector2(12, 36));
            var image = bar.gameObject.AddComponent<Image>(); image.color = Cream; image.raycastTarget = false;
        }
        button.onClick.AddListener(() => FindPausable()?.TogglePause());
        _pauseRoot.SetActive(false);
    }

    // ---------------------------------------------------------------- runtime UI, matching MobileControlsBuilder's theme

    static void Stick(string name, Transform parent, Vector2 anchor, Vector2 position, bool look)
    {
        var stick = Circle(name, parent, anchor, position, 230, Brass);
        Circle("Inset", stick, new Vector2(.5f, .5f), Vector2.zero, 204, Dark).GetComponent<Image>().raycastTarget = false;
        var handle = Circle("Handle", stick, new Vector2(.5f, .5f), Vector2.zero, 98, Cream);
        handle.GetComponent<Image>().raycastTarget = false;
        stick.gameObject.AddComponent<MobileJoystick>().SetHandle(handle, 70f, look);
    }

    static void Scale(GameObject root)
    {
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position; rect.sizeDelta = size;
        return rect;
    }

    static RectTransform Circle(string name, Transform parent, Vector2 anchor, Vector2 position, float size, Color color)
    {
        var rect = Rect(name, parent, anchor, anchor, position, new Vector2(size, size));
        var image = rect.gameObject.AddComponent<Image>(); image.sprite = MinigameUI.Circle; image.color = color;
        return rect;
    }

    static Button RoundButton(string name, Transform parent, Vector2 anchor, Vector2 position, float size, string label, int fontSize)
    {
        var rect = Circle(name, parent, anchor, position, size, Brass);
        Circle("Inset", rect, new Vector2(.5f, .5f), Vector2.zero, size - 10, Dark).GetComponent<Image>().raycastTarget = false;
        var button = rect.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, .95f, .78f, 1f);
        colors.pressedColor = new Color(.83f, .72f, .52f, 1f);
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        if (!string.IsNullOrEmpty(label))
        {
            var text = Rect("Label", rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<TextMeshProUGUI>();
            text.text = label; text.font = MinigameUI.Font(false); text.fontSize = fontSize; text.fontStyle = FontStyles.Bold; text.color = Cream;
            text.alignment = TextAlignmentOptions.Center; text.textWrappingMode = TextWrappingModes.NoWrap; text.raycastTarget = false;
        }
        return button;
    }
}
