using UnityEngine;
using UnityEngine.UI;

// Tasks, pause-state, and the HUD. Movement and camera
// live in PlayerMovement.cs; input action lookup lives in PlayerInputBindings.cs.
// Runs before PlayerMovement so overlay-open state is up to date the same frame it changes.
[DefaultExecutionOrder(-1)]
[RequireComponent(typeof(PlayerInputBindings))]
public class PlayerControls : MonoBehaviour
{
    [Header("HUD")]
    public bool tasksOpen;
    public bool pauseOpen;
    public bool dialogueOpen;
    public bool questOpen;
    public bool minigameOpen;
    [SerializeField] bool tasksAvailable = true;
    public Color hudButtonColor = new Color(0.02f, 0.10f, 0.18f, 0.92f);

    [Header("Stress")]
    public float stressLevel = 10f;
    public float stressMax = 100f;
    [Min(0.1f)] public float criticalStressDuration = 10f;

    public bool IsStressCritical => _criticalStressActive;
    public float CriticalStressRemaining { get; private set; }

    [Header("HUD References — assign the real UI objects here")]
    [SerializeField] GameObject hudRoot;
    [SerializeField] PauseMenu pauseMenu;
    [SerializeField] Button tasksButton;
    [SerializeField] Button menuButton;
    [SerializeField] Image tasksButtonBackground;
    [SerializeField] Image menuButtonBackground;
    [SerializeField] Color hudButtonActiveColor = new Color(0.04f, 0.20f, 0.34f, 0.98f);
    [SerializeField] GameObject tasksPanelRoot;
    [SerializeField] GameObject hudPanelOverlay;
    [SerializeField] Button tasksCloseButton;
    [SerializeField] Transform tasksTextRoot;

    private PlayerInputBindings _input;
    bool _criticalStressActive;
    bool _stressGameOverTriggered;

    void Awake()
    {
        _input = GetComponent<PlayerInputBindings>();

        if (tasksButton != null) tasksButton.onClick.AddListener(ToggleTasks);
        if (menuButton != null && pauseMenu != null) menuButton.onClick.AddListener(pauseMenu.TogglePause);
        if (tasksCloseButton != null) tasksCloseButton.onClick.AddListener(CloseTasks);
        UpdateHudVisuals();
    }

    void Update()
    {
        HandleHudToggle();
        UpdateCriticalStress();
        UpdateHudVisuals();
    }

    void HandleHudToggle()
    {
        if (tasksAvailable && _input.OpenTasks != null && _input.OpenTasks.WasPressedThisFrame())
            ToggleTasks();

        // ESC / pause is handled by PauseMenu.cs
    }

    public void ToggleTasks()
    {
        if (!tasksAvailable) return;
        QuestHUDController.Instance?.Close();
        tasksOpen = !tasksOpen;
        pauseOpen = false;

        UpdateHudVisuals();
    }

    public void CloseTasks()
    {
        tasksOpen = false;
        UpdateHudVisuals();
    }

    // Movement, camera look, and the crosshair (in PlayerMovement.cs) all pause while any full-screen panel is open.
    public bool IsOverlayOpen()
    {
        return tasksOpen || pauseOpen || dialogueOpen || questOpen || minigameOpen;
    }

    public void ChangeStress(float amount)
    {
        stressLevel = Mathf.Clamp(stressLevel + amount, 0f, stressMax);
        if (stressLevel < stressMax)
        {
            _criticalStressActive = false;
            _stressGameOverTriggered = false;
            CriticalStressRemaining = 0f;
        }
        UpdateHudVisuals();
    }

    void UpdateCriticalStress()
    {
        if (stressMax <= 0f || stressLevel < stressMax)
        {
            _criticalStressActive = false;
            _stressGameOverTriggered = false;
            CriticalStressRemaining = 0f;
            return;
        }

        if (_stressGameOverTriggered) return;

        if (!_criticalStressActive)
        {
            _criticalStressActive = true;
            CriticalStressRemaining = Mathf.Max(0.1f, criticalStressDuration);
        }

        CriticalStressRemaining -= Time.unscaledDeltaTime;
        if (CriticalStressRemaining > 0f) return;

        CriticalStressRemaining = 0f;
        _criticalStressActive = false;
        _stressGameOverTriggered = true;
        QuestHUDController.Instance?.ShowStressGameOver();
    }

    void UpdateHudVisuals()
    {
        if (hudPanelOverlay != null)
            hudPanelOverlay.SetActive(tasksOpen);

        if (hudRoot != null)
            hudRoot.SetActive(!pauseOpen);

        SetHudButtonActive(tasksButtonBackground, tasksOpen);
        SetHudButtonActive(menuButtonBackground, pauseOpen);

        if (tasksPanelRoot != null)
            tasksPanelRoot.SetActive(tasksOpen);
    }

    void SetHudButtonActive(Image background, bool active)
    {
        if (background == null) return;
        background.color = active ? hudButtonActiveColor : hudButtonColor;
    }

}
