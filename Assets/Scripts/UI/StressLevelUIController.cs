using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Keeps the always-visible stress HUD in sync with the active PlayerControls.
/// The visual tree stays readable during normal episode play.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class StressLevelUIController : MonoBehaviour
{
    [SerializeField] float refreshInterval = 0.05f;
    [SerializeField] bool useChapterLayout;

    UIDocument document;
    VisualElement root;
    VisualElement hud;
    VisualElement fill;
    Label valueLabel;
    Label stateLabel;
    Label criticalTimerLabel;
    PlayerControls playerControls;
    float refreshTimer;

    static readonly Color CalmColor = new Color(88f / 255f, 112f / 255f, 83f / 255f);
    static readonly Color FocusColor = new Color(157f / 255f, 111f / 255f, 47f / 255f);
    static readonly Color AlertColor = new Color(149f / 255f, 58f / 255f, 43f / 255f);

    void Awake()
    {
        document = GetComponent<UIDocument>();
        playerControls = FindAnyObjectByType<PlayerControls>();
    }

    void OnEnable()
    {
        InitializeUI();
    }

    void Start()
    {
        if (root == null) InitializeUI();
        Refresh();
    }

    void Update()
    {
        if (playerControls == null)
            playerControls = FindAnyObjectByType<PlayerControls>();

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer > 0f) return;

        refreshTimer = refreshInterval;
        Refresh();
    }

    void InitializeUI()
    {
        if (document == null) document = GetComponent<UIDocument>();
        root = document != null ? document.rootVisualElement : null;
        if (root == null) return;

        hud = root.Q<VisualElement>("stress-hud");
        if (hud != null) hud.EnableInClassList("chapter-status", useChapterLayout);
        fill = root.Q<VisualElement>("stress-fill");
        valueLabel = root.Q<Label>("stress-value");
        stateLabel = root.Q<Label>("stress-state");
        criticalTimerLabel = root.Q<Label>("stress-critical-timer");
    }

    void Refresh()
    {
        if (root == null || playerControls == null) return;
        if (hud != null)
            hud.style.display = PauseMenu.Instance != null && PauseMenu.Instance.IsPaused ? DisplayStyle.None : DisplayStyle.Flex;

        float max = Mathf.Max(0.01f, playerControls.stressMax);
        float current = Mathf.Clamp(playerControls.stressLevel, 0f, max);
        float pct = current / max;
        bool critical = playerControls.IsStressCritical;
        Color color = critical || pct >= 0.8f ? AlertColor : pct < 0.5f ? CalmColor : FocusColor;
        string state = critical ? "CRITICAL" : pct < 0.5f ? "STEADY" : pct < 0.8f ? "RISING" : "OVERLOAD";

        if (fill != null)
        {
            fill.style.width = new Length(pct * 100f, LengthUnit.Percent);
            fill.style.backgroundColor = color;
        }

        if (valueLabel != null) valueLabel.text = Mathf.RoundToInt(current) + " / " + Mathf.RoundToInt(max);
        if (stateLabel != null)
        {
            stateLabel.text = state;
            stateLabel.style.color = color;
        }

        if (criticalTimerLabel != null)
        {
            criticalTimerLabel.text = "CRITICAL  " + Mathf.CeilToInt(playerControls.CriticalStressRemaining) + "s";
            criticalTimerLabel.EnableInClassList("hidden", !critical);
        }
    }

    public void HideForGameOver()
    {
        if (root != null)
            root.style.display = DisplayStyle.None;
    }
}
