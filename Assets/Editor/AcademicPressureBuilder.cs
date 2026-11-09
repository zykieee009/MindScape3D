using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mindscape.AcademicPressure;
using Mindscape.EpisodeOne;
using Mindscape.EpisodeThree;
using Mindscape.EpisodeTwo;
using Mindscape.Minigames;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Builds Chapter 1 · Academic Pressure (four scenes) and moves the Family Expectations episodes to Chapter 2.
// Every step is safe to re-run: generated scenes are rebuilt from their sources, the move happens once.
public static partial class AcademicPressureBuilder
{
    const string Menu = "Mindscape/Chapter 1 - Academic Pressure/";
    const string MaterialFolder = "Assets/AcademicPressure/Materials";
    static readonly string[] FamilyRoots = { "Episode 1 - Family Expectations", "Episode 2 - Before the Bell", "Episode 3 - Running Out of Time" };
    static string ScenePath(int episode) => "Assets/Scenes/" + AcademicPressureChapter.Scenes[episode - 1] + ".unity";
    static string FamilyPath(int episode) => "Assets/Scenes/Chapter 2 - Episode " + episode + ".unity";

    static Transform root;
    static Material cream, ink, teal, gold, wood, paper, navy, sage, skin, hair, coral, glow;

    // ================================================================ menu

    [MenuItem(Menu + "Build All Episodes", priority = 0)]
    public static void BuildAll()
    {
        if (!Prepare()) return;
        MoveFamilyToChapterTwo();
        BuildHouse(); BuildClassroom(); BuildCafeteria(); BuildDormitory();
        RegisterScenes();
        EditorSceneManager.OpenScene(ScenePath(1));
        Debug.Log("Chapter 1 · Academic Pressure built: 4 episodes. Family Expectations is Chapter 2. Database: " + Mindscape.Data.MindscapeDatabase.StorageDescription);
    }

    [MenuItem(Menu + "Build Episode 1 - House", priority = 20)] public static void MenuHouse() => BuildOne(BuildHouse, 1);
    [MenuItem(Menu + "Build Episode 2 - Classroom", priority = 21)] public static void MenuClassroom() => BuildOne(BuildClassroom, 2);
    [MenuItem(Menu + "Build Episode 3 - Cafeteria", priority = 22)] public static void MenuCafeteria() => BuildOne(BuildCafeteria, 3);
    [MenuItem(Menu + "Build Episode 4 - Dormitory", priority = 23)] public static void MenuDormitory() => BuildOne(BuildDormitory, 4);

    [MenuItem(Menu + "Move Family Expectations to Chapter 2", priority = 40)]
    public static void MenuMoveFamily() { if (Prepare()) { MoveFamilyToChapterTwo(); RegisterScenes(); } }

    [MenuItem(Menu + "Reveal Database File", priority = 60)]
    public static void RevealDatabase()
    {
        Debug.Log("Mindscape database: " + Mindscape.Data.MindscapeDatabase.StorageDescription);
        if (File.Exists(Mindscape.Data.MindscapeDatabase.DatabasePath)) EditorUtility.RevealInFinder(Mindscape.Data.MindscapeDatabase.DatabasePath);
    }

    static void BuildOne(Action build, int episode)
    {
        if (!Prepare()) return;
        MoveFamilyToChapterTwo();
        build(); RegisterScenes();
        EditorSceneManager.OpenScene(ScenePath(episode));
    }

