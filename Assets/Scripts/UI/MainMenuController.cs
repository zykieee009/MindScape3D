using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

// LEGACY: the old uGUI main menu used only by Assets/Scenes/MainMenu.unity, which is no longer in the build.
// The live menu is MainMenuNewController (main_menu_new scene). Kept so the old scene still opens; safe to ignore.
[System.Serializable]
public class Episode
{
    public string name = "Episode 1";
    [Tooltip("Scene to play before gameplay. Leave empty to skip straight to Gameplay Scene.")]
    public string cutsceneScene;
    [Tooltip("The actual playable scene. If Cutscene Scene is set, that scene is responsible for loading this one when it finishes.")]
    public string gameplayScene;
}

[System.Serializable]
public class Chapter
{
    public string name = "Chapter";
    [Tooltip("Empty list means this chapter shows as Coming Soon.")]
    public Episode[] episodes = new Episode[0];
}

// UI Toolkit main menu. Music/SFX sliders only write to PlayerPrefs — MusicManager
// and SFXManager (persistent singletons) poll those prefs and apply the volume
// themselves, so audio works regardless of what's loaded in this scene.
[RequireComponent(typeof(UIDocument))]
public class MainMenuController : MonoBehaviour
{
    [Header("Chapters and their episodes (empty episode list means coming soon)")]
    public Chapter[] chapters = {
        new Chapter { name = "Chapter 1 · A New Day", episodes = new [] {
            new Episode { name = "Tutorial", cutsceneScene = "", gameplayScene = "Tutorial" },
            new Episode { name = "Episode 1", cutsceneScene = "Cutscene - Episode 1", gameplayScene = "Chapter 1 - Episode 1" },
        }},
        new Chapter { name = "Chapter 2 · Finding Balance", episodes = new Episode[0] },
        new Chapter { name = "Chapter 3 · Growing Forward", episodes = new Episode[0] },
    };

    const string MusicKey = "Mindscape.Music";
    const string SfxKey = "Mindscape.SFX";
    const string MobileControlsKey = "Mindscape.MobileControls";

    VisualElement _root;
    VisualElement[] _pages;
    int _currentPage;
    int _selectedChapter = -1;

    Label _chapterNotice;
    Label _episodeNotice;
    Label _episodesTitle;
    VisualElement _chapterList;
    VisualElement _episodeList;

    readonly System.Collections.Generic.List<System.Action> _keyBindingRefreshers = new System.Collections.Generic.List<System.Action>();

    void OnEnable()
    {
        var document = GetComponent<UIDocument>();
        _root = document.rootVisualElement;
        _root.RegisterCallback<GeometryChangedEvent>(evt =>
        {
            _root.EnableInClassList("compact-height", _root.resolvedStyle.height < 680);
            _root.EnableInClassList("narrow-width", _root.resolvedStyle.width < 900);
            _root.EnableInClassList("portrait", _root.resolvedStyle.width < _root.resolvedStyle.height);
        });

        _pages = new[]
        {
            _root.Q<VisualElement>("page-main"),          // 0
            _root.Q<VisualElement>("page-settings"),      // 1
            _root.Q<VisualElement>("page-chapters"),      // 2
            _root.Q<VisualElement>("page-keybindings"),   // 3
            _root.Q<VisualElement>("page-episodes"),      // 4
            _root.Q<VisualElement>("page-how-to-play"),   // 5
            _root.Q<VisualElement>("page-resources"),     // 6
            _root.Q<VisualElement>("page-about-us"),      // 7
        };

        BuildHome();
        BuildSettings();
        BuildChapters();
        BuildKeyBindings();
        BuildStaticPage("btn-how-to-play-back");
        BuildStaticPage("btn-resources-back");
        BuildStaticPage("btn-about-us-back");

        int startPage = PlayerPrefs.GetInt("Mindscape.StartPage", 0);
        ShowPage(startPage);
        if (startPage != 0) PlayerPrefs.DeleteKey("Mindscape.StartPage");
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ShowPage(0);
    }

    void BuildHome()
    {
        _root.Q<Button>("btn-new-game").clicked += () => { SFXManager.PlayClick(); StartNewGame(); };

        var continueButton = _root.Q<Button>("btn-continue");
        bool hasSave = PlayerPrefs.HasKey(Mindscape.Data.MindscapeProgress.ChapterKey) && PlayerPrefs.HasKey(Mindscape.Data.MindscapeProgress.EpisodeKey);
        continueButton.SetEnabled(hasSave);
        continueButton.EnableInClassList("unavailable", !hasSave);
        continueButton.clicked += () => { SFXManager.PlayClick(); ContinueGame(); };

        _root.Q<Button>("btn-journal").clicked += () => { SFXManager.PlayClick(); ShowPage(2); };
        _root.Q<Button>("btn-options").clicked += () => { SFXManager.PlayClick(); ShowPage(1); };
        _root.Q<Button>("btn-how-to-play").clicked += () => { SFXManager.PlayClick(); ShowPage(5); };
        _root.Q<Button>("btn-resources").clicked += () => { SFXManager.PlayClick(); ShowPage(6); };
        _root.Q<Button>("btn-about-us").clicked += () => { SFXManager.PlayClick(); ShowPage(7); };
        _root.Q<Button>("btn-quit").clicked += () => { SFXManager.PlayClick(); Quit(); };
    }

