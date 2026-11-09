using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mindscape.EpisodeOne;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Integration checks through the same public actions used by the episode UI.</summary>
public static class FamilyEpisodeOneVerification
{
    public static string Report { get; private set; } = "Not run";
    public static bool Running { get; private set; }
    static List<string> results;
    // Superseded: Chapter 2 is built on Chapter 1's scenes by Mindscape > Chapter 2 - Family Expectations > Build All Episodes
    // (FamilyExpectationsBuilder.cs). The original scenes are kept in Assets/Scenes/Prototypes.

    // [MenuItem("Mindscape/Chapter 2 - Family Expectations/Episode 1/Verify Gameplay (Play Mode)")]
    public static async void Run()
    {
        if(Running) return;
        Running=true; results=new List<string>(); Report="Running";
        int previous=PlayerPrefs.GetInt(EpisodeOneStory.SaveKey,-1);
        bool oldBackground=Application.runInBackground;
        try
        {
            Check(EditorApplication.isPlaying,"Play mode active");
            Application.runInBackground=true;
            var story=UnityEngine.Object.FindAnyObjectByType<EpisodeOneStory>();
            Check(story!=null,"Episode controller exists");
            await Task.Delay(500);
            story.SkipOpening();
            await WaitFor(()=>story.CurrentPhase==EpisodeOneStory.Phase.Exploring);
            Check(story.MemoryCount==0 && !story.ReadyForEnding,"Fresh start keeps ending locked");
            Check(!story.TryInspect(-1) && !story.TryInspect(3),"Invalid object indices rejected");
            var cc=story.player.GetComponent<CharacterController>();
            Vector3 original=story.player.position;
            cc.enabled=false; story.player.position=new Vector3(-21,.65f,-8); cc.enabled=true;
            Check(!story.TryInspect(0),"Distant interaction rejected");
            cc.enabled=false; story.player.position=original; cc.enabled=true;
            story.ToggleJournal();
            Check(!story.player.GetComponent<PlayerMovement>().enabled && !story.TryInspect(0),"Journal locks movement and interaction");
            story.ToggleJournal();
            story.TogglePause();
            Check(Time.timeScale==0 && !story.TryInspect(0),"Pause locks time and interaction");
            story.TogglePause();
            Check(Time.timeScale==1 && story.player.GetComponent<PlayerMovement>().enabled,"Resume restores controls");
            foreach(int index in new[]{2,2,0,1})
            {
                int before=story.MemoryCount;
                Check(story.TryInspect(index),"Object "+index+" opens");
                await Task.Delay(1300); story.Advance();
                await Task.Delay(index==0?2200:1300); story.Advance();
                await WaitFor(()=>story.CurrentPhase==EpisodeOneStory.Phase.Exploring);
                if(index==2 && before==1) Check(story.MemoryCount==1,"Rereading does not double count");
            }
            Check(story.SeenMask==7 && story.MemoryCount==3 && story.ReadyForEnding,"All memories collected in non-linear order");
            story.ToggleJournal();
            Check(story.GetComponentsInChildren<TMPro.TMP_Text>().AnyText("I remember watching the clock"),"Journal contains collected memories");
            story.ToggleJournal();
            Check(story.TryInspect(0),"Returning to paper starts ending");
            await Task.Delay(1300); story.Advance();
            await Task.Delay(1300); story.Advance();
            await WaitFor(()=>story.CurrentPhase==EpisodeOneStory.Phase.Complete);
            Check(PlayerPrefs.GetInt(EpisodeOneStory.SaveKey,0)==1,"Completion checkpoint persisted");
            Check(story.nextEpisodeScene==Mindscape.EpisodeTwo.EpisodeTwoStory.SceneName && Application.CanStreamedLevelBeLoaded(story.nextEpisodeScene),"Episode 2 continuation points to an included scene");
            story.Replay();
            await WaitFor(()=>UnityEngine.Object.FindAnyObjectByType<EpisodeOneStory>()!=null && UnityEngine.Object.FindAnyObjectByType<EpisodeOneStory>()!=story);
            story=UnityEngine.Object.FindAnyObjectByType<EpisodeOneStory>();
            await Task.Delay(600);
            Check(story.MemoryCount==0,"Replay resets memory progress");
            story.ReturnToMenu();
            await WaitFor(()=>SceneManager.GetActiveScene().name=="main_menu_new");
            Check(UnityEngine.Object.FindAnyObjectByType<MainMenuNewController>()!=null,"Return to main menu works");
            SceneManager.LoadScene("Cutscene - Episode 1");
            await WaitFor(()=>SceneManager.GetActiveScene().name==Mindscape.EpisodeOne.EpisodeOneStory.SceneName);
            Check(UnityEngine.Object.FindAnyObjectByType<EpisodeOneStory>()!=null,"Existing cutscene entry routes to playable episode");
            Report="ALL CHECKS PASSED\n"+string.Join("\n",results);
        }
        catch(Exception ex) { Report="FAIL: "+ex+"\n"+string.Join("\n",results); Debug.LogError(Report); }
        finally
        {
            Time.timeScale=1;
            if(previous<0) PlayerPrefs.DeleteKey(EpisodeOneStory.SaveKey); else PlayerPrefs.SetInt(EpisodeOneStory.SaveKey,previous);
            PlayerPrefs.Save(); Application.runInBackground=oldBackground; Running=false;
            System.IO.Directory.CreateDirectory("Temp"); System.IO.File.WriteAllText("Temp/EpisodeOneVerification.txt",Report);
            Debug.Log(Report);
        }
    }
    static void Check(bool value,string description) { if(!value) throw new Exception(description); results.Add("PASS: "+description); }
    static async Task WaitFor(Func<bool> condition)
    {
        double deadline=EditorApplication.timeSinceStartup+15;
        while(!condition()) { if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup>deadline) throw new Exception("Timed out waiting for episode transition"); await Task.Delay(100); }
    }
    static bool AnyText(this TMPro.TMP_Text[] labels,string text)
    { foreach(var label in labels) if(label.text.Contains(text)) return true; return false; }
}
