using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Wraps each Assets/Models/Doors/*.fbx (frame, leaf and handle with their Blender hinge/handle actions) in a prefab under
// Assets/Prefabs/Doors: a root with DoorInteraction, whose child "Model" is the FBX, moved so the closed leaf's bottom middle is the
// root's origin (DoorSetup stands the root on the doorway). Each leaf gets a box collider that swings with it; the frame has none, the
// wall already holds the doorway's edges.
// Safe to re-run: the prefabs are rewritten in place.
public static class DoorPrefabBuilder
{
    public const string PrefabFolder = "Assets/Prefabs/Doors";

    public static readonly (string name, string model)[] Doors =
    {
        ("Door_Classic", "Assets/Models/Doors/Door_Classic_Panel.fbx"),
        ("Door_French", "Assets/Models/Doors/Door_Double_French.fbx"),
        ("Door_Modern", "Assets/Models/Doors/Door_Modern_Glass.fbx"),
        ("Door_Rustic", "Assets/Models/Doors/Door_Rustic_Barn.fbx"),
    };

    public static string PrefabPath(string name) => PrefabFolder + "/" + name + ".prefab";

    [MenuItem("Mindscape/Doors/Rebuild Door Prefabs")]
    public static void RebuildMenu()
    {
        foreach (var (name, model) in Doors) Debug.Log("Door prefab " + name + ": " + Build(name, model));
        AssetDatabase.SaveAssets();
    }

    static string Build(string name, string modelPath)
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (fbx == null) throw new InvalidOperationException("Door model missing: " + modelPath);
        var clipAssets = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().ToArray();

        var root = new GameObject(name);
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
            model.name = "Model";
            var all = model.GetComponentsInChildren<Transform>(true);
            var hinges = all.Where(t => t.name.Contains("Hinge")).ToArray();
            // Blender's export names every action "<object>|<object>Action" (and attaches all of them to every object).
            var clips = all.Where(t => t.name.Contains("Hinge") || t.name.Contains("Pivot"))
                .Select(t => clipAssets.FirstOrDefault(c => c.name == t.name + "|" + t.name + "Action")).Where(c => c != null).Distinct().ToArray();
            var leaves = hinges.SelectMany(h => h.GetComponentsInChildren<Renderer>(true)).Where(r => !r.name.Contains("Handle")).ToArray();
            if (hinges.Length == 0 || clips.Length == 0 || leaves.Length == 0)
                throw new InvalidOperationException(name + ": no hinge, clip or leaf found in " + modelPath);

            Bounds closed = Union(leaves);
            model.transform.localPosition = -new Vector3(closed.center.x, closed.min.y, closed.center.z);
            closed = Union(leaves);

            foreach (var leaf in leaves)
            {
                if (leaf.name.Contains("Glass")) continue;
                var box = leaf.gameObject.AddComponent<BoxCollider>();
                var local = leaf.GetComponent<MeshFilter>().sharedMesh.bounds;
                box.center = local.center; box.size = local.size;
            }

            var door = root.AddComponent<DoorInteraction>();
            door.model = model;
            door.tracks = Bake(model, clips, door.sampleRate);
            door.hinges = hinges;
            door.leaves = leaves;
            door.leafSize = new Vector2(closed.size.x, closed.size.y);
            MeasureSpans(model, hinges, clips, out door.openSpan, out door.closeSpan);

            Directory.CreateDirectory(PrefabFolder);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(name));
            return "leaf " + door.leafSize.ToString("F2") + " open " + door.openSpan.ToString("F2") + " close " + door.closeSpan.ToString("F2") + ", " + clips.Length + " clip(s), " + door.tracks.Length + " track(s)";
        }
        finally { Object.DestroyImmediate(root); }
    }

    // The clips played together, recorded as the local pose of every part they move. DoorInteraction plays these back: a
    // generic clip cannot be sampled in a player build.
    static DoorInteraction.Track[] Bake(GameObject model, AnimationClip[] clips, float rate)
    {
        var parts = model.GetComponentsInChildren<Transform>(true);
        int frames = Mathf.CeilToInt(clips.Max(c => c.length) * rate) + 1;
        var positions = parts.Select(_ => new Vector3[frames]).ToArray();
        var rotations = parts.Select(_ => new Quaternion[frames]).ToArray();
        for (int f = 0; f < frames; f++)
        {
            foreach (var clip in clips) clip.SampleAnimation(model, Mathf.Min(f / rate, clip.length));
            for (int p = 0; p < parts.Length; p++) { positions[p][f] = parts[p].localPosition; rotations[p][f] = parts[p].localRotation; }
        }
        foreach (var clip in clips) clip.SampleAnimation(model, 0f);
        return Enumerable.Range(0, parts.Length)
            .Where(p => positions[p].Any(v => (v - positions[p][0]).sqrMagnitude > 1e-8f) || rotations[p].Any(q => Quaternion.Angle(q, rotations[p][0]) > .01f))
            .Select(p => new DoorInteraction.Track { target = parts[p], positions = positions[p], rotations = rotations[p] })
            .ToArray();
    }

    static Bounds Union(Renderer[] renderers)
    {
        var b = renderers[0].bounds;
        foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds);
        return b;
    }

    // Each action is one swing: rest, move to a peak, hold, move back. Open runs from the first movement of any clip to the hinge
    // reaching its peak; close from the hinge leaving its peak to the last clip coming to rest.
    static void MeasureSpans(GameObject model, Transform[] hinges, AnimationClip[] clips, out Vector2 open, out Vector2 close)
    {
        const float step = 1f / 60f, rest = .5f;
        float openStart = float.MaxValue, openEnd = 0f, closeStart = float.MaxValue, closeEnd = 0f;
        foreach (var clip in clips)
        {
            var target = model.GetComponentsInChildren<Transform>(true).First(t => clip.name.StartsWith(t.name + "|"));
            clip.SampleAnimation(model, 0f);
            var home = target.localRotation;
            float first = -1f, peak = 0f, firstPeak = 0f, lastPeak = 0f, last = 0f;
            for (float t = 0f; t <= clip.length + step; t += step)
            {
                clip.SampleAnimation(model, Mathf.Min(t, clip.length));
                float angle = Quaternion.Angle(home, target.localRotation);
                if (angle > rest) { if (first < 0f) first = t; last = t; }
                if (angle > peak + .01f) { peak = angle; firstPeak = t; lastPeak = t; }
                else if (angle >= peak - .01f && peak > rest) lastPeak = t;
            }
            if (first < 0f) continue;
            openStart = Mathf.Min(openStart, first);
            closeEnd = Mathf.Max(closeEnd, last + step);
            if (hinges.Contains(target)) { openEnd = Mathf.Max(openEnd, firstPeak); closeStart = Mathf.Min(closeStart, lastPeak); }
        }
        foreach (var clip in clips) clip.SampleAnimation(model, 0f);
        open = new Vector2(openStart, openEnd);
        close = new Vector2(closeStart, closeEnd);
    }
}
