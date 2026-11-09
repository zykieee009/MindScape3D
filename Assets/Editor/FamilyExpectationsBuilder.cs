using System;
using System.IO;
using System.Linq;
using Mindscape.AcademicPressure;
using Mindscape.FamilyExpectations;
using Mindscape.FamilyFinale;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Builds Chapter 2 · Family Expectations on Chapter 1's scenes, so both chapters share one house, one school, one
// cast, one player rig and one interface; only the stories and tasks differ.
//   Episodes 1, 2, 4, 5: a copy of Chapter 1 - Episode 1 (house, street, bus stop, street life) with its story removed.
//   Episode 3: a copy of Chapter 1 - Episode 2 (school grounds, Room 03, classmates, teacher).
// The original Chapter 2 scenes are kept as prototypes in Assets/Scenes/Prototypes: Chapter 1 builds its house,
// its player rig and Chapter 3's home from them, exactly as before.
public static partial class AcademicPressureBuilder
{
    const string FamilyMenu = "Mindscape/Chapter 2 - Family Expectations/";
    static readonly string[] PrototypeRoots = { "Episode 1 - Family Expectations", "Episode 2 - Before the Bell", "Episode 3 - Running Out of Time", "Episode 4 - Family Finale", "Episode 5 - Family Finale" };
    // Mom, Dad and Tim come from the NPC cast (each has Idle, Walk, Run, Seated_Idle, Sitting_Eating...). These three
    // models belong to the family only: they are left out of the student and walker casts.
    public static readonly string[] FamilyModels = { CastFolder + "NPC_11_Amara.fbx", CastFolder + "NPC_13_David.fbx", CastFolder + "NPC_16_Tariq.fbx" };
    // The original family set, replaced in saved scenes by RecastFamily.
    static readonly string[] OldFamilyModels = { "Assets/FamilySet/Mother.fbx", "Assets/FamilySet/Father.fbx", "Assets/FamilySet/Boy.fbx" };
    const float ChildHeight = 2.05f * PeopleScale;

    // Fixed spots in the house (world units; the house model is shared by every house scene).
    static readonly Vector2 DeskPaper = new Vector2(-10.7f, -9.3f), DeskNotebook = new Vector2(-10.73f, -10.45f);
    static readonly Vector3 BedroomStart = new Vector3(-15.4f, .65f, -9.8f);

    public static string PrototypePath(int episode) => "Assets/Scenes/Prototypes/Chapter 2 - Episode " + episode + " (prototype).unity";

    // Snapshots the original Chapter 2 scenes once, before anything replaces them.
    public static void EnsurePrototypes()
    {
        for (int n = 1; n <= 5; n++)
        {
            string proto = PrototypePath(n), current = FamilyPath(n);
            if (File.Exists(proto) || !File.Exists(current) || !File.ReadAllText(current).Contains(PrototypeRoots[n - 1])) continue;
            if (!AssetDatabase.IsValidFolder("Assets/Scenes/Prototypes")) AssetDatabase.CreateFolder("Assets/Scenes", "Prototypes");
            if (!AssetDatabase.CopyAsset(current, proto)) throw new InvalidOperationException("Could not snapshot " + current);
            Debug.Log("Chapter 2 prototype kept: " + proto);
        }
    }

    // ================================================================ menu

    [MenuItem(FamilyMenu + "Build All Episodes", priority = 0)]
    public static void BuildFamilyAll()
    {
        if (!Prepare()) return;
        BuildAfterSchool(); BuildMorning(); BuildExamination(); BuildFinale(4); BuildFinale(5);
        RegisterScenes();
        EditorSceneManager.OpenScene(FamilyPath(1));
        Debug.Log("Chapter 2 · Family Expectations built on Chapter 1's scenes: 5 episodes.");
    }

