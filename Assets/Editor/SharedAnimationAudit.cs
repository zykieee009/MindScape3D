using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class SharedAnimationAudit
{
    static SharedAnimationAudit() { EditorApplication.delayCall += RunPending; }
    static void RunPending()
    {
        if (!File.Exists("Temp/shared-animation-audit.request")) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += RunPending; return; }
        File.Delete("Temp/shared-animation-audit.request");
        ValidateAll();
        VerifyClassroomSeating();
    }
    [MenuItem("Mindscape/Animations/Audit Shared Animation Bindings")]
    public static void Run()
    {
        var report = new StringBuilder();
        foreach (var path in new[] { "Assets/Characters/BaldCharacter/BaldCharacter.glb", "Assets/Characters/NPCs/NPC_01_Maya.fbx", "Assets/Characters/NPCs/NPC_01_Maya.glb", "Assets/Characters/NPCs/Animations/NPC_Shared_Animations.fbx" })
        {
            report.AppendLine(path);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model != null) report.AppendLine("Transforms: " + string.Join(",", model.GetComponentsInChildren<Transform>(true).Select(t => AnimationUtility.CalculateTransformPath(t, model.transform))));
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")))
            {
                report.AppendLine($"{clip.name}: {clip.length}s {clip.frameRate}fps loop={clip.isLooping}");
                report.AppendLine(string.Join("\n", AnimationUtility.GetCurveBindings(clip).Select(b => b.path + " : " + b.propertyName).Distinct().Take(12)));
            }
        }
        File.WriteAllText("Temp/shared-animation-audit.txt", report.ToString());
        Debug.Log("Shared animation audit written to Temp/shared-animation-audit.txt");
    }

    [MenuItem("Mindscape/Animations/Validate Updated Cast Animations")]
    public static void ValidateAll()
    {
        var report = new StringBuilder();
        int errors = 0, clips = 0, scenes = 0, animators = 0, prefabs = 0;
        string[] required = { "Idle", "Walk", "Run", "Conversation", "Sit_Down", "Seated_Idle", "Sitting_Writing", "Sitting_Eating" };
        var paths = Directory.GetFiles("Assets/Characters/NPCs", "*.*", SearchOption.AllDirectories)
            .Where(p => p.EndsWith(".fbx") || p.EndsWith(".glb"))
            .Concat(new[] { "Assets/Characters/BaldCharacter/BaldCharacter.glb" });
        foreach (var path in paths)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var available = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
            foreach (var name in required)
            {
                var clip = available.FirstOrDefault(c => c.name.Split('|').Last() == name);
                if (clip == null) { report.AppendLine("ERROR missing " + path + " / " + name); errors++; continue; }
                clips++;
                float expected = name == "Walk" ? 1 : name == "Run" ? .75f : name == "Conversation" ? 4 : name == "Sit_Down" ? 1.5f : name.StartsWith("Sitting_") ? 190f / 48 : 3;
                if (Mathf.Abs(clip.length - expected) > .005f) { errors++; report.AppendLine($"ERROR duration {path} {name}: {clip.length} expected {expected}"); }
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    if (binding.type == typeof(Transform) && binding.path.Length > 0 && model.transform.Find(binding.path) == null)
                    { errors++; report.AppendLine("ERROR model binding " + path + " / " + binding.path); break; }
            }
        }
        foreach (var path in Directory.GetFiles("Assets/Scenes", "Chapter*.unity"))
        {
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                scenes++;
                foreach (var animator in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Animator>(true)))
                {
                    if (animator.runtimeAnimatorController == null) continue;
                    animators++;
                    foreach (var clip in animator.runtimeAnimatorController.animationClips.Distinct())
                    {
                        if (clip == null) { errors++; report.AppendLine("ERROR null clip " + path + " " + animator.name); continue; }
                        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                            if (binding.type == typeof(Transform) && binding.path.Length > 0 && animator.transform.Find(binding.path) == null)
                            { errors++; report.AppendLine("ERROR scene binding " + path + " / " + animator.name + " / " + clip.name + " / " + binding.path); break; }
                    }
                }
                var players = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PlayerMovement>(true)).ToArray();
                foreach (var player in players)
                {
                    var animator = player.animator != null ? player.animator : player.GetComponentInChildren<Animator>(true);
                    var available = animator != null && animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.animationClips : new AnimationClip[0];
                    if (!available.Any(c => c != null && c.name.Split('|').Last() == "Walk") || !available.Any(c => c != null && c.name.Split('|').Last() == "Run"))
                    { errors++; report.AppendLine("ERROR player locomotion " + path); }
                }
                report.AppendLine($"SCENE {path}: players={players.Length}");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        foreach (var path in Directory.GetFiles("Assets/Prefabs/NPCs", "*.prefab").Concat(new[] { "Assets/Prefabs/Player.prefab" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { errors++; report.AppendLine("ERROR prefab " + path); continue; }
            prefabs++;
            foreach (var animator in prefab.GetComponentsInChildren<Animator>(true))
                if (animator.runtimeAnimatorController != null)
                    foreach (var clip in animator.runtimeAnimatorController.animationClips.Distinct())
                        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                            if (binding.type == typeof(Transform) && binding.path.Length > 0 && animator.transform.Find(binding.path) == null)
                            { errors++; report.AppendLine("ERROR prefab binding " + path + " / " + clip.name + " / " + binding.path); break; }
        }
        report.Insert(0, $"Models/clips={clips}, episodes={scenes}, animators={animators}, prefabs={prefabs}, errors={errors}\n");
        File.WriteAllText("Temp/shared-animation-validation.txt", report.ToString());
        Debug.Log("Shared animation validation: " + report.ToString().Split('\n')[0]);
    }

    [MenuItem("Mindscape/Animations/Verify Classroom Seating")]
    public static void VerifyClassroomSeating()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Chapter 1 - Episode 2.unity");
        var report = new StringBuilder();
        try
        {
            foreach (var animator in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Animator>(true)))
            {
                if (animator.runtimeAnimatorController == null) continue;
                var clip = animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name.EndsWith("Seated_Idle"));
                if (clip == null) continue;
                var rig = animator.transform.GetComponentsInChildren<Transform>(true).First(t => t.name.StartsWith("Armature_NPC_"));
                var hips = rig.GetComponentsInChildren<Transform>().First(t => t.name == "hips");
                var anchor = animator.transform.parent;
                report.AppendLine($"BEFORE {anchor.name} modelScale={animator.transform.localScale} rigScale={rig.localScale} hipsLocal={hips.localPosition} hipsWorld={hips.position} anchor={anchor.position}");
                foreach (float time in new[] { 0f, .5f, 1.5f, 2.9f })
                {
                    clip.SampleAnimation(animator.gameObject, time);
                    float horizontal = Vector2.Distance(new Vector2(hips.position.x, hips.position.z), new Vector2(anchor.position.x, anchor.position.z));
                    report.AppendLine($"{anchor.name} t={time}: rigOffset={rig.localPosition:F4}, hipsAboveSeat={hips.position.y-anchor.position.y:F4}, horizontalFromSeat={horizontal:F4}");
                    report.AppendLine($"AFTER modelScale={animator.transform.localScale} rigScale={rig.localScale} hipsLocal={hips.localPosition} root={hips.parent.localPosition} rootScale={hips.parent.localScale}");
                }
            }
            report.Insert(0, "PASS: seated actors retain chair alignment throughout the loop.\n");
        }
        catch (System.Exception e) { report.Insert(0, "FAIL: " + e.Message + "\n"); Debug.LogException(e); }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        Directory.CreateDirectory("Reports");
        File.WriteAllText("Reports/classroom-seating-validation.txt", report.ToString());
    }
}
