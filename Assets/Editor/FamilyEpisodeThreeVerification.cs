using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Mindscape.EpisodeThree;
using Mindscape.EpisodeTwo;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

public static class FamilyEpisodeThreeVerification
{
    public static bool Running { get; private set; }
    public static string Report { get; private set; } = "Not run";
    static List<string> results;    // Superseded: Chapter 2 is built on Chapter 1's scenes by Mindscape > Chapter 2 - Family Expectations > Build All Episodes
    // (FamilyExpectationsBuilder.cs). The original scenes are kept in Assets/Scenes/Prototypes.

    // [MenuItem("Mindscape/Chapter 2 - Family Expectations/Episode 3/Verify Gameplay (Play Mode)")]
    public static async void Run()
    {
        if (Running) return;
        Running = true; Report = "Running"; results = new List<string>();
        string[] keys = { EpisodeThreeStory.SaveKey, EpisodeTwoStory.SaveKey, "Mindscape.Chapter", "Mindscape.Episode" };
        var saves = keys.Select(k => PlayerPrefs.HasKey(k) ? (int?)PlayerPrefs.GetInt(k) : null).ToArray();
        bool background = Application.runInBackground; Application.runInBackground = true;
        var errors = new List<string>();
        Application.LogCallback capture = (message, stack, type) => { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) errors.Add(message); };
        Application.logMessageReceived += capture;
        try
        {
            Check(EditorApplication.isPlaying, "Play mode is active");
            var s = await Ready(); s.SkipOpening();
            Check(s.CurrentPhase == EpisodeThreeStory.Phase.Corridor && s.AnsweredCount == 0, "Fresh start and skip opening");
            Check(!s.TrySit() && !s.Answer(0) && !s.TryEnter(), "Phase and distance guards reject early interactions");
            // Exercise the CharacterController across the actual corridor and open classroom door.
            await Walk(s, new Vector3(s.entrance.position.x, .1f, -11)); await Walk(s, new Vector3(s.entrance.position.x, .1f, -9.6f));
            Check(s.TryEnter(), "Traversable corridor reaches teacher interaction"); await AdvanceUntil(s, EpisodeThreeStory.Phase.FindingSeat);
            await Walk(s, new Vector3(s.entrance.position.x, .1f, -6.6f)); await Walk(s, new Vector3(s.seat.position.x, .1f, s.seat.position.z));
            Check(s.TrySit(), "Open doorway and chair approach are traversable"); await AdvanceUntil(s, EpisodeThreeStory.Phase.Examination);
            await Capture("examination");
            Check(!s.player.gameObject.activeSelf && !s.player.GetComponent<PlayerMovement>().enabled, "Seated examination locks movement and hides standing player");
            s.TogglePause(); float before = s.RemainingSeconds; s.TickExamination(30);
            Check(Time.timeScale == 0 && s.RemainingSeconds == before && !s.Answer(1) && !s.SelectQuestion(2), "Pause blocks examination actions and freezes time");
            s.TogglePause(); Check(Time.timeScale == 1, "Resume restores time");
            Check(!s.SelectQuestion(-1) && !s.Answer(3), "Invalid question and answer indexes rejected");
            Check(s.SelectQuestion(4) && s.Answer(2) && s.AnsweredCount == 1, "Questions can be answered out of order");
            Check(s.Answer(1) && s.AnsweredCount == 1 && s.AnswerFor(4) == 1, "Revising an answer never duplicates progress");
            s.FlagQuestion(); Check(s.SelectQuestion(0) && s.Answer(1), "Second distinct answer accepted");
            Check(s.CurrentPhase == EpisodeThreeStory.Phase.Grounding, "Two answers trigger the grounding story beat");
            await Capture("grounding");
            before = s.RemainingSeconds; s.TickExamination(50); Check(s.RemainingSeconds == before && !s.Answer(0), "Grounding freezes clock and blocks paper input");
            s.TogglePause(); Check(!s.Ground(), "Pause also blocks grounding input"); s.TogglePause();
            for (int i = 0; i < 3; i++) { await Task.Delay(900); Check(s.Ground(), "Grounding step " + (i + 1)); }
            Check(s.HasGrounded && s.CurrentPhase == EpisodeThreeStory.Phase.Examination && s.AnsweredCount == 2, "Grounding returns to preserved examination");
            s.TickExamination(10000); Check(s.TimeExpired && s.RemainingSeconds == 0 && s.CurrentPhase == EpisodeThreeStory.Phase.Examination, "Clock expiry allows wrap-up without failure");
            Check(s.SelectQuestion(3) && s.Answer(0), "Answering still works after the bell");
            s.RequestSubmit(); before = s.RemainingSeconds; s.TickExamination(20);
            Check(!s.Answer(2) && !s.SelectQuestion(1) && s.RemainingSeconds == before, "Confirmation modal guards the paper");
            s.CancelSubmit(); Check(s.SelectQuestion(2), "Submission can be cancelled");
            s.RequestSubmit(); Check(s.Submit(), "Incomplete paper can be submitted deliberately");
            Check(!s.Submit() && !s.Answer(0), "Submission cannot repeat or mutate answers");
            await AdvanceUntil(s, EpisodeThreeStory.Phase.Complete); Check(PlayerPrefs.GetInt(EpisodeThreeStory.SaveKey) == 1, "Completion persists");
            await Capture("completion");
            s.Replay(); s = await Ready(s); Check(!s.TimeExpired && s.AnsweredCount == 0 && !s.HasGrounded, "Replay resets all transient state");
            s.SkipOpening(); s.ReturnToMenu(); await WaitFor(() => SceneManager.GetActiveScene().name == "main_menu_new" && Object.FindAnyObjectByType<MainMenuNewController>() != null, "main menu");
            var menu = Object.FindAnyObjectByType<MainMenuNewController>(); menu.ShowPage("episodes");
            var ui = menu.GetComponent<UIDocument>().rootVisualElement;
            Check(ui.Q<UnityEngine.UIElements.Button>("episode-3").enabledSelf && !ui.Q<UnityEngine.UIElements.Button>("episode-4").enabledSelf, "Menu enables Episode 3 and keeps Episode 4 unavailable");
            Check(ui.Q("episode-3").Query<UnityEngine.UIElements.Label>().ToList().Any(x => x.text.Contains("REPLAY")), "Completed episode shows Replay");
            await Capture("menu");
            menu.ShowPage("continue"); Check(ui.Query<UnityEngine.UIElements.Label>().ToList().Any(x => x.text == "Running Out of Time"), "Continue identifies Episode 3 save ordinal");
            Check(Application.CanStreamedLevelBeLoaded(EpisodeThreeStory.SceneName), "Episode 3 included in active build settings");
            Check(Resources.Load<Texture2D>("EpisodeThree/ClassroomPreview") != null, "Episode 3 has classroom loading artwork");
            // Validate the Episode 2 completion button through its real listener and loading screen.
            SceneManager.LoadScene(EpisodeTwoStory.SceneName); await WaitFor(() => Object.FindAnyObjectByType<EpisodeTwoStory>() != null, "Episode 2"); await Task.Delay(700);
            var two = Object.FindAnyObjectByType<EpisodeTwoStory>(); two.SkipOpening();
            var phase = typeof(EpisodeTwoStory).GetProperty("CurrentPhase"); phase.SetValue(two, EpisodeTwoStory.Phase.Complete);
            two.ContinueToNextEpisode();
            await WaitFor(() => SceneManager.GetActiveScene().name == EpisodeThreeStory.SceneName && EpisodeLoadingScreen.IsActive, "Episode 3 loading screen");
            // The shared loader deliberately waits for a real input event after its five-second minimum.
            await Task.Delay(6500);
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Space));
            await Task.Delay(150);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            s = await Ready();
            Check(SceneManager.GetActiveScene().name == EpisodeThreeStory.SceneName && PlayerPrefs.GetInt("Mindscape.Episode") == 4, "Episode 2 continuation loads Episode 3 and its save ordinal");
            // A second run submits zero answers and verifies opening dialogue can be advanced normally.
            await AdvanceUntil(s, EpisodeThreeStory.Phase.Corridor); Move(s, s.entrance.position - Vector3.up * 1.5f); s.TryEnter(); await AdvanceUntil(s, EpisodeThreeStory.Phase.FindingSeat);
            Move(s, s.seat.position - Vector3.up * 1.5f); s.TrySit(); await AdvanceUntil(s, EpisodeThreeStory.Phase.Examination);
            s.RequestSubmit(); Check(s.Submit(), "Zero-answer submission is accepted"); await AdvanceUntil(s, EpisodeThreeStory.Phase.Complete);
            Check(errors.Count == 0, "No runtime errors during both story flows");
            Report = "ALL CHECKS PASSED\n" + string.Join("\n", results);
        }
        catch (Exception ex) { Report = "FAIL: " + ex + "\n" + string.Join("\n", results); Debug.LogError(Report); }
        finally
        {
            Application.logMessageReceived -= capture; Time.timeScale = 1;
            for (int i = 0; i < keys.Length; i++) { if (saves[i].HasValue) PlayerPrefs.SetInt(keys[i], saves[i].Value); else PlayerPrefs.DeleteKey(keys[i]); }
            PlayerPrefs.Save(); Application.runInBackground = background; Running = false;
            System.IO.File.WriteAllText("Temp/EpisodeThreeVerification.txt", Report); Debug.Log(Report);
        }
    }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); results.Add("PASS: " + message); }
    static async Task WaitFor(Func<bool> condition, string label)
    { double end = EditorApplication.timeSinceStartup + 35; while (!condition()) { if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > end) throw new Exception("Timed out waiting for " + label); await Task.Delay(150); } }
    static async Task<EpisodeThreeStory> Ready(EpisodeThreeStory previous = null)
    { await WaitFor(() => Object.FindAnyObjectByType<EpisodeThreeStory>() is EpisodeThreeStory s && s != previous && s.Ready, "Episode 3 ready"); return Object.FindAnyObjectByType<EpisodeThreeStory>(); }
    static async Task Capture(string name)
    { System.IO.Directory.CreateDirectory("tmp/episode3-verification"); ScreenCapture.CaptureScreenshot("tmp/episode3-verification/" + name + ".png"); await Task.Delay(350); }
    static async Task AdvanceUntil(EpisodeThreeStory s, EpisodeThreeStory.Phase phase)
    { double end = EditorApplication.timeSinceStartup + 25; while (s.CurrentPhase != phase) { if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > end) throw new Exception("Timed out: " + s.CurrentPhase + " -> " + phase); s.Advance(); await Task.Delay(350); } }
    static void Move(EpisodeThreeStory s, Vector3 at)
    { var cc = s.player.GetComponent<CharacterController>(); cc.enabled = false; s.player.position = at; cc.enabled = true; Physics.SyncTransforms(); }
    static async Task Walk(EpisodeThreeStory s, Vector3 target)
    {
        var movement = s.player.GetComponent<PlayerMovement>(); movement.enabled = false; var cc = s.player.GetComponent<CharacterController>(); double end = EditorApplication.timeSinceStartup + 15;
        while (Vector3.ProjectOnPlane(s.player.position - target, Vector3.up).magnitude > .28f)
        { if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > end) throw new Exception("Walk blocked at " + s.player.position + " toward " + target); cc.Move(Vector3.ProjectOnPlane(target - s.player.position, Vector3.up).normalized * .15f + Vector3.down * .05f); await Task.Delay(20); }
        movement.enabled = true;
    }
}