    static bool Prepare()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before building.");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        // Leave every scene closed so files can move safely.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Directory.CreateDirectory(MaterialFolder);
        EnsurePrototypes();
        return true;
    }

    // ================================================================ Family Expectations -> Chapter 2

    public static void MoveFamilyToChapterTwo()
    {
        for (int n = 1; n <= 3; n++)
        {
            string from = "Assets/Scenes/Chapter 1 - Episode " + n + ".unity", to = FamilyPath(n);
            if (File.Exists(to) || !File.Exists(from) || !File.ReadAllText(from).Contains(FamilyRoots[n - 1])) continue;
            string error = AssetDatabase.MoveAsset(from, to);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException("Could not move " + from + ": " + error);
            Debug.Log("Moved " + from + " -> " + to);
        }
        // Serialized scene names (next-episode links, the cutscene entry) follow the move.
        var renames = new Dictionary<string, string>();
        for (int n = 1; n <= 3; n++) renames["Chapter 1 - Episode " + n] = "Chapter 2 - Episode " + n;
        foreach (var path in new[] { FamilyPath(1), FamilyPath(2), FamilyPath(3), "Assets/Scenes/Cutscene - Episode 1.unity" })
        {
            if (!File.Exists(path)) continue;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int changed = 0;
            foreach (var go in scene.GetRootGameObjects())
                foreach (var component in go.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component == null) continue;
                    var so = new SerializedObject(component); var it = so.GetIterator();
                    while (it.Next(true))
                        if (it.propertyType == SerializedPropertyType.String && renames.TryGetValue(it.stringValue, out string renamed)) { it.stringValue = renamed; changed++; }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            if (changed > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); Debug.Log("Updated " + changed + " scene link(s) in " + path); }
        }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    // Build order: menus, tutorial, Chapter 1, Chapter 2, then anything else already listed.
    static void RegisterScenes()
    {
        var ordered = new List<string> { "Assets/Scenes/main_menu_new.unity", "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Tutorial.unity" };
        for (int i = 1; i <= AcademicPressureChapter.EpisodeCount; i++) ordered.Add(ScenePath(i));
        ordered.Add("Assets/Scenes/Cutscene - Episode 1.unity");
        for (int i = 1; i <= 3; i++) ordered.Add(FamilyPath(i));
        List<EditorBuildSettingsScene> Merge(IEnumerable<EditorBuildSettingsScene> existing)
        {
            var current = existing.Where(x => File.Exists(x.path)).ToList();
            var list = ordered.Where(File.Exists).Select(p => new EditorBuildSettingsScene(p, true)).ToList();
            list.AddRange(current.Where(x => !ordered.Contains(x.path)));
            return list;
        }
        EditorBuildSettings.scenes = Merge(EditorBuildSettings.scenes).ToArray();
        foreach (var guid in AssetDatabase.FindAssets("t:BuildProfile"))
        {
            var profile = AssetDatabase.LoadAssetAtPath<UnityEditor.Build.Profile.BuildProfile>(AssetDatabase.GUIDToAssetPath(guid));
            if (profile == null || !profile.overrideGlobalScenes) continue;
            var list = Merge(profile.scenes.Select(x => new EditorBuildSettingsScene(x.path, x.enabled)));
            profile.scenes = new EditorBuildSettingsScene[0]; profile.scenes = list.ToArray(); EditorUtility.SetDirty(profile);
        }
        AssetDatabase.SaveAssets();
    }

    // Opens a copy of an existing furnished scene at the Chapter 1 path.
    static Scene CopyScene(string source, int episode)
    {
        if (!File.Exists(source)) throw new FileNotFoundException("Source scene missing. Build Chapter 2 first.", source);
        string target = ScenePath(episode);
        if (File.Exists(target) && FamilyRoots.Any(r => File.ReadAllText(target).Contains(r)))
            throw new InvalidOperationException(target + " still holds a Family Expectations episode. Run 'Move Family Expectations to Chapter 2' first.");
        var scene = EditorSceneManager.OpenScene(source, OpenSceneMode.Single);
        EditorSceneManager.SaveScene(scene, target, true);
        AssetDatabase.ImportAsset(target);
        return EditorSceneManager.OpenScene(target, OpenSceneMode.Single);
    }

    static void Materials()
    {
        cream = Mat("Ivory", new Color32(237, 224, 196, 255)); ink = Mat("Ink", new Color32(34, 45, 49, 255));
        teal = Mat("School teal", new Color32(53, 98, 100, 255)); gold = Mat("Brass", new Color32(220, 174, 94, 255));
        wood = Mat("Walnut", new Color32(123, 83, 54, 255)); paper = Mat("Paper", new Color32(246, 238, 220, 255));
        navy = Mat("Canvas navy", new Color32(48, 65, 81, 255)); sage = Mat("Sage", new Color32(122, 143, 105, 255));
        skin = Mat("Warm skin", new Color32(179, 126, 91, 255)); hair = Mat("Dark hair", new Color32(40, 33, 30, 255));
        coral = Mat("Coral", new Color32(196, 112, 92, 255)); glow = Mat("Screen glow", new Color32(170, 214, 210, 255), true);
    }

    // ================================================================ Episode 1 · House

    public static void BuildHouse()
    {
        var scene = CopyScene(PrototypePath(2), 1);
        Materials();
        var old = Object.FindAnyObjectByType<EpisodeTwoStory>();
        if (old == null) throw new InvalidOperationException("Chapter 2 - Episode 2 has no EpisodeTwoStory to copy from.");
        root = old.transform; root.name = "Chapter 1 · Episode 1 - House";
        var bag = old.items.Length > 2 ? old.items[2] : null;
        for (int i = 0; i < old.items.Length - 1; i++) if (old.items[i] != null) Object.DestroyImmediate(old.items[i].gameObject);
        if (old.phoneModel != null) Object.DestroyImmediate(old.phoneModel);
        var story = root.gameObject.AddComponent<HouseEpisode>();
        story.player = old.player; story.storyCamera = old.storyCamera; story.frontDoor = old.exit; story.busStop = old.stop; story.bus = old.bus;
        story.routeMarkers = old.routeMarkers; story.carriedBag = old.carriedBag;
        RestyleRoute(story.routeMarkers);
        // Swap the copied cube-built bus for the school bus model, door at the boarding spot by the shelter.
        if (old.bus != null)
        {
            var board = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Board bus");
            float doorX = board != null ? board.position.x : old.bus.position.x + 2.2f, kerbZ = old.bus.position.z - 2f;
            story.bus = PlaceSchoolBus("School bus", old.bus.parent, doorX, kerbZ, old.bus.position.y);   // re-seated on the road below
            story.bus.SetSiblingIndex(old.bus.GetSiblingIndex());
            Object.DestroyImmediate(old.bus.gameObject);
        }
        SyncPlayerMaterials(story.player);
        if (old.deskLamp != null) old.deskLamp.intensity = .6f;
        Object.DestroyImmediate(old);
        foreach (var name in new[] { "Study notebook interaction", "Late study camera" }) { var t = root.Find(name); if (t != null) Object.DestroyImmediate(t.gameObject); }
        foreach (var label in root.GetComponentsInChildren<TextMeshPro>(true)) if (label.text == "07:50") label.text = "07:10";
        UseCurrentHouse(scene);
        HouseModelSync.Sync(scene);   // street and bus stop come from the house model
        SeatOnGround(story.bus);      // the street exists now
        SingleBedroom();
        Physics.SyncTransforms();

        // Bedroom: phone on the bedside table, laptop and books on the desk, bag by the bed.
        story.phone = Pivot("Phone", Surface(-13.62f, -14.45f, 1.86f, 2.75f));
        Box("Phone body", story.phone.position + Vector3.up * .02f, new Vector3(.24f, .035f, .44f), ink, story.phone, false).transform.rotation = Quaternion.Euler(0, 18, 0);
        Box("Phone glass", story.phone.position + Vector3.up * .04f, new Vector3(.2f, .01f, .38f), glow, story.phone, false).transform.rotation = Quaternion.Euler(0, 18, 0);

        story.laptop = Pivot("Laptop", Surface(-15.15f, -6.62f, 2.39f, 2.9f));
        Vector3 l = story.laptop.position;
        Box("Laptop base", l + Vector3.up * .02f, new Vector3(.82f, .04f, .56f), ink, story.laptop, false);
        Box("Keyboard", l + new Vector3(0, .042f, -.04f), new Vector3(.7f, .006f, .3f), navy, story.laptop, false);
        Box("Laptop lid", l + new Vector3(0, .29f, .27f), new Vector3(.82f, .52f, .035f), ink, story.laptop, false).transform.rotation = Quaternion.Euler(12, 0, 0);
        var screen = Box("Laptop screen (on)", l + new Vector3(0, .29f, .245f), new Vector3(.74f, .44f, .01f), glow, story.laptop, false);
        screen.transform.rotation = Quaternion.Euler(12, 0, 0); story.laptopScreen = screen;
        WorldLabel("DRAFT\n1. Introduction ...", l + new Vector3(0, .3f, .235f), Quaternion.Euler(12, 0, 0), .66f, .38f, .55f, ink.color, screen.transform.parent);
        screen.SetActive(false);

        story.books = Pivot("Books", Surface(-14.0f, -6.65f, 2.39f, 2.9f));
        Vector3 b = story.books.position;
        Box("Reference book", b + Vector3.up * .06f, new Vector3(.62f, .12f, .82f), teal, story.books, false);
        Box("Research notes", b + new Vector3(.02f, .16f, 0), new Vector3(.56f, .08f, .76f), coral, story.books, false).transform.rotation = Quaternion.Euler(0, 9, 0);
        Box("Library book", b + new Vector3(-.03f, .25f, .02f), new Vector3(.5f, .1f, .7f), navy, story.books, false).transform.rotation = Quaternion.Euler(0, -6, 0);
        MoveStudyCorner(story.laptop, story.books);

        if (bag != null)
        {
            foreach (var glint in bag.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Objective glint").ToArray()) Object.DestroyImmediate(glint.gameObject);
            bag.position = new Vector3(-17.3f, 1.3f, -10.5f); story.schoolBag = bag;
        }
        else story.schoolBag = MakeBag("School bag", new Vector3(-17.3f, 1.3f, -10.5f));

        // Chores: an unmade bed (switching between messy and fixed modes), a breakfast plate on the dining table, the kitchen sink.
        story.bed = Pivot("Bed", new Vector3(-11.6f, 1.8f, -11.9f));
        var houseTr = GameObject.Find("House And Lot")?.transform;
        var modelMessy = houseTr != null ? houseTr.Find("West bedroom | bed messy") : null;
        var modelFixed = houseTr != null ? houseTr.Find("West bedroom | bed fixed") : null;
        if (modelMessy != null && modelFixed != null)
        {
            story.messyBed = modelMessy.gameObject;
            story.fixedBed = modelFixed.gameObject;
            story.messyBed.SetActive(true);
            story.fixedBed.SetActive(false);
            var oldMessy = root.Find("Unmade bed");
            if (oldMessy != null) Object.DestroyImmediate(oldMessy.gameObject);
        }
        else
        {
            var messy = Pivot("Unmade bed", Vector3.zero); story.messyBed = messy.gameObject;
            Vector3 mattress = Surface(-11.6f, -12.9f, 2.06f, 3.2f);
            Box("Rumpled blanket", mattress + Vector3.up * .1f, new Vector3(2.05f, .16f, 2.1f), teal, messy, false).transform.rotation = Quaternion.Euler(3, 11, -2);
            Box("Blanket fold", mattress + new Vector3(.35f, .2f, .5f), new Vector3(.9f, .14f, .65f), sage, messy, false).transform.rotation = Quaternion.Euler(-4, -18, 5);
            Box("Pillow askew", mattress + new Vector3(-.3f, .22f, -1.55f), new Vector3(.9f, .22f, .5f), cream, messy, false).transform.rotation = Quaternion.Euler(0, 28, 6);
        }

        story.plate = Pivot("Breakfast plate", Surface(-15.6f, -.45f, 2.69f, 3.3f));   // clear of the potted plant
        Cylinder("Plate", story.plate.position + Vector3.up * .015f, new Vector3(.6f, .012f, .6f), paper, story.plate);
        Box("Toast crust", story.plate.position + new Vector3(.08f, .04f, .05f), new Vector3(.18f, .03f, .12f), gold, story.plate, false);
        Box("Fork", story.plate.position + new Vector3(-.2f, .035f, 0), new Vector3(.04f, .01f, .42f), cream, story.plate, false);
        var carried = Cylinder("Carried plate", Vector3.zero, new Vector3(.5f, .012f, .5f), paper, story.player);
        carried.transform.localPosition = new Vector3(0, 1.55f, .55f); story.carriedPlate = carried;
        story.sink = Pivot("Kitchen sink", new Vector3(-11.9f, 2.6f, -1.58f));

        story.outsideSpawn = new Vector3(-22.1f, .7f, 8.2f); story.outsideYaw = 0;
        story.player.SetPositionAndRotation(new Vector3(-15.9f, .65f, -10.8f), Quaternion.Euler(0, 90, 0));
        var movement = story.player.GetComponent<PlayerMovement>(); movement.cameraTransform = story.storyCamera.transform;
        ScalePlayer(story.player);
        StreetLifeBuilder.Populate(scene);
        CollectibleModels.Apply(scene);
        WorldModelSync.Apply(scene);   // the shared city around the house lot
        AmbientLifeBuilder.Apply(scene);   // family at home, five walkers, everyone talkable
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("Chapter 1 · Episode 1 built: " + ScenePath(1));
    }

    // Everyone in Chapter 1 is 1.4x the original rig. The player model is 2.54 m tall, level with the kitchen
    // countertop in a house built at 2.5x scale; at 1.4x (about 3.56 m) they stand slightly taller than the sink,
    // faucet included. The camera rig and walking speed grow with the body.
    public const float PeopleScale = 1.4f;

    static void ScalePlayer(Transform player)
    {
        if (player == null || Mathf.Approximately(player.localScale.x, PeopleScale)) return;
        player.localScale = Vector3.one * PeopleScale;
        var m = player.GetComponent<PlayerMovement>(); if (m == null) return;
        m.cameraOffset *= PeopleScale; m.cameraTargetHeight *= PeopleScale; m.cameraShoulderOffset *= PeopleScale;
        m.minDistance *= PeopleScale; m.maxDistance *= PeopleScale; m.moveSpeed *= PeopleScale; m.runSpeed *= PeopleScale;
    }

    // The source scenes keep their own copy of the player, made before BaldCharacter.glb gained hair. A mesh part
    // with no material is not drawn, so add the materials the current model has beyond the ones already set.
    public static void SyncPlayerMaterials(Transform player)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/BaldCharacter/BaldCharacter.glb")?.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (model == null || player == null) return;
        foreach (var renderer in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var current = renderer.sharedMaterials; var latest = model.sharedMaterials;
            if (renderer.sharedMesh != model.sharedMesh || current.Length >= latest.Length) continue;
            renderer.sharedMaterials = current.Concat(latest.Skip(current.Length)).ToArray();
        }
    }

    // The Chapter 2 - Episode 2 prototype holds an older import of the house (walls lying on their side, the two-bed furniture), so
    // swap in the current house from Chapter 2 - Episode 1 and point the story's references at the matching parts.
    static void UseCurrentHouse(Scene scene)
    {
        var oldHouse = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "House And Lot");
        var source = EditorSceneManager.OpenScene(PrototypePath(1), OpenSceneMode.Additive);
        var house = source.GetRootGameObjects().FirstOrDefault(g => g.name == "House And Lot");
        if (house == null) { EditorSceneManager.CloseScene(source, true); throw new InvalidOperationException(PrototypePath(1) + " has no House And Lot to copy."); }
        SceneManager.MoveGameObjectToScene(house, scene);
        EditorSceneManager.CloseScene(source, true);   // closed unsaved: the Chapter 2 file keeps its house
        SceneManager.SetActiveScene(scene);
        // The story brings its own phone and 07:10 clock; the model's copies would sit right beside them.
        foreach (var name in new[] { "West bedroom | smartphone", "West bedroom | alarm clock" })
        { var t = house.transform.Find(name); if (t != null) Object.DestroyImmediate(t.gameObject); }
        // Furniture the player bumps into and the story places things on (phone, blanket, plate) needs colliders.
        foreach (Transform part in house.transform)
            if (new[] { "West bedroom |", "Dining", "Kitchen |", "Living |", "Sofa" }.Any(part.name.StartsWith))
                foreach (var mf in part.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.GetComponent<Collider>() == null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        // The reach is measured from the middle of the door; the school bag is about 3.2 m away, out of reach.
        foreach (var door in house.GetComponentsInChildren<DoorInteraction>(true))
            if (door.name == "Door_Bedroom_Left") door.interactRange = 2.2f;
        if (oldHouse == null) return;

        int moved = 0;
        foreach (var go in scene.GetRootGameObjects())
        {
            if (go == oldHouse || go == house) continue;
            foreach (var component in go.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is Transform) continue;
                var so = new SerializedObject(component); var it = so.GetIterator();
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                    var target = it.objectReferenceValue is GameObject g ? g.transform : (it.objectReferenceValue as Component)?.transform;
                    if (target == null || !target.IsChildOf(oldHouse.transform)) continue;
                    var match = target == oldHouse.transform ? house.transform : house.transform.Find(AnimationUtility.CalculateTransformPath(target, oldHouse.transform));
                    Object replacement = match == null ? null : it.objectReferenceValue is GameObject ? match.gameObject : (Object)match.GetComponent(it.objectReferenceValue.GetType());
                    if (replacement == null) Debug.LogWarning(component.name + "." + it.propertyPath + " pointed at " + target.name + ", which the current house does not have.");
                    it.objectReferenceValue = replacement; moved++;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        house.transform.SetSiblingIndex(oldHouse.transform.GetSiblingIndex());
        Object.DestroyImmediate(oldHouse);
        if (moved > 0) Debug.Log("House swap: " + moved + " reference(s) moved to the current house.");
    }

    // The bedroom door used to swing into the room along the north wall, and the 1.4x player (1.12 m wide) did not fit
    // between the open door and the desk and chair next to it. Move the whole study corner (desk, legs, chair, lamp,
    // its light, and the laptop and books on it) to the east wall between the wardrobe and the bed, turned a quarter
    // so the desk faces that wall with the chair on the room side.
    static void MoveStudyCorner(params Transform[] onDesk)
    {
        var desk = root.Find("Study desk"); if (desk == null) return;
        Vector3 from = desk.GetComponent<Renderer>().bounds.center;
        Vector3 to = new Vector3(-10.68f, from.y, -9.75f);   // 1 m deep against the east wall (x -10.13), wardrobe ends z -7.83, bed starts z -11.64
        var turn = Quaternion.Euler(0, 90, 0);              // "toward the wall" was +Z, now +X
        var names = new[] { "Study desk", "Desk leg", "Desk lamp base", "Evening desk light", "Study chair" };
        var parts = root.Cast<Transform>().Where(t => names.Contains(t.name)).Concat(onDesk.Where(t => t != null)).ToList();
        foreach (var t in parts) { t.position = to + turn * (t.position - from); t.rotation = turn * t.rotation; }
        Physics.SyncTransforms();
    }

    // The house model (Blender Files/house-and-lot-cozy.blend) has a single bed and a small bedside table in
    // the bedroom's south-east corner. The copied story's alarm clock still sits where the old, taller cabinet
    // was, so put it back on the table top. The wardrobe goes flush into the north-east corner.
    static void SingleBedroom()
    {
        var house = GameObject.Find("House And Lot")?.transform;
        if (house != null) BedroomLayout.WardrobeToCorner(house);
        var leftover = house != null ? house.Find("West bedroom | pillow.001") : null;   // removed from the model
        if (leftover != null) Object.DestroyImmediate(leftover.gameObject);
        var clock = root.Find("Bedside alarm clock");
        if (clock == null) return;
        clock.localScale *= .75f; Physics.SyncTransforms();
        var b = clock.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((x, y) => { x.Encapsulate(y); return x; });
        clock.position += new Vector3(-13.2f - b.center.x, 1.86f - b.min.y, -14.4f - b.center.z);
    }

    // ================================================================ Episode 2 · Classroom

    public static void BuildClassroom()
    {
        PrepareCast();
        var scene = NewSceneWithPlayer(2, out var player, out var camera);
        var story = root.gameObject.AddComponent<ClassroomEpisode>();
        story.player = player; story.storyCamera = camera; story.friendName = "Sam";
        var school = PlaceSchool();
        Lighting(new Color(1, .91f, .77f), 1.35f, new Color(.68f, .73f, .75f), new Color(.74f, .83f, .88f));
        Bounds floor = Part(school, "Room 03 | Floor"), header = Part(school, "Room 03 | Door_Header"), board = Part(school, "Room 03 | Chalkboard");
        Vector3 roomCenter = floor.center, outward = Axis(header.center - roomCenter), toBoard = Axis(board.center - roomCenter);
        Vector3 side = Vector3.Cross(Vector3.up, toBoard);
        Vector3 doorway = new Vector3(header.center.x, floor.max.y, header.center.z);
        float floorY = floor.max.y;
        PointLight("Classroom light", new Vector3(roomCenter.x, 5.2f, roomCenter.z), new Color(1, .93f, .82f), 1.4f, 18);

        // Arrival: the bus stops on the road outside the gate and you step off onto the sidewalk.
        Bounds gate = Part(school, "School gate | pillars and arch"), road = Part(school, "Street | road"), corridor = Part(school, "Corridor | floor");
        Vector3 toStreet = Axis(road.center - gate.center);
        Vector3 spawnAt = Surface(gate.center.x, gate.center.z + toStreet.z * 2.6f, 0, 3);
        story.spawn = Pivot("Bus stop arrival", spawnAt + Vector3.up * .1f); story.spawn.rotation = Quaternion.LookRotation(-toStreet);
        // The school bus pulls up at the kerb with its door at the gate.
        story.bus = PlaceSchoolBus("Arrival bus", root, gate.center.x, Mathf.Min(road.min.z, road.max.z), road.max.y);
        story.openingView = Pivot("School arrival view", gate.center + toStreet * 16 + Vector3.up * 9 + Vector3.right * 12);
        story.openingView.LookAt(gate.center + Vector3.up * 2 - toStreet * 4);

        // Gold markers: sidewalk, through the gate, up the path, along the corridor to Room 03.
        float corridorZ = corridor.center.z;
        story.entrance = Pivot("Classroom door", doorway + outward * 1.4f + Vector3.up * 1.6f);
        story.routeMarkers = Route("Route to Room 03", spawnAt, new Vector3(gate.center.x, 0, gate.center.z), new Vector3(gate.center.x, 0, corridorZ), new Vector3(doorway.x, 0, corridorZ), doorway + outward * 1.6f);

        // Room 03 inside: your seat is the chair nearest the door. Sam sits beside you at the same double desk
        // (chairs share a desk when their names carry the same row/column, e.g. "R4C3"). The class has four fewer
        // students than seats once held: Sam takes the seat beside you and the three seats farthest from yours stay empty.
        var chairs = Parts(school, "Room 03 | ", "Wood_Seat").OrderBy(r => Vector3.Distance(r.bounds.center, doorway)).ToList();
        Vector3 ownPos = chairs[0].bounds.center;
        string Desk(Renderer chair) => chair.name.Split('_').FirstOrDefault(part => part.Length >= 4 && part[0] == 'R' && part.Contains('C')) ?? chair.name;
        var partner = chairs.Skip(1).Where(c => Desk(c) == Desk(chairs[0])).DefaultIfEmpty(chairs[1]).First();
        story.seat = Pivot("Seat interaction", new Vector3(ownPos.x, floorY + 1.6f, ownPos.z) - toBoard * 1.6f);
        story.seatView = Pivot("Seated view", ownPos + Vector3.up * 2.4f - toBoard * .15f); story.seatView.LookAt(board.center + Vector3.down * .6f);
        var others = chairs.Skip(1).Where(c => c != partner).OrderBy(c => Vector3.Distance(c.bounds.center, ownPos)).ToList();
        var students = new List<Transform>();
        for (int i = 0; i < others.Count - 3; i++)
        {
            var c = others[i].bounds;
            students.Add(Person("Student at desk " + (i + 1), NextStudent(), new Vector3(c.center.x, c.max.y, c.center.z), Quaternion.LookRotation(toBoard), true, ClassroomClip));
        }
        var near = students.OrderBy(t => Vector3.Distance(t.position, ownPos)).First();
        near.name = "Classmate (almost finished)"; story.classmate = near;
        MakeBag("Your school bag", new Vector3(ownPos.x, floorY + .6f, ownPos.z) + side * .8f);

        Physics.SyncTransforms();
        Vector3 stand = ClearSpot(new Vector3(board.center.x, 0, board.center.z) - toBoard * 2.4f + side * Mathf.Max(board.size.x, board.size.z) * .38f, .2f, story.entrance.position, 0, 1.5f, floorY);
        Person("Teacher", TeacherModel, stand, Quaternion.LookRotation(-toBoard), false, "Conversation", AdultHeight);
        story.teacher = Pivot("Teacher look target", stand + Vector3.up * (AdultHeight - .9f));
        WorldLabel("RESEARCH PROJECT\n\nPresentations: TOMORROW\nMake sure your work is ready.", board.center - toBoard * .12f, Quaternion.LookRotation(toBoard),
            Mathf.Max(board.size.x, board.size.z) * .9f, board.size.y * .8f, 3.8f, cream.color, null);

        var ps = partner.bounds;
        var friend = Person("Friend · Sam", SamModel, new Vector3(ps.center.x, ps.max.y, ps.center.z), Quaternion.LookRotation(toBoard), true, ClassroomClip);
        story.friend = Pivot("Friend look target", new Vector3(ps.center.x, ps.max.y + 1.5f, ps.center.z)); story.friend.SetParent(friend, true);
        foreach (var writer in students.Append(friend)) PaperOnDesk(writer); // something to write on
        story.landing = ClassroomLanding(doorway, outward, floorY);

        player.SetPositionAndRotation(story.spawn.position, story.spawn.rotation);
        camera.transform.SetPositionAndRotation(story.openingView.position, story.openingView.rotation);
        Save(scene, 2);
    }

    // Where "Walk into your classroom" puts you: through the (closed) door and a few steps into the room, facing in,
    // clear of the desks so the camera has room behind you.
    public static Transform ClassroomLanding(Vector3 doorway, Vector3 outward, float floorY)
    {
        Physics.SyncTransforms();
        Vector3 at = ClearSpot(doorway - outward * 3f, 0, doorway, 2.2f, 1.5f, floorY);
        var landing = Pivot("Inside Room 03", at + Vector3.up * .1f);
        landing.rotation = Quaternion.LookRotation(-outward);
        return landing;
    }

    // ================================================================ Episode 3 · Cafeteria

    public static void BuildCafeteria()
    {
        PrepareCast();
        var scene = NewSceneWithPlayer(3, out var player, out var camera);
        var story = root.gameObject.AddComponent<CafeteriaEpisode>();
        story.player = player; story.storyCamera = camera;
        story.worryCourtroomPrefab = AssetDatabase.LoadAssetAtPath<Mindscape.Minigames.WorryCourtroomMinigame>("Assets/Prefabs/Minigames/WorryCourtroom.prefab");
        story.worryCase = AssetDatabase.LoadAssetAtPath<Mindscape.Minigames.WorryCourtroomCase>("Assets/Minigames/WorryCourtroom/Cases/Chapter1_EveryoneAhead.asset");
        var school = PlaceSchool();
        Lighting(new Color(1, .93f, .8f), 1.3f, new Color(.66f, .69f, .7f), new Color(.74f, .83f, .88f));
        Bounds cafFloor = Part(school, "Cafeteria | Floor"), cafHeader = Part(school, "Cafeteria | Front_Door_Header");
        Vector3 center = cafFloor.center, cafOut = Axis(cafHeader.center - center);
        Vector3 cafDoor = new Vector3(cafHeader.center.x, cafFloor.max.y, cafHeader.center.z);
        PointLight("Cafeteria light", new Vector3(center.x, 5.2f, center.z), new Color(1, .92f, .78f), 2.2f, 22);

        // Start outside Room 03 after class, then walk the corridor to the cafeteria door.
        Bounds roomFloor = Part(school, "Room 03 | Floor"), roomHeader = Part(school, "Room 03 | Door_Header"), corridor = Part(school, "Corridor | floor");
        Vector3 roomOut = Axis(roomHeader.center - roomFloor.center);
        Vector3 start = Surface(roomHeader.center.x + roomOut.x * 2.4f, roomHeader.center.z + roomOut.z * 2.4f, 0, 3);
        story.spawn = Pivot("Outside Room 03", start + Vector3.up * .1f); story.spawn.rotation = Quaternion.LookRotation(Flat(cafDoor - start));
        story.cafeteriaDoor = Pivot("Cafeteria door", cafDoor + cafOut * 1.4f + Vector3.up * 1.6f);
        story.inside = Pivot("Inside the cafeteria", cafDoor - cafOut * 2.6f + Vector3.up * .1f); story.inside.rotation = Quaternion.LookRotation(-cafOut);
        story.routeMarkers = Route("Route to the cafeteria", start, new Vector3(start.x, 0, corridor.center.z), new Vector3(cafDoor.x, 0, corridor.center.z), cafDoor + cafOut * 1.6f);
        player.SetPositionAndRotation(story.spawn.position, story.spawn.rotation);
        Vector3 inDoor = story.inside.position;

        // Your classmates' table is the one nearest the door; the far bench faces you.
        var tables = Parts(school, "Cafeteria | ", "_Table").OrderBy(t => Vector3.Distance(t.bounds.center, inDoor)).ToList();
        var benchesAll = Parts(school, "Cafeteria | ", "_Bench").ToList();
        var table = tables[0].bounds;
        var benches = benchesAll.Where(r => Vector2.Distance(XZ(r.bounds.center), XZ(table.center)) < 4f).OrderBy(r => Vector3.Distance(r.bounds.center, inDoor)).ToList();
        Bounds nearBench = benches[0].bounds, farBench = benches[benches.Count - 1].bounds;
        Vector3 farSeat = new Vector3(farBench.center.x, farBench.max.y, farBench.center.z), nearSeat = new Vector3(nearBench.center.x, nearBench.max.y, nearBench.center.z);
        Vector3 along = new Vector3(farBench.size.x >= farBench.size.z ? 1 : 0, 0, farBench.size.x >= farBench.size.z ? 0 : 1);
        float spread = Mathf.Max(farBench.size.x, farBench.size.z) * .28f;
        Quaternion faceNear = Quaternion.LookRotation(Flat(nearSeat - farSeat)), faceFar = Quaternion.LookRotation(Flat(farSeat - nearSeat));
        var one = Person("Student 1", NextStudent(), farSeat - along * spread, faceNear, true, LunchClip);
        var two = Person("Student 2", NextStudent(), farSeat + along * spread, faceNear, true, LunchClip);
        var friend = Person("Friend · Sam", SamModel, nearSeat + along * spread, faceFar, true, LunchClip);
        story.studentOne = Pivot("Student 1 look target", one.position + Vector3.up * .1f); story.studentOne.SetParent(one, true);
        story.studentTwo = Pivot("Student 2 look target", two.position + Vector3.up * .1f); story.studentTwo.SetParent(two, true);
        story.friend = Pivot("Sam look target", friend.position + Vector3.up * .1f); story.friend.SetParent(friend, true);
        Vector3 top = new Vector3(table.center.x, table.max.y, table.center.z);
        foreach (var at in new[] { top + along * spread * 1.1f + Flat(farSeat - top).normalized * .35f, top - along * spread * 1.1f + Flat(farSeat - top).normalized * .35f, top + along * spread + Flat(nearSeat - top).normalized * .35f })
            Box("Lunch tray", at + Vector3.up * .03f, new Vector3(.9f, .05f, .6f), teal, null, false);
        foreach (var eater in new[] { one, two, friend }) LunchOnTable(eater); // a plate and a glass on each tray
        story.table = Pivot("Join the table", nearSeat - along * spread + Vector3.up * 1.2f);
        story.tableView = Pivot("Table view", nearSeat - along * spread + Vector3.up * 2.3f + Flat(nearSeat - top).normalized * .5f);
        story.tableView.LookAt(top + Vector3.up * 1.2f);

        int index = 0;
        foreach (var other in tables.Skip(1).Take(3))
            foreach (var seat in benchesAll.Where(r => Vector2.Distance(XZ(r.bounds.center), XZ(other.bounds.center)) < 4f).Take(2))
            {
                var bs = seat.bounds; Vector3 at = new Vector3(bs.center.x, bs.max.y, bs.center.z);
                LunchOnTable(Person("Student at lunch " + (++index), NextStudent(), at, Quaternion.LookRotation(Flat(other.bounds.center - at)), true, LunchClip));
            }
        var counter = Parts(school, "Cafeteria | ", "Serving_Back_Panel").FirstOrDefault();
        if (counter != null)
        {
            // Chef Rosa stands between the serving counter and its back panel, facing the room.
            Vector3 toRoom = Flat(center - counter.bounds.center).normalized;
            Vector3 post = ClearSpot(new Vector3(counter.bounds.center.x, 0, counter.bounds.center.z) + toRoom * 1.2f, .2f, inDoor, 0, 1.5f, cafFloor.max.y);
            Person("Cook · Rosa", CookModel, post, Quaternion.LookRotation(toRoom), false, "Idle", AdultHeight);
        }
        if (counter != null) WorldLabel("CAFETERIA", counter.bounds.center + Vector3.up * 1.6f - Flat(counter.bounds.center - center).normalized * .2f, Quaternion.LookRotation(Flat(counter.bounds.center - center)), 6, 1.2f, 6, gold.color, null);
        camera.transform.SetPositionAndRotation(story.spawn.position + Vector3.up * 5 - story.spawn.forward * 5, Quaternion.LookRotation(story.spawn.forward));
        Save(scene, 3);
    }

    // ================================================================ Episode 4 · Dormitory

    public static void BuildDormitory()
    {
        var scene = NewSceneWithPlayer(4, out var player, out var camera);
        var story = root.gameObject.AddComponent<DormitoryEpisode>();
        story.player = player; story.storyCamera = camera;
        var school = PlaceSchool();
        // Evening: a low warm sun and a cooler room, so the desk lamp carries the scene.
        Lighting(new Color(1, .74f, .52f), .55f, new Color(.36f, .4f, .5f), new Color(.2f, .24f, .32f));
        Bounds dormFloor = Part(school, "Dormitory | Floor"), dormHeader = Part(school, "Dormitory | Front_Door_Header");
        Vector3 center = dormFloor.center;
        PointLight("Ceiling light", new Vector3(center.x, 5.2f, center.z), new Color(1, .9f, .75f), 1.1f, 18);
        story.spawn = DoorSpawn(dormHeader, center, dormFloor.max.y);
        player.SetPositionAndRotation(story.spawn.position, story.spawn.rotation);

        var deskTop = Part(school, "Dormitory | Detail_Study_Table_Top");
        var stool = Parts(school, "Dormitory | ", "Detail_Study_Stool").OrderBy(r => Vector3.Distance(r.bounds.center, story.spawn.position)).First().bounds;
        Vector3 top = new Vector3(deskTop.center.x, deskTop.max.y, deskTop.center.z);
        Vector3 toDesk = Flat(top - stool.center).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, toDesk);
        story.desk = Pivot("Desk", new Vector3(stool.center.x, stool.max.y + 1.2f, stool.center.z));
        story.deskView = Pivot("Desk view", new Vector3(stool.center.x, stool.max.y + 2.2f, stool.center.z) - toDesk * .3f);
        story.deskView.LookAt(top);

        float reach = Mathf.Max(deskTop.size.x, deskTop.size.z) * .25f, wide = Mathf.Min(deskTop.size.x, deskTop.size.z) * .3f;
        // Spread over the desk (the collectible models are bigger than the old boxes): laptop in front of the seat,
        // phone to its right at the near edge, notebook and project further back, lamp in the far corner.
        story.laptop = Pivot("Laptop", top - toDesk * reach * 1.15f - side * wide * .12f);
        Box("Laptop base", story.laptop.position + Vector3.up * .02f, new Vector3(.8f, .04f, .55f), ink, story.laptop, false).transform.rotation = Quaternion.LookRotation(toDesk);
        var lid = Box("Laptop lid", story.laptop.position + Vector3.up * .28f + toDesk * .27f, new Vector3(.8f, .5f, .035f), ink, story.laptop, false);
        lid.transform.rotation = Quaternion.LookRotation(toDesk) * Quaternion.Euler(12, 0, 0);
        var lit = Box("Laptop screen", story.laptop.position + Vector3.up * .28f + toDesk * .25f, new Vector3(.72f, .42f, .01f), glow, story.laptop, false);
        lit.transform.rotation = lid.transform.rotation;
        story.notebook = Pivot("Notebook", top - toDesk * reach * .08f + side * wide * .75f);
        Box("Notebook cover", story.notebook.position + Vector3.up * .025f, new Vector3(.55f, .05f, .75f), teal, story.notebook, false).transform.rotation = Quaternion.LookRotation(toDesk) * Quaternion.Euler(0, 12, 0);
        story.phone = Pivot("Phone", top - toDesk * reach * 1.55f + side * wide * 1.15f);
        Box("Phone body", story.phone.position + Vector3.up * .02f, new Vector3(.22f, .035f, .42f), ink, story.phone, false).transform.rotation = Quaternion.LookRotation(toDesk) * Quaternion.Euler(0, -20, 0);
        story.unfinishedProject = Pivot("Unfinished project", top + toDesk * reach * .15f - side * wide * .75f);
        for (int i = 0; i < 4; i++)
            Box("Draft page", story.unfinishedProject.position + Vector3.up * (.012f + i * .012f), new Vector3(.6f, .01f, .8f), paper, story.unfinishedProject, false)
                .transform.rotation = Quaternion.LookRotation(toDesk) * Quaternion.Euler(0, i * 7 - 10, 0);
        WorldLabel("RESEARCH PROJECT\n(draft)", story.unfinishedProject.position + Vector3.up * .07f, Quaternion.LookRotation(Vector3.down, toDesk), .55f, .4f, .9f, ink.color, story.unfinishedProject);

        var lamp = Pivot("Desk lamp", top + toDesk * reach * 1.6f + side * wide * 1.15f);
        Box("Lamp base", lamp.position + Vector3.up * .04f, new Vector3(.36f, .08f, .36f), gold, lamp, false);
        Box("Lamp stem", lamp.position + Vector3.up * .45f, new Vector3(.06f, .8f, .06f), gold, lamp, false);
        Box("Lamp shade", lamp.position + Vector3.up * .9f, new Vector3(.5f, .2f, .4f), teal, lamp, false);
        story.deskLamp = PointLight("Desk lamp light", lamp.position + Vector3.up * .75f, new Color(1, .78f, .45f), 1.2f, 7);

        camera.transform.SetPositionAndRotation(story.spawn.position + Vector3.up * 4.5f, Quaternion.LookRotation(top - story.spawn.position));
        AddDormWalk(story, school);   // start outside the cafeteria and walk to the dorm; roommates at the table
        Save(scene, 4);
    }

    // ================================================================ scene helpers

    // A new scene holding a copy of the Chapter 2 classroom player and camera (same model and controller).
    static Scene NewSceneWithPlayer(int episode, out Transform player, out Camera camera)
    {
        string source = PrototypePath(3);
        if (!File.Exists(source)) throw new FileNotFoundException("Source scene missing.", source);
        var src = EditorSceneManager.OpenScene(source, OpenSceneMode.Single);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
        var p = Object.Instantiate(src.GetRootGameObjects().First(x => x.name == "Player")); p.name = "Player"; SceneManager.MoveGameObjectToScene(p, scene);
        var c = Object.Instantiate(src.GetRootGameObjects().First(x => x.name == "Main Camera")); c.name = "Main Camera"; SceneManager.MoveGameObjectToScene(c, scene);
        EditorSceneManager.CloseScene(src, true);
        Materials();
        root = new GameObject("Chapter 1 · Episode " + episode + " - " + AcademicPressureChapter.Locations[episode - 1].Substring(0, 1) + AcademicPressureChapter.Locations[episode - 1].Substring(1).ToLowerInvariant()).transform;
        player = p.transform; camera = c.GetComponent<Camera>();
        SyncPlayerMaterials(player);
        var movement = p.GetComponent<PlayerMovement>(); movement.cameraTransform = c.transform; movement.enabled = true;
        movement.cameraOffset = new Vector3(0, .45f, -3.5f); movement.cameraTargetHeight = 2.1f; movement.minDistance = 1.5f; movement.maxDistance = 5;
        ScalePlayer(p.transform);
        p.GetComponent<PlayerControls>().enabled = false;
        MinigameUI.EnsureInput();
        return scene;
    }

    static GameObject PlaceModel(string path, string name)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) throw new FileNotFoundException("Model missing.", path);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(asset); model.transform.SetParent(root); model.name = name;
        model.transform.localScale = Vector3.one * 2; // matches the classroom scale used by the player
        foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
            if (filter.sharedMesh != null && filter.GetComponent<Collider>() == null) filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
        Physics.SyncTransforms();
        return model;
    }

    // ---------------------------------------------------------------- shared school grounds model
    // Episodes 2-4 share one model (Blender Files/school-grounds.blend): street and bus stop, the gate, the yard,
    // Rooms 01-04 (Room 03 furnished) on a covered corridor, the cafeteria, the dormitory, a library and a hall.
    const string SchoolModel = "Assets/Models/School_Grounds.glb";

    static GameObject PlaceSchool()
    {
        var school = PlaceModel(SchoolModel, "School grounds");
        foreach (var r in school.GetComponentsInChildren<Renderer>(true))
        {
            // The cafeteria and dormitory doorways stand open; roofs cast no shadow so rooms stay lit.
            if (r.name.EndsWith("| Front_Door")) r.gameObject.SetActive(false);
            else if (r.name.Contains("Roof") || r.name.Contains("canopy")) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        ClassroomDoors(school);
        Physics.SyncTransforms();
        var labels = Pivot("School signs", Vector3.zero);
        Bounds gate = Part(school, "School gate | pillars and arch"), road = Part(school, "Street | road");
        Vector3 toStreet = Axis(road.center - gate.center);
        WorldLabel("SCHOOL", new Vector3(gate.center.x, gate.max.y - .75f, gate.center.z) + toStreet * .6f, Quaternion.LookRotation(-toStreet), 7, 1.2f, 7, cream.color, labels);
        var shelter = Part(school, "Bus stop | shelter");
        Vector3 signAt = new Vector3(Mathf.Abs(shelter.min.x - gate.center.x) > Mathf.Abs(shelter.max.x - gate.center.x) ? shelter.min.x + .9f : shelter.max.x - .9f, shelter.min.y + 3.85f, shelter.center.z);
        signAt = new Vector3(signAt.x, signAt.y, (toStreet.z > 0 ? shelter.max.z : shelter.min.z) + toStreet.z * .03f);   // street side of the sign board
        WorldLabel("BUS\n02 / SCHOOL", signAt, Quaternion.LookRotation(-toStreet), 1.6f, .95f, 3, gold.color, labels);
        foreach (var header in school.GetComponentsInChildren<Renderer>(true).Where(r => r.name.EndsWith("| Door_Header") || r.name.EndsWith("| Front_Door_Header")))
        {
            string building = header.name.Substring(0, header.name.IndexOf(" |"));
            var floor = school.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == building + " | Floor");
            if (floor == null) continue;
            Vector3 outward = Axis(header.bounds.center - floor.bounds.center);
            string words = building == "Room 03" ? "03 / RESEARCH" : building.StartsWith("Room ") ? building.Substring(5) : building.ToUpperInvariant();
            WorldLabel(words, header.bounds.center + outward * .2f + Vector3.down * .1f, Quaternion.LookRotation(-outward), 3, .7f, 2.6f, ink.color, labels);
        }
        foreach (var shell in new[] { "Library | Shell", "Hall | Shell" })
        {
            // The Blender campus now replaces the placeholder hall with the furnished Reunion Hall.
            var part = school.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == shell);
            if (part == null && shell == "Hall | Shell")
                part = school.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == "Reunion Hall | Architecture");
            if (part == null) continue;
            var b = part.bounds; Vector3 toCorridor = Axis(Part(school, "Corridor | floor").center - b.center);
            Vector3 face = b.center + Vector3.Scale(toCorridor, b.extents) + toCorridor * .3f;
            WorldLabel(shell.Substring(0, shell.IndexOf(" |")).ToUpperInvariant(), new Vector3(face.x, b.min.y + 5.45f, face.z), Quaternion.LookRotation(-toCorridor), 2.4f, .5f, 2.2f, cream.color, labels);
        }
        return school;
    }

    // Every classroom has a closed door. The model only has one, in Room 03 (handle on the room side), so it is copied
    // into the other rooms' doorways, and each door gets a second handle facing the corridor. The story's own
    // "Walk into your classroom" spot takes you through Room 03's door; the other rooms stay shut.
    public static void ClassroomDoors(GameObject school)
    {
        var parts = school.GetComponentsInChildren<Renderer>(true);
        var door = parts.FirstOrDefault(r => r.name == "Room 03 | Door"); var handle = parts.FirstOrDefault(r => r.name == "Room 03 | Door_Handle");
        var header = parts.FirstOrDefault(r => r.name == "Room 03 | Door_Header");
        if (door == null || handle == null || header == null) return;
        door.gameObject.SetActive(true); handle.gameObject.SetActive(true);
        foreach (var other in parts.Where(r => r.name.StartsWith("Room ") && r.name.EndsWith("| Door_Header")))
        {
            string room = other.name.Substring(0, other.name.IndexOf(" |"));
            Vector3 offset = other.bounds.center - header.bounds.center;
            var roomDoor = school.transform.Find(room + " | Door")?.GetComponent<Renderer>();
            if (roomDoor == null) roomDoor = Copy(door, room + " | Door", offset);
            if (school.transform.Find(room + " | Door_Handle") == null) Copy(handle, room + " | Door_Handle", offset);
            if (school.transform.Find(room + " | Door_Handle (corridor)") == null)
            {
                // Mirror the room-side handle through the middle of the door panel.
                var corridorHandle = Copy(handle, room + " | Door_Handle (corridor)", offset);
                Vector3 pivot = roomDoor.bounds.center, normal = Axis(other.bounds.size.x < other.bounds.size.z ? Vector3.right : Vector3.forward);
                Vector3 p = corridorHandle.transform.position; float d = Vector3.Dot(p - pivot, normal);
                corridorHandle.transform.SetPositionAndRotation(p - 2 * d * normal, Quaternion.AngleAxis(180, Vector3.up) * corridorHandle.transform.rotation);
            }
        }

        Renderer Copy(Renderer source, string name, Vector3 offset)
        {
            var copy = Object.Instantiate(source.gameObject, source.transform.parent);
            copy.name = name; copy.SetActive(true);
            copy.transform.SetPositionAndRotation(source.transform.position + offset, source.transform.rotation);
            return copy.GetComponent<Renderer>();
        }
    }

    static Bounds Part(GameObject model, string name) => model.GetComponentsInChildren<Renderer>(true).First(r => r.name == name).bounds;
    static IEnumerable<Renderer> Parts(GameObject model, string prefix, string contains) =>
        model.GetComponentsInChildren<Renderer>(true).Where(r => r.name.StartsWith(prefix) && r.name.Contains(contains) && r.gameObject.activeInHierarchy);
    // The main horizontal axis a direction points along (walls and doors are axis-aligned).
    static Vector3 Axis(Vector3 v) => Mathf.Abs(v.x) > Mathf.Abs(v.z) ? new Vector3(Mathf.Sign(v.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(v.z));

    // Gold floor tiles every few metres along a path of points (heights follow the ground).
    static GameObject Route(string name, params Vector3[] points)
    {
        var group = Pivot(name, Vector3.zero);
        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector3 a = points[i], b = points[i + 1]; float length = Vector2.Distance(XZ(a), XZ(b));
            int steps = Mathf.Max(1, Mathf.RoundToInt(length / 4.5f));
            for (int k = (i == 0 ? 1 : 0); k <= steps; k++)
            {
                if (i < points.Length - 2 && k == steps) continue; // the next segment starts here
                Vector3 at = Vector3.Lerp(a, b, k / (float)steps);
                Vector3 ground = Surface(at.x, at.z, 0, 3);
                RouteMarker(ground, group.transform);
            }
        }
        return group.gameObject;
    }

    static Transform DoorSpawn(Bounds header, Vector3 center, float floorY)
    {
        Vector3 inward = -Axis(header.center - center);
        var spawn = Pivot("Spawn inside door", new Vector3(header.center.x, floorY + .1f, header.center.z) + inward * 2.6f);
        spawn.rotation = Quaternion.LookRotation(inward);
        return spawn;
    }

    static void Lighting(Color sunColor, float sunIntensity, Color ambient, Color background)
    {
        var sun = new GameObject("Directional Light", typeof(Light)).GetComponent<Light>();
        sun.type = LightType.Directional; sun.color = sunColor; sun.intensity = sunIntensity; sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(48, -32, 0);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = ambient;
        RenderSettings.fog = false;
        var cam = Object.FindAnyObjectByType<Camera>();
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = background; cam.farClipPlane = 150; }
    }

    static Light PointLight(string name, Vector3 at, Color color, float intensity, float range)
    {
        var light = new GameObject(name, typeof(Light)).GetComponent<Light>(); light.transform.SetParent(root); light.transform.position = at;
        light.type = LightType.Point; light.color = color; light.intensity = intensity; light.range = range; return light;
    }

    static void Save(Scene scene, int episode)
    {
        CollectibleModels.Apply(scene);
        WorldModelSync.Apply(scene);
        AmbientLifeBuilder.Apply(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath(episode));
        Debug.Log("Chapter 1 · Episode " + episode + " built: " + ScenePath(episode));
    }

    // ================================================================ characters

    // The rigged cast in Assets/Characters/NPCs (one FBX per person, each with its own Idle / Seated_Idle /
    // Conversation clips). Sam keeps one face across episodes; the teacher and the cook have their own models.
    // Amara, David and Tariq are Mom, Dad and Tim (FamilyModels), so they are not students.
    const string CastFolder = "Assets/Characters/NPCs/", AnimatorFolder = "Assets/AcademicPressure/Animators";
    const string SamModel = "NPC_15_Kai", TeacherModel = "NPC_18_Henderson", CookModel = "NPC_19_Chef_Rosa";
    static readonly string[] StudentModels =
    {
        "NPC_01_Maya", "NPC_03_Leo", "NPC_02_Nia", "NPC_06_Ethan", "NPC_04_Hana", "NPC_09_Julian", "NPC_05_Chloe", "NPC_07_Malik",
        "NPC_08_Zara", "NPC_12_Noah", "NPC_10_Olivia", "NPC_14_Mei", "NPC_17_Penelope"
    };
    // Heights relative to the player rig (2.54 m before PeopleScale): classmates a touch shorter, adults a touch taller.
    const float StudentHeight = 2.45f * PeopleScale, AdultHeight = 2.62f * PeopleScale;
    static int nextStudent;
    // Seated classmates write in class (Episode 2) and eat at lunch (Episode 3); every cast FBX has both clips.
    const string ClassroomClip = "Sitting_Writing", LunchClip = "Sitting_Eating";

    static string NextStudent() => StudentModels[nextStudent++ % StudentModels.Length];

    // Imported clips do not loop by default; loop everything except the one-shot sit-down.
    static void PrepareCast()
    {
        nextStudent = 0;
        foreach (var model in StudentModels.Concat(new[] { SamModel, TeacherModel, CookModel }).Select(m => CastFolder + m + ".fbx").Concat(FamilyModels))
        {
            var importer = AssetImporter.GetAtPath(model) as ModelImporter;
            if (importer == null || importer.clipAnimations.Length > 0) continue;
            importer.clipAnimations = importer.defaultClipAnimations.Select(c => { c.loopTime = !c.name.Contains("Sit_Down"); return c; }).ToArray();
            importer.SaveAndReimport();
        }
    }

    // A cast member at 'at' facing 'facing'. Standing: feet on 'at'. Seated: hips on the seat top at 'at'.
    // The clip is sampled into the saved pose so the scene reads right in the editor, and an Animator loops it in play.
    static Transform Person(string name, string model, Vector3 at, Quaternion facing, bool seated, string clip = null, float height = StudentHeight)
    {
        string path = model.Contains("/") ? model : CastFolder + model + ".fbx";   // the cast by name, or any model by path
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) throw new FileNotFoundException("Character model missing.", path);
        var p = Pivot(name, at); p.rotation = facing;
        var body = (GameObject)PrefabUtility.InstantiatePrefab(asset, p); body.name = "Model";
        body.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        var rest = BoundsOf(body); body.transform.localScale *= height / Mathf.Max(.01f, rest.size.y);
        Physics.SyncTransforms(); rest = BoundsOf(body);
        var hips = body.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.Equals("hips", StringComparison.OrdinalIgnoreCase));
        float hipsAboveFeet = hips != null ? hips.position.y - rest.min.y : height * .5f;

        clip ??= seated ? "Seated_Idle" : "Idle";
        var motion = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__") && (c.name == clip || c.name.EndsWith("|" + clip)));
        if (motion != null)
        {
            motion.SampleAnimation(body, 0);
            var animator = body.GetComponent<Animator>(); if (animator == null) animator = body.AddComponent<Animator>();
            animator.runtimeAnimatorController = Controller(Path.GetFileNameWithoutExtension(path), clip, motion); animator.applyRootMotion = false;
        }
        else Debug.LogWarning(path + " has no '" + clip + "' clip; it keeps its rest pose.");

        // Snap by the hips bone: skinned bounds do not follow a sampled pose in the editor.
        if (hips != null)
        {
            Vector3 want = seated ? at + Vector3.up * height * .05f - p.forward * height * .03f : at + Vector3.up * hipsAboveFeet;
            body.transform.position += new Vector3(want.x - hips.position.x, want.y - hips.position.y, want.z - hips.position.z);
        }
        else body.transform.position += at - new Vector3(rest.center.x, rest.min.y, rest.center.z);

        foreach (var c in body.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        if (!seated) { var block = p.gameObject.AddComponent<BoxCollider>(); block.center = new Vector3(0, height * .5f, 0); block.size = new Vector3(1.1f, height, .7f); }
        return p;
    }

    static RuntimeAnimatorController Controller(string model, string clip, AnimationClip motion)
    {
        if (!AssetDatabase.IsValidFolder(AnimatorFolder)) AssetDatabase.CreateFolder("Assets/AcademicPressure", "Animators");
        string path = AnimatorFolder + "/" + model + " " + clip + ".controller";
        var existing = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(path);
        return existing != null ? existing : UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPathWithClip(path, motion);
    }

    static Transform MakeBag(string name, Vector3 at)
    {
        var p = Pivot(name, at);
        Box("Canvas", at, new Vector3(.85f, 1.2f, .48f), teal, p, false); Box("Pocket", at + new Vector3(0, -.1f, -.28f), new Vector3(.66f, .45f, .13f), ink, p, false);
        Box("Handle", at + Vector3.up * .68f, new Vector3(.35f, .1f, .14f), gold, p, false); return p;
    }

    static IEnumerable<Renderer> Named(GameObject model, string contains) =>
        model.GetComponentsInChildren<Renderer>(true).Where(r => r.name.Contains(contains));

    // Clear floor for a standing person near 'around' (rings outward from 'radius'), kept away from 'avoid'.
    static Vector3 ClearSpot(Vector3 around, float radius, Vector3 avoid, float avoidRadius, float castFrom, float floorY)
    {
        Vector3 fallback = new Vector3(around.x, floorY, around.z);
        for (int ring = 0; ring < 4; ring++)
            for (int i = 0; i < 16; i++)
            {
                float r = radius + ring * .8f, a = i * Mathf.PI / 8;
                Vector3 c = around + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                if (Vector2.Distance(XZ(c), XZ(avoid)) < avoidRadius) continue;
                if (!Physics.Raycast(new Vector3(c.x, castFrom, c.z), Vector3.down, out var hit, castFrom + 4, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (Mathf.Abs(hit.point.y - floorY) > .35f) continue; // landed on furniture, not the floor
                if (Physics.CheckCapsule(hit.point + Vector3.up * .55f, hit.point + Vector3.up * 3f, .55f, ~0, QueryTriggerInteraction.Ignore)) continue;
                return hit.point;
            }
        Debug.LogWarning("No clear floor found near " + around + "; using " + fallback);
        return fallback;
    }

    // The first surface below (x, z), falling back to a known height when nothing is hit.
    static Vector3 Surface(float x, float z, float fallbackY, float from = 6)
    {
        if (Physics.Raycast(new Vector3(x, from, z), Vector3.down, out var hit, from + 4, ~0, QueryTriggerInteraction.Ignore)) return hit.point;
        return new Vector3(x, fallbackY, z);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude < 1e-4f ? Vector3.forward : v; }
    static Vector2 XZ(Vector3 v) => new Vector2(v.x, v.z);

    static Bounds BoundsOf(GameObject obj)
    {
        var renderers = obj.GetComponentsInChildren<Renderer>(); var b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds); return b;
    }

    static Transform Pivot(string name, Vector3 at) { var t = new GameObject(name).transform; t.SetParent(root); t.position = at; return t; }

    static GameObject Box(string name, Vector3 at, Vector3 size, Material m, Transform parent = null, bool collider = true)
    {
        var o = GameObject.CreatePrimitive(PrimitiveType.Cube); o.name = name; o.transform.SetParent(parent == null ? root : parent);
        o.transform.position = at; o.transform.localScale = size; o.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(o.GetComponent<Collider>()); return o;
    }

    static GameObject Cylinder(string name, Vector3 at, Vector3 size, Material m, Transform parent)
    {
        var o = GameObject.CreatePrimitive(PrimitiveType.Cylinder); o.name = name; Object.DestroyImmediate(o.GetComponent<Collider>());
        o.transform.SetParent(parent == null ? root : parent); o.transform.position = at; o.transform.localScale = size; o.GetComponent<Renderer>().sharedMaterial = m; return o;
    }

    static void WorldLabel(string words, Vector3 at, Quaternion rotation, float width, float height, float size, Color color, Transform parent)
    {
        var o = new GameObject(words.Split('\n')[0], typeof(TextMeshPro)); o.transform.SetParent(parent == null ? root : parent);
        o.transform.SetPositionAndRotation(at, rotation);
        var t = o.GetComponent<TextMeshPro>(); t.font = MinigameUI.Font(false); t.text = words; t.fontSize = size; t.alignment = TextAlignmentOptions.Center; t.color = color;
        t.rectTransform.sizeDelta = new Vector2(width, height);
    }

    // ---------------------------------------------------------------- school bus

    // Assets/Prefabs/SchoolBus.prefab (Blender Files/school-bus.blend, built by Mindscape > Vehicles): its
    // SchoolBusAnimator rolls the wheels from the bus's movement and adds engine idle and suspension sway.
    // The model faces -X with its door on the +Z side, 3.38 m ahead of the pivot. The stories drive buses toward +X,
    // so it is turned around (front toward +X, door on the -Z kerb side) and scaled with the people.
    const string SchoolBusPrefab = "Assets/Prefabs/SchoolBus.prefab";
    const float BusDoorAhead = 3.38f, BusKerbGap = .35f;

    // A linked school bus whose door is at doorX, its -Z side just off the kerb line kerbZ, wheels on roadY.
    static Transform PlaceSchoolBus(string name, Transform parent, float doorX, float kerbZ, float roadY)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(SchoolBusPrefab);
        if (asset == null) throw new FileNotFoundException("School bus prefab missing (Mindscape > Vehicles > Build School Bus Prefabs).", SchoolBusPrefab);
        var bus = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        bus.name = name;
        bus.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0, 180, 0));
        bus.transform.localScale = Vector3.one * PeopleScale;
        var b = BoundsOf(bus);
        // Door ahead of the pivot along the travel direction (+X); the bus's -Z side sits a little off the kerb.
        bus.transform.position = new Vector3(doorX - BusDoorAhead * PeopleScale, roadY - b.min.y, kerbZ + BusKerbGap + (0 - b.min.z));
        return bus.transform;
    }

    // Drops a vehicle so its lowest point rests on whatever is below its middle (its own colliders ignored).
    static void SeatOnGround(Transform vehicle)
    {
        if (vehicle == null) return;
        bool was = vehicle.gameObject.activeSelf; vehicle.gameObject.SetActive(true); Physics.SyncTransforms();
        var b = BoundsOf(vehicle.gameObject);
        var hit = Physics.RaycastAll(new Vector3(b.center.x, b.max.y + 20, b.center.z), Vector3.down, b.size.y + 60, ~0, QueryTriggerInteraction.Ignore)
            .Where(h => !h.collider.transform.IsChildOf(vehicle)).OrderBy(h => h.distance).FirstOrDefault();
        if (hit.collider != null) vehicle.position += Vector3.up * (hit.point.y - b.min.y);
        vehicle.gameObject.SetActive(was);
    }

    // ---------------------------------------------------------------- route markers

    // Round, glowing blue route markers: an emissive core disc (bloom picks it up) over a wider, soft translucent
    // halo, so they read as glowing even where there is no post-processing.
    static Material routeCore, routeHalo;

    static GameObject RouteMarker(Vector3 ground, Transform parent)
    {
        if (routeCore == null || routeHalo == null) RouteMaterials();
        var marker = new GameObject("Route marker"); marker.transform.SetParent(parent, false); marker.transform.position = ground;
        var halo = Cylinder("Glow", ground + Vector3.up * .03f, new Vector3(1.6f, .004f, 1.6f), routeHalo, marker.transform);
        var core = Cylinder("Core", ground + Vector3.up * .045f, new Vector3(.85f, .012f, .85f), routeCore, marker.transform);
        foreach (var r in new[] { halo.GetComponent<Renderer>(), core.GetComponent<Renderer>() })
        { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
        return marker;
    }

    static void RouteMaterials()
    {
        routeCore = Mat("Route glow", new Color32(80, 175, 255, 255));
        routeCore.EnableKeyword("_EMISSION"); routeCore.SetColor("_EmissionColor", new Color(.31f, .69f, 1f) * 4.5f);
        routeCore.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; EditorUtility.SetDirty(routeCore);
        routeHalo = Mat("Route halo", new Color(.35f, .72f, 1f, .5f), true);
        routeHalo.SetFloat("_Surface", 1); routeHalo.SetFloat("_Blend", 0); routeHalo.SetOverrideTag("RenderType", "Transparent");
        routeHalo.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        routeHalo.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);   // additive: brightens what is under it
        routeHalo.SetFloat("_ZWrite", 0); routeHalo.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        routeHalo.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent; EditorUtility.SetDirty(routeHalo);
    }

    // Episode 1's route comes with the Chapter 2 street (diamond gold tiles); give it the same round blue markers.
    static void RestyleRoute(GameObject group)
    {
        if (group == null) return;
        foreach (var tile in group.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Gold route tile").ToArray())
        {
            // The group starts hidden, so read the tile's own transform rather than its (inactive) renderer bounds.
            RouteMarker(tile.position - Vector3.up * (tile.lossyScale.y * .5f + .03f), group.transform);
            Object.DestroyImmediate(tile.gameObject);
        }
    }

    static Material Mat(string name, Color color, bool unlit = false)
    {
        string path = MaterialFolder + "/" + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader; m.color = color; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .12f); EditorUtility.SetDirty(m); return m;
    }
}
