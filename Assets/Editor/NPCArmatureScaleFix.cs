using UnityEditor;
using UnityEngine;

// The NPC roster (Blender Files/NPC_Roster.blend) is sometimes exported with Blender's default FBX scale option,
// which leaves every armature at scale 100 and keys that scale into every clip. The NPCs then look right in the
// editor but grow ~100x tall the moment an animation plays (and pop around while they walk and turn).
// Bones and meshes inside the armature are already in metres, so on import put the armature back at scale 1 and
// drop the clips' armature-scale keys. Files exported correctly (armature at 1) are left as they are.
public class NPCArmatureScaleFix : AssetPostprocessor
{
    const string Folder = "Assets/Characters/NPCs/";

    public override uint GetVersion() => 1;

    static bool Applies(string path) => path.StartsWith(Folder) && path.EndsWith(".fbx");

    void OnPostprocessModel(GameObject model)
    {
        if (!Applies(assetPath)) return;
        foreach (Transform child in model.transform)
            if (child.name.StartsWith("Armature") && child.localScale.x > 50f)
                child.localScale = Vector3.one;
    }

    void OnPostprocessAnimation(GameObject root, AnimationClip clip)
    {
        if (!Applies(assetPath)) return;
        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
        {
            // Only the armature object itself (a top-level path), never the bones below it.
            if (!binding.path.StartsWith("Armature") || binding.path.Contains("/") || !binding.propertyName.StartsWith("m_LocalScale")) continue;
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve == null || curve.length == 0 || curve.keys[0].value < 50f) continue;
            AnimationUtility.SetEditorCurve(clip, binding, null);
        }
    }
}
