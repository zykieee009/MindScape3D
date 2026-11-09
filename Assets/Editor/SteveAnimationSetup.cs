using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class SteveAnimationSetup
{
    const string ModelPath = "Assets/1_Steve.fbx";
    const string ControllerPath = "Assets/SteveAnimatorController.controller";
    const string IdlePath = "Assets/SteveIdle.anim";

    static SteveAnimationSetup()
    {
        EditorApplication.delayCall += InitializeEmptyController;
    }

    static void InitializeEmptyController()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null) return;
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller != null && (controller.layers.Length == 0 || controller.layers[0].stateMachine.states.Length == 0)) Setup();
        else if (controller != null) ValidateController();
    }

    [MenuItem("Tools/Steve/Set Up Walk and Run")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Stop Play Mode before setting up Steve's animations.");
            return;
        }
        try
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Steve FBX is missing.");
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            var clips = importer.clipAnimations;
            if (clips.Length == 0) clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                if (clip.name.EndsWith("Walk") || clip.name.EndsWith("Run"))
                    clip.loopTime = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            var imported = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().ToArray();
            var walk = imported.First(c => c.name.EndsWith("Walk"));
            var run = imported.First(c => c.name.EndsWith("Run"));
            if (AnimationUtility.GetCurveBindings(walk).Length == 0 || AnimationUtility.GetCurveBindings(run).Length == 0)
                throw new InvalidOperationException("Imported animation clips contain no curves.");

            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdlePath);
            if (idle == null)
            {
                idle = new AnimationClip { name = "SteveIdle", frameRate = 24 };
                // Freeze the lowered-arm pose, straightening legs in their rest pose.
                foreach (var binding in AnimationUtility.GetCurveBindings(walk))
                {
                    var curve = AnimationUtility.GetEditorCurve(walk, binding);
                    float value = curve.Evaluate(0);
                    string bone = binding.path.Split('/').Last();
                    bool leg = bone.EndsWith("_Thigh") || bone.EndsWith("_Shin") || bone.EndsWith("_Foot");
                    if (leg && binding.propertyName.StartsWith("m_LocalRotation."))
                        value = binding.propertyName.EndsWith(".w") ? 1 : 0;
                    if (bone == "Root" && binding.propertyName.StartsWith("m_LocalPosition.")) value = 0;
                    AnimationUtility.SetEditorCurve(idle, binding, AnimationCurve.Constant(0, 1, value));
                }
                var settings = AnimationUtility.GetAnimationClipSettings(idle);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(idle, settings);
                AssetDatabase.CreateAsset(idle, IdlePath);
            }
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            if (controller.layers.Length == 0 || controller.layers[0].stateMachine.states.Length == 0)
            {
                if (controller.layers.Length == 0) controller.AddLayer("Base Layer");
                var layers = controller.layers;
                layers[0].defaultWeight = 1;
                controller.layers = layers;
                if (!controller.parameters.Any(p => p.name == "Speed"))
                    controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
                var machine = controller.layers[0].stateMachine;
                var idleState = machine.AddState("Idle", new Vector3(240, 0, 0));
                var walkState = machine.AddState("Walk", new Vector3(480, 0, 0));
                var runState = machine.AddState("Run", new Vector3(720, 0, 0));
                idleState.motion = idle;
                walkState.motion = walk;
                runState.motion = run;
                machine.defaultState = idleState;
                Transition(idleState, walkState, .1f, AnimatorConditionMode.Greater, 1.5f);
                Transition(idleState, runState, 1.5f, AnimatorConditionMode.Greater);
                Transition(walkState, idleState, .1f, AnimatorConditionMode.Less);
                Transition(walkState, runState, 1.5f, AnimatorConditionMode.Greater);
                Transition(runState, idleState, .1f, AnimatorConditionMode.Less);
                Transition(runState, walkState, .1f, AnimatorConditionMode.Greater, 1.5f);
                EditorUtility.SetDirty(controller);
            }

            int wired = 0;
            foreach (var player in UnityEngine.Object.FindObjectsByType<PlayerMovement>(FindObjectsInactive.Include))
            {
                var animator = player.animator != null ? player.animator : player.GetComponentInChildren<Animator>(true);
                if (animator == null) continue;
                Undo.RecordObjects(new UnityEngine.Object[] { player, animator }, "Connect Steve Animator");
                player.animator = animator;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                EditorUtility.SetDirty(player);
                EditorUtility.SetDirty(animator);
                PrefabUtility.RecordPrefabInstancePropertyModifications(player);
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
                EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
                wired++;
            }
            AssetDatabase.SaveAssets();
            string report = $"Controller states: {controller.layers[0].stateMachine.states.Length}; Walk curves: {AnimationUtility.GetCurveBindings(walk).Length}; Run curves: {AnimationUtility.GetCurveBindings(run).Length}; Players connected: {wired}";
            Directory.CreateDirectory("GeneratedAssets");
            File.WriteAllText("GeneratedAssets/unity-animation-setup.txt", report);
            Debug.Log(report);
            ValidateController();
        }
        catch (Exception error) { Debug.LogException(error); }
    }

    [MenuItem("Tools/Steve/Validate Animation Playback")]
    public static void ValidateController()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null)
        {
            Debug.LogWarning($"Cannot validate Steve animation: model is missing at {ModelPath}.");
            return;
        }
        GameObject preview = null;
        try
        {
            preview = UnityEngine.Object.Instantiate(model);
            preview.hideFlags = HideFlags.HideAndDontSave;
            var animator = preview.GetComponent<Animator>();
            if (animator == null) animator = preview.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            foreach (var pair in new[] { (0f, "Idle"), (1f, "Walk"), (2f, "Run"), (0f, "Idle") })
            {
                animator.SetFloat("Speed", pair.Item1);
                for (int i = 0; i < 10; i++) animator.Update(.05f);
                if (!animator.GetCurrentAnimatorStateInfo(0).IsName(pair.Item2))
                    throw new InvalidOperationException("Animator failed to enter " + pair.Item2);
                if (pair.Item1 == 0) continue;
                var thigh = preview.GetComponentsInChildren<Transform>().First(t => t.name == "Left_Thigh");
                Quaternion before = thigh.localRotation;
                animator.Update(.17f);
                if (Quaternion.Angle(before, thigh.localRotation) < .1f)
                    throw new InvalidOperationException(pair.Item2 + " is not moving the skeleton.");
            }
            File.WriteAllText("GeneratedAssets/unity-animation-validation.txt", "PASS: Speed 0 -> Idle, 1 -> Walk, 2 -> Run, 0 -> Idle. Walk and Run both move the imported skeleton.");
            Debug.Log("Steve animation playback validation passed.");
        }
        catch (Exception error) { Debug.LogException(error); }
        finally { if (preview != null) UnityEngine.Object.DestroyImmediate(preview); }
    }

    static void Transition(AnimatorState from, AnimatorState to, float threshold, AnimatorConditionMode mode, float upper = -1)
    {
        var transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = .1f;
        transition.AddCondition(mode, threshold, "Speed");
        if (upper > 0) transition.AddCondition(AnimatorConditionMode.Less, upper, "Speed");
    }
}
