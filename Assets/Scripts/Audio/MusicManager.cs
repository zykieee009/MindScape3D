using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Persistent background music. Each scene plays the track set in Resources/SceneMusicMap, crossfading from the
// previous one; Options > Audio can skip to the next playlist track or stop the music, and the stop is remembered.
// Menus fade the music out before leaving (FadeOut, LoadSceneFaded) and duck it while settings are open (SetDucked).
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    const string VolumeKey = "Mindscape.Music";
    const string StoppedKey = "Mindscape.MusicStopped";
    const string ThemeTitle = "Mindscape Theme";
    const float DuckLevel = 0.4f;
    const float NextFadeSeconds = 0.6f;

    /// <summary>Raised when the track changes or the music is stopped or started.</summary>
    public static event Action Changed;

    AudioSource _source;    // the track the player hears
    AudioSource _outgoing;  // the previous track while it fades away
    SceneMusicMap _map;
    readonly List<AudioClip> _all = new List<AudioClip>();
    readonly List<AudioClip> _tracks = new List<AudioClip>(); // the Next-track playlist (_all minus reserved tracks)
    bool _held;
    float _pollTimer;
    float _lastVolume;

    // Fade weights: the crossfade between tracks, the fade-out / fade-to level, and the settings duck.
    float _fade = 1f, _outFade;
    float _crossfade = 1.5f;
    float _master = 1f, _masterTarget = 1f, _masterRate = 1f;
    float _duck = 1f, _duckTarget = 1f;

    public static bool IsStopped => PlayerPrefs.GetInt(StoppedKey, 0) == 1;
    public static int TrackCount => Instance != null ? Instance._tracks.Count : 0;
    public static string CurrentTitle
    {
        get
        {
            var clip = Instance != null && Instance._source != null ? Instance._source.clip : null;
            if (clip == null) return "No music";
            return clip.name == "bgm" ? ThemeTitle : clip.name;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("MusicManager");
        DontDestroyOnLoad(go);
        go.AddComponent<MusicManager>();
    }

    // The intro holds the music while its story video plays, then resumes it for the title card.
    public static void SetPaused(bool paused)
    {
        if (Instance == null || Instance._source == null) return;
        Instance._held = paused;
        if (paused) Instance._source.Pause();
        else if (!IsStopped) { Instance._source.UnPause(); if (!Instance._source.isPlaying) Instance._source.Play(); }
    }

    public static void Next()
    {
        if (Instance == null || Instance._tracks.Count == 0) return;
        // A scene's own track may be outside the playlist (index -1), in which case Next starts the playlist.
        int index = Instance._tracks.IndexOf(Instance._source.clip);
        Instance.Switch(Instance._tracks[(index + 1) % Instance._tracks.Count], NextFadeSeconds);
        if (IsStopped) Play(); else Changed?.Invoke();
    }

    public static void Stop()
    {
        PlayerPrefs.SetInt(StoppedKey, 1); PlayerPrefs.Save();
        if (Instance != null && Instance._source != null) { Instance._source.Stop(); Instance._outgoing.Stop(); }
        Changed?.Invoke();
    }

    public static void Play()
    {
        PlayerPrefs.SetInt(StoppedKey, 0); PlayerPrefs.Save();
        if (Instance != null && Instance._source != null && !Instance._held && !Instance._source.isPlaying) Instance._source.Play();
        Changed?.Invoke();
    }

    /// <summary>Plays a Resources/Music track by name now (a scene can call this mid-story, e.g. the Episode 8 walk).</summary>
    public static void PlayTrack(string trackName)
    {
        if (Instance == null) return;
        var clip = Instance._all.Find(t => t.name == trackName);
        if (clip == null) { Debug.LogWarning("MusicManager: no track named '" + trackName + "'."); return; }
        Instance.Switch(clip, Instance._crossfade);
        Changed?.Invoke();
    }

    /// <summary>Fades the music to silence. The next scene load fades it back in; the Stop setting is untouched.</summary>
    public static void FadeOut(float seconds) => FadeTo(0f, seconds);

    /// <summary>Fades the music to a level between 0 and 1 of the volume setting.</summary>
    public static void FadeTo(float level, float seconds = 0.5f)
    {
        if (Instance == null) return;
        Instance._masterTarget = Mathf.Clamp01(level);
        Instance._masterRate = 1f / Mathf.Max(0.01f, seconds);
    }

    /// <summary>Lowers the music to about 40% (settings open, so the volume sliders can be heard) or restores it.</summary>
    public static void SetDucked(bool ducked)
    {
        if (Instance != null) Instance._duckTarget = ducked ? DuckLevel : 1f;
    }

    /// <summary>Fades the music out, then loads the scene, so leaving to the menu has no hard cut.</summary>
    public static void LoadSceneFaded(string sceneName, float seconds = 0.8f)
    {
        if (Instance == null) { SceneManager.LoadScene(sceneName); return; }
        FadeOut(seconds);
        Instance.StartCoroutine(Instance.LoadAfter(sceneName, seconds));
    }

    IEnumerator LoadAfter(string sceneName, float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        SceneManager.LoadScene(sceneName);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _map = Resources.Load<SceneMusicMap>("SceneMusicMap");
        if (_map != null) _crossfade = _map.crossfadeSeconds;

        var clips = Resources.LoadAll<AudioClip>("Music");
        Array.Sort(clips, (a, b) => string.CompareOrdinal(a.name, b.name));
        _all.AddRange(clips);
        foreach (var clip in _all)
            if (_map == null || !_map.reservedTracks.Contains(clip.name)) _tracks.Add(clip);
        if (_all.Count == 0)
        {
            Debug.LogWarning("MusicManager: no music found in Resources/Music.");
            return;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;

        _source = gameObject.AddComponent<AudioSource>();
        _outgoing = gameObject.AddComponent<AudioSource>();
        foreach (var s in new[] { _source, _outgoing }) { s.loop = true; s.playOnAwake = false; s.volume = 0f; }
        _lastVolume = PlayerPrefs.GetFloat(VolumeKey, 0.65f);
    }

    // In case the first scene's sceneLoaded fired before this manager existed.
    void Start()
    {
        if (_source != null && _source.clip == null) Switch(TrackFor(SceneManager.GetActiveScene().name), _crossfade);
    }

    void OnDestroy()
    {
        if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SilenceSceneMusic(scene);
        if (mode == LoadSceneMode.Additive) return;

        // A menu may have faded the music out or ducked it; the new scene brings it back up.
        _masterTarget = 1f;
        _masterRate = 1f / _crossfade;
        _duckTarget = 1f;
        Switch(TrackFor(scene.name), _crossfade);
    }

    // The scene's mapped track, else whatever is playing, else the first playlist track.
    AudioClip TrackFor(string sceneName)
    {
        string name = _map != null ? _map.TrackFor(sceneName) : null;
        var clip = string.IsNullOrEmpty(name) ? null : _all.Find(t => t.name == name);
        if (!string.IsNullOrEmpty(name) && clip == null) Debug.LogWarning("MusicManager: '" + sceneName + "' maps to missing track '" + name + "'.");
        if (clip != null) return clip;
        if (_source.clip != null) return _source.clip;
        return _tracks.Count > 0 ? _tracks[0] : _all[0];
    }

    // This is the only background music. A scene's own looping music source (Chapter 3 once had one) would play a
    // second track on top of the playlist, so it is silenced as the scene loads. Short loops (alarms, engines) stay.
    void SilenceSceneMusic(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        foreach (var other in root.GetComponentsInChildren<AudioSource>(true))
        {
            var clip = other.resource as AudioClip;
            if (other == _source || other == _outgoing || !other.loop || clip == null || (clip.length < MusicLength && !_all.Contains(clip))) continue;
            other.Stop();
            other.enabled = false;
            Debug.LogWarning("MusicManager: silenced the extra music source on '" + other.name + "' in " + scene.name + ".");
        }
    }

    const float MusicLength = 30f;

    // Crossfades to the clip; it plays unless the music is stopped or held by the intro. The same clip carries on.
    void Switch(AudioClip clip, float seconds)
    {
        if (clip == null || _source.clip == clip) return;
        _crossfade = Mathf.Max(0.01f, seconds);

        bool audible = !IsStopped && !_held;
        var previous = _source;
        _source = _outgoing;
        _outgoing = previous;
        _outFade = audible && _outgoing.isPlaying ? _fade : 0f;

        _source.Stop();
        _source.clip = clip;
        if (audible)
        {
            _fade = 0f;
            _source.volume = 0f;
            _source.Play();
        }
        else
        {
            _fade = 1f;
            _outgoing.Stop();
        }
        Changed?.Invoke();
    }

    void Update()
    {
        if (_source == null) return;

        _pollTimer += Time.unscaledDeltaTime;
        if (_pollTimer >= 0.1f)
        {
            _pollTimer = 0f;
            _lastVolume = PlayerPrefs.GetFloat(VolumeKey, 0.65f);
        }

        float dt = Time.unscaledDeltaTime;
        _fade = Mathf.MoveTowards(_fade, 1f, dt / _crossfade);
        _master = Mathf.MoveTowards(_master, _masterTarget, dt * _masterRate);
        _duck = Mathf.MoveTowards(_duck, _duckTarget, dt / 0.3f);
        if (_outFade > 0f)
        {
            _outFade = Mathf.MoveTowards(_outFade, 0f, dt / _crossfade);
            if (_outFade <= 0f) _outgoing.Stop();
        }

        float gain = _lastVolume * _master * _duck;
        _source.volume = gain * _fade;
        _outgoing.volume = gain * _outFade;
    }
}
