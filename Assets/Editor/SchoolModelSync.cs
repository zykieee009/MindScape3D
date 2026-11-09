using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Model prefab instances inherit new Blender geometry on import. Scene-added physics and roof
// settings do not: repair those without rebuilding episodes or replacing their gameplay objects.
public static class SchoolModelSync
{
    public const string ModelPath = "Assets/Models/School_Grounds.glb";

    [Serializable]
    public sealed class Result
    {
        public string scene;
        public int campuses, hallParts, collidersAdded, collidersRepaired, roofsUpdated;
    }

    public static string[] FindScenes()
    {
        return AssetDatabase.FindAssets("t:Scene", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => AssetDatabase.GetDependencies(path, true).Contains(ModelPath))
            .OrderBy(path => path).ToArray();
    }

    public static GameObject[] FindCampuses(Scene scene)
    {
        return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Where(t => PrefabUtility.GetNearestPrefabInstanceRoot(t.gameObject) == t.gameObject
                && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == ModelPath)
            .Select(t => t.gameObject).ToArray();
    }

    public static Result Sync(Scene scene)
    {
        var result = new Result { scene = scene.path };
        foreach (var campus in FindCampuses(scene))
        {
            result.campuses++;
            // The reunion venue leaves its overhead lights and beams without collision (see SocialDynamicsBuilder.PlaceVenue).
            bool venue = campus.name == SocialDynamicsBuilder.VenueName;
            foreach (var filter in campus.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) throw new InvalidOperationException("Missing mesh: " + scene.path + " / " + filter.name);
                bool hall = filter.name.StartsWith("Reunion Hall |", StringComparison.Ordinal);
                if (hall) result.hallParts++;
                // Other scene colliders can intentionally be absent or simplified; only create new hall physics.
                if (hall && filter.GetComponent<Collider>() == null && !(venue && filter.name == SocialDynamicsBuilder.HallOverhead))
                {
                    filter.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
                    result.collidersAdded++;
                }
                foreach (var collider in filter.GetComponents<MeshCollider>())
                {
                    if (collider.sharedMesh != null) continue;
                    collider.sharedMesh = mesh;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
                    result.collidersRepaired++;
                }
                // Match the existing campus convention: roofs keep collision but let daylight into rooms.
                var renderer = filter.GetComponent<Renderer>();
                if (hall && filter.name.Contains("Roof") && renderer != null
                    && renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    result.roofsUpdated++;
                }
            }
        }
        return result;
    }

    // The campus model's furniture (the dormitory's bunk beds, wardrobes, stools and boxes, the cafeteria's counters and
    // tables, the classrooms' desks) came without collision, so the player could walk through it. Every floor-standing
    // piece gets a box collider; wall decoration, rugs, lights and paper stay without. Safe to run again.
    static readonly string[] NotSolid = { "Poster", "Frame", "Window", "Board", "Shade", "Bulb", "Light", "Rug", "Carpet", "Mat", "Paper", "Sheet", "Pencil", "Roof", "Floor", "Ceiling", "Bunting", "Flag", "Glass", "Curtain", "Pennant", "Garland", "String", "Door", "Handle" };
    public static int AddFurnitureColliders(Scene scene)
    {
        int added = 0;
        foreach (var campus in FindCampuses(scene))
        {
            float ground = campus.transform.position.y;
            foreach (var filter in campus.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null) continue;
                var renderer = filter.GetComponent<Renderer>();
                if (renderer == null || NotSolid.Any(word => filter.name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                var b = renderer.bounds;
                if (b.min.y > ground + 2.2f || b.max.y < ground + .25f || (b.size.x < .12f && b.size.z < .12f) || b.size.y < .04f) continue;
                if (b.size.x > 12 || b.size.z > 12) continue;   // walls, slabs and paths carry their own colliders
                filter.gameObject.AddComponent<BoxCollider>();
                added++;
            }
        }
        return added;
    }

    [MenuItem("Mindscape/School/Add Furniture Colliders in All Scenes")]
    public static void AddFurnitureCollidersMenu()
    {
        string active = SceneManager.GetActiveScene().path;
        foreach (var path in FindScenes())
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int added = AddFurnitureColliders(scene);
            if (added == 0) continue;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("Furniture colliders · " + Path.GetFileNameWithoutExtension(path) + ": " + added);
        }
        if (!string.IsNullOrEmpty(active)) EditorSceneManager.OpenScene(active, OpenSceneMode.Single);
    }

    [MenuItem("Mindscape/School/Sync Updated Campus in All Scenes")]
    public static void SyncAllScenesMenu() => Debug.Log(SyncAllScenes());

    public static string SyncAllScenes()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before syncing campus scenes.");
        // Keep already-open scenes in place, including unrelated unsaved work.
        var paths = FindScenes();
        foreach (var path in paths)
        {
            var loaded = SceneManager.GetSceneByPath(path);
            if (loaded.IsValid() && loaded.isLoaded && loaded.isDirty)
                throw new InvalidOperationException("Save this campus scene before syncing: " + path);
        }
        var results = new List<Result>();
        var active = SceneManager.GetActiveScene();
        foreach (var path in paths)
        {
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var result = Sync(scene);
                if (result.collidersAdded + result.collidersRepaired + result.roofsUpdated > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + path);
                }
                results.Add(result);
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            }
        }
        return Newtonsoft.Json.JsonConvert.SerializeObject(results, Newtonsoft.Json.Formatting.Indented);
    }
}
