using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using Mindscape.AcademicPressure;
using Mindscape.Data;

// Pause menu (UI Toolkit). Esc / the mobile pause button opens it: Resume, Settings (music + SFX volume, saved in
// PlayerPrefs "Mindscape.Music" / "Mindscape.SFX"), Save game slots and Main Menu. A story episode can host it by
// setting `owner`. One per scene; reach it through PauseMenu.Instance (can be null).
[RequireComponent(typeof(UIDocument))]
public class PauseMenu : MonoBehaviour, IPausable
{
    public static PauseMenu Instance { get; private set; }

    const string MusicKey = "Mindscape.Music";
    const string SfxKey = "Mindscape.SFX";

    private UIDocument _document;
    private VisualElement _root;
    private VisualElement _overlay;
    private VisualElement _pagePause;
    private VisualElement _pageSettings;
    private VisualElement _pageSave;
    private Button _btnSave, _btnSaveBack;
    private readonly Button[] _saveSlots = new Button[MindscapeSaves.SlotCount];
    private Label _saveStatus;
    private int _confirmSlot = -1;   // a used slot asks to be clicked again before it is replaced

    private Button _btnResume;
    private Button _btnJournal;
    private Button _btnSettings;
    private Button _btnMainMenu;
    private Button _btnSettingsBack;
    private Button _btnSettingsDone;

    private Slider _sliderMusic;
    private Slider _sliderSfx;
    private Label _labelMusicValue;
    private Label _labelSfxValue;
    private Label _labelChapter;

    private PlayerControls _player;
    private bool _isPaused;
    private InputAction _pauseAction;

    public bool IsPaused => _isPaused;
    // True while a sub-page (Settings or Save game) is open: Esc goes back to the pause page first.
    public bool SettingsOpen => (_pageSettings != null && !_pageSettings.ClassListContains("hidden")) || (_pageSave != null && !_pageSave.ClassListContains("hidden"));

    // Set by a story episode that hosts this menu (before it is enabled). The owner then handles Esc, time and
    // the cursor; Resume asks the owner to unpause instead of just hiding the menu.
    public IPausable owner;

