using System;
using System.IO;
using System.Linq;
using Mindscape.SocialDynamics;
using Mindscape.Minigames;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class SocialDynamicsBuilder
{
    static Transform root;
    static Material cream, teal, wood, gold, glass;
    // Chapter 2 - Episode 5 as it is now (built on Chapter 1's house): the same house, player rig (1.4x), camera and
    // animated family, so Chapter 3 looks like Chapters 1 and 2. Build Chapter 2 before Chapter 3.
    const string HomeSource = "Assets/Scenes/Chapter 2 - Episode 5.unity";
    const float Scale = AcademicPressureBuilder.PeopleScale, FriendHeight = 2.45f * AcademicPressureBuilder.PeopleScale;
    // Joey's room in the current house: the desk is on the east wall, the free south-west wall holds the mirror.
    static readonly Vector3 DeskTop = new Vector3(-10.7f, 2.39f, -9.75f), MirrorWall = new Vector3(-18.1f, 0, -14.1f);
    [MenuItem("Mindscape/Chapter 3 - Social Dynamics/Build All Eight Episodes")]
    public static void Build() => Build(1, 8);

    // Episodes 2-6 only (the reunion on the school grounds); the home episodes are left as they are.
    [MenuItem("Mindscape/Chapter 3 - Social Dynamics/Rebuild the Reunion Episodes (2-6)")]
    public static void BuildReunion() => Build(2, 6);

    static void Build(int first, int last)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before building.");
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the open scene first.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        Directory.CreateDirectory("Assets/SocialDynamics/Materials"); Directory.CreateDirectory("Assets/Resources/SocialDynamics");
        cream = Mat("Warm plaster", new Color32(228, 215, 191, 255)); teal = Mat("Deep teal", new Color32(42, 83, 83, 255));
        wood = Mat("Walnut", new Color32(111, 73, 48, 255)); gold = Mat("Amber", new Color32(231, 176, 86, 255));
        glass = Mat("Evening window", new Color32(54, 76, 107, 255));
        try
        {
            for (int n = first; n <= last; n++) BuildEpisode(n);
            var list = EditorBuildSettings.scenes.ToList();
            for (int n = 1; n <= 8; n++) { string path = "Assets/Scenes/" + SocialDynamicsChapter.Scene(n) + ".unity"; var entry = list.FirstOrDefault(x => x.path == path); if (entry == null) list.Add(new EditorBuildSettingsScene(path, true)); else entry.enabled = true; }
            EditorBuildSettings.scenes = list.ToArray();
            foreach (var guid in AssetDatabase.FindAssets("t:BuildProfile"))
            {
                var p = AssetDatabase.LoadAssetAtPath<UnityEditor.Build.Profile.BuildProfile>(AssetDatabase.GUIDToAssetPath(guid));
                if (p == null || !p.overrideGlobalScenes) continue;
                var entries = p.scenes.ToList();
                foreach (var e in list.Where(x => x.path.Contains("Chapter 3 -"))) { var old = entries.FirstOrDefault(x => x.path == e.path); if (old == null) entries.Add(new EditorBuildSettingsScene(e.path, true)); else old.enabled = true; }
                p.scenes = entries.ToArray(); EditorUtility.SetDirty(p);
            }
            AssetDatabase.SaveAssets();
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        Debug.Log("Social Dynamics built: episodes " + first + "-" + last + ", previews, and build profiles.");
    }

    static void BuildEpisode(int n)
    {
        var source = EditorSceneManager.OpenScene(HomeSource);
        var old = Object.FindAnyObjectByType<Mindscape.FamilyFinale.FamilyFinaleEpisode>();
        if (old == null) throw new InvalidOperationException(HomeSource + " has no FamilyFinaleEpisode. Build Chapter 2 first.");
        bool home = n == 1 || n >= 7;
        Scene scene;
        GameObject player, camera;
        if (home)
        {
            scene = source; player = old.player.gameObject; camera = old.storyCamera.gameObject;
            Object.DestroyImmediate(old);
            foreach (var c in Object.FindObjectsByType<CollectibleItem>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(c);
            foreach (var c in Object.FindObjectsByType<CollectiblesManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(c);
            foreach (var c in Object.FindObjectsByType<Quest>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(c);
            foreach (var t in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray())
                if (t != null && (t.name == "Old certificate" || t.name == "Old hobby item" || t.name == "Old notebook" || t.name == "Old photograph frame" || t.name == "Old photograph")) t.gameObject.SetActive(false);
        }
        else
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
            player = Object.Instantiate(old.player.gameObject); player.name = "Player"; SceneManager.MoveGameObjectToScene(player, scene);
            camera = Object.Instantiate(old.storyCamera.gameObject); camera.name = "Main Camera"; SceneManager.MoveGameObjectToScene(camera, scene);
            EditorSceneManager.CloseScene(source, true);
        }
        root = new GameObject("Chapter 3 - " + SocialDynamicsChapter.Titles[n - 1]).transform;
        var story = root.gameObject.AddComponent<SocialDynamicsEpisode>(); story.episodeNumber = n; story.player = player.transform; story.storyCamera = camera.GetComponent<Camera>();
        if (home) BuildHome(story, n); else BuildVenue(story, n);
        // No scene music: MusicManager already plays the game's playlist in every scene.
        player.SetActive(true); player.transform.SetPositionAndRotation(story.spawn.position, story.spawn.rotation);
        var controls = player.GetComponent<PlayerControls>(); controls.enabled = false;
        controls.tasksOpen = controls.pauseOpen = controls.questOpen = controls.dialogueOpen = controls.minigameOpen = false;
        var movement = player.GetComponent<PlayerMovement>(); movement.enabled = true; movement.cameraTransform = camera.transform;
        // The Chapter 1 school rig (already scaled 1.4x with the player).
        movement.cameraOffset = new Vector3(0, .45f, -3.5f) * Scale; movement.cameraTargetHeight = 2.1f * Scale; movement.minDistance = 1.2f * Scale; movement.maxDistance = 5 * Scale;
        story.storyCamera.transform.SetPositionAndRotation(story.openingView.position, story.openingView.rotation);
        story.storyCamera.farClipPlane = 180; story.storyCamera.backgroundColor = new Color(.09f, .13f, .20f); story.storyCamera.clearFlags = CameraClearFlags.SolidColor;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.36f, .39f, .45f); RenderSettings.fog = false;
        MinigameUI.EnsureInput();
        WorldModelSync.Apply(scene);
        AmbientLifeBuilder.Apply(scene);
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/" + SocialDynamicsChapter.Scene(n) + ".unity");
        Preview(story, n);
    }

    static void BuildHome(SocialDynamicsEpisode s, int n)
    {
        // On the desk (east wall): Justin's message, the old photograph standing against the wall, the desk itself.
        var phone = Pivot("Justin's invitation phone", DeskTop + new Vector3(-.05f, .02f, .45f));
        var phoneModel = Prop("Assets/Models/Collectibles/Collectible_Phone.glb", phone.position, .45f, phone);
        var photo = Pivot("Old group photograph", DeskTop + new Vector3(.38f, .3f, -.7f));
        Box("Photo frame", photo.position, new Vector3(.06f, .55f, .6f), wood, false, photo);
        Box("Photograph", photo.position + Vector3.left * .04f, new Vector3(.02f, .45f, .5f), cream, false, photo);
        for (int i = 0; i < 4; i++) { Box("Friend in photograph", photo.position + new Vector3(-.06f, -.04f, -.18f + i * .12f), new Vector3(.02f, .22f, .09f), i % 2 == 0 ? teal : gold, false, photo); }
        var desk = Pivot("Desk memories", DeskTop + Vector3.up * .05f);
        // The mirror hangs on the south-west wall, facing into the room (+X).
        var mirror = Pivot("Mirror interaction", MirrorWall + new Vector3(.6f, 2.6f, 0));
        Box("Mirror frame", MirrorWall + new Vector3(.05f, 2.75f, 0), new Vector3(.12f, 3.6f, 1.65f), wood, false);
        var surface = Box("Mirror surface", MirrorWall + new Vector3(.13f, 2.75f, 0), new Vector3(.015f, 3.34f, 1.4f), glass, false);
        var reflection = surface.AddComponent<SocialMirror>(); reflection.player = s.player; reflection.planeX = MirrorWall.x + .14f; reflection.side = 1; reflection.eyeHeight = 3.4f;
        s.spawn = Pivot("Bedroom start", new Vector3(-15.4f, .65f, -9.8f)); s.spawn.rotation = Quaternion.Euler(0, 90, 0);
        s.bedroomReturn = Pivot("Back in the room", new Vector3(-15.4f, .65f, -9.8f)); s.bedroomReturn.rotation = Quaternion.Euler(0, 90, 0);
        s.openingView = View("Bedroom opening", new Vector3(-13.6f, 4.4f, -12f), phone.position);
        s.warmth = PointLight("Soft bedroom lamp", DeskTop + new Vector3(0, 1.3f, 0), 1.4f, 10);
        var sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(x => x.type == LightType.Directional);
        if (sun != null) { sun.color = new Color(.58f, .69f, 1); sun.intensity = .2f; }
        if (n == 1) s.points = new[] { phone, photo, desk };
        if (n == 8) { s.points = new[] { mirror }; s.spawn.position = new Vector3(-13.2f, .65f, MirrorWall.z); s.spawn.rotation = Quaternion.Euler(0, 270, 0); s.openingView = View("Mirror opening", new Vector3(-13.4f, 4.2f, MirrorWall.z + 2.2f), mirror.position); }
        if (n == 7)
        {
            // Coming home, the desk is bare: the phone and the photograph appear when you put your phone down.
            s.deskItems = new[] { phoneModel, photo.gameObject };
            foreach (var item in s.deskItems) item.SetActive(false);
            // Follow the house model's existing front sidewalk, through its pedestrian gate.
            var house = GameObject.Find("House And Lot");
            HouseModelSync.AddMissingParts(house.transform);
            foreach (var t in house.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("Pedestrian gate |")) t.gameObject.SetActive(false);
            OpenNeighborhood(house);
            // Pools of light along both sidewalks, the whole length of the street.
            for (float x = -66; x <= 66; x += 15) { PointLight("Streetlight pool", new Vector3(x, 5.3f, 24), 2.2f, 16); PointLight("Streetlight pool", new Vector3(x + 7.5f, 5.3f, 37), 1.6f, 14); }
            // East of the bus shelter (x 20.5-27.9), clear of it and of the people waiting there.
            s.spawn.position = new Vector3(32, .65f, 22.5f); s.spawn.rotation = Quaternion.Euler(0, 270, 0);
            s.points = new[] { Pivot("Walk beneath the lights", new Vector3(8, 2, 22.5f)), Pivot("Photo on the way home", new Vector3(-12, 2, 22.5f)), Pivot("Home entrance", new Vector3(-22.1f, 2, 5)), phone, mirror };
            s.openingView = View("Night street opening", new Vector3(37, 8, 32), new Vector3(-9, 1.8f, 20));
        }
    }

    // The whole neighbourhood can be explored on the walk home: both sidewalks, the road and the houses across it.
    // Chapter 2's walls that kept you on the near sidewalk come down; invisible walls follow the edge of the street's
    // ground (Street | verge ground) instead, leaving the way through to the house lot open.
    static void OpenNeighborhood(GameObject house)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == "Road boundary" || t.name == "West route end" || t.name == "East route end") t.gameObject.SetActive(false);
        var verge = house.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == "Street | verge ground");
        if (verge == null) throw new InvalidOperationException("House And Lot has no 'Street | verge ground'.");
        Physics.SyncTransforms();
        Bounds b = verge.bounds; const float height = 10, thick = .5f;
        var edges = Pivot("Neighborhood edge", Vector3.zero);
        void Wall(Vector3 from, Vector3 to)
        {
            var wall = new GameObject("Edge wall", typeof(BoxCollider)).transform; wall.SetParent(edges);
            wall.position = (from + to) * .5f + Vector3.up * height * .5f;
            wall.GetComponent<BoxCollider>().size = new Vector3(Mathf.Max(thick, Mathf.Abs(to.x - from.x)), height, Mathf.Max(thick, Mathf.Abs(to.z - from.z)));
        }
        Wall(new Vector3(b.min.x, 0, b.min.z), new Vector3(b.min.x, 0, b.max.z));
        Wall(new Vector3(b.max.x, 0, b.min.z), new Vector3(b.max.x, 0, b.max.z));
        Wall(new Vector3(b.min.x, 0, b.max.z), new Vector3(b.max.x, 0, b.max.z));
        // Along the near edge, wall off only where there is no ground beyond it (everywhere but the house lot).
        float z = b.min.z, start = float.NaN;
        for (float x = b.min.x; x <= b.max.x + .01f; x += .5f)
        {
            bool open = x < b.max.x && Physics.Raycast(new Vector3(x, 20, z - 1), Vector3.down, 40, ~0, QueryTriggerInteraction.Ignore);
            if (!open && float.IsNaN(start)) start = x;
            if (open && !float.IsNaN(start)) { Wall(new Vector3(start, 0, z), new Vector3(x, 0, z)); start = float.NaN; }
        }
        if (!float.IsNaN(start)) Wall(new Vector3(start, 0, z), new Vector3(b.max.x, 0, z));
    }

    static void BuildVenue(SocialDynamicsEpisode s, int n)
    {
        // The reunion is held in the Reunion Hall on the school grounds (see PlaceVenue for the room's layout).
        PlaceVenue();
        // The model's string lights are unlit: amber glows under its two strands, a lamp by the lounge window.
        foreach (float z in new[] { -2.4f, 2.4f })
            for (int i = 0; i < 5; i++) PointLight("Amber string glow", H(-10 + i * 5, 4.6f, z), .8f, 7);
        PointLight("Lounge window lamp", H(-10.8f, 3.2f, 1.6f), 1.8f, 7);
        var points = new[] {
            Pivot("Meet Justin", Floor(-1, -2.2f, 1.6f)),                       // past the reception desk, facing the doors
            Pivot("Join the friends", Floor(-4.6f, -1, 1.6f)),                  // the west group, mid-floor
            Pivot("Career conversation", Floor(-4.2f, 2.2f, 1.6f)),             // the same two friends, by the buffet
            Pivot("Travel conversation", Floor(3, -.6f, 1.6f)),                 // the east group, before the stage
            Pivot("Quiet window corner", Floor(-11, 1.4f, 1.6f)),               // the lounge, under the west window
            Pivot("Quiet corner phone", Floor(-9.6f, .3f, .02f)),             // on the lounge coffee table
            Pivot("Group photograph place", Floor(8.4f, .8f, 1.5f)) };         // on the stage, before the backdrop
        points[6].rotation = Quaternion.Euler(0, 270, 0);   // facing the room, like the friends behind
        Prop("Assets/Models/Collectibles/Collectible_Phone.glb", points[5].position, .45f, points[5]);
        s.points = points;
        // Five people: Justin and the four friends who speak (Friend 1-2 in the west group, 3-4 in the east).
        // Ambient life adds one guest walking along the buffet.
        string[] models = { "NPC_06_Ethan", "NPC_01_Maya", "NPC_03_Leo", "NPC_05_Chloe", "NPC_08_Zara" };
        Vector3[] atPeople = { Floor(-1, -2.2f), Floor(-3.4f, .4f), Floor(-5.6f, 1.2f), Floor(2.4f, 1.6f), Floor(4.6f, .6f) };
        Vector3[] facing = { Floor(0, -4.4f), points[1].position, points[1].position, points[3].position, points[3].position };
        if (n == 3) { facing[1] = facing[2] = points[2].position; }   // turned toward each other's conversation
        if (n == 5) { atPeople[0] = Floor(-10.4f, 2.8f); facing[0] = points[4].position; }   // Justin finds you by the window
        // Lined up on the stage for the photograph, facing the room.
        if (n == 6) for (int i = 0; i < atPeople.Length; i++) { atPeople[i] = Floor(9.8f, -1.4f + i * 1.1f / Roomier); facing[i] = atPeople[i] + Vector3.left; }
        for (int i = 0; i < models.Length; i++)
        {
            string modelPath = "Assets/Characters/NPCs/" + models[i] + ".fbx";
            var person = Prop(modelPath, atPeople[i], FriendHeight, root, true);
            person.name = i == 0 ? "Justin" : "Old friend " + i;
            Vector3 look = facing[i] - atPeople[i]; look.y = 0;
            person.transform.rotation = Quaternion.LookRotation(look);
            var idle = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__") && (c.name == "Idle" || c.name.EndsWith("|Idle")));
            if (idle != null)
            {
                string controllerPath = "Assets/SocialDynamics/" + models[i] + " Idle.controller";
                var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
                if (controller == null) controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath, idle);
                var animator = person.GetComponent<Animator>(); if (animator == null) animator = person.AddComponent<Animator>(); animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
                idle.SampleAnimation(person, 0);
            }
        }
        // Just inside the glass doors; Episodes 5 and 6 begin near the lounge.
        s.spawn = Pivot("Venue start", n == 5 || n == 6 ? Floor(-8.2f, 2.8f) : Floor(0, -4.4f));
        s.spawn.rotation = Quaternion.Euler(0, n == 5 || n == 6 ? 250 : 0, 0);
        s.openingView = View("Reunion opening", HV(4.6f, 4.8f, -5), HV(-4, 1.5f, 1.5f));
        if (n == 4 || n == 5) s.openingView = View("Quiet corner opening", HV(-6, 4, -1), HV(-11, 1.8f, 1.4f));
        if (n == 6) s.openingView = View("Stage opening", HV(2.4f, 3.6f, .8f), HV(9.8f, 2, .8f));
        s.warmth = PointLight("Reunion warm fill", H(0, 5, 0), 3, 20 * Roomier);
        var sun = new GameObject("Evening sun", typeof(Light)).GetComponent<Light>(); sun.transform.SetParent(root); sun.type = LightType.Directional; sun.color = new Color(.75f, .8f, 1); sun.intensity = .55f; sun.transform.rotation = Quaternion.Euler(40, -25, 0);
    }

    // The venue is the Reunion Hall of the shared school campus (Blender Files/school-grounds.blend -> School_Grounds.glb,
    // the model Chapter 1 walks through), placed at Chapter 1's 2x scale and turned a half turn so the hall's glass doors
    // face south (-Z), with the middle of the hall's floor at the origin. glTF puts a Blender point (x, y, z) at
    // (-x, z, -y); the half turn makes that (x, z, y), so hall coordinates are 2 * (x - 14, z, y + 8.5).
    // Inside: x -12.3..12.3, z -5.9..5.9. Glass doors x -2.4..2.4 on the south wall with the reception desk west of them
    // (x -5.3..-3.5, z -4.2..-3); the buffet table along the north wall (x -6.5..5.3, z 4.1..5.9); the lounge in the west
    // (sofa x -11.1..-7.3, z -3.4..-1.7; coffee table x -10.1..-8.3, z -1.1..0.7; window on the west wall, z -2..2); the
    // roped-off stage in the east (x 6.8..11.6, z -2.2..3.8, 0.4 high) with speakers and a backdrop on the east wall.
    const string SchoolModel = "Assets/Models/School_Grounds.glb";
    // The hall is built 30% roomier than Chapter 1's 2x campus (the capstone list: "medyo lakihan para hindi masikip"):
    // the model goes in at 2.6x and every hall coordinate below (written at 2x) is spread by the same factor through
    // Floor and H, while the people keep their size.
    const float HallScale = 2.6f, Roomier = HallScale / 2;
    static Vector3 H(float x, float y, float z) => new Vector3(x * Roomier, y * Roomier, z * Roomier);
    // Camera spots: spread across the floor, but at people's eye level, which does not change.
    static Vector3 HV(float x, float y, float z) => new Vector3(x * Roomier, y, z * Roomier);
    public const string VenueName = "School grounds · Reunion Hall";
    static readonly Vector2 HallMiddle = new Vector2(14, -8.5f);   // Blender X/Y of the middle of the hall floor
    // Everything in the hall collides except the overhead string lights and beams ("Lighting and Decor"), which would
    // only knock the camera about. The glass doors and windows keep the player inside the hall.
    public const string HallOverhead = "Reunion Hall | Lighting and Decor";
    static readonly string[] HallParts = { "Reunion Hall | Architecture", "Reunion Hall | Roof", "Reunion Hall | Catering Buffet Table", "Reunion Hall | Lounge and Reception", "Reunion Hall | Sound and Stage", HallOverhead };
    static GameObject PlaceVenue()
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(SchoolModel);
        if (asset == null) throw new InvalidOperationException("Missing asset: " + SchoolModel);
        var campus = (GameObject)PrefabUtility.InstantiatePrefab(asset, root); campus.name = VenueName;
        campus.transform.SetLocalPositionAndRotation(new Vector3(-HallScale * HallMiddle.x, 0, -HallScale * HallMiddle.y), Quaternion.Euler(0, 180, 0));
        campus.transform.localScale = Vector3.one * HallScale;
        var filters = campus.GetComponentsInChildren<MeshFilter>(true);
        foreach (string part in HallParts)
            if (!filters.Any(f => f.name == part)) throw new InvalidOperationException(SchoolModel + " has no '" + part + "'.");
        foreach (var filter in filters.Where(f => HallParts.Contains(f.name) && f.name != HallOverhead))
        {
            if (filter.GetComponent<Collider>() == null) filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            // The roof casts no shadow so the evening light still reaches the room.
            if (filter.name.EndsWith("Roof")) filter.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        Physics.SyncTransforms();
        var hall = filters.First(f => f.name == "Reunion Hall | Architecture").GetComponent<Renderer>().bounds;
        if (Mathf.Abs(hall.center.x) > .5f || Mathf.Abs(hall.max.z - 6.4f * Roomier) > .5f * Roomier)
            throw new InvalidOperationException("The Reunion Hall moved in school-grounds.blend; update HallMiddle and the venue coordinates in SocialDynamicsBuilder.");
        return campus;
    }
    // The highest surface below knee height at (x, z): the hall floor, the stage, or the coffee table (needs PlaceVenue first).
    static Vector3 Floor(float x, float z, float up = 0)
    {
        x *= Roomier; z *= Roomier;
        float y = float.MinValue;
        foreach (var hit in Physics.RaycastAll(new Vector3(x, 1.2f * Roomier, z), Vector3.down, 3 * Roomier, ~0, QueryTriggerInteraction.Ignore))
            if (hit.collider.GetComponentInParent<CharacterController>() == null) y = Mathf.Max(y, hit.point.y);   // not the player
        if (y == float.MinValue) throw new InvalidOperationException("No floor under the reunion spot (" + x + ", " + z + ").");
        return new Vector3(x, y + up, z);
    }

    static GameObject Prop(string path, Vector3 at, float size, Transform parent, bool height = false)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) throw new InvalidOperationException("Missing asset: " + path);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        foreach (var c in go.GetComponentsInChildren<MonoBehaviour>(true)) if (c != null) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) throw new InvalidOperationException("Asset contains no visible model: " + path);
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        go.transform.localScale *= size / Mathf.Max(.01f, height ? b.size.y : Mathf.Max(b.size.x, b.size.y, b.size.z));
        b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        go.transform.position += at - new Vector3(b.center.x, b.min.y, b.center.z);
        return go;
    }
    static Transform Pivot(string name, Vector3 at) { var t = new GameObject(name).transform; t.SetParent(root); t.position = at; return t; }
    static Transform View(string name, Vector3 at, Vector3 target) { var t = Pivot(name, at); t.LookAt(target); return t; }
    static GameObject Box(string name, Vector3 at, Vector3 size, Material m, bool collision, Transform parent = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent == null ? root : parent); go.transform.position = at; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = m; if (!collision) Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
    }
    static Light PointLight(string name, Vector3 at, float intensity, float range) { var l = Pivot(name, at).gameObject.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1, .76f, .48f); l.intensity = intensity; l.range = range; return l; }
    static Material Mat(string name, Color color) { string path = "Assets/SocialDynamics/Materials/" + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path); if (m != null) return m; m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color }; AssetDatabase.CreateAsset(m, path); return m; }
    static void Preview(SocialDynamicsEpisode s, int n)
    {
        var rt = RenderTexture.GetTemporary(960, 540, 24); var previous = RenderTexture.active; var oldTarget = s.storyCamera.targetTexture; var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
        try { s.storyCamera.targetTexture = rt; s.storyCamera.Render(); RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply(); File.WriteAllBytes("Assets/Resources/SocialDynamics/Episode" + n + ".png", image.EncodeToPNG()); }
        finally { s.storyCamera.targetTexture = oldTarget; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(image); }
        AssetDatabase.ImportAsset("Assets/Resources/SocialDynamics/Episode" + n + ".png");
    }
}