    [MenuItem(FamilyMenu + "Build Episode 1 - After School", priority = 20)] public static void MenuAfterSchool() => BuildFamilyOne(BuildAfterSchool, 1);
    [MenuItem(FamilyMenu + "Build Episode 2 - Before the Bell", priority = 21)] public static void MenuMorning() => BuildFamilyOne(BuildMorning, 2);
    [MenuItem(FamilyMenu + "Build Episode 3 - Running Out of Time", priority = 22)] public static void MenuExamination() => BuildFamilyOne(BuildExamination, 3);
    [MenuItem(FamilyMenu + "Build Episodes 4 and 5 - Evening", priority = 23)] public static void MenuFinale() => BuildFamilyOne(() => { BuildFinale(4); BuildFinale(5); }, 4);

    static void BuildFamilyOne(Action build, int episode)
    {
        if (!Prepare()) return;
        build(); RegisterScenes();
        EditorSceneManager.OpenScene(FamilyPath(episode));
    }

    // ================================================================ shared house

    sealed class HouseKit
    {
        public Transform player, frontDoor, busStop, boarding, bus, schoolBag, bed;
        public Camera camera;
        public GameObject routeMarkers, carriedBag, messyBed, fixedBed;
        public Light deskLight, sun;
    }

    // Opens a copy of a Chapter 1 scene saved as the Chapter 2 episode.
    static Scene CopyToFamily(string source, int episode)
    {
        if (!File.Exists(source)) throw new FileNotFoundException("Build Chapter 1 first.", source);
        var scene = EditorSceneManager.OpenScene(source, OpenSceneMode.Single);
        EditorSceneManager.SaveScene(scene, FamilyPath(episode), true);
        AssetDatabase.ImportAsset(FamilyPath(episode));
        scene = EditorSceneManager.OpenScene(FamilyPath(episode), OpenSceneMode.Single);
        Materials(); PrepareCast();
        return scene;
    }

