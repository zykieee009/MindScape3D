using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

// LOD for the story scenes. Nothing is baked: lighting stays fully real-time and there is no occlusion culling.
//   The city's small detail (lane dashes, crosswalks, park props, bus stops, street lamps, trees) is not drawn far away (one LOD level
//   that culls), and the NPC models carry generated mesh LODs (lower-poly bodies at a distance).
public static class PerformanceSetup
{
    // City detail, culled past this distance in the city model's own metres (at LOD bias 1; PC's bias of 2 doubles it).
    static readonly (string prefix, float distance)[] Detail =
    {
        ("Lane dash", 90), ("Crosswalk", 90), ("Park |", 120), ("Bus Stop |", 120), ("Light |", 160), ("Tree |", 220),
    };

    static readonly string[] NpcModels = { "Assets/Characters/NPCs" };

    [MenuItem("Mindscape/Performance/Set Up LOD")]
    public static void SetUpMenu() => Debug.Log("Performance: " + MeshLods() + " NPC models given mesh LODs, " + SetUpWorldPrefab() + " city detail LOD groups");

    // The NPC bodies (about 12,500 triangles each) get lower-poly versions that Unity switches to by distance.
    static int MeshLods()
    {
        int changed = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Model", NpcModels))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) || path.Contains("/Animations/")) continue;
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer) || importer.generateMeshLods) continue;
            importer.generateMeshLods = true; importer.SaveAndReimport(); changed++;
        }
        return changed;
    }

    // In place (LoadPrefabContents keeps every child's file ID, so the scenes' overrides on the city stay attached).
    public static int SetUpWorldPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(WorldModelSync.PrefabPath);
        int groups = 0;
        try
        {
            float tan = Mathf.Tan(30f * Mathf.Deg2Rad);     // the story cameras' 60 degree field of view
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var go = renderer.gameObject;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
                var detail = Detail.FirstOrDefault(d => go.name.StartsWith(d.prefix, StringComparison.Ordinal));
                if (detail.prefix == null) continue;
                var group = go.GetComponent<LODGroup>();
                if (group == null) group = go.AddComponent<LODGroup>();
                float size = Mathf.Max(renderer.bounds.size.x, Mathf.Max(renderer.bounds.size.y, renderer.bounds.size.z));
                group.SetLODs(new[] { new LOD(Mathf.Clamp(size / (2f * detail.distance * tan), .002f, .9f), new Renderer[] { renderer }) });
                group.RecalculateBounds();
                groups++;
            }
            PrefabUtility.SaveAsPrefabAsset(root, WorldModelSync.PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return groups;
    }
}
