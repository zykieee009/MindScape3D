using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Mindscape.Data
{
    // Where "Continue with Google" opens. Google refuses sign-in inside an embedded web view, so the in-app options are the
    // platforms' own sign-in browsers:
    //   Android: a Chrome Custom Tab over the game (Assets/Plugins/Android/MindscapeCustomTab.java). The last page goes
    //            to mindscape://google-done (AndroidManifest.xml), which brings the game back and closes the tab.
    //   macOS:   Apple's sign-in sheet, ASWebAuthenticationSession (Assets/Plugins/macOS/MindscapeAuth.bundle, source in
    //            NativePlugins/macOS), also in the Editor on a Mac. It closes itself at mindscape://google-done.
    //   Others:  the system browser, as before.
    // 2026-11-09: switched off (UseInApp = false) after the in-app sign-in did not work in the builds; every platform
    // opens the system browser and the game polls for the session. Set UseInApp back to true to try the in-app windows.
    // Either way the session itself still comes from the server (MindscapeCloud.WaitForGoogle polls for it); the return
    // to mindscape:// only closes the sheet and makes the game look straight away.
    public static class GoogleSignInSheet
    {
        public const string Scheme = "mindscape";
        const bool UseInApp = false;

        // True when the platform opens the sign-in inside the app (the start link then asks for the mindscape:// return).
        public static bool InApp
        {
            get
            {
                if (!UseInApp) return false;
#if UNITY_ANDROID && !UNITY_EDITOR
                return true;
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                try { return MindscapeAuthAvailable() == 1; } catch (Exception) { return false; }
#else
                return false;
#endif
            }
        }

        // Set when the sign-in page has come back to the game (mindscape://google-done), so it polls at once.
        static bool returned;
        public static bool Returned
        {
            get
            {
                if (!InApp) return false;
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                try { if (MindscapeAuthState() == 2) return true; } catch (Exception) { }
#endif
                return returned;
            }
        }

        // True once the player closed the macOS sheet without finishing.
        public static bool Dismissed
        {
            get
            {
                if (!InApp) return false;
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                try { return MindscapeAuthState() == 3; } catch (Exception) { return false; }
#else
                return false;
#endif
            }
        }

        public static void Open(string url)
        {
            returned = false;
            if (!InApp) { Application.OpenURL(url); return; }
            Watch();
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var tab = new AndroidJavaClass("com.mindscape.auth.MindscapeCustomTab"))
                    if (tab.CallStatic<bool>("open", activity, url, unchecked((int)0xFF1D2B2F))) return;   // the game's ink colour
            }
            catch (Exception e) { Debug.LogWarning("Mindscape: Custom Tab unavailable, using the browser. " + e.Message); }
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            try { if (MindscapeAuthStart(url, Scheme) == 1) return; }
            catch (Exception e) { Debug.LogWarning("Mindscape: sign-in sheet unavailable, using the browser. " + e.Message); }
#endif
            Application.OpenURL(url);
        }

        // Closes the macOS sheet if it is still up (the player cancelled in the game, or it finished).
        public static void Close()
        {
            if (!InApp) return;
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            try { MindscapeAuthCancel(); } catch (Exception) { }
#endif
        }

        static bool watching;
        static void Watch()
        {
            if (watching) return;
            watching = true;
            Application.deepLinkActivated += url =>
            {
                if (url != null && url.StartsWith(Scheme + "://google-done", StringComparison.OrdinalIgnoreCase)) returned = true;
            };
        }

        public static void ClearReturned() => returned = false;

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        [DllImport("MindscapeAuth")] static extern int MindscapeAuthAvailable();
        [DllImport("MindscapeAuth")] static extern int MindscapeAuthStart(string url, string scheme);
        [DllImport("MindscapeAuth")] static extern int MindscapeAuthState();
        [DllImport("MindscapeAuth")] static extern void MindscapeAuthCancel();
#endif
    }
}