    void StartNewGame()
    {
        PlayerPrefs.DeleteKey(Mindscape.Data.MindscapeProgress.ChapterKey);
        PlayerPrefs.DeleteKey(Mindscape.Data.MindscapeProgress.EpisodeKey);
        PlayerPrefs.DeleteKey(Mindscape.Data.MindscapeProgress.ChapterUnlockedKey);
        PlayerPrefs.Save();
        _selectedChapter = 0;
        OpenEpisode(0);
    }

    void ContinueGame()
    {
        int chapter = PlayerPrefs.GetInt(Mindscape.Data.MindscapeProgress.ChapterKey, 0) - 1;
        int episode = PlayerPrefs.GetInt(Mindscape.Data.MindscapeProgress.EpisodeKey, 0) - 1;
        if (chapter < 0 || chapter >= chapters.Length) return;
        _selectedChapter = chapter;
        OpenEpisode(episode);
    }

    // Wires a page's back button to return home. Used by the static content pages
    // (How to Play / Resources / About Us) that are just text — edit their copy in
    // the UXML/UI Builder, no code changes needed.
    void BuildStaticPage(string backButtonName)
    {
        _root.Q<Button>(backButtonName).clicked += () => { SFXManager.PlayClick(); ShowPage(0); };
    }

    void BuildSettings()
    {
        _root.Q<Button>("btn-settings-back").clicked += () => ShowPage(0);
        _root.Q<Button>("btn-open-keybindings").clicked += () => { SFXManager.PlayClick(); ShowPage(3); };

        BindVolumeSlider("slider-music", "label-music-value", MusicKey);
        BindVolumeSlider("slider-sfx", "label-sfx-value", SfxKey);

        var mobileToggle = _root.Q<Toggle>("toggle-mobile");
        mobileToggle.SetValueWithoutNotify(MobileControls.Enabled);
        mobileToggle.RegisterValueChangedCallback(evt =>
        {
            PlayerPrefs.SetInt(MobileControlsKey, evt.newValue ? 1 : 0);
            PlayerPrefs.Save();
        });
    }

    void BindVolumeSlider(string sliderName, string valueLabelName, string prefKey)
    {
        var slider = _root.Q<Slider>(sliderName);
        var valueLabel = _root.Q<Label>(valueLabelName);

        float initial = PlayerPrefs.GetFloat(prefKey, 0.65f);
        slider.SetValueWithoutNotify(initial);
        valueLabel.text = Mathf.RoundToInt(initial * 100) + "%";

        slider.RegisterValueChangedCallback(evt =>
        {
            valueLabel.text = Mathf.RoundToInt(evt.newValue * 100) + "%";
            PlayerPrefs.SetFloat(prefKey, evt.newValue);
        });
    }

    void BuildChapters()
    {
        _root.Q<Button>("btn-chapters-back").clicked += () => ShowPage(0);
        _chapterList = _root.Q<VisualElement>("chapter-list");
        _chapterNotice = _root.Q<Label>("chapter-notice");

        for (int i = 0; i < chapters.Length; i++)
        {
            int index = i;
            bool available = chapters[i].episodes.Length > 0 || IsChapterUnlocked(i);

            var button = new Button(() => { SFXManager.PlayClick(); OpenChapter(index); })
            {
                text = available ? chapters[i].name : chapters[i].name + "  (Coming Soon)"
            };
            button.AddToClassList("list-button");
            if (!available) button.AddToClassList("unavailable");
            _chapterList.Add(button);
        }
    }

    void BuildEpisodesContent()
    {
        Chapter chapter = chapters[_selectedChapter];
        _episodesTitle.text = chapter.name.ToUpper();
        _episodeList.Clear();

        for (int i = 0; i < chapter.episodes.Length; i++)
        {
            int index = i;
            Episode episode = chapter.episodes[i];
            bool available = !string.IsNullOrEmpty(episode.cutsceneScene) || !string.IsNullOrEmpty(episode.gameplayScene);

            var button = new Button(() => { SFXManager.PlayClick(); OpenEpisode(index); })
            {
                text = available ? episode.name : episode.name + "  (Coming Soon)"
            };
            button.AddToClassList("list-button");
            if (!available) button.AddToClassList("unavailable");
            _episodeList.Add(button);
        }
    }

