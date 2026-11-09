using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// The house scenes hold an unpacked copy of Assets/house-and-lot-cozy.fbx ("House And Lot"), so parts added to
// the model later (the street, the bus stop and the roof) never show up on their own. This adds the model's "Street |",
// "Bus stop |" and "Roof |" parts a scene is missing, and retires the old cube-built street and bus shelter that the model now replaces. The invisible
// route limits, the "Wait here" / "Board bus" spots, the route markers and the moving bus stay as they are.
public static class HouseModelSync
{
    public const string ModelPath = "Assets/house-and-lot-cozy.fbx";
    public static readonly string[] Scenes =
    {
        "Assets/Scenes/Chapter 1 - Episode 1.unity",
        "Assets/Scenes/Chapter 2 - Episode 1.unity",
        "Assets/Scenes/Chapter 2 - Episode 2.unity",
        "Assets/Scenes/Chapter 2 - Episode 4.unity",
        "Assets/Scenes/Chapter 2 - Episode 5.unity",
        "Assets/Scenes/Chapter 3 - Episode 1.unity",
        "Assets/Scenes/Chapter 3 - Episode 7.unity",
        "Assets/Scenes/Chapter 3 - Episode 8.unity",
    };
    static readonly string[] SyncedParts = { "Street |", "Bus stop |", "Roof |" };
    const string GlassPath = "Assets/Models/House Window Glass.mat";

    [MenuItem("Mindscape/House/Sync House Model (open scene)")]
    public static void SyncOpenScene()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play mode first."); return; }
        var scene = SceneManager.GetActiveScene();
        Debug.Log("House model sync, " + scene.name + ": " + Sync(scene));
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("Mindscape/House/Sync House Model (all house scenes)")]
    public static void SyncAllScenes()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play mode first."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string active = SceneManager.GetActiveScene().path;
        foreach (var path in Scenes)
        {
            if (!System.IO.File.Exists(path)) continue;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Debug.Log("House model sync, " + scene.name + ": " + Sync(scene));
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        if (System.IO.File.Exists(active)) EditorSceneManager.OpenScene(active, OpenSceneMode.Single);
    }

    public static string Sync(Scene scene)
    {
        var house = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "House And Lot");
        if (house == null) return "no House And Lot in this scene.";
        int added = AddMissingParts(house.transform), retired = RetireBuiltStreet(scene);
        return added + " model part(s) added, " + retired + " cube street/shelter piece(s) removed.";
    }

    public static int AddMissingParts(Transform house)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        int added = 0;
        foreach (Transform part in model.transform)
        {
            // Only the outdoor parts: scenes are allowed to have removed other model parts on purpose.
            if (!SyncedParts.Any(part.name.StartsWith)) continue;
            var existing = house.Find(part.name);
            if (existing != null)
            {
                // A house still linked to the model gets new parts automatically, but without colliders.
                var meshFilter = existing.GetComponent<MeshFilter>();
                if (meshFilter != null && existing.GetComponent<Collider>() == null) existing.gameObject.AddComponent<MeshCollider>().sharedMesh = meshFilter.sharedMesh;
                RoofCastsNoShadow(existing);
                continue;
            }
            var copy = Object.Instantiate(part.gameObject, house, false);
            copy.name = part.name;
            copy.transform.SetLocalPositionAndRotation(part.localPosition, part.localRotation);
            copy.transform.localScale = part.localScale;
            foreach (var mf in copy.GetComponentsInChildren<MeshFilter>(true))
                if (mf.GetComponent<Collider>() == null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            RoofCastsNoShadow(copy.transform);
            added++;
        }
        GlazeWindows(house);
        return added;
    }

    // The model's window panes share the street houses' opaque "window blue". The house's own windows have real
    // openings in the walls behind them, so give their panes see-through glass instead.
    public static void GlazeWindows(Transform house)
    {
        var glass = WindowGlass();
        foreach (Transform part in house)
        {
            if (!part.name.EndsWith("| window")) continue;
            var renderer = part.GetComponent<Renderer>(); if (renderer == null) continue;
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
                if (materials[i] != null && materials[i].name == "Street - window blue") materials[i] = glass;
            renderer.sharedMaterials = materials;
        }
    }

    static Material WindowGlass()
    {
        var glass = AssetDatabase.LoadAssetAtPath<Material>(GlassPath);
        if (glass != null) return glass;
        glass = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "House Window Glass" };
        glass.SetColor("_BaseColor", new Color(.62f, .80f, .90f, .22f));
        glass.SetFloat("_Smoothness", .95f);
        glass.SetFloat("_Metallic", 0f);
        glass.SetFloat("_Surface", 1f);   // transparent
        glass.SetFloat("_Blend", 0f);     // alpha
        glass.SetFloat("_ZWrite", 0f);
        glass.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        glass.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        glass.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        glass.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        glass.SetOverrideTag("RenderType", "Transparent");
        glass.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        glass.SetShaderPassEnabled("ShadowCaster", false);   // sunlight comes in through the window
        glass.SetShaderPassEnabled("DepthOnly", false);
        AssetDatabase.CreateAsset(glass, GlassPath);
        return glass;
    }

    // The rooms are lit by the sun from above; a shadow-casting roof would leave every room in shade.
    // Its collider stays, so the follow camera stops under the roof instead of passing through it.
    static void RoofCastsNoShadow(Transform part)
    {
        if (!part.name.StartsWith("Roof |")) return;
        foreach (var r in part.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static int RetireBuiltStreet(Scene scene)
    {
        int removed = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            var street = root.transform.Find("Neighborhood");
            if (street != null)
                foreach (var piece in street.Cast<Transform>().Where(t => t.GetComponent<Renderer>() != null).ToArray())
                { Object.DestroyImmediate(piece.gameObject); removed++; }

            var shelter = root.transform.Find("Neighborhood bus shelter");
            if (shelter != null)
            {
                // Keep the sign's text; the sign board itself is in the model now.
                foreach (var label in shelter.GetComponentsInChildren<TMPro.TextMeshPro>(true)) label.transform.SetParent(root.transform, true);
                removed += shelter.GetComponentsInChildren<Renderer>(true).Length;
                Object.DestroyImmediate(shelter.gameObject);
            }
        }
        return removed;
    }
}
