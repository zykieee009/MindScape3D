using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Mindscape.EpisodeTwo;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FamilyEpisodeTwoVerification
{
    public static string Report {get;private set;}="Not run";
    public static bool Running {get;private set;}
    static List<string> results;    // Superseded: Chapter 2 is built on Chapter 1's scenes by Mindscape > Chapter 2 - Family Expectations > Build All Episodes
    // (FamilyExpectationsBuilder.cs). The original scenes are kept in Assets/Scenes/Prototypes.

    // [MenuItem("Mindscape/Chapter 2 - Family Expectations/Episode 2/Verify Gameplay (Play Mode)")]
    public static async void Run()
    {
        if(Running) return; Running=true; results=new List<string>(); Report="Running";
        string[] keys={EpisodeTwoStory.SaveKey,"Mindscape.Chapter","Mindscape.Episode"}; var saves=keys.Select(k=>PlayerPrefs.GetInt(k,-1)).ToArray(); bool background=Application.runInBackground;
        try
        {
            Check(EditorApplication.isPlaying,"Play mode active"); Application.runInBackground=true;
            var s=UnityEngine.Object.FindAnyObjectByType<EpisodeTwoStory>(); await Task.Delay(500); s.SkipOpening();
            Check(s.CurrentPhase==EpisodeTwoStory.Phase.Alarm && s.CollectedCount==0,"Fresh start at alarm");
            Check(!s.TryCollect(0),"No collecting before alarm"); Check(s.SilenceAlarm(),"Alarm starts preparation");
            s.TogglePause(); float before=s.RemainingSeconds; s.TickPreparation(20); await Task.Delay(300);
            Check(s.RemainingSeconds==before && Time.timeScale==0 && !s.TryCollect(0),"Pause freezes timer and interactions"); s.TogglePause();
            Check(!s.TryCollect(2),"Distant collection rejected");
            Move(s,new Vector3(-22, .65f,3.8f)); Check(!s.TryLeave(),"Door requires all three items");
            Move(s,s.items[2].position-Vector3.up*.6f); Check(s.TryCollect(2),"Bag can be collected first"); Check(!s.TryCollect(2),"Duplicate collection rejected");
            s.TickPreparation(500); Check(s.RunningLate && s.RemainingSeconds==0 && s.CurrentPhase==EpisodeTwoStory.Phase.Preparing,"Timer expiry keeps gameplay active");
            Move(s,new Vector3(-16.8f,.65f,-11)); Check(s.TryCollect(0),"ID remains collectible after timer expires");
            Move(s,new Vector3(-16.8f,.65f,-8.7f)); Check(s.TryCollect(1) && s.AllPacked,"All three belongings collected");
            Check(!s.TryLeave(),"Distant exit rejected");
            Move(s,new Vector3(-22,.65f,3.8f)); Check(s.TryLeave(),"Packed player can leave");
            await AdvanceUntil(s,EpisodeTwoStory.Phase.Walking);
            s.TogglePhone(); Check(!s.player.GetComponent<PlayerMovement>().enabled && !s.ReachStop(),"Phone overlay locks movement and interaction"); s.TogglePhone();
            // Move the real CharacterController, rather than teleporting, through the yard and sidewalk.
            await Walk(s,new Vector3(-22.1f,.7f,22.5f)); await Walk(s,new Vector3(24,.7f,22.5f));
            Check(s.ReachStop(),"Walkable gate-to-stop route reaches waiting objective"); Check(!s.TryBoard(),"Cannot board before bus arrives");
            Check(s.CheckTimeAtStop(),"Waiting starts the late-study flashback"); await AdvanceUntil(s,EpisodeTwoStory.Phase.Boarding);
            Check(s.bus.gameObject.activeSelf && s.player.gameObject.activeSelf,"Bus arrival restores player and scene");
            Move(s,new Vector3(26,.5f,24)); Check(s.TryBoard(),"Boarding starts departure"); await AdvanceUntil(s,EpisodeTwoStory.Phase.Complete);
            Check(PlayerPrefs.GetInt(EpisodeTwoStory.SaveKey)==1,"Episode completion saved");
            s.Replay(); await Task.Delay(1200); s=UnityEngine.Object.FindAnyObjectByType<EpisodeTwoStory>();
            Check(s!=null && s.CollectedCount==0 && !s.RunningLate,"Replay resets belongings and timer");
            s.ReturnToMenu(); await Task.Delay(1500); Check(SceneManager.GetActiveScene().name=="main_menu_new","Return to menu works");
            Check(Application.CanStreamedLevelBeLoaded(EpisodeTwoStory.SceneName),"Episode 2 included in active build scenes");
            Report="ALL CHECKS PASSED\n"+string.Join("\n",results);
        }
        catch(Exception ex) { Report="FAIL: "+ex+"\n"+string.Join("\n",results); Debug.LogError(Report); }
        finally
        { Time.timeScale=1; for(int i=0;i<keys.Length;i++) { if(saves[i]<0) PlayerPrefs.DeleteKey(keys[i]); else PlayerPrefs.SetInt(keys[i],saves[i]); } PlayerPrefs.Save(); Application.runInBackground=background; Running=false; System.IO.File.WriteAllText("Temp/EpisodeTwoVerification.txt",Report); Debug.Log(Report); }
    }
    static void Check(bool ok,string message) { if(!ok) throw new Exception(message); results.Add("PASS: "+message); }
    public static async void VerifyInterior()
    {
        Running=true; Report="Checking indoor route"; Application.runInBackground=true;
        try
        {
            var s=UnityEngine.Object.FindAnyObjectByType<EpisodeTwoStory>(); s.SkipOpening(); s.SilenceAlarm();
            await Walk(s,new Vector3(-16.8f,.65f,-8.7f));
            await Walk(s,new Vector3(-17,.65f,-7.4f));
            await Walk(s,new Vector3(-22,.65f,-7.4f));
            await Walk(s,new Vector3(-22,.65f,2));
            Report="PASS: Bedroom, study desk, open doorway and living room are traversable.";
        }
        catch(Exception ex) { Report="FAIL: "+ex; Debug.LogError(Report); }
        finally { Running=false; }
    }
    static void Move(EpisodeTwoStory s,Vector3 at) { var cc=s.player.GetComponent<CharacterController>(); cc.enabled=false; s.player.position=at; cc.enabled=true; Physics.SyncTransforms(); }
    static async Task AdvanceUntil(EpisodeTwoStory s,EpisodeTwoStory.Phase target)
    { double end=EditorApplication.timeSinceStartup+40; while(s.CurrentPhase!=target) { if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup>end) throw new Exception("Timed out: "+s.CurrentPhase+" -> "+target); s.Advance(); await Task.Delay(350); } }
    static async Task Walk(EpisodeTwoStory s,Vector3 destination)
    {
        var move=s.player.GetComponent<PlayerMovement>(); move.enabled=false; var cc=s.player.GetComponent<CharacterController>();
        double end=EditorApplication.timeSinceStartup+25;
        while(Vector3.Distance(Vector3.ProjectOnPlane(s.player.position-destination,Vector3.up),Vector3.zero)>.4f)
        { if(EditorApplication.timeSinceStartup>end) throw new Exception("Walking blocked at "+s.player.position+" toward "+destination); Vector3 dir=Vector3.ProjectOnPlane(destination-s.player.position,Vector3.up).normalized; cc.Move(dir*.22f+Vector3.down*.06f); await Task.Delay(20); }
        move.enabled=true;
    }
}