    PlayerControls Player => _player != null ? _player : (_player = FindAnyObjectByType<PlayerControls>());

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _document = GetComponent<UIDocument>();
    }

    void OnEnable()
    {
        InitializeUI();
    }

    void Start()
    {
        InitializeUI();

        var gameplay = owner == null ? KeyBindings.Gameplay : null;
        if (gameplay != null)
        {
            gameplay.Enable();
            _pauseAction = gameplay.FindAction("TogglePause");
        }

        SetVisible(false);
    }

    void InitializeUI()
    {
        if (_document == null)
            _document = GetComponent<UIDocument>();

        _root = _document != null ? _document.rootVisualElement : null;
        if (_root == null) return;

        _overlay = _root.Q<VisualElement>("pause-overlay");
        _pagePause = _root.Q<VisualElement>("page-pause");
        _pageSettings = _root.Q<VisualElement>("page-settings");
        _pageSave = _root.Q<VisualElement>("page-save");
        _btnSave = _root.Q<Button>("btn-save");
        _btnSaveBack = _root.Q<Button>("btn-save-back");
        MenuIcon.AttachIconOnly(_btnSaveBack, MenuIcon.Kind.Back, "Back to pause menu");
        _saveStatus = _root.Q<Label>("save-status");
        MenuIcon.AttachIconOnly(_btnSave, MenuIcon.Kind.Save, "Save game");
        if (_btnSave != null) { _btnSave.clicked -= OpenSave; _btnSave.clicked += OpenSave; }
        if (_btnSaveBack != null) { _btnSaveBack.clicked -= BackToPauseMenu; _btnSaveBack.clicked += BackToPauseMenu; }
        for (int i = 0; i < _saveSlots.Length; i++)
        {
            int index = i;
            _saveSlots[i] = _root.Q<Button>("save-slot-" + (i + 1));
            // A fresh Clickable, so re-initialising the UI never adds a second handler.
            if (_saveSlots[i] != null) _saveSlots[i].clickable = new Clickable(() => SaveToSlot(index));
        }

        _btnResume = _root.Q<Button>("btn-resume");
        _btnJournal = _root.Q<Button>("btn-journal");
        _btnSettings = _root.Q<Button>("btn-settings");
        _btnMainMenu = _root.Q<Button>("btn-mainmenu");
        // Vector icons in place of the emoji in the UXML labels (the menu font has no emoji glyphs).
        MenuIcon.AttachIconOnly(_btnResume, MenuIcon.Kind.Play, "Resume game");
        MenuIcon.AttachIconOnly(_btnJournal, MenuIcon.Kind.Journal, "Thought journal");
        MenuIcon.AttachIconOnly(_btnSettings, MenuIcon.Kind.Gear, "Settings and audio");
        MenuIcon.AttachIconOnly(_btnMainMenu, MenuIcon.Kind.Home, "Return to main menu");
        _btnSettingsBack = _root.Q<Button>("btn-settings-back");
        MenuIcon.AttachIconOnly(_btnSettingsBack, MenuIcon.Kind.Back, "Back to pause menu");
        _btnSettingsDone = _root.Q<Button>("btn-settings-done");

        _sliderMusic = _root.Q<Slider>("slider-music");
        _sliderSfx = _root.Q<Slider>("slider-sfx");
        _labelMusicValue = _root.Q<Label>("label-music-value");
        _labelSfxValue = _root.Q<Label>("label-sfx-value");
        _labelChapter = _root.Q<Label>("label-chapter");

        if (_btnResume != null)
        {
            _btnResume.clicked -= Resume;
            _btnResume.clicked += Resume;
        }

        if (_btnJournal != null)
        {
            _btnJournal.clicked -= OnJournalClicked;
            _btnJournal.clicked += OnJournalClicked;
        }

        if (_btnSettings != null)
        {
            _btnSettings.clicked -= OpenSettings;
            _btnSettings.clicked += OpenSettings;
        }

        if (_btnSettingsBack != null)
        {
            _btnSettingsBack.clicked -= BackToPauseMenu;
            _btnSettingsBack.clicked += BackToPauseMenu;
        }

        if (_btnSettingsDone != null)
        {
            _btnSettingsDone.clicked -= BackToPauseMenu;
            _btnSettingsDone.clicked += BackToPauseMenu;
        }

        if (_btnMainMenu != null)
        {
            _btnMainMenu.clicked -= GoMainMenu;
            _btnMainMenu.clicked += GoMainMenu;
        }

        BindVolumeSlider(_sliderMusic, _labelMusicValue, MusicKey, 0.65f);
        BindVolumeSlider(_sliderSfx, _labelSfxValue, SfxKey, 0.65f);

        UpdateChapterRibbon();
        ShowPausePanel();
        SetVisible(_isPaused);
    }

    void BindVolumeSlider(Slider slider, Label label, string prefKey, float defaultVal)
    {
        if (slider == null) return;

        float current = PlayerPrefs.GetFloat(prefKey, defaultVal);
        slider.SetValueWithoutNotify(current);
        if (label != null) label.text = Mathf.RoundToInt(current * 100) + "%";

        slider.RegisterValueChangedCallback(evt =>
        {
            if (label != null) label.text = Mathf.RoundToInt(evt.newValue * 100) + "%";
            PlayerPrefs.SetFloat(prefKey, evt.newValue);
            PlayerPrefs.Save();
        });
    }

    void Update()
    {
        if (owner != null) return;

        var keyboard = Keyboard.current;
        bool escPressed = (_pauseAction != null && _pauseAction.WasPressedThisFrame()) ||
                          (keyboard != null && keyboard.escapeKey.wasPressedThisFrame);

        if (escPressed)
        {
            // If another overlay is open (e.g. tasks), don't trigger pause
            bool otherOpen = Player != null && Player.tasksOpen;
            if (!otherOpen)
            {
                if (_isPaused && SettingsOpen)
                {
                    BackToPauseMenu();
                }
                else
                {
                    TogglePause();
                }
            }
        }
    }

    public void TogglePause()
    {
        SetVisible(!_isPaused);
    }

    public void Resume()
    {
        if (owner != null && owner.IsPaused) owner.TogglePause();
        else SetVisible(false);
    }

    public void OnJournalClicked()
    {
        Resume();
        if (QuestHUDController.Instance != null)
        {
            QuestHUDController.Instance.Open();
        }
    }

    void UpdateChapterRibbon()
    {
        if (_labelChapter == null) return;

        // Query active chapter/episode context if available in the scene
        var academic = FindAnyObjectByType<Mindscape.AcademicPressure.AcademicEpisode>();
        if (academic != null)
        {
            _labelChapter.text = $"Chapter {academic.Chapter} · Episode {academic.Episode:00}";
            return;
        }

        var scene = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrEmpty(scene))
        {
            _labelChapter.text = scene;
        }
    }

    // Kept for story episodes that pass a pause hint. The compact pause page
    // intentionally shows only its title and chapter/episode.
    public void SetHint(string text)
    {
    }

    public void OpenSettings()
    {
        if (_pagePause != null) _pagePause.AddToClassList("hidden");
        if (_pageSettings != null) _pageSettings.RemoveFromClassList("hidden");
        MusicManager.SetDucked(true); // quieter music so the volume sliders can be heard
    }

    public void BackToPauseMenu()
    {
        ShowPausePanel();
    }

    // ---------------------------------------------------------------- save slots
    // Saving is offered in story episodes (AcademicEpisode); a slot keeps where the story is, and loading it continues there.
    static Mindscape.AcademicPressure.AcademicEpisode StoryEpisode => FindAnyObjectByType<Mindscape.AcademicPressure.AcademicEpisode>();

    public void OpenSave()
    {
        if (StoryEpisode == null) return;
        _confirmSlot = -1;
        if (_saveStatus != null) _saveStatus.text = "";
        RefreshSaveSlots();
        if (_pagePause != null) _pagePause.AddToClassList("hidden");
        if (_pageSave != null) _pageSave.RemoveFromClassList("hidden");
    }

    void SaveToSlot(int index)
    {
        var episode = StoryEpisode;
        if (episode == null) return;
        if (MindscapeSaves.Get(index) != null && _confirmSlot != index)
        {
            _confirmSlot = index;
            if (_saveStatus != null) _saveStatus.text = "Slot " + (index + 1) + " is occupied. Select again to overwrite.";
            return;
        }
        if (!episode.CanSave) { if (_saveStatus != null) _saveStatus.text = "The story can't be saved right now."; return; }
        _confirmSlot = -1;
        MindscapeSaves.Save(index, episode.Chapter, episode.Episode, episode.CaptureSave);
        RefreshSaveSlots();
        if (_saveStatus != null) _saveStatus.text = "Saved to slot " + (index + 1) + ".";
    }

    void RefreshSaveSlots()
    {
        for (int i = 0; i < _saveSlots.Length; i++)
        {
            if (_saveSlots[i] == null) continue;
            var slot = MindscapeSaves.Get(i);
            _saveSlots[i].text = slot == null ? "SLOT " + (i + 1) + "  ·  Empty"
                : "SLOT " + (i + 1) + "  ·  Chapter " + slot.chapter + ", Episode " + slot.episode +
                  "\nSaved " + MindscapeSaves.When(slot);
        }
    }

    void ShowPausePanel()
    {
        if (_pageSettings != null) _pageSettings.AddToClassList("hidden");
        if (_pageSave != null) _pageSave.AddToClassList("hidden");
        if (_pagePause != null) _pagePause.RemoveFromClassList("hidden");
        MusicManager.SetDucked(false);
    }

    public void GoMainMenu()
    {
        PlayerPrefs.DeleteKey("Mindscape.StartPage");
        PlayerPrefs.Save();
        Time.timeScale = 1f;
        MusicManager.LoadSceneFaded("main_menu_new");
    }

    public void SetVisible(bool visible)
    {
        _isPaused = visible;

        if (_overlay == null && _root != null)
            _overlay = _root.Q<VisualElement>("pause-overlay");

        if (_overlay != null)
        {
            _overlay.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible)
                _overlay.RemoveFromClassList("hidden");
            else
                _overlay.AddToClassList("hidden");
        }

        if (visible)
        {
            ShowPausePanel();
            if (_btnSave != null) _btnSave.style.display = StoryEpisode != null ? DisplayStyle.Flex : DisplayStyle.None;
            // Hide actions unavailable in this scene.
            bool journal = QuestHUDController.Instance != null && QuestHUDController.Instance.JournalAvailable;
            if (_btnJournal != null) _btnJournal.style.display = journal ? DisplayStyle.Flex : DisplayStyle.None;
        }
        else
        {
            MusicManager.SetDucked(false); // closing the menu from the settings page
            // A button left focused would "click" again on Space / Enter, which the story uses to advance.
            _root?.focusController?.focusedElement?.Blur();
        }

        if (owner != null) return; // the hosting episode owns the cursor and player state

        if (Player != null)
            Player.pauseOpen = visible;

        UnityEngine.Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
        UnityEngine.Cursor.visible = visible;
    }
}
