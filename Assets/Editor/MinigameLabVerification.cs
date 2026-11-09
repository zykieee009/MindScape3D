using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mindscape.Minigames;
using UnityEditor;
using UnityEngine;

public static class MinigameLabVerification
{
    static object Field(object o,string name)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
    static void Set(object o,string name,object value)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,value);
    static void Invoke(object o,string name)=>o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,null);
    static void Require(bool value,string message) { if(!value)throw new Exception(message); }
    [MenuItem("Mindscape/Minigame Lab/Verify All Games (Play Mode)")]
    public static void Verify()
    {
        var report=new List<string>();
        try
        {
            Require(EditorApplication.isPlaying,"Run verification in Play mode.");
            var hub=UnityEngine.Object.FindAnyObjectByType<MinigameTestHub>(); Require(hub!=null,"Open the MinigameTestLab scene first.");
            Require(hub.minigames.Length==10,"Expected ten minigames.");
            foreach(var game in hub.minigames)
            {
                int completions=0,closes=0;
                UnityEngine.Events.UnityAction done=()=>completions++;
                UnityEngine.Events.UnityAction closed=()=>closes++;
                game.onCompleted.AddListener(done); game.onClosed.AddListener(closed);
                try
                {
                    game.Open(); Require(game.IsOpen && !game.IsComplete,game.Title+" did not open cleanly.");
                    if(game is BreathingRhythmMinigame breathing)
                    {
                        for(int i=0;i<breathing.cycles*3;i++) {Set(breathing,"elapsed",100f);Invoke(breathing,"Update");}
                        Require(!breathing.IsComplete && (int)Field(breathing,"phase")==-1,"Missed breathing phases accepted.");
                        breathing.Tap(); Require(!breathing.IsComplete,"Late breathing tap accepted."); breathing.Replay();
                        for(int i=0;i<breathing.cycles*3;i++) {breathing.Tap();breathing.Tap();Set(breathing,"elapsed",100f);Invoke(breathing,"Update");}
                    }
                    else if(game is ThoughtReframingMinigame thought){thought.Choose(2);Require(!game.IsComplete,"Wrong thought accepted.");thought.Choose(0);}
                    else if(game is OrderedActionsMinigame ordered)
                    {ordered.Choose(2);Require(!game.IsComplete,"Wrong order accepted.");int n=game is TaskPrioritizationMinigame?3:4;for(int i=0;i<n;i++)ordered.Choose(i);}
                    else if(game is FocusChallengeMinigame focus)
                    {
                        focus.ClickItem(); focus.ClickItem(); Require((int)Field(focus,"score")==0,"Distraction penalty failed.");
                        for(int i=0;i<focus.targetScore;i++){ if(!(bool)Field(focus,"important"))Invoke(focus,"NextItem"); focus.ClickItem(); }
                    }
                    else if(game is MemoryMatchMinigame memory)
                    {
                        int[] deck=(int[])Field(memory,"deck");
                        int mismatch=1; while(deck[mismatch]/2==deck[0]/2)mismatch++;
                        memory.Flip(0);memory.Flip(mismatch);Require((bool)Field(memory,"busy"),"Mismatch should temporarily lock cards.");
                        memory.Replay();Require(!(bool)Field(memory,"busy"),"Replay did not reset mismatch lock.");deck=(int[])Field(memory,"deck");
                        for(int pair=0;pair<4;pair++)for(int i=0;i<deck.Length;i++)if(deck[i]/2==pair)memory.Flip(i);
                    }
                    else if(game is GroundingExerciseMinigame grounding)
                    {
                        grounding.Choose(0); grounding.Choose(0); Require((int)Field(grounding,"count")==1,"Grounding double count.");
                        for(int i=1;i<5;i++)grounding.Choose(i);
                        foreach(int i in new[]{1,2,3,5})grounding.Choose(i);
                        foreach(int i in new[]{0,4,5})grounding.Choose(i);
                        foreach(int i in new[]{1,6})grounding.Choose(i);
                        grounding.Choose(7);
                    }
                    else if(game is SchedulePuzzleMinigame schedule)
                    {
                        schedule.Check(); Require(!game.IsComplete,"Empty schedule accepted.");
                        schedule.Select(0);schedule.Place(0);schedule.Select(0);schedule.Place(1);
                        Require(((int[])Field(schedule,"placed"))[0]==-1,"Schedule duplicated card.");
                        for(int i=0;i<5;i++){schedule.Select(i);schedule.Place(i);}schedule.Check();
                    }
                    else if(game is EmotionCheckMinigame emotion){emotion.Choose(3);Require(!game.IsComplete,"Wrong emotion accepted.");for(int i=0;i<3;i++)emotion.Choose(i);}
                    else if(game is StressMeterMinigame stress){stress.Choose(0);stress.Choose(0);Require((int)Field(stress,"moves")==1,"Stress action counted twice.");stress.Choose(2);stress.Choose(3);}
                    Require(game.IsComplete && completions==1,game.Title+" completion failed or fired twice.");
                    game.Replay(); Require(game.IsOpen && !game.IsComplete,"Replay failed.");
                    game.Close();game.Close(); Require(!game.IsOpen && closes==1,"Close cleanup or event failed.");
                    report.Add("PASS: "+game.Title+" — completion, replay, close");
                }
                finally { game.Close();game.onCompleted.RemoveListener(done);game.onClosed.RemoveListener(closed); }
            }
            hub.minigames[0].Open();hub.minigames[1].Open();Require(!hub.minigames[0].IsOpen && hub.minigames[1].IsOpen,"Popup exclusivity failed.");hub.minigames[1].Close();
            report.Add("PASS: popup exclusivity"); report.Add("ALL 10 MINIGAMES PASSED");
        }
        catch(Exception e){report.Add("FAIL: "+e);Debug.LogException(e);}
        Directory.CreateDirectory("Temp");File.WriteAllLines("Temp/MinigameLabVerification.txt",report);Debug.Log(string.Join("\n",report));
    }
}
