using System;
using System.IO;
using Mindscape.Minigames;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MinigameTestSceneBuilder
{
    public const string ScenePath="Assets/Scenes/MinigameTestLab.unity";
    static readonly Type[] Types={typeof(BreathingRhythmMinigame),typeof(ThoughtReframingMinigame),typeof(TaskPrioritizationMinigame),typeof(FocusChallengeMinigame),typeof(MemoryMatchMinigame),typeof(GroundingExerciseMinigame),typeof(SchedulePuzzleMinigame),typeof(EmotionCheckMinigame),typeof(StressMeterMinigame),typeof(QuickSequenceMinigame)};

    [MenuItem("Mindscape/Minigame Lab/Create or Open Test Scene")]
    public static void CreateScene()
    {
        if(EditorApplication.isPlaying) { Debug.LogWarning("Exit Play mode before opening the test scene."); return; }
        var previous=SceneManager.GetActiveScene();
        // Preserve any unsaved work by keeping the existing scene loaded when dirty.
        if(File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath,previous.isDirty?OpenSceneMode.Additive:OpenSceneMode.Single); return; }
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener)); camera.tag="MainCamera";
        camera.GetComponent<Camera>().clearFlags=CameraClearFlags.SolidColor; camera.GetComponent<Camera>().backgroundColor=MinigameUI.Ink;
        var root=new GameObject("Minigame Test Lab"); var hub=root.AddComponent<MinigameTestHub>(); hub.minigames=new MinigamePopup[Types.Length];
        Directory.CreateDirectory("Assets/Prefabs/Minigames"); AssetDatabase.Refresh();
        for(int i=0;i<Types.Length;i++)
        {
            string path="Assets/Prefabs/Minigames/"+Types[i].Name+".prefab";
            if(!File.Exists(path))
            {
                var source=new GameObject(Types[i].Name); var component=(MinigamePopup)source.AddComponent(Types[i]); source.name=component.Title;
                PrefabUtility.SaveAsPrefabAsset(source,path); UnityEngine.Object.DestroyImmediate(source);
            }
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),scene);
            instance.transform.SetParent(root.transform,false); hub.minigames[i]=instance.GetComponent<MinigamePopup>();
        }
        MinigameUI.EnsureInput();
        EditorSceneManager.SaveScene(scene,ScenePath); AssetDatabase.SaveAssets();
        if(previous.IsValid() && !previous.isDirty)EditorSceneManager.CloseScene(previous,true);
        Selection.activeGameObject=root;
        Debug.Log("Created Minigame Test Lab: 10 separate prefab GameObjects. Press Play to test.");
    }
}
