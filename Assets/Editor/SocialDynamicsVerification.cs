using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Mindscape.AcademicPressure;
using Mindscape.Data;
using Mindscape.SocialDynamics;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SocialDynamicsVerification
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static FieldInfo Field(string name) => typeof(AcademicEpisode).GetField(name, Flags);
    public static bool Running;
    [MenuItem("Mindscape/Chapter 3 - Social Dynamics/Verify All Episodes (Play Mode)")]
    public static async void Run()
    {
        if (!EditorApplication.isPlaying || Running) return;
        Running = true;
        var ints = new[] { "Mindscape.Chapter", "Mindscape.Episode", "Mindscape.SocialDynamics.Complete" }.Concat(Enumerable.Range(1, 8).Select(SocialDynamicsChapter.SaveKey)).ToArray();
        var strings = new[] { "Mindscape.C3.Choice.social.invitation", "Mindscape.C3.Choice.social.check_in", "Mindscape.C3.Choice.social.appreciation", "Mindscape.C3.Choice.social.group_photo", "Mindscape.C3.E6.Journal" };
        var oldInts = ints.ToDictionary(k => k, k => PlayerPrefs.HasKey(k) ? (int?)PlayerPrefs.GetInt(k) : null);
        var oldStrings = strings.ToDictionary(k => k, k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null);
        bool initialized = MindscapeDatabase.UsingSqlite;
        var db = typeof(MindscapeDatabase).GetField("db", BindingFlags.Static | BindingFlags.NonPublic); var savedDb = db.GetValue(null); db.SetValue(null, null);
        bool background = Application.runInBackground; Application.runInBackground = true;
        var results = new List<string>(); var errors = new List<string>();
        Application.LogCallback listener = (m, st, t) => { if (t == LogType.Error || t == LogType.Exception) errors.Add(m); };
        Application.logMessageReceived += listener;
        string report;
        try
        {
            for (int ep = 1; ep <= 8; ep++)
            for (int branch = 0; branch < (ep == 6 ? 3 : ep == 1 || ep == 3 ? 2 : 1); branch++)
            {
                SceneManager.LoadScene(SocialDynamicsChapter.Scene(ep)); await Task.Delay(500);
                var s = UnityEngine.Object.FindAnyObjectByType<SocialDynamicsEpisode>();
                if (s == null) throw new Exception("Missing episode " + ep);
                double deadline = EditorApplication.timeSinceStartup + 60;
                bool pauseChecked = false;
                while (!s.IsComplete)
                {
                    if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > deadline) throw new Exception("Stalled episode " + ep + ": " + s.CurrentObjective);
                    if (!s.Ready) { await Task.Delay(100); continue; }
                    Field("lineAt").SetValue(s, -100f);
                    int count = (int)Field("choiceCount").GetValue(s);
                    if (count > 0) typeof(AcademicEpisode).GetMethod("Pick", Flags).Invoke(s, new object[] { branch % count });
                    else if (s.IsRoaming)
                    {
                        if (!pauseChecked) { s.TogglePause(); if (!s.IsPaused || Time.timeScale != 0) throw new Exception("Pause failed"); s.TogglePause(); pauseChecked = true; }
                        var eligible = new List<object>();
                        foreach (object spot in (IEnumerable)Field("spots").GetValue(s))
                            if (((Func<bool>)spot.GetType().GetField("available").GetValue(spot))()) eligible.Add(spot);
                        if (eligible.Count == 0) throw new Exception("No available interactions");
                        var selected = eligible[branch % eligible.Count]; var target = (Transform)selected.GetType().GetField("target").GetValue(selected);
                        var cc = s.player.GetComponent<CharacterController>(); cc.enabled = false;
                        s.player.position = target.position + new Vector3(0, -1.5f, -1); s.player.rotation = Quaternion.identity; cc.enabled = true;
                        Physics.SyncTransforms();
                        if (typeof(AcademicEpisode).GetMethod("FindNearest", Flags).Invoke(s, null) == null) throw new Exception("Unreachable interaction " + target.name);
                        Field("pending").SetValue(s, selected);
                    }
                    else s.Advance();
                    await Task.Delay(65);
                }
                if (PlayerPrefs.GetInt(SocialDynamicsChapter.SaveKey(ep)) != 1) throw new Exception("Completion missing");
                if ((ep == 1 || ep == 3 || ep == 6) && s.Response != branch) throw new Exception("Choice branch mismatch");
                if (ep == 1 && s.SeenMask != 7 || ep == 3 && s.SeenMask != 3) throw new Exception("Exploration incomplete");
                if (ep == 6 && MindscapeDatabase.LatestJournal(3, 6) != SocialDynamicsChapter.Appreciations[branch]) throw new Exception("Journal not saved");
                if (ep == 8 && PlayerPrefs.GetInt("Mindscape.SocialDynamics.Complete") != 1) throw new Exception("Chapter ending not saved");
                results.Add("PASS episode " + ep + " branch " + branch + ": exploration, pause, dialogue, choices, completion");
            }
            if (errors.Count > 0) throw new Exception(string.Join("\n", errors));
            report = "ALL CHECKS PASSED\n" + string.Join("\n", results);
        }
        catch (Exception ex) { report = "FAIL " + ex + "\n" + string.Join("\n", results); }
        finally
        {
            Application.logMessageReceived -= listener; db.SetValue(null, savedDb);
            foreach (var p in oldInts) { if (p.Value.HasValue) PlayerPrefs.SetInt(p.Key, p.Value.Value); else PlayerPrefs.DeleteKey(p.Key); }
            foreach (var p in oldStrings) { if (p.Value != null) PlayerPrefs.SetString(p.Key, p.Value); else PlayerPrefs.DeleteKey(p.Key); }
            PlayerPrefs.Save(); Application.runInBackground = background; Running = false;
        }
        System.IO.File.WriteAllText("Temp/SocialDynamicsVerification.txt", report); Debug.Log(report);
    }
}
