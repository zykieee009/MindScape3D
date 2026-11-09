using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static partial class AcademicPressureBuilder
{
    // Mom, Dad and Tim wear their cast models (FamilyModels) everywhere, without rebuilding the scenes: the old family set
    // is swapped for the cast model in place (same pivot, height, clip and NPC setup), and any student who wore one of the
    // family's models gets another student's model. Ambient Life is then re-applied, which re-picks the street walkers
    // (they no longer use the family's models) and rebuilds Chapter 1 Episode 1's family. Safe to re-run.
    [MenuItem("Mindscape/NPCs/Give the Family Their Cast Models (All Scenes)")]
    public static void RecastFamilyInAllScenes()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play mode first."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string active = SceneManager.GetActiveScene().path;
        foreach (var path in Directory.GetFiles("Assets/Scenes", "*.unity").OrderBy(p => p))
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            string result = RecastFamily(scene);
            if (result == null) continue;
            WorldModelSync.Apply(scene);
            AmbientLifeBuilder.Apply(scene);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("Family cast, " + scene.name + ": " + result);
        }
        if (File.Exists(active)) EditorSceneManager.OpenScene(active, OpenSceneMode.Single);
    }

    static readonly string[] FamilyNames = { "Mother", "Father", "Tim", "Mom", "Dad" };

    // Returns what changed, or null when the scene has no family and nobody wearing the family's models.
    public static string RecastFamily(Scene scene)
    {
        var people = scene.GetRootGameObjects()
            .Where(g => g.name != "Ambient Life")   // rebuilt by AmbientLifeBuilder.Apply
            .SelectMany(g => g.GetComponentsInChildren<Animator>(true))
            .Where(a => a.name == "Model" && a.transform.parent != null && a.transform.root.name != "Player")
            .Select(a => (model: a.transform, source: ModelSource(a.transform)))
            .Where(p => p.source != null).ToList();
        int family = 0, students = 0;
        // Fewest repeats: a student gets the cast model least used in this scene.
        var used = people.GroupBy(p => Path.GetFileNameWithoutExtension(p.source)).ToDictionary(g => g.Key, g => g.Count());
        foreach (var (model, source) in people)
        {
            string person = model.parent.name;
            int role = Array.IndexOf(FamilyNames, person);
            int old = Array.IndexOf(OldFamilyModels, source);
            if (role >= 0 && old >= 0)
            {
                // The family member keeps their role; only the body changes.
                Recast(model, FamilyModels[old]);
                family++;
            }
            else if (role < 0 && FamilyModels.Contains(source))
            {
                string pick = StudentModels.OrderBy(m => used.TryGetValue(m, out var n) ? n : 0).First();
                used[pick] = (used.TryGetValue(pick, out var c) ? c : 0) + 1;
                var npc = model.parent.GetComponent<NPC>();
                Recast(model, CastFolder + pick + ".fbx");
                // Let Ambient Life give them the new name and small talk.
                if (npc != null) { npc.npcName = "NPC"; npc.dialogueNodes = new List<NPCDialogueNode> { new NPCDialogueNode() }; EditorUtility.SetDirty(npc); }
                students++;
            }
        }
        // Ambient Life needs re-applying where its family still wears the old set or a walker wears a family model.
        bool ambient = scene.GetRootGameObjects().Where(g => g.name == "Ambient Life")
            .SelectMany(g => g.GetComponentsInChildren<Animator>(true))
            .Any(a => { string s = ModelSource(a.transform); return s != null && (OldFamilyModels.Contains(s) || (FamilyModels.Contains(s) && !FamilyNames.Contains(a.transform.parent != null ? a.transform.parent.name : ""))); });
        if (family == 0 && students == 0 && !ambient) return null;
        return family + " family members recast, " + students + " students given other models" + (ambient ? ", ambient life refreshed." : ".");
    }

    // The model file a character's body comes from (its skinned mesh's asset).
    static string ModelSource(Transform model)
    {
        var smr = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
        return smr != null && smr.sharedMesh != null ? AssetDatabase.GetAssetPath(smr.sharedMesh) : null;
    }

    // Swaps a character's body for another model, keeping its height, its clip and where it stands or sits.
    static void Recast(Transform oldBody, string modelPath)
    {
        var person = oldBody.parent;
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (asset == null) throw new FileNotFoundException("Character model missing.", modelPath);
        var oldAnimator = oldBody.GetComponent<Animator>();
        string controllerName = oldAnimator != null && oldAnimator.runtimeAnimatorController != null ? oldAnimator.runtimeAnimatorController.name : "";
        string clip = controllerName.Contains(' ') ? controllerName.Substring(controllerName.IndexOf(' ') + 1) : "Idle";
        if (clip == "Running") clip = "Run";
        bool seated = clip.Contains("Seated") || clip.Contains("Sitting") || clip.Contains("Sit_");
        float height = BoundsOf(oldBody.gameObject).size.y;   // skinned bounds stay at the rest pose in the editor
        var oldHips = Hips(oldBody);
        Vector3 seat = oldHips != null ? oldHips.position : oldBody.position;

        var body = (GameObject)PrefabUtility.InstantiatePrefab(asset, person); body.name = "Model";
        body.transform.SetSiblingIndex(oldBody.GetSiblingIndex());
        body.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        var rest = BoundsOf(body); body.transform.localScale *= height / Mathf.Max(.01f, rest.size.y);
        Physics.SyncTransforms(); rest = BoundsOf(body);
        var hips = Hips(body.transform);
        float hipsAboveFeet = hips != null ? hips.position.y - rest.min.y : height * .5f;

        var motion = Clip(modelPath, clip) ?? Clip(modelPath, "Idle");
        if (motion != null)
        {
            motion.SampleAnimation(body, 0);
            var animator = body.GetComponent<Animator>(); if (animator == null) animator = body.AddComponent<Animator>();
            animator.runtimeAnimatorController = Controller(Path.GetFileNameWithoutExtension(modelPath), motion == Clip(modelPath, clip) ? clip : "Idle", motion);
            animator.applyRootMotion = false;
        }
        // Seated people keep their seat; standing people stand on their pivot, like Person().
        if (hips != null)
        {
            Vector3 want = seated ? seat : person.position + Vector3.up * hipsAboveFeet;
            body.transform.position += want - hips.position;
        }
        else body.transform.position += person.position - new Vector3(rest.center.x, rest.min.y, rest.center.z);
        foreach (var c in body.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        Object.DestroyImmediate(oldBody.gameObject);
        EditorUtility.SetDirty(person.gameObject);
    }

    static Transform Hips(Transform body) =>
        body.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.Equals("hips", StringComparison.OrdinalIgnoreCase));

    static AnimationClip Clip(string modelPath, string clip) =>
        AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__") && (c.name == clip || c.name.EndsWith("|" + clip)));
}
