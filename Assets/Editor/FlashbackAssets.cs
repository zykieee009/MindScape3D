using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// The people and props Chapter 2 Episode 5's keepsake flashbacks bring in at runtime (FamilyFinale/KeepsakeFlashbacks.cs),
// saved as prefabs in Resources/Flashback:
//   Joey      the player's rig with all eight shared actions (Joey_Actions.fbx, exported from NPC_Roster.blend's
//             BaldCharacter_Rig): Idle, Walk, Talk, Seated, Writing.
//   Justin    NPC_06_Ethan and Aaron NPC_03_Leo, the friends from Chapter 3, as boys playing catch: Idle, Walk, Talk.
//   Baseball set  the bat, glove and ball (Assets/Models/Collectibles/Keepsake_Baseball.glb).
public static class FlashbackAssets
{
    const string Folder = "Assets/Resources/Flashback";
    const string JoeyModel = "Assets/Characters/BaldCharacter/Joey_Actions.fbx";
    public const string BaseballModel = "Assets/Models/Collectibles/Keepsake_Baseball.glb";

    [MenuItem("Mindscape/Flashbacks/Build Flashback Assets")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder);
        LoopClips(JoeyModel);
        Prefab("Joey", JoeyModel, ("Idle", "Idle"), ("Walk", "Walk"), ("Talk", "Conversation"), ("Seated", "Seated_Idle"), ("Writing", "Sitting_Writing"));
        Prefab("Justin", "Assets/Characters/NPCs/NPC_06_Ethan.fbx", ("Idle", "Idle"), ("Walk", "Walk"), ("Talk", "Conversation"));
        Prefab("Aaron", "Assets/Characters/NPCs/NPC_03_Leo.fbx", ("Idle", "Idle"), ("Walk", "Walk"), ("Talk", "Conversation"));
        var set = AssetDatabase.LoadAssetAtPath<GameObject>(BaseballModel);
        if (set == null) Debug.LogWarning("Missing " + BaseballModel);
        else
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(set);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            PrefabUtility.SaveAsPrefabAsset(go, Folder + "/Baseball set.prefab");
            Object.DestroyImmediate(go);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Flashback assets built in " + Folder);
    }

    // Imported clips do not loop by default; loop everything except the one-shot sit-down.
    static void LoopClips(string model)
    {
        var importer = AssetImporter.GetAtPath(model) as ModelImporter;
        if (importer == null || importer.clipAnimations.Length > 0) return;
        importer.clipAnimations = importer.defaultClipAnimations.Select(c => { c.loopTime = !c.name.Contains("Sit_Down"); return c; }).ToArray();
        importer.SaveAndReimport();
    }

    static void Prefab(string name, string model, params (string state, string clip)[] states)
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(model).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        AnimationClip Clip(string n) => clips.FirstOrDefault(c => c.name == n || c.name.EndsWith("|" + n));
        string controllerPath = Folder + "/" + name + ".controller";
        AssetDatabase.DeleteAsset(controllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var machine = controller.layers[0].stateMachine;
        foreach (var (state, clip) in states)
        {
            var motion = Clip(clip);
            if (motion == null) { Debug.LogWarning(model + " has no '" + clip + "' clip."); continue; }
            var s = machine.AddState(state); s.motion = motion;
            if (state == "Idle") machine.defaultState = s;
        }
        EditorUtility.SetDirty(controller);

        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(model);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        go.name = name;
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        var animator = go.GetComponent<Animator>(); if (animator == null) animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        PrefabUtility.SaveAsPrefabAsset(go, Folder + "/" + name + ".prefab");
        Object.DestroyImmediate(go);
    }
}
