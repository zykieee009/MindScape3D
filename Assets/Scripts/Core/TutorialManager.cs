using Mindscape.AcademicPressure;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

// The tutorial uses the same objective document as the story chapters.
// It teaches movement and the menu without opening the legacy Tasks panel or Journal.
public class TutorialManager : MonoBehaviour
{
    enum Step { Move, OpenPause, Done }

    [Header("References — auto-found if left empty")]
    [SerializeField] PlayerControls player;
    [SerializeField] PlayerInputBindings input;
    [SerializeField] PauseMenu pauseMenu;

    [Header("Tutorial Completion")]
    [SerializeField] string gameSceneName = "Chapter 1 - Episode 1";
    [SerializeField] string playButtonText = "Play Game";

    [Header("Prompts")]
    [SerializeField] string movePrompt = "Use WASD to move around";
    [SerializeField] string openPausePrompt = "Press Esc to open the Menu";
    [SerializeField] string donePrompt = "Tutorial complete!";

    [Header("Prompts with touch controls")]
    [SerializeField] string moveTouchPrompt = "Use the joystick to move around";
    [SerializeField] string openPauseTouchPrompt = "Tap Menu to open the Menu";

    [Header("Move Detection")]
    [SerializeField] float moveDistanceThreshold = 1f;

    Step step = Step.Move;
    Vector3 startPosition;
    VisualElement objective;
    Label stepLabel, title, checklist;
    Button playGameButton;

    bool Touch => MobileControls.Enabled;

    void Awake()
    {
        if (player == null) player = FindAnyObjectByType<PlayerControls>();
        if (input == null && player != null) input = player.GetComponent<PlayerInputBindings>();
        if (pauseMenu == null) pauseMenu = FindAnyObjectByType<PauseMenu>();

        var ui = EpisodeUI.Load();
        var document = ui != null ? EpisodeUI.Spawn("Tutorial objective", ui.objective, 0, transform) : null;
        if (document == null) return;

        var root = document.rootVisualElement;
        objective = root.Q<VisualElement>("objective");
        stepLabel = root.Q<Label>("objective-chapter");
        title = root.Q<Label>("objective-title");
        checklist = root.Q<Label>("objective-checklist");
        if (objective == null || stepLabel == null || title == null || checklist == null) return;

        // The shared chapter objective UXML has old inline preview colors and dimensions.
        // Set its live appearance to the chapter HUD palette and compact top-left layout.
        objective.style.left = 21;
        objective.style.top = 21;
        objective.style.height = StyleKeyword.Auto;
        objective.style.backgroundColor = (Color)new Color32(29, 43, 47, 247);
        objective.EnableInClassList("mobile", Touch);
        stepLabel.style.color = (Color)new Color32(219, 179, 111, 255);
        title.style.color = (Color)new Color32(242, 232, 209, 255);
        title.style.fontSize = 25;
        checklist.style.color = (Color)new Color32(242, 232, 209, 255);
        checklist.style.fontSize = 20;

        playGameButton = new Button(PlayGame) { text = playButtonText };
        playGameButton.style.alignSelf = Align.FlexStart;
        playGameButton.style.marginTop = 12;
        playGameButton.style.paddingLeft = 20;
        playGameButton.style.paddingRight = 20;
        playGameButton.style.height = 42;
        playGameButton.style.backgroundColor = (Color)new Color32(219, 179, 111, 255);
        playGameButton.style.color = (Color)new Color32(29, 43, 47, 255);
        playGameButton.style.fontSize = 18;
        playGameButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        playGameButton.style.display = DisplayStyle.None;
        objective.Add(playGameButton);
    }

    void Start()
    {
        startPosition = player != null ? player.transform.position : Vector3.zero;
        ShowStep();
    }

    void Update()
    {
        if (player == null || input == null) return;

        if (objective != null)
            objective.style.display = player.pauseOpen ? DisplayStyle.None : DisplayStyle.Flex;

        switch (step)
        {
            case Step.Move:
                if (HasMoved()) Advance(Step.OpenPause);
                break;
            case Step.OpenPause:
                if (player.pauseOpen) Advance(Step.Done);
                break;
        }
    }

    bool HasMoved()
    {
        Vector2 move = input.Move != null ? input.Move.ReadValue<Vector2>() : Vector2.zero;
        return move.sqrMagnitude > 0.01f ||
               (player.transform.position - startPosition).sqrMagnitude > moveDistanceThreshold * moveDistanceThreshold;
    }

    void Advance(Step next)
    {
        step = next;
        ShowStep();
        if (next != Step.Done) return;

        // The final step completes when the player opens the menu. Close it explicitly
        // so the chapter start action is visible immediately.
        if (pauseMenu != null) pauseMenu.SetVisible(false);
        if (objective != null) objective.style.display = DisplayStyle.Flex;
    }

    void ShowStep()
    {
        if (stepLabel == null || title == null || checklist == null) return;
        stepLabel.text = step == Step.Done ? "TUTORIAL  /  COMPLETE" : "TUTORIAL  /  STEP " + ((int)step + 1).ToString("00") + " OF 02";
        title.text = step == Step.Move ? "Move around" : step == Step.OpenPause ? "Open the menu" : donePrompt;
        checklist.text = step == Step.Move ? (Touch ? moveTouchPrompt : movePrompt) :
                         step == Step.OpenPause ? (Touch ? openPauseTouchPrompt : openPausePrompt) :
                         "You're ready to begin Chapter 1.";
        if (playGameButton != null)
            playGameButton.style.display = step == Step.Done ? DisplayStyle.Flex : DisplayStyle.None;
    }

    void PlayGame()
    {
        Time.timeScale = 1f;
        EpisodeLoadingScreen.Load(gameSceneName, 1);
    }
}
