using UnityEngine;

// The player's graphics choices (Options > Graphics), saved in PlayerPrefs and applied once before the first scene loads, so they hold
// from the intro on. The quality level is applied first because switching level resets V-Sync and texture size to that level's own.
//   Quality:          a Unity quality level (Mobile / PC).
//   Texture quality:  Full, Half or Quarter size (mipmap limit 0 / 1 / 2); lower uses less memory on small devices.
//   Frame rate limit: 30, 60, 120 or Unlimited. Ignored while V-Sync is on (V-Sync locks to the screen's refresh rate).
//   V-Sync:           waits for the screen's refresh, so frames never tear. Phones and browsers always do this.
//   Display:          Windowed (a resizable window that can be maximised or minimised) or Fullscreen. F11 or
//                     Alt+Enter switches between them at any time. Desktop builds only.
public static class GraphicsPrefs
{
    public const string QualityKey = "Mindscape.Quality", TextureKey = "Mindscape.TextureQuality", FrameRateKey = "Mindscape.FrameRate", VSyncKey = "Mindscape.VSync", DisplayKey = "Mindscape.Display";

    public static readonly string[] TextureNames = { "Full", "Half", "Quarter" };
    public static readonly string[] FrameRateNames = { "30 FPS", "60 FPS", "120 FPS", "Unlimited" };
    static readonly int[] FrameRates = { 30, 60, 120, -1 };
    public static readonly string[] DisplayNames = { "Windowed", "Fullscreen" };
    public static bool DesktopDisplay => Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.LinuxPlayer || Application.isEditor;

    public static int Quality => Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1);
    public static int Texture => Mathf.Clamp(PlayerPrefs.GetInt(TextureKey, 0), 0, TextureNames.Length - 1);
    public static int FrameRate => Mathf.Clamp(PlayerPrefs.GetInt(FrameRateKey, 1), 0, FrameRates.Length - 1);
    public static bool VSync => PlayerPrefs.GetInt(VSyncKey, 1) == 1;
    public static int Display => Mathf.Clamp(PlayerPrefs.GetInt(DisplayKey, Screen.fullScreen ? 1 : 0), 0, DisplayNames.Length - 1);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Apply()
    {
        QualitySettings.SetQualityLevel(Quality, true);
        QualitySettings.globalTextureMipmapLimit = Texture;
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        Application.targetFrameRate = FrameRates[FrameRate];
        ApplyDisplay();
    }

    static void ApplyDisplay()
    {
        if (Application.isEditor || !DesktopDisplay || !PlayerPrefs.HasKey(DisplayKey)) return;
        if (Display == 1) Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.FullScreenWindow);
        else if (Screen.fullScreen) Screen.SetResolution(Mathf.Min(1280, Screen.currentResolution.width), Mathf.Min(720, Screen.currentResolution.height), FullScreenMode.Windowed);
    }

    // F11 anywhere in the game switches between a window and fullscreen (Alt+Enter is Unity's own shortcut for the same).
    public static void ToggleDisplay() => Set(DisplayKey, Screen.fullScreen ? 0 : 1);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void WatchShortcut()
    {
        if (!DesktopDisplay) return;
        var go = new GameObject("Display shortcut (F11)") { hideFlags = HideFlags.HideAndDontSave };
        Object.DontDestroyOnLoad(go);
        go.AddComponent<DisplayShortcut>();
    }

    sealed class DisplayShortcut : MonoBehaviour
    {
        void Update()
        {
            var k = UnityEngine.InputSystem.Keyboard.current;
            if (k != null && k.f11Key.wasPressedThisFrame) ToggleDisplay();
        }
    }

    public static void Set(string key, int value)
    {
        PlayerPrefs.SetInt(key, value); PlayerPrefs.Save();
        if (Application.isPlaying) Apply();
    }
}
