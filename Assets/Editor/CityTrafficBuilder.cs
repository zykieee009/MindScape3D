using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mindscape.Vehicles;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Fills the shared city's streets with driving vehicles (Assets/Models/Vehicles). The city's roads form a grid (east-west roads at
// z = +-26 and +-78, north-south roads at x = +-51 and +-153, all 8 wide, in the model's own space), so every block gets a loop that runs
// round it in the right-hand lane, clockwise, turning at every corner. Neighbouring blocks share a road but drive opposite ways on it, and
// no loop ever crosses another: each one stays inside its own quarter of an intersection. Vehicles have no colliders (the player walks through
// them), and WorldModelSync.Apply calls this for every scene, so the loops follow each scene's placement of the city.
public static class CityTrafficBuilder
{
    const string Folder = "Assets/Prefabs/Vehicles", RootName = "City Traffic";
    // Model name, and the speed (model units per second) at which its wheel clip rolls the tyres without slipping (2 turns per 2.03 s loop).
    static readonly (string model, float rollSpeed)[] Models =
    {
        ("Sedan", 2.47f), ("SUV", 2.84f), ("Pickup", 2.97f), ("SportsCar", 2.22f), ("DeliveryVan", 2.72f), ("SchoolBus", 4.33f)
    };
    // Eighteen vehicles over the nine blocks: a mix of the six models, buses kept rare.
    static readonly int[] Mix = { 0, 1, 2, 3, 4, 0, 5, 1, 2, 3, 4, 0, 1, 2, 3, 5, 4, 0 };
    static readonly float[] Cruise = { 7f, 8.5f, 6.5f, 9.5f, 7.5f, 8f };   // model units per second, so a world scaled 2.5x drives 2.5x as fast

    static readonly float[] RoadX = { -153, -51, 51, 153 }, RoadZ = { -78, -26, 26, 78 };
    const float Lane = 1.7f, CornerRadius = 3f;

    [MenuItem("Mindscape/World/Rebuild Vehicle Prefabs")]
    public static void BuildPrefabsMenu() => Debug.Log("Vehicle prefabs: " + BuildPrefabs());

    public static int BuildPrefabs()
    {
        Directory.CreateDirectory(Folder);
        int built = 0;
        foreach (var (name, rollSpeed) in Models)
        {
            string modelPath = "Assets/Models/Vehicles/" + name + ".glb";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) { Debug.LogWarning("Traffic: missing " + modelPath); continue; }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                instance.name = name;
                foreach (var old in instance.GetComponents<CityTrafficVehicle>()) Object.DestroyImmediate(old);
                var vehicle = instance.AddComponent<CityTrafficVehicle>();
                vehicle.modelRollSpeed = rollSpeed;
                vehicle.clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>()
                    .Where(c => !c.name.StartsWith("__preview__")).OrderBy(c => c.name).ToArray();
                PrefabUtility.SaveAsPrefabAsset(instance, Folder + "/" + name + ".prefab");
                built++;
            }
            finally { Object.DestroyImmediate(instance); }
        }
        return built;
    }

    public static string Apply(Scene scene, GameObject world)
    {
        foreach (var old in scene.GetRootGameObjects().Where(g => g.name == RootName).ToArray()) Object.DestroyImmediate(old);
        var prefabs = Models.Select(m => AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + m.model + ".prefab")).ToArray();
        if (prefabs.Any(p => p == null)) { BuildPrefabs(); prefabs = Models.Select(m => AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + m.model + ".prefab")).ToArray(); }
        if (prefabs.Any(p => p == null)) return "vehicle prefabs missing, no traffic.";

        var road = world.GetComponentsInChildren<Renderer>(true).First(r => r.name.StartsWith("Road EW"));
        float roadY = road.bounds.max.y + .02f, scale = world.transform.lossyScale.x;
        var root = new GameObject(RootName); SceneManager.MoveGameObjectToScene(root, scene);

        int count = 0;
        for (int i = 0; i < RoadX.Length - 1; i++)
            for (int j = 0; j < RoadZ.Length - 1; j++)
            {
                // The loop round this block, clockwise seen from above, in the lane on the inside of each road.
                var corners = new[]
                {
                    new Vector2(RoadX[i] + Lane, RoadZ[j + 1] - Lane), new Vector2(RoadX[i + 1] - Lane, RoadZ[j + 1] - Lane),
                    new Vector2(RoadX[i + 1] - Lane, RoadZ[j] + Lane), new Vector2(RoadX[i] + Lane, RoadZ[j] + Lane)
                };
                var loop = Loop(corners).Select(p => world.transform.TransformPoint(new Vector3(p.x, 0, p.y))).Select(p => { p.y = roadY; return p; }).ToArray();
                float length = 0; for (int k = 0; k < loop.Length; k++) length += Vector3.Distance(loop[k], loop[(k + 1) % loop.Length]);

                for (int v = 0; v < 2; v++)
                {
                    int serial = (i * (RoadZ.Length - 1) + j) * 2 + v, kind = Mix[serial % Mix.Length];
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[kind], root.transform);
                    go.name = Models[kind].model + " " + (serial + 1);
                    go.transform.localScale = Vector3.one * scale;
                    var vehicle = go.GetComponent<CityTrafficVehicle>();
                    vehicle.path = loop;
                    vehicle.speed = Cruise[(serial * 5 + 1) % Cruise.Length] * scale;
                    vehicle.PlaceAt(length * (v * .5f + .17f * ((serial * 7) % 5) / 5f));
                    count++;
                }
            }
        return count + " vehicles on 9 block loops.";
    }

    // The block's rectangle with each corner rounded by a quadratic curve, as a closed list of points (model space: x, z).
    static List<Vector2> Loop(Vector2[] c)
    {
        var points = new List<Vector2>();
        for (int k = 0; k < 4; k++)
        {
            Vector2 prev = c[(k + 3) % 4], at = c[k], next = c[(k + 1) % 4];
            Vector2 a = at + (prev - at).normalized * CornerRadius, b = at + (next - at).normalized * CornerRadius;
            for (int s = 0; s <= 6; s++)
            {
                float t = s / 6f;
                points.Add((1 - t) * (1 - t) * a + 2 * (1 - t) * t * at + t * t * b);
            }
        }
        return points;
    }
}