    // Chapter 1's house with its story taken out: what stays is the house, the street, the player rig, the light,
    // the study corner, the bed, the school bag, and (for the morning) the bus, the stop and the route.
    static HouseKit HouseBase(int episode, string name, bool street)
    {
        CopyToFamily(ScenePath(1), episode);
        // Chapter 1's walkers and family come along with the copy; this episode adds its own when it is saved.
        foreach (var life in SceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.name == "Ambient Life").ToArray()) Object.DestroyImmediate(life);
        var h = Object.FindAnyObjectByType<HouseEpisode>();
        if (h == null) throw new InvalidOperationException(ScenePath(1) + " has no HouseEpisode. Build Chapter 1 - Episode 1 first.");
        root = h.transform; root.name = name;
        var kit = new HouseKit
        {
            player = h.player, camera = h.storyCamera, frontDoor = h.frontDoor, busStop = h.busStop, bus = h.bus, schoolBag = h.schoolBag, bed = h.bed,
            routeMarkers = h.routeMarkers, carriedBag = h.carriedBag, messyBed = h.messyBed, fixedBed = h.fixedBed,
            boarding = root.Find("Board bus"), deskLight = root.Find("Evening desk light")?.GetComponent<Light>(),
            sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional)
        };
        foreach (var t in new[] { h.phone, h.laptop, h.books, h.plate, h.sink }) if (t != null) Object.DestroyImmediate(t.gameObject);
        if (h.carriedPlate != null) Object.DestroyImmediate(h.carriedPlate);
        Object.DestroyImmediate(h);
        if (!street)
        {
            foreach (var t in new[] { kit.bus, kit.busStop, kit.boarding, kit.frontDoor }) if (t != null) Object.DestroyImmediate(t.gameObject);
            if (kit.routeMarkers != null) Object.DestroyImmediate(kit.routeMarkers);
            if (kit.carriedBag != null) Object.DestroyImmediate(kit.carriedBag);
        }
        // Only the morning's bed is unmade.
        if (kit.messyBed != null) kit.messyBed.SetActive(street);
        if (kit.fixedBed != null) kit.fixedBed.SetActive(!street);
        return kit;
    }

    static void SaveFamily(int episode)
    {
        var scene = SceneManager.GetActiveScene();
        CollectibleModels.Apply(scene);
        WorldModelSync.Apply(scene);
        AmbientLifeBuilder.Apply(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, FamilyPath(episode));
        Debug.Log("Chapter 2 · Episode " + episode + " built: " + FamilyPath(episode));
    }

    static Transform Mark(string name, Vector3 at, float yaw) { var t = Pivot(name, at); t.rotation = Quaternion.Euler(0, yaw, 0); return t; }

    // The examination paper and the study notebook on the desk (the collectible models replace the boxes).
    static Transform DeskPaperProp(string name)
    {
        var p = Pivot(name, Surface(DeskPaper.x, DeskPaper.y, 2.39f, 2.9f));
        var sheet = Box("Examination paper", p.position + Vector3.up * .013f, new Vector3(.65f, .025f, .85f), paper, p, false);
        for (int i = 0; i < 6; i++) Box("Answer line", p.position + new Vector3(0, .028f, .25f - i * .08f), new Vector3(.48f, .005f, .01f), ink, sheet.transform, false);
        return p;
    }

    static Transform DeskNotebookProp()
    {
        var p = Pivot("Study notebook interaction", Surface(DeskNotebook.x, DeskNotebook.y, 2.39f, 2.9f));
        Box("Study notebook", p.position + Vector3.up * .06f, new Vector3(.72f, .12f, .95f), teal, p, false);
        return p;
    }

    static void Afternoon(Light sun) { if (sun == null) return; sun.color = new Color(1, .86f, .7f); sun.intensity = 1.1f; sun.transform.rotation = Quaternion.Euler(32, -35, 0); }

    static void Evening(Light sun, float intensity)
    {
        if (sun != null) { sun.color = new Color(1, .72f, .49f); sun.intensity = intensity; sun.transform.rotation = Quaternion.Euler(12, -35, 0); }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.32f, .35f, .43f);
        RenderSettings.fog = false;
    }

    // ================================================================ Episode 1 · The Paper on the Desk

    public static void BuildAfterSchool()
    {
        var kit = HouseBase(1, "Chapter 2 · Episode 1 - After School", false);
        var story = root.gameObject.AddComponent<FamilyAfterSchoolEpisode>();
        story.player = kit.player; story.storyCamera = kit.camera;
        story.messyBed = kit.messyBed; story.fixedBed = kit.fixedBed; story.deskLight = kit.deskLight; story.sun = kit.sun;
        story.paper = DeskPaperProp("Examination paper interaction");
        story.notebook = DeskNotebookProp();
        kit.schoolBag.name = "School bag memory interaction"; story.schoolBag = kit.schoolBag;

        // The bag memory: the ID and revision notes that were scattered this morning, on the floor by the bag.
        var fragments = Pivot("Morning search fragments", Vector3.zero);
        var id = Pivot("Student ID", Surface(-17.95f, -9.7f, .53f, 3)); id.SetParent(fragments, true);
        Box("ID card", id.position + Vector3.up * .012f, new Vector3(.38f, .02f, .55f), paper, id, false);
        var notes = Pivot("Dropped revision notes", Surface(-16.6f, -11.3f, .53f, 3)); notes.SetParent(fragments, true);
        Box("Notes", notes.position + Vector3.up * .012f, new Vector3(.62f, .02f, .78f), paper, notes, false);
        story.morningItems = fragments.gameObject;

        story.bedroomSpawn = Mark("Bedroom start", BedroomStart, 90);
        story.bedroomView = Pivot("Bedroom view", new Vector3(-13.4f, 4.4f, -11.9f)); story.bedroomView.LookAt(story.paper.position);
        story.outsideStart = Mark("Walking home", new Vector3(-22.1f, .7f, 15.5f), 180);
        story.outsideEnd = Pivot("Front step", new Vector3(-22.1f, .7f, 9.2f));
        story.examMemoryView = RememberedExamination();
        Afternoon(kit.sun);
        kit.player.SetPositionAndRotation(story.outsideStart.position, story.outsideStart.rotation);
        Vector3 opening = story.outsideStart.position + new Vector3(2.6f, 4.2f, 4.5f);
        kit.camera.transform.SetPositionAndRotation(opening, Quaternion.LookRotation(story.outsideEnd.position + Vector3.up * 2.2f - opening));
        SaveFamily(1);
    }

    // Room 03 as it was this morning, far from the house (beyond the camera's reach): classmates bent over their
    // papers, the teacher at the board, the clock moving.
    static Transform RememberedExamination()
    {
        var school = PlaceModel(SchoolModel, "Remembered classroom");
        school.transform.position = new Vector3(0, 0, -700);
        foreach (var r in school.GetComponentsInChildren<Renderer>(true))
            if (r.name.Contains("Roof") || r.name.Contains("canopy")) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ClassroomDoors(school);
        Physics.SyncTransforms();
        Bounds floor = Part(school, "Room 03 | Floor"), board = Part(school, "Room 03 | Chalkboard");
        Vector3 toBoard = Axis(board.center - floor.center), side = Vector3.Cross(Vector3.up, toBoard);
        foreach (var chair in Parts(school, "Room 03 | ", "Wood_Seat").OrderBy(r => Vector3.Distance(r.bounds.center, board.center)).Take(12))
        {
            var b = chair.bounds;
            Person("Classmate (memory)", NextStudent(), new Vector3(b.center.x, b.max.y, b.center.z), Quaternion.LookRotation(toBoard), true).SetParent(school.transform, true);
        }
        Physics.SyncTransforms();
        Vector3 stand = ClearSpot(new Vector3(board.center.x, 0, board.center.z) - toBoard * 2.4f + side * Mathf.Max(board.size.x, board.size.z) * .38f, .2f, floor.center, 0, 1.5f, floor.max.y);
        Person("Teacher (memory)", TeacherModel, stand, Quaternion.LookRotation(-toBoard), false, "Idle", AdultHeight).SetParent(school.transform, true);
        WorldLabel("EXAMINATION\n\n08:15", board.center - toBoard * .12f, Quaternion.LookRotation(toBoard), Mathf.Max(board.size.x, board.size.z) * .9f, board.size.y * .8f, 4.2f, cream.color, school.transform);
        PointLight("Remembered classroom light", new Vector3(floor.center.x, 5.2f, floor.center.z), new Color(1, .93f, .82f), 1.4f, 18).transform.SetParent(school.transform, true);
        float depth = Mathf.Abs(Vector3.Dot(floor.size, toBoard));
        var view = Pivot("Examination memory view", floor.center - toBoard * depth * .42f + Vector3.up * 4.4f);
        view.LookAt(board.center + Vector3.down * .4f);
        return view;
    }

    // ================================================================ Episode 2 · Before the Bell

    public static void BuildMorning()
    {
        var kit = HouseBase(2, "Chapter 2 · Episode 2 - Before the Bell", true);
        var story = root.gameObject.AddComponent<FamilyMorningEpisode>();
        story.player = kit.player; story.storyCamera = kit.camera;
        story.messyBed = kit.messyBed; story.fixedBed = kit.fixedBed; story.sun = kit.sun; story.deskLamp = kit.deskLight;
        story.frontDoor = kit.frontDoor; story.busStop = kit.busStop; story.boardingPoint = kit.boarding; story.bus = kit.bus;
        story.routeMarkers = kit.routeMarkers; story.carriedBag = kit.carriedBag;
        story.alarmClock = root.Find("Bedside alarm clock");
        foreach (var label in root.GetComponentsInChildren<TextMeshPro>(true)) if (label.text == "07:10") label.text = "07:50";

        var id = Pivot("Student ID", Surface(-13.62f, -13.95f, 2.09f, 2.75f));
        Box("ID card", id.position + Vector3.up * .012f, new Vector3(.38f, .02f, .55f), paper, id, false);
        var materials = Pivot("Examination materials", Surface(DeskPaper.x, DeskPaper.y, 2.39f, 2.9f));
        Box("Review sheets", materials.position + Vector3.up * .035f, new Vector3(.7f, .07f, .9f), cream, materials, false);
        // The school bag waits in the living room, by the front door.
        if (kit.schoolBag != null) kit.schoolBag.position += new Vector3(-24.4f - kit.schoolBag.position.x, 0, 4.3f - kit.schoolBag.position.z);
        story.items = new[] { id, materials, kit.schoolBag };

        story.studyView = Pivot("Night before view", new Vector3(-12.9f, 4.3f, -8.1f)); story.studyView.LookAt(new Vector3(-10.7f, 2.4f, -9.7f));
        story.outsideSpawn = new Vector3(-22.1f, .7f, 8.2f); story.outsideYaw = 0;
        Physics.SyncTransforms();
        var start = ClearSpot(new Vector3(-14.5f, 0, -11.4f), 0, Vector3.zero, 0, 3, .53f);
        kit.player.SetPositionAndRotation(start + Vector3.up * .12f, Quaternion.Euler(0, 160, 0));
        kit.camera.transform.SetPositionAndRotation(story.alarmClock.position + new Vector3(-2.8f, 2.2f, 2.4f), Quaternion.LookRotation(-new Vector3(-2.8f, 2.2f, 2.4f)));
        SaveFamily(2);
    }

    // ================================================================ Episode 3 · Running Out of Time

    public static void BuildExamination()
    {
        CopyToFamily(ScenePath(2), 3);
        var c = Object.FindAnyObjectByType<ClassroomEpisode>();
        if (c == null) throw new InvalidOperationException(ScenePath(2) + " has no ClassroomEpisode. Build Chapter 1 - Episode 2 first.");
        root = c.transform; root.name = "Chapter 2 · Episode 3 - Running Out of Time";
        var story = root.gameObject.AddComponent<FamilyExamEpisode>();
        story.player = c.player; story.storyCamera = c.storyCamera;
        story.entrance = c.entrance; story.landing = c.landing; story.seat = c.seat; story.seatView = c.seatView; story.teacher = c.teacher;
        story.openingView = c.openingView; story.spawn = c.spawn; story.bus = c.bus; story.routeMarkers = c.routeMarkers;
        // The examination is a quiet room: no friend waiting by the desks, and the board says what is happening.
        var friend = c.friend != null ? c.friend.parent : null;
        if (friend != null) Object.DestroyImmediate(friend.gameObject);
        foreach (var label in Object.FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (label.text.StartsWith("RESEARCH PROJECT")) label.text = "EXAMINATION\n\nWork quietly.\nUse the time that remains.";
        Object.DestroyImmediate(c);
        SaveFamily(3);
    }

    // ================================================================ Episodes 4 and 5 · Evening

    public static void BuildFinale(int episode)
    {
        var kit = HouseBase(episode, "Chapter 2 · Episode " + episode + " - " + (episode == 4 ? "The Weight Behind the Worry" : "What I Left Behind"), false);
        var story = root.gameObject.AddComponent<FamilyFinaleEpisode>();
        story.episodeNumber = episode; story.player = kit.player; story.storyCamera = kit.camera;
        story.paper = DeskPaperProp("Examination paper interaction");
        var notebook = DeskNotebookProp();
        story.bedroomSpawn = Mark("Bedroom start", BedroomStart, 90);
        story.bedroomView = Pivot("Bedroom view", new Vector3(-13.4f, 4.4f, -11.9f)); story.bedroomView.LookAt(story.paper.position);
        story.bed = kit.bed;
        story.bedView = Pivot("Night camera", new Vector3(-14.6f, 4.8f, -9.4f)); story.bedView.LookAt(story.bed.position);
        story.eveningLight = kit.deskLight;
        if (story.eveningLight != null) story.eveningLight.intensity = 2;

        // Dinner at the house's own dining table: Mother and Father at either end, Tim on the far side.
        var table = GameObject.Find("House And Lot").transform.Find("Dining table").GetComponent<Renderer>().bounds;
        Vector3 top = new Vector3(table.center.x, table.max.y, table.center.z);
        float floorY = table.min.y;
        story.dinner = Pivot("Family dinner interaction", new Vector3(top.x, floorY + 1.4f, table.min.z - 1.6f));
        // Above the near chair, looking down the table at the family.
        story.dinnerView = Pivot("Dinner camera", new Vector3(top.x - 2.4f, floorY + 6f, table.min.z - 3.6f)); story.dinnerView.LookAt(top + Vector3.up * .6f);
        Physics.SyncTransforms();
        Vector3 Spot(Vector3 near) => ClearSpot(new Vector3(near.x, 0, near.z), 0, story.dinner.position, 1.4f, 3, floorY);
        Vector3 mom = Spot(new Vector3(table.min.x - .9f, 0, top.z)), dad = Spot(new Vector3(table.max.x + .9f, 0, top.z)), tim = Spot(new Vector3(table.min.x - .7f, 0, table.max.z + 1.2f));
        story.mother = Person("Mother", FamilyModels[0], mom, Quaternion.LookRotation(Flat(top - mom)), false, "Idle", AdultHeight);
        story.father = Person("Father", FamilyModels[1], dad, Quaternion.LookRotation(Flat(top - dad)), false, "Idle", AdultHeight);
        story.tim = Person("Tim", FamilyModels[2], tim, Quaternion.LookRotation(Flat(top - tim)), false, "Idle", ChildHeight);
        for (int i = 0; i < 3; i++)
        {
            Vector3 p = top + new Vector3((i - 1) * .8f, .02f, -.25f);
            Cylinder("Dinner plate", p, new Vector3(.55f, .012f, .55f), paper, root);
            Cylinder("Rice bowl", p + Vector3.up * .08f, new Vector3(.25f, .07f, .25f), cream, root);
        }
        PointLight("Warm dinner light", top + Vector3.up * 2.8f, new Color(1, .78f, .53f), 2, 9);
        // Episode 4 is dinner itself: a longer table with four chairs, the family seated and eating.
        if (episode == 4) DinnerTable(story);

        // Episode 5: keepsakes from a time when trying felt like enough, on the desk.
        story.keepsakes = new[]
        {
            Pivot("Old certificate", Surface(-10.38f, -10.55f, 2.39f, 2.9f)),
            Pivot("Old hobby item", Surface(-10.85f, -10.35f, 2.39f, 2.9f)),
            Pivot("Old notebook", Surface(-10.85f, -9.05f, 2.39f, 2.9f))
        };
        Box("Certificate frame", story.keepsakes[0].position + Vector3.up * .02f, new Vector3(.45f, .04f, .6f), gold, story.keepsakes[0], false);
        // The hobby keepsake is the old baseball set, on the floor beside the wardrobe.
        KeepsakeBaseball.Place(story.keepsakes[1]);
        Box("Old notebook cover", story.keepsakes[2].position + Vector3.up * .06f, new Vector3(.5f, .12f, .66f), wood, story.keepsakes[2], false);
        var photo = Pivot("Old photograph", new Vector3(-10.22f, 2.66f, -9.15f));
        Box("Photograph frame", photo.position, new Vector3(.045f, .5f, .35f), wood, photo, false);
        Box("Photograph", photo.position + Vector3.left * .03f, new Vector3(.012f, .43f, .29f), teal, photo, false);
        foreach (var k in story.keepsakes) k.gameObject.SetActive(episode == 5);
        photo.gameObject.SetActive(episode == 5);
        if (episode == 5) { story.paper.gameObject.SetActive(false); notebook.gameObject.SetActive(false); }

        Evening(kit.sun, episode == 4 ? .65f : .15f);
        kit.player.SetPositionAndRotation(story.bedroomSpawn.position, story.bedroomSpawn.rotation);
        kit.camera.transform.SetPositionAndRotation(story.bedroomView.position, story.bedroomView.rotation);
        SaveFamily(episode);
    }
}
