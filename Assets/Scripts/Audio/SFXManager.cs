using UnityEngine;

// Mirrors MusicManager: a persistent volume-synced AudioSource for one-shot sound
// effects. Anything that wants to play a UI/gameplay sound should route through
// SFXManager.Play so it respects the "Mindscape.SFX" volume setting.
public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance { get; private set; }

    const string VolumeKey = "Mindscape.SFX";

    AudioSource _source;
    AudioClip _clickClip;
    float _pollTimer;
    float _lastVolume;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("SFXManager");
        DontDestroyOnLoad(go);
        go.AddComponent<SFXManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _source = gameObject.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _lastVolume = PlayerPrefs.GetFloat(VolumeKey, 0.65f);
        _source.volume = _lastVolume;

        _clickClip = BuildClickClip();
    }

    void Update()
    {
        _pollTimer += Time.deltaTime;
        if (_pollTimer < 0.1f) return;
        _pollTimer = 0f;

        float v = PlayerPrefs.GetFloat(VolumeKey, 0.65f);
        if (Mathf.Abs(v - _lastVolume) > 0.001f)
        {
            _lastVolume = v;
            _source.volume = v;
        }
    }

    public static void Play(AudioClip clip)
    {
        if (Instance == null || clip == null) return;
        Instance._source.PlayOneShot(clip);
    }

    public static void PlayClick()
    {
        if (Instance == null) return;
        Instance._source.PlayOneShot(Instance._clickClip);
    }

    // A short, soft sine blip so UI buttons have click feedback without needing an
    // imported audio asset.
    static AudioClip BuildClickClip()
    {
        const int sampleRate = 44100;
        const float duration = 0.06f;
        const float frequency = 880f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);

        var samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = 1f - (i / (float)sampleCount);
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.5f;
        }

        AudioClip clip = AudioClip.Create("UIClick", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