    void BuildKeyBindings()
    {
        _root.Q<Button>("btn-keybindings-back").clicked += () => ShowPage(1);
        _episodesTitle = _root.Q<Label>("episodes-title");
        _episodeNotice = _root.Q<Label>("episode-notice");
        _episodeList = _root.Q<VisualElement>("episode-list");
        _root.Q<Button>("btn-episodes-back").clicked += () => ShowPage(2);

        var leftColumn = _root.Q<VisualElement>("keybind-left");
        var rightColumn = _root.Q<VisualElement>("keybind-right");
        var map = KeyBindings.Gameplay;

        (string label, string action, string part)[] left = {
            ("Move Up", "Move", "up"), ("Move Down", "Move", "down"),
            ("Move Left", "Move", "left"), ("Move Right", "Move", "right"),
            ("Run", "Run", null), ("Free Look", "FreeLook", null),
        };
        (string label, string action, string part)[] right = {
            ("Zoom In", "ZoomIn", null), ("Zoom Out", "ZoomOut", null),
            ("Open Tasks", "OpenTasks", null),
            ("Pause Menu", "TogglePause", null),
        };

        foreach (var row in left) RebindRow(leftColumn, map, row.label, row.action, row.part);
        foreach (var row in right) RebindRow(rightColumn, map, row.label, row.action, row.part);

        _root.Q<Button>("btn-reset-bindings").clicked += () =>
        {
            SFXManager.PlayClick();
            KeyBindings.ResetToDefaults();
            foreach (var refresh in _keyBindingRefreshers) refresh();
        };
    }

    void RebindRow(VisualElement parent, InputActionMap map, string label, string actionName, string part)
    {
        InputAction action = map != null ? map.FindAction(actionName) : null;
        int bindingIndex = FindBindingIndex(action, part);

        var row = new VisualElement();
        row.AddToClassList("keybind-row");

        var keyButton = new Button { text = BindingDisplay(action, bindingIndex) };
        keyButton.AddToClassList("keybind-key");

        var nameLabel = new Label(label);
        nameLabel.AddToClassList("keybind-label");

        keyButton.clicked += () => StartRebind(action, bindingIndex, keyButton);

        row.Add(keyButton);
        row.Add(nameLabel);
        parent.Add(row);

        _keyBindingRefreshers.Add(() => keyButton.text = BindingDisplay(action, bindingIndex));
    }

    int FindBindingIndex(InputAction action, string part)
    {
        if (action == null) return -1;
        if (string.IsNullOrEmpty(part)) return 0;
        for (int i = 0; i < action.bindings.Count; i++)
            if (action.bindings[i].name == part) return i;
        return -1;
    }

    string BindingDisplay(InputAction action, int bindingIndex)
    {
        if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count) return "?";
        return action.bindings[bindingIndex].ToDisplayString();
    }

    void StartRebind(InputAction action, int bindingIndex, Button keyButton)
    {
        if (action == null || bindingIndex < 0) return;
        keyButton.text = "...";
        keyButton.SetEnabled(false);
        action.Disable();
        action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("Mouse")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(op =>
            {
                op.Dispose();
                action.Enable();
                keyButton.SetEnabled(true);
                keyButton.text = BindingDisplay(action, bindingIndex);
                KeyBindings.SaveOverrides();
            })
            .OnCancel(op =>
            {
                op.Dispose();
                action.Enable();
                keyButton.SetEnabled(true);
                keyButton.text = BindingDisplay(action, bindingIndex);
            })
            .Start();
    }

    public void ShowPage(int page)
    {
        _currentPage = Mathf.Clamp(page, 0, _pages.Length - 1);
        for (int i = 0; i < _pages.Length; i++)
        {
            if (_pages[i] == null) continue;
            _pages[i].EnableInClassList("hidden", i != _currentPage);
        }
        PlayerPrefs.Save();
    }

    public void OpenChapter(int index)
    {
        if (index < 0 || index >= chapters.Length) return;
        if (chapters[index].episodes.Length == 0 && !IsChapterUnlocked(index))
        {
            _chapterNotice.text = "This chapter is still growing. Come back soon.";
            return;
        }
        _selectedChapter = index;
        BuildEpisodesContent();
        ShowPage(4);
    }

    bool IsChapterUnlocked(int index)
    {
        return index == 0 || PlayerPrefs.GetInt(Mindscape.Data.MindscapeProgress.ChapterUnlockedKey, 1) >= index + 1;
    }

    public void OpenEpisode(int index)
    {
        if (_selectedChapter < 0 || _selectedChapter >= chapters.Length) return;
        Episode[] episodes = chapters[_selectedChapter].episodes;
        if (index < 0 || index >= episodes.Length) return;
        Episode episode = episodes[index];

        PlayerPrefs.SetInt(Mindscape.Data.MindscapeProgress.ChapterKey, _selectedChapter + 1);
        PlayerPrefs.SetInt(Mindscape.Data.MindscapeProgress.EpisodeKey, index + 1);
        PlayerPrefs.Save();

        // The cutscene scene is responsible for loading the gameplay scene once it finishes (see CutscenePlayer).
        if (!string.IsNullOrEmpty(episode.cutsceneScene)) { SceneManager.LoadScene(episode.cutsceneScene); return; }
        if (!string.IsNullOrEmpty(episode.gameplayScene)) { SceneManager.LoadScene(episode.gameplayScene); return; }
        _episodeNotice.text = "This episode is still growing. Come back soon.";
    }

    void Quit()
    {
        PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnDisable()
    {
        PlayerPrefs.Save();
    }
}
