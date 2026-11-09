using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static partial class AcademicPressureBuilder
{
    // Gives an already-built seated cast member a different clip (e.g. Seated_Idle -> Sitting_Writing) without
    // rebuilding the scene: samples the new pose, keeps the hips where they sat, and swaps the Animator controller.
    // Returns false when the person has no model or the model has no such clip.
    public static bool SetSeatedClip(Transform person, string clip)
    {
        var body = person != null ? person.Find("Model") : null;
        if (body == null) return false;
        var source = PrefabUtility.GetCorrespondingObjectFromSource(body.gameObject);
        string path = source != null ? AssetDatabase.GetAssetPath(source) : null;
        if (string.IsNullOrEmpty(path)) return false;
        var motion = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__") && (c.name == clip || c.name.EndsWith("|" + clip)));
        if (motion == null) return false;

        var hips = body.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.Equals("hips", StringComparison.OrdinalIgnoreCase));
        Vector3 seat = hips != null ? hips.position : body.position;
        motion.SampleAnimation(body.gameObject, 0);
        if (hips != null) body.position += seat - hips.position; // the new pose sits on the same seat

        var animator = body.GetComponent<Animator>(); if (animator == null) animator = body.gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = Controller(System.IO.Path.GetFileNameWithoutExtension(path), clip, motion);
        animator.applyRootMotion = false;
        EditorUtility.SetDirty(animator); EditorUtility.SetDirty(body);
        return true;
    }
}
