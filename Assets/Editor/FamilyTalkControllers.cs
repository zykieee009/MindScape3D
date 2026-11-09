using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Animator controllers for Mom, Dad and Tim with Idle (default), Talk (their Conversation clip) and Walk states, in
// Resources/FamilyTalk so the story can switch to them at runtime (HomeWelcome) without touching the scenes.
public static class FamilyTalkControllers
{
    const string Folder = "Assets/Resources/FamilyTalk";
    // Mom, Dad and Tim, then the reunion cast (Justin and the four old friends), who talk while they speak in Chapter 3.
    static readonly string[] Models = { "NPC_11_Amara", "NPC_13_David", "NPC_16_Tariq", "NPC_06_Ethan", "NPC_01_Maya", "NPC_03_Leo", "NPC_05_Chloe", "NPC_08_Zara" };

    [MenuItem("Mindscape/NPCs/Build Family Talk Controllers")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder);
        foreach (var model in Models)
        {
            string fbx = "Assets/Characters/NPCs/" + model + ".fbx";
            var clips = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
            AnimationClip Clip(string n) => clips.FirstOrDefault(c => c.name == n || c.name.EndsWith("|" + n));
            var idle = Clip("Idle"); var talk = Clip("Conversation"); var walk = Clip("Walk");
            if (idle == null) { Debug.LogWarning(fbx + " has no Idle clip; skipped."); continue; }
            string path = Folder + "/" + model + " Talk.controller";
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            machine.defaultState = machine.AddState("Idle");
            machine.defaultState.motion = idle;
            if (talk != null) machine.AddState("Talk").motion = talk;
            if (walk != null) machine.AddState("Walk").motion = walk;
            EditorUtility.SetDirty(controller);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Family talk controllers built in " + Folder);
    }
}
