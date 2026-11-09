using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// One city for every episode: Assets/Models/World/world_new.glb. It already contains the house lot ("World | House anchor") and the school
// campus ("World | School anchor") at 1x. It is wrapped once in Assets/Prefabs/World.prefab (the model plus colliders), and each story scene
// gets an instance of that prefab named "World". The instance is scaled to the scene's own house or campus and turned so the matching anchor
// lands exactly on it; that anchor (the world's copy) is switched off and the scene's copy, which carries the story's doors, bed, routes and
// NPCs, stays. The scene's own street goes, because the city's streets replace it. Everything else in the scene stays where it is.
public static class WorldModelSync
{
    public const string ModelPath = "Assets/Models/World/world_new.glb";
    public const string PrefabPath = "Assets/Prefabs/World.prefab";
    public const string InstanceName = "World";
    public const string HouseAnchor = "World | House anchor", SchoolAnchor = "World | School anchor";

    static readonly string[] NoCollider = { "Lane dash", "Crosswalk", "Light |", "Tree |", "Park |" };
    // ...except the parts you can walk into: tree trunks, lamp posts and the park's benches and stonework (leaves,
    // lamp glow and road paint stay without, so the camera is not pushed around under the trees).
    static bool Solid(string name) => name.Contains("Tree Trunk") || (name.StartsWith("Light |") && name.Contains("Metal")) || (name.StartsWith("Park |") && !name.Contains("Glass"));
    public static bool NeedsCollider(string name) => Solid(name) || !NoCollider.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));

    // Adds the colliders NeedsCollider asks for to the saved prefab in place (its objects keep their ids, so the scenes'
    // overrides on it stay put), without rebuilding it.
    [MenuItem("Mindscape/World/Add Missing Colliders")]
    public static void AddMissingColliders()
    {
        var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        int added = 0;
        try
        {
            foreach (var filter in contents.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null || !NeedsCollider(filter.name)) continue;
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                added++;
            }
            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        Debug.Log("World prefab: " + added + " colliders added.");
    }

    [MenuItem("Mindscape/World/Rebuild World Prefab")]
    public static void RebuildPrefabMenu() => Debug.Log("World prefab: " + BuildPrefab() + " colliders.");

    public static int BuildPrefab()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null) throw new InvalidOperationException("World model missing: " + ModelPath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        int colliders = 0;
        try
        {
            instance.name = "World";
            foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(filter.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
                if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null) continue;
                if (!NeedsCollider(filter.name)) continue;
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                colliders++;
            }
            Directory.CreateDirectory("Assets/Prefabs");
            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
        PerformanceSetup.SetUpWorldPrefab();   // LOD groups for the city detail
        return colliders;
    }

    public static GameObject Find(Scene scene) =>
        scene.GetRootGameObjects().FirstOrDefault(g => g.name == InstanceName && PrefabUtility.GetCorrespondingObjectFromSource(g) != null);

    // Called by every episode builder before it saves (and by the menu below): puts the city around whatever this scene is built on.
    public static string Apply(Scene scene)
    {
        DoorSetup.Apply(scene);   // every doorway gets its animated door; the city does not move them
        HouseSpareRooms.Apply(scene);   // the bathroom and the second bedroom are furnished
        var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
        var house = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "House And Lot");
        if (house != null) return ApplyTo(scene, house.transform, HouseAnchor, "Bus Stop | House Block", "Bus stop | shelter");
        var campus = all.FirstOrDefault(t => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == SchoolModelSync.ModelPath
            && PrefabUtility.GetNearestPrefabInstanceRoot(t.gameObject) == t.gameObject);
        if (campus != null) return ApplyTo(scene, campus, SchoolAnchor, "Bus Stop | School Gate", "Bus stop | shelter");
        if (scene.name == "Tutorial") return ApplyToTutorial(scene);
        return "no house lot or campus here, skipped.";
    }

    // The tutorial has no house or campus, only a flat 50 x 50 field: it is moved into the city park (Block 33, turf 120 x 80 at 2x, fountain and
    // benches) with the player's start on the turf's east side, clear of the fountain and of the walkers' lanes. The flat field is switched off.
    const float TutorialScale = 2f;
    static readonly Vector3 TutorialSpot = new Vector3(-97f, 0.1f, 52f);   // park turf, in the model's own (Unity-imported) coordinates

    static string ApplyToTutorial(Scene scene)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { BuildPrefab(); prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath); }
        var old = Find(scene);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        var world = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        world.name = InstanceName;
        world.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        world.transform.localScale = Vector3.one * TutorialScale;
        // Spot -> origin horizontally, and the turf's top (0.1 in the model) -> y 0.
        world.transform.position = new Vector3(-TutorialSpot.x * TutorialScale, -TutorialSpot.y * TutorialScale, -TutorialSpot.z * TutorialScale);
        var ground = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Ground");
        if (ground != null) ground.SetActive(false);
        Physics.SyncTransforms();
        OpenWorld(scene, world, null);
        return "tutorial moved into the city park (" + TutorialScale + "x), flat ground off, " + CityTrafficBuilder.Apply(scene, world);
    }

    // Scales and turns the city so its anchor sits exactly on the scene's own copy, then switches the world's copy off.
    static string ApplyTo(Scene scene, Transform sceneModel, string anchorName, string cityBusStop, string keepShelter)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { BuildPrefab(); prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath); }
        var old = Find(scene);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        var world = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        world.name = InstanceName;
        var anchor = world.GetComponentsInChildren<Transform>(true).First(t => t.name == anchorName);

        world.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        world.transform.localScale = Vector3.one * sceneModel.lossyScale.x;
        // Turn the city by the difference between the scene model's heading and the anchor's, then slide the anchor onto the model.
        float yaw = Mathf.DeltaAngle(anchor.eulerAngles.y, sceneModel.eulerAngles.y);
        world.transform.rotation = Quaternion.Euler(0, yaw, 0);
        world.transform.position += sceneModel.position - anchor.position;

        anchor.gameObject.SetActive(false);
        foreach (var stop in world.GetComponentsInChildren<Transform>(true).Where(t => t.name == cityBusStop)) stop.gameObject.SetActive(false);
        int hidden = 0;
        foreach (Transform part in sceneModel)
        {
            if (part.name.StartsWith("Street |", StringComparison.Ordinal) && part.gameObject.activeSelf) { part.gameObject.SetActive(false); hidden++; }
            if (part.name == keepShelter) part.gameObject.SetActive(true);
        }
        Physics.SyncTransforms();
        int ramps = AddKerbRamps(scene, world, sceneModel);
        OpenWorld(scene, world, sceneModel);
        string traffic = CityTrafficBuilder.Apply(scene, world);
        int onPavement = KeepStreetLifeOnSidewalk(scene, world, sceneModel);
        foreach (var bus in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => t.name == "School bus" || t.name == "Arrival bus").ToArray()) { SeatOnGround(bus); OffPavement(bus); }
        return "city scaled " + world.transform.lossyScale.x + "x, turned " + yaw + " deg, at " + world.transform.position.ToString("F1") + "; " + hidden + " old street part(s) hidden, " + ramps + " kerb ramp(s), " + traffic + " " + onPavement + " street NPC point(s) moved onto the sidewalk.";
    }

    // Street Life NPCs (and the waypoints of their sidewalk routes) were laid out for the old street model, whose sidewalk was a band at z 21-24 beside the
    // lot. In the city that band is grass; the pavement is the strip "... | sidewalk end" along the road. Anything standing on the lot side of the strip's
    // inner 1.2 units is moved onto the strip (keeping its order across the old band), and set down on the ground there. Safe to run again.
    static int KeepStreetLifeOnSidewalk(Scene scene, GameObject world, Transform sceneModel)
    {
        if (sceneModel == null || sceneModel.name != "House And Lot") return 0;
        var strip = world.GetComponentsInChildren<Renderer>(true)
            .Where(r => r.name.Contains("sidewalk end") && r.gameObject.activeInHierarchy && r.bounds.size.z < r.bounds.size.x)
            .OrderBy(r => r.bounds.SqrDistance(sceneModel.position)).FirstOrDefault();
        if (strip == null) return 0;
        float edge = 1.2f, zMin = strip.bounds.min.z, zMax = strip.bounds.max.z;
        float oldFrom = zMin - edge, oldTo = zMin + edge, newFrom = zMin + edge, newTo = zMax - edge;
        Physics.SyncTransforms();
        int moved = 0;
        foreach (var life in scene.GetRootGameObjects().Where(g => g.name == "Street Life"))
            foreach (var t in life.GetComponentsInChildren<Transform>(true))
            {
                bool person = t.name.StartsWith("Street NPC - ", StringComparison.Ordinal);
                bool waypoint = t.name.StartsWith("Waypoint ", StringComparison.Ordinal) && t.parent != null && t.parent.name.EndsWith(" sidewalk route", StringComparison.Ordinal);
                if (!person && !waypoint) continue;
                Vector3 at = t.position;
                if (at.z >= newFrom) continue;                       // already on the pavement (or beyond it)
                at.z = Mathf.Lerp(newFrom, newTo, Mathf.InverseLerp(oldFrom, oldTo, Mathf.Max(at.z, oldFrom)));
                float best = float.MinValue;
                foreach (var hit in Physics.RaycastAll(new Vector3(at.x, 8f, at.z), Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore))
                    if (hit.collider.GetComponentInParent<NPC>() == null && hit.point.y <= 1.5f && hit.point.y > best) best = hit.point.y;
                if (best > float.MinValue) at.y = best;
                t.position = at; moved++;
            }
        return moved;
    }

    // Open world: the whole city can be walked. The story walls that kept the player on the near sidewalk come down, the lot's pedestrian gate
    // stands open (as in Chapter 3 Episode 7), the cameras see across the city, and one invisible boundary runs round the city's outer edge
    // ("World boundary", just outside the foundation slab) instead of the old street-sized walls.
    const string BoundaryRoot = "World boundary";
    const float CameraFar = 800;

    static void OpenWorld(Scene scene, GameObject world, Transform sceneModel)
    {
        var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
        foreach (var t in all)
            if (t.name == "Road boundary" || t.name == "West route end" || t.name == "East route end") t.gameObject.SetActive(false);
        // Chapter 3 Episode 7 builds its street-sized walls ("Neighborhood edge") under the episode root, so look through the whole scene.
        foreach (var old in all.Where(t => t != null && (t.name == "Neighborhood edge" || (t.parent == null && t.name == BoundaryRoot))).ToArray()) UnityEngine.Object.DestroyImmediate(old.gameObject);
        all = all.Where(t => t != null).ToArray();   // the walls just destroyed took their children with them
        if (sceneModel != null && sceneModel.name == "House And Lot")
            foreach (Transform part in sceneModel)
                if (part.name.StartsWith("Pedestrian gate |", StringComparison.Ordinal)) part.gameObject.SetActive(false);
        foreach (var cam in all.Select(t => t.GetComponent<Camera>()).Where(c => c != null && c.targetTexture == null && c.farClipPlane > 100 && c.farClipPlane < CameraFar))
            cam.farClipPlane = CameraFar;

        var slab = world.GetComponentsInChildren<Renderer>(true).First(r => r.name == "World foundation");
        Bounds b = slab.bounds; const float height = 60, thick = 4;
        var root = new GameObject(BoundaryRoot); SceneManager.MoveGameObjectToScene(root, scene);
        void Wall(string name, Vector3 center, Vector3 size)
        {
            var wall = new GameObject(name, typeof(BoxCollider)); wall.transform.SetParent(root.transform, false);
            wall.transform.position = center; wall.GetComponent<BoxCollider>().size = size;
        }
        float mid = height * .5f - 5, lengthX = b.size.x + thick * 2, lengthZ = b.size.z + thick * 2;
        Wall("North", new Vector3(b.center.x, mid, b.max.z + thick * .5f), new Vector3(lengthX, height, thick));
        Wall("South", new Vector3(b.center.x, mid, b.min.z - thick * .5f), new Vector3(lengthX, height, thick));
        Wall("East", new Vector3(b.max.x + thick * .5f, mid, b.center.z), new Vector3(thick, height, lengthZ));
        Wall("West", new Vector3(b.min.x - thick * .5f, mid, b.center.z), new Vector3(thick, height, lengthZ));
        Physics.SyncTransforms();
    }

    // The city's entrance slabs ("... approach") sit level with the lot or campus; the pavement beyond them is lower than the player can step
    // (CharacterController step offset 0.3). A bare collider wedge ("World kerb ramps", no mesh) runs from each slab's street edge down to the pavement.
    const string RampRoot = "World kerb ramps";
    static readonly string[] Approaches = { "House | pedestrian approach", "House | driveway approach", "School | main gate approach" };

    static int AddKerbRamps(Scene scene, GameObject world, Transform sceneModel)
    {
        foreach (var old in scene.GetRootGameObjects().Where(g => g.name == RampRoot).ToArray()) UnityEngine.Object.DestroyImmediate(old);
        var root = new GameObject(RampRoot); SceneManager.MoveGameObjectToScene(root, scene);
        int made = 0;
        foreach (var slab in world.GetComponentsInChildren<Renderer>(true).Where(r => Approaches.Contains(r.name) && r.gameObject.activeInHierarchy))
        {
            var b = slab.bounds;
            Vector3 toSlab = b.center - sceneModel.position; toSlab.y = 0;
            Vector3 outward = Mathf.Abs(toSlab.x) > Mathf.Abs(toSlab.z) ? new Vector3(Mathf.Sign(toSlab.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(toSlab.z));
            Vector3 across = Vector3.Cross(Vector3.up, outward);
            float half = Mathf.Abs(Vector3.Dot(b.extents, outward)), width = Mathf.Abs(Vector3.Dot(b.size, across)) + .6f;
            Vector3 start = b.center + outward * half; start.y = b.max.y;
            var hit = Physics.RaycastAll(start + outward * 2.5f + Vector3.up * 5, Vector3.down, 12, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => h.collider.transform != slab.transform).OrderBy(h => h.distance).FirstOrDefault();
            if (hit.collider == null) continue;
            float drop = start.y - hit.point.y;
            if (drop <= .12f) continue;
            float length = Mathf.Max(2.5f, drop * 6), angle = Mathf.Atan2(drop, length) * Mathf.Rad2Deg;
            var ramp = new GameObject("Kerb ramp - " + slab.name);
            ramp.transform.SetParent(root.transform, false);
            ramp.transform.rotation = Quaternion.LookRotation(outward) * Quaternion.Euler(angle, 0, 0);
            ramp.transform.position = start + outward * (length * .5f) + Vector3.down * (drop * .5f);
            var box = ramp.AddComponent<BoxCollider>();
            box.size = new Vector3(width, .1f, Mathf.Sqrt(length * length + drop * drop)); box.center = new Vector3(0, -.05f, 0);
            made++;
        }
        Physics.SyncTransforms();
        return made;
    }

    // Drops a vehicle so its lowest point rests on whatever is below its middle (its own colliders ignored).
    // A parked story bus stands on the road beside the kerb, not partly on the pavement: looking out from its middle
    // across the road (its short axis), the nearer edge where the road ends is the kerb, and the bus moves away from it
    // until its side is a little clear of it. Returns how far it moved.
    public static float OffPavement(Transform bus)
    {
        if (bus == null) return 0;
        bool was = bus.gameObject.activeSelf; bus.gameObject.SetActive(true); Physics.SyncTransforms();
        try
        {
            var renderers = bus.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return 0;
            var b = renderers[0].bounds; foreach (var r in renderers) b.Encapsulate(r.bounds);
            Vector3 across = b.size.x > b.size.z ? Vector3.forward : Vector3.right;
            float half = Vector3.Scale(b.extents, across).magnitude;
            bool Road(Vector3 at)
            {
                var hit = Physics.RaycastAll(at + Vector3.up * 30, Vector3.down, 60, ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => !h.collider.transform.IsChildOf(bus)).OrderBy(h => h.distance).FirstOrDefault();
                return hit.collider != null && hit.collider.name.StartsWith("Road", StringComparison.Ordinal);
            }
            Vector3 middle = new Vector3(b.center.x, b.min.y, b.center.z);
            if (!Road(middle)) return 0;
            float Edge(Vector3 direction) { for (float d = .2f; d < 14; d += .1f) if (!Road(middle + direction * d)) return d; return 99; }
            float plus = Edge(across), minus = Edge(-across);
            Vector3 toKerb = plus < minus ? across : -across;
            float kerb = Mathf.Min(plus, minus), need = half + .3f;
            if (kerb >= need) return 0;
            bus.position -= toKerb * (need - kerb);
            return need - kerb;
        }
        finally { bus.gameObject.SetActive(was); }
    }

    static void SeatOnGround(Transform vehicle)
    {
        bool was = vehicle.gameObject.activeSelf; vehicle.gameObject.SetActive(true); Physics.SyncTransforms();
        var renderers = vehicle.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        var b = renderers[0].bounds; foreach (var r in renderers) b.Encapsulate(r.bounds);
        var hit = Physics.RaycastAll(new Vector3(b.center.x, b.max.y + 20, b.center.z), Vector3.down, b.size.y + 60, ~0, QueryTriggerInteraction.Ignore)
            .Where(h => !h.collider.transform.IsChildOf(vehicle)).OrderBy(h => h.distance).FirstOrDefault();
        if (hit.collider != null) vehicle.position += Vector3.up * (hit.point.y - b.min.y);
        vehicle.gameObject.SetActive(was);
    }

    [MenuItem("Mindscape/World/Apply World to All Story Scenes")]
    public static void ApplyAllMenu()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play mode first."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string active = SceneManager.GetActiveScene().path;
        foreach (var path in Directory.GetFiles("Assets/Scenes", "*.unity").Where(p => Path.GetFileName(p).StartsWith("Chapter ") || Path.GetFileName(p) == "Tutorial.unity").OrderBy(p => p))
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            string result = Apply(scene);
            Debug.Log("World, " + scene.name + ": " + result);
            if (result.StartsWith("no house")) continue;
            // The house street's walking routes follow the city's sidewalks, so the ambient walkers are laid out again.
            if (scene.GetRootGameObjects().Any(g => g.name == "House And Lot")) AmbientLifeBuilder.Apply(scene);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        if (File.Exists(active)) EditorSceneManager.OpenScene(active, OpenSceneMode.Single);
    }

    [MenuItem("Mindscape/World/Apply World to Open Scene")]
    public static void ApplyOpenSceneMenu()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play mode first."); return; }
        var scene = SceneManager.GetActiveScene();
        Debug.Log("World, " + scene.name + ": " + Apply(scene));
        EditorSceneManager.MarkSceneDirty(scene);
    }
}
