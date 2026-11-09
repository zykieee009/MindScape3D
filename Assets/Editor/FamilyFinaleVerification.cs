using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Mindscape.AcademicPressure;
using Mindscape.FamilyFinale;
using Mindscape.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Exercises the real story coroutines; private interaction state is driven only in this editor test.
public static class FamilyFinaleVerification
{
    public static string Report = "Not run";
    public static bool Running;
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static FieldInfo Field(string n) => typeof(AcademicEpisode).GetField(n, Flags);    // Superseded: Chapter 2 is built on Chapter 1's scenes by Mindscape > Chapter 2 - Family Expectations > Build All Episodes
    // (FamilyExpectationsBuilder.cs). The original scenes are kept in Assets/Scenes/Prototypes.

    // [MenuItem("Mindscape/Chapter 2 - Family Expectations/Verify Finale (Play Mode)")]
    public static async void Run()
    {
        if (!EditorApplication.isPlaying || Running) return;
        Running = true; Report = "Running";
        string[] ints = { "Mindscape.Chapter", "Mindscape.Episode", "Mindscape.ChapterUnlocked", "Mindscape.FamilyExpectations.Complete", FamilyFinaleEpisode.SaveKey(4), FamilyFinaleEpisode.SaveKey(5) };
        string[] strings = { "Mindscape.C2.Choice.family.main_worry", "Mindscape.C2.Choice.family.dinner_response", "Mindscape.C2.Choice.family.past_reflection" };
        var oldInts = new Dictionary<string, int?>(); var oldStrings = new Dictionary<string, string>();
        foreach (var k in ints) oldInts[k] = PlayerPrefs.HasKey(k) ? PlayerPrefs.GetInt(k) : (int?)null;
        foreach (var k in strings) oldStrings[k] = PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null;
        // Suppress database writes during automated branch coverage, preserving the live connection.
        bool initialized = MindscapeDatabase.UsingSqlite;
        var db = typeof(MindscapeDatabase).GetField("db", BindingFlags.Static | BindingFlags.NonPublic);
        object savedDb = db.GetValue(null); db.SetValue(null, null);
        var results = new List<string>(); var errors = new List<string>();
        Application.LogCallback listener = (m, st, t) => { if (t == LogType.Error || t == LogType.Exception) errors.Add(m); };
        Application.logMessageReceived += listener;
        bool background = Application.runInBackground; Application.runInBackground = true;
        try
        {
            for (int ep = 4; ep <= 5; ep++)
            for (int branch = 0; branch < 3; branch++)
            {
                SceneManager.LoadScene(FamilyFinaleEpisode.Scene(ep));
                await Task.Delay(600);
                var s = UnityEngine.Object.FindAnyObjectByType<FamilyFinaleEpisode>();
                double deadline = EditorApplication.timeSinceStartup + 45;
                while (s != null && !s.IsComplete)
                {
                    if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > deadline) throw new Exception("Story stalled: episode " + ep + " / " + s.CurrentObjective);
                    Field("lineAt").SetValue(s, -100f);
                    int count = (int)Field("choiceCount").GetValue(s);
                    if (count > 0) typeof(AcademicEpisode).GetMethod("Pick", Flags).Invoke(s, new object[] { branch % count });
                    else if (s.IsRoaming)
                    {
                        var eligible = new List<object>();
                        foreach (object spot in (IEnumerable)Field("spots").GetValue(s))
                            if (((Func<bool>)spot.GetType().GetField("available").GetValue(spot))()) eligible.Add(spot);
                        if (eligible.Count == 0) throw new Exception("No available interactions");
                        object selected = eligible[branch % eligible.Count];
                        var target = (Transform)selected.GetType().GetField("target").GetValue(selected);
                        var cc = s.player.GetComponent<CharacterController>(); cc.enabled = false;
                        s.player.position = target.position + new Vector3(0, -1.5f, -1);
                        s.player.rotation = Quaternion.identity; cc.enabled = true;
                        Physics.SyncTransforms();
                        if (typeof(AcademicEpisode).GetMethod("FindNearest", Flags).Invoke(s, null) == null) throw new Exception("Interaction unreachable: " + target.name);
                        Field("pending").SetValue(s, selected);
                    }
                    else s.Advance();
                    await Task.Delay(80);
                }
                if (s == null || !s.IsComplete) throw new Exception("Missing story");
                if (ep == 4 && (s.WorryChoice != branch || s.DinnerChoice != branch % 2 || !s.DinnerFinished)) throw new Exception("Dinner branch mismatch");
                if (ep == 5 && (s.SeenMask != 7 || s.ReflectionChoice != branch)) throw new Exception("Keepsake/reflection mismatch");
                if (PlayerPrefs.GetInt(FamilyFinaleEpisode.SaveKey(ep)) != 1) throw new Exception("Completion not saved");
                results.Add("PASS Episode " + ep + " branch " + branch + ": story, interactions, choices, completion");
            }
            if (errors.Count != 0) throw new Exception(string.Join("\n", errors));
            Report = "ALL CHECKS PASSED\n" + string.Join("\n", results);
        }
        catch (Exception ex) { Report = "FAIL: " + ex + "\n" + string.Join("\n", results); }
        finally
        {
            Application.logMessageReceived -= listener; db.SetValue(null, savedDb);
            foreach (var p in oldInts) { if (p.Value.HasValue) PlayerPrefs.SetInt(p.Key, p.Value.Value); else PlayerPrefs.DeleteKey(p.Key); }
            foreach (var p in oldStrings) { if (p.Value != null) PlayerPrefs.SetString(p.Key, p.Value); else PlayerPrefs.DeleteKey(p.Key); }
            PlayerPrefs.Save(); Application.runInBackground = background; Running = false;
            System.IO.File.WriteAllText("Temp/FamilyFinaleVerification.txt", Report); Debug.Log(Report);
        }
    }
}
