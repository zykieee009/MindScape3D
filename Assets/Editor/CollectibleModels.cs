using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Swaps the cube-built story props in Chapters 1 and 2 (phone, laptop, books, school bag, plate, notebook,
// examination paper, student ID, research notes) for the models in Assets/Models/Collectibles.
// Each model takes its placeholder's place: same long axis, same footprint, resting on the same surface.
// The story objects themselves stay (their names, pivots, markers and glints), so every interaction keeps working.
// Safe to re-run: a prop that already holds a "Collectible model" is left alone.
public static class CollectibleModels
{
    const string ModelFolder = "Assets/Models/Collectibles/Collectible_";
    const string ModelName = "Collectible model";

    public static readonly string[] Scenes =
    {
        "Assets/Scenes/Chapter 1 - Episode 1.unity", "Assets/Scenes/Chapter 1 - Episode 2.unity",
        "Assets/Scenes/Chapter 1 - Episode 3.unity", "Assets/Scenes/Chapter 1 - Episode 4.unity",
        "Assets/Scenes/Chapter 2 - Episode 1.unity", "Assets/Scenes/Chapter 2 - Episode 2.unity",
        "Assets/Scenes/Chapter 2 - Episode 3.unity", "Assets/Scenes/Chapter 2 - Episode 4.unity",
        "Assets/Scenes/Chapter 2 - Episode 5.unity",
    };

    // Chapter 2 - Episode 2 saves its pick-ups as prefabs; Episode 3's builder places copies of them.
    static readonly string[] Prefabs =
    {
        "Assets/EpisodeTwo/Prefabs/Student ID.prefab", "Assets/EpisodeTwo/Prefabs/Examination materials.prefab",
        "Assets/EpisodeTwo/Prefabs/School bag.prefab", "Assets/EpisodeTwo/Prefabs/Phone.prefab",
    };

    // Story object name -> model. The names are the ones the episode builders give their props.
    static readonly Dictionary<string, string> Props = new Dictionary<string, string>
    {
        { "Phone", "Phone" },
        { "Laptop", "Laptop" },
        { "Books", "Books" },
        { "Notebook", "Notebook" },
        { "Study notebook interaction", "Notebook" },
        { "School bag", "SchoolBag" },
        { "Your school bag", "SchoolBag" },
        { "Carried school bag", "SchoolBag" },
        { "Joey school bag", "SchoolBag" },
        { "School bag memory interaction", "SchoolBag" },
        { "Breakfast plate", "BreakfastPlate" },
        { "Carried plate", "BreakfastPlate" },
        { "Examination paper interaction", "ExaminationPaper" },
        { "Examination materials", "ExaminationPaper" },
        { "Joey examination materials", "ExaminationPaper" },
        { "Exam paper", "ExaminationPaper" },
        { "Student ID", "StudentID" },
        { "Dropped revision notes", "ResearchNotes" },
        { "Unfinished project", "ResearchNotes" },
        // Chapter 2 - Episodes 4 and 5: the keepsakes on Joey's desk.
        { "Old certificate", "OldCertificate" },
        // "Old hobby item" is the baseball set, placed by KeepsakeBaseball (not a desk-top swap).
        { "Old notebook", "OldNotebook" },
    };

    // Models built to stand up (a frame on its kickstand) -> width in metres. They keep their own upright pose,
    // face away from the nearest wall, and sit on the surface instead of lying in their placeholder's footprint.
    static readonly Dictionary<string, float> Upright = new Dictionary<string, float> { { "OldCertificate", .45f } };

    // Loose detail cubes that belonged to a prop that is now a model.
    static readonly string[] Leftovers = { "ID photo" };

    // Children that are not part of the look: story markers, and the laptop's "opened" screen overlay.
    static readonly string[] Keep = { "Memory marker", "Objective glint", "Laptop screen (on)" };

    [MenuItem("Mindscape/Collectibles/Use Collectible Models (Chapters 1-2)")]
    public static void ApplyAll()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play mode first."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string active = SceneManager.GetActiveScene().path;
        var report = new List<string>();
        foreach (var path in Prefabs)
        {
            if (!System.IO.File.Exists(path)) continue;
            var contents = PrefabUtility.LoadPrefabContents(path);
            int n = Swap(contents.transform);
            if (n > 0) PrefabUtility.SaveAsPrefabAsset(contents, path);
            PrefabUtility.UnloadPrefabContents(contents);
            report.Add(System.IO.Path.GetFileNameWithoutExtension(path) + " prefab: " + n);
        }
        foreach (var path in Scenes)
        {
            if (!System.IO.File.Exists(path)) continue;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int n = Apply(scene);
            if (n > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            report.Add(scene.name + ": " + n);
        }
        if (System.IO.File.Exists(active)) EditorSceneManager.OpenScene(active, OpenSceneMode.Single);
        Debug.Log("Collectible models placed:\n" + string.Join("\n", report));
    }

    // Swaps every known prop in the scene; returns how many were replaced.
    public static int Apply(Scene scene)
    {
        int count = 0;
        foreach (var root in scene.GetRootGameObjects()) count += Swap(root.transform);
        return count;
    }

    // Swaps the known props at or under one object (for builders that save a prop as a prefab).
    public static int ApplyTo(Transform root) => root == null ? 0 : Swap(root);

    static int Swap(Transform root)
    {
        int count = 0;
        var all = root.GetComponentsInChildren<Transform>(true);
        foreach (var t in all.Where(t => t != null && Leftovers.Contains(t.name)).ToArray()) Object.DestroyImmediate(t.gameObject);
        foreach (var target in all.Where(t => t != null && Props.ContainsKey(t.name)).ToArray())
            if (target != null && Place(target, Props[target.name])) count++;
        return count;
    }

    // Props the story hides until later (Episode 4's keepsakes) are shown while they are fitted:
    // an inactive renderer has no bounds to measure.
    static bool Place(Transform target, string model)
    {
        if (target.Find(ModelName) != null) return false;
        var hidden = new List<GameObject>();
        for (var t = target; t != null; t = t.parent) if (!t.gameObject.activeSelf) { hidden.Add(t.gameObject); t.gameObject.SetActive(true); }
        try { return Fit(target, model); }
        finally { foreach (var go in hidden) if (go != null) go.SetActive(false); }
    }

    static bool Fit(Transform target, string model)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + model + ".glb");
        if (asset == null) { Debug.LogWarning("Collectible model missing: " + ModelFolder + model + ".glb"); return false; }

        // The placeholder: the target's own mesh (a bare cube or cylinder) or the boxes under it.
        var parts = target.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r => r.GetComponent<MeshFilter>() != null && !IsKept(r.transform, target) && Props.ContainsKey(r.name) == (r.transform == target)).ToList();
        if (parts.Count == 0) return false;
        var main = parts.OrderByDescending(r => Volume(r)).First();
        var mainFilter = main.GetComponent<MeshFilter>();
        Vector3 size = Vector3.Scale(mainFilter.sharedMesh.bounds.size, Abs(main.transform.lossyScale));
        Vector3[] axes = { main.transform.right, main.transform.up, main.transform.forward };
        Vector3 mainCenter = main.bounds.center;
        Bounds group = parts[0].bounds; foreach (var r in parts) group.Encapsulate(r.bounds);
        Vector3? laptopFront = null;
        var lid = parts.FirstOrDefault(r => r.name.ToLower().Contains("lid"));
        if (lid != null) laptopFront = Flat(mainCenter - lid.bounds.center);

        // Clear the placeholder, keeping labels and markers that sit inside it.
        foreach (var r in parts)
        {
            if (r == null) continue;   // went with a parent box already
            if (r.transform == target)
            {
                var box = target.GetComponent<BoxCollider>(); Vector3 oldScale = target.localScale;
                Object.DestroyImmediate(r); Object.DestroyImmediate(target.GetComponent<MeshFilter>());
                target.localScale = Vector3.one;
                if (box != null) box.size = Vector3.Scale(box.size, oldScale);
                continue;
            }
            foreach (var keep in r.GetComponentsInChildren<Transform>(true).Where(k => k != r.transform && (IsKept(k, target) || k.GetComponent<TMP_Text>() != null)).ToArray())
                keep.SetParent(target, true);
            Object.DestroyImmediate(r.gameObject);
        }

        // Fit the model: its long / middle / thin axes onto the placeholder's, then scale and seat it.
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, target);
        instance.name = ModelName;
        foreach (var c in instance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        instance.transform.localScale = Vector3.one / Mathf.Max(.0001f, target.lossyScale.x);
        Vector3 modelSize = WorldBounds(instance).size;
        int[] modelRank = Rank(modelSize), boxRank = Rank(size);
        Vector3 Axis(int modelAxis) => axes[boxRank[System.Array.IndexOf(modelRank, modelAxis)]];
        Vector3 up = Axis(1), forward = Axis(2);
        if (Mathf.Abs(Vector3.Dot(up, Vector3.up)) > .7f) up *= Mathf.Sign(Vector3.Dot(up, Vector3.up));
        else up *= -Mathf.Sign(Vector3.Dot(up, target.root.forward));   // held upright: face the holder
        forward = Vector3.ProjectOnPlane(forward, up).normalized;
        instance.transform.rotation = Quaternion.LookRotation(forward, up);
        // Fit inside the placeholder's two largest sides, so an open laptop or a bag does not spill onto its neighbours.
        float scale = Mathf.Min(size[boxRank[0]] / Mathf.Max(.0001f, modelSize[modelRank[0]]), size[boxRank[1]] / Mathf.Max(.0001f, modelSize[modelRank[1]]));
        instance.transform.localScale *= scale;
        if (Upright.TryGetValue(model, out float standingWidth))
        {
            instance.transform.rotation = Quaternion.LookRotation(AwayFromWall(mainCenter, target), Vector3.up);
            instance.transform.localScale *= standingWidth / Mathf.Max(.0001f, modelSize.x * scale);
        }

        // A laptop opens toward where its placeholder's lid faced (the chair side).
        if (laptopFront.HasValue && Screen(instance, out var screenCenter, out var screenNormal, out _, out _)
            && Vector3.Dot(Flat(screenNormal), laptopFront.Value) < 0)
            instance.transform.rotation = Quaternion.AngleAxis(180, Vector3.up) * instance.transform.rotation;

        var placed = WorldBounds(instance);
        instance.transform.position += new Vector3(mainCenter.x - placed.center.x, group.min.y - placed.min.y, mainCenter.z - placed.center.z);
        placed = WorldBounds(instance);

        // Some placeholders were sunk into the surface below them (the exam papers in the writing tablets);
        // lift the model onto that surface. Props carried by the player are left where they are.
        if (target.GetComponentInParent<PlayerControls>(true) == null)
        {
            Physics.SyncTransforms();
            var hits = Physics.RaycastAll(new Vector3(placed.center.x, placed.max.y + .5f, placed.center.z), Vector3.down, placed.size.y + 1, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => !h.collider.transform.IsChildOf(target)).OrderBy(h => h.distance).ToArray();
            // Lift a sunk prop, or set down one hovering just above (the cube placeholders often floated a few cm).
            if (hits.Length > 0 && Mathf.Abs(hits[0].point.y - placed.min.y) > .005f && hits[0].point.y - placed.min.y < .3f && placed.min.y - hits[0].point.y < .08f)
            { instance.transform.position += Vector3.up * (hits[0].point.y - placed.min.y); placed = WorldBounds(instance); }
        }

        FitOverlays(target, instance, placed);
        return true;
    }

    // Labels lying on a prop move onto the model's top; the laptop's overlay screen and DRAFT text onto its screen.
    static void FitOverlays(Transform target, GameObject instance, Bounds placed)
    {
        var overlay = target.Find("Laptop screen (on)");
        bool hasScreen = Screen(instance, out var center, out var normal, out var width, out var height);
        Vector3 screenUp = hasScreen ? Vector3.ProjectOnPlane(Vector3.up, normal).normalized : Vector3.up;
        if (overlay != null && hasScreen)
        {
            overlay.SetPositionAndRotation(center + normal * .006f, Quaternion.LookRotation(normal, screenUp));
            overlay.localScale = new Vector3(width * .96f, height * .94f, .004f);
        }
        foreach (var label in target.GetComponentsInChildren<TextMeshPro>(true))
        {
            if (hasScreen && overlay != null)
            {
                float was = label.rectTransform.sizeDelta.x;
                label.transform.SetPositionAndRotation(center + normal * .012f, Quaternion.LookRotation(-normal, screenUp));
                label.rectTransform.sizeDelta = new Vector2(width * .88f, height * .84f);
                label.fontSize *= width * .88f / Mathf.Max(.01f, was);
                label.transform.SetParent(overlay, true);   // shown only while the story has the screen on
            }
            else if (Vector3.Dot(label.transform.forward, Vector3.down) > .7f)   // a title lying on paper: the model is printed already
                Object.DestroyImmediate(label.gameObject);
        }
    }

    // The model's glowing screen, from the submesh that uses a "ScreenGlow" material.
    static bool Screen(GameObject instance, out Vector3 center, out Vector3 normal, out float width, out float height)
    {
        center = normal = Vector3.zero; width = height = 0;
        foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh; var mats = renderer.sharedMaterials;
            if (mesh == null) continue;
            for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
            {
                if (mats[s] == null || !mats[s].name.Contains("ScreenGlow")) continue;
                var v = mesh.vertices; var tris = mesh.GetTriangles(s);
                var world = tris.Distinct().Select(i => renderer.transform.TransformPoint(v[i])).ToList();
                if (world.Count == 0) continue;
                center = world.Aggregate(Vector3.zero, (a, b) => a + b) / world.Count;
                // The screen is a thin slab: its normal is that of its largest face, on the side facing the
                // keyboard (toward the middle of the model).
                Vector3 best = Vector3.zero; float bestArea = 0;
                for (int k = 0; k < tris.Length; k += 3)
                {
                    Vector3 a = renderer.transform.TransformPoint(v[tris[k]]), b = renderer.transform.TransformPoint(v[tris[k + 1]]), c2 = renderer.transform.TransformPoint(v[tris[k + 2]]);
                    var cross = Vector3.Cross(b - a, c2 - a);
                    if (cross.magnitude > bestArea) { bestArea = cross.magnitude; best = cross.normalized; }
                }
                normal = best * Mathf.Sign(Vector3.Dot(best, WorldBounds(instance).center - center));
                var up = Vector3.ProjectOnPlane(Vector3.up, normal).normalized; var right = Vector3.Cross(up, normal);
                Vector3 c = center;
                width = world.Max(p => Vector3.Dot(p - c, right)) - world.Min(p => Vector3.Dot(p - c, right));
                height = world.Max(p => Vector3.Dot(p - c, up)) - world.Min(p => Vector3.Dot(p - c, up));
                return normal != Vector3.zero;
            }
        }
        return false;
    }

    // The horizontal direction away from the closest wall around a point (toward the room).
    static Vector3 AwayFromWall(Vector3 at, Transform ignore)
    {
        Physics.SyncTransforms();
        Vector3 best = Vector3.back; float closest = float.MaxValue;
        foreach (var dir in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            foreach (var hit in Physics.RaycastAll(at + Vector3.up * .25f, dir, 3, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(ignore) && hit.distance < closest) { closest = hit.distance; best = -dir; }
        return best;
    }

    static bool IsKept(Transform t, Transform target)
    {
        for (var p = t; p != null && p != target; p = p.parent) if (Keep.Contains(p.name)) return true;
        return t.GetComponent<TMP_Text>() != null;
    }

    static float Volume(Renderer r)
    {
        var mesh = r.GetComponent<MeshFilter>().sharedMesh; if (mesh == null) return 0;
        var s = Vector3.Scale(mesh.bounds.size, Abs(r.transform.lossyScale)); return s.x * s.y * s.z;
    }

    // Axis indices (0 x, 1 y, 2 z) ordered longest first.
    static int[] Rank(Vector3 size) => new[] { 0, 1, 2 }.OrderByDescending(i => size[i]).ToArray();

    static Bounds WorldBounds(GameObject obj)
    {
        var renderers = obj.GetComponentsInChildren<Renderer>(true); var b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds); return b;
    }

    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    static Vector3 Flat(Vector3 v) { v.y = 0; return v.normalized; }
}
