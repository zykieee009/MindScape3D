using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Life around the story, in every story scene:
//   * five extra people who walk or jog routes across the map (street, school corridor and yard, reunion room, tutorial field),
//   * every character can be talked to (F): short, neutral small talk through the NPC system,
//   * the house always has Mom, Dad and Tim (added when the episode's own story does not place them).
// The episode builders call Apply before saving; Mindscape > NPCs > Add Ambient Life updates the saved scenes. Safe to re-run.
public static class AmbientLifeBuilder
{
    const string RootName = "Ambient Life";
    const float Scale = AcademicPressureBuilder.PeopleScale;
    const string AnimatorFolder = "Assets/AcademicPressure/Animators";
    enum Place { None, House, School, Venue, Field }

    static readonly string[] SceneList =
    {
        "Chapter 1 - Episode 1", "Chapter 1 - Episode 2", "Chapter 1 - Episode 3", "Chapter 1 - Episode 4",
        "Chapter 2 - Episode 1", "Chapter 2 - Episode 2", "Chapter 2 - Episode 3", "Chapter 2 - Episode 4", "Chapter 2 - Episode 5",
        "Chapter 3 - Episode 1", "Chapter 3 - Episode 2", "Chapter 3 - Episode 3", "Chapter 3 - Episode 4", "Chapter 3 - Episode 5",
        "Chapter 3 - Episode 6", "Chapter 3 - Episode 7", "Chapter 3 - Episode 8", "Tutorial"
    };

    // Walkers come from the student cast; the teacher, the cook and the officer keep their own roles, and
    // Amara, David and Tariq are the family (AcademicPressureBuilder.FamilyModels).
    static readonly string[] WalkerCast =
    {
        "NPC_02_Nia", "NPC_04_Hana", "NPC_09_Julian", "NPC_10_Olivia",
        "NPC_17_Penelope", "NPC_12_Noah", "NPC_14_Mei", "NPC_05_Chloe", "NPC_08_Zara", "NPC_07_Malik", "NPC_06_Ethan", "NPC_03_Leo", "NPC_01_Maya"
    };

    [MenuItem("Mindscape/NPCs/Add Ambient Life to All Story Scenes")]
    public static void ApplyAll()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play mode first."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string active = SceneManager.GetActiveScene().path;
        foreach (var name in SceneList)
        {
            string path = "Assets/Scenes/" + name + ".unity";
            if (!File.Exists(path)) continue;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Debug.Log("Ambient life, " + name + ": " + Apply(scene));
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        if (File.Exists(active)) EditorSceneManager.OpenScene(active, OpenSceneMode.Single);
    }

    public static string Apply(Scene scene)
    {
        foreach (var old in scene.GetRootGameObjects().Where(g => g.name == RootName).ToArray()) Object.DestroyImmediate(old);
        Physics.SyncTransforms();
        var place = Detect(scene);
        if (place == Place.None) return "not a story location, skipped.";
        var root = new GameObject(RootName).transform;
        SceneManager.MoveGameObjectToScene(root.gameObject, scene);
        if (Object.FindObjectsByType<NPCInteractionManager>(FindObjectsInactive.Include).Length == 0)
            new GameObject("NPC System", typeof(NPCInteractionManager)).transform.SetParent(root, false);

        int family = place == Place.House ? AddFamily(root) : 0;
        int walkers = AddWalkers(place, root);
        int talkers = MakeEveryoneTalk(root, place);
        Physics.SyncTransforms();
        return walkers + " walkers/joggers added, " + family + " family members added, " + talkers + " characters can be talked to.";
    }

    static Place Detect(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        if (roots.Any(g => g.name == "House And Lot")) return Place.House;
        if (roots.SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Any(t => t.name == "School grounds")) return Place.School;
        if (roots.SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Any(t => t.name == SocialDynamicsBuilder.VenueName || t.name == "Reunion floor")) return Place.Venue;
        if (scene.name == "Tutorial") return Place.Field;
        return Place.None;
    }

    // ================================================================ family

    static int AddFamily(Transform root)
    {
        // The evening episodes place the family at the dinner table themselves.
        bool storyFamily = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
            .Any(t => (t.name == "Mother" || t.name == "Father" || t.name == "Tim") && !t.IsChildOf(root));
        if (storyFamily) return 0;
        var family = AcademicPressureBuilder.FamilyModels;
        Character("Mom", family[0], Free(new Vector3(-13.8f, 0, 3.0f)), 270, 2.62f * Scale, root, true);
        Character("Dad", family[1], Free(new Vector3(-26.4f, 0, -2.4f)), 270, 2.62f * Scale, root, true);
        Character("Tim", family[2], Free(new Vector3(-27.6f, 0, 2.4f)), 200, 2.05f * Scale, root, true);
        return 3;
    }

    // An animated, standing character (Idle) on the ground at 'at', with a body collider.
    static Transform Character(string name, string modelPath, Vector3 at, float yaw, float height, Transform parent, bool block)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (asset == null) throw new FileNotFoundException("Character model missing.", modelPath);
        var pivot = new GameObject(name).transform; pivot.SetParent(parent, false); pivot.SetPositionAndRotation(at, Quaternion.Euler(0, yaw, 0));
        var body = (GameObject)PrefabUtility.InstantiatePrefab(asset, pivot); body.name = "Model";
        body.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        var b = Bounds(body); body.transform.localScale *= height / Mathf.Max(.01f, b.size.y);
        b = Bounds(body); body.transform.position += at - new Vector3(b.center.x, b.min.y, b.center.z);
        var idle = Clip(modelPath, "Idle");
        if (idle != null)
        {
            Directory.CreateDirectory(AnimatorFolder);
            string path = AnimatorFolder + "/" + Path.GetFileNameWithoutExtension(modelPath) + " Idle.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(path, idle);
            var animator = body.GetComponent<Animator>(); if (animator == null) animator = body.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
            idle.SampleAnimation(body, 0);
        }
        foreach (var c in body.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        if (block) { var box = pivot.gameObject.AddComponent<BoxCollider>(); box.center = new Vector3(0, height * .5f, 0); box.size = new Vector3(1.1f, height, .7f); }
        return pivot;
    }

    // ================================================================ walkers and joggers

    struct Route { public Vector3[] points; public bool run, loop; }

    static int AddWalkers(Place place, Transform root)
    {
        var routes = Routes(place);
        if (routes.Count == 0) return 0;
        var used = new HashSet<string>(Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include)
            .Select(r => r.sharedMesh != null ? Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(r.sharedMesh)) : ""));
        var cast = WalkerCast.Where(m => !used.Contains(m)).Concat(WalkerCast).ToList();
        for (int i = 0; i < routes.Count; i++)
        {
            string model = cast[i];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NPCs/" + model + ".prefab");
            if (prefab == null) { Debug.LogWarning("Ambient life: missing NPC prefab " + model); continue; }
            var route = routes[i];
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            go.name = (route.run ? "Jogger - " : "Walker - ") + DisplayName(model);
            Vector3 start = route.points[i % route.points.Length];
            if (!route.loop) start = Vector3.Lerp(route.points[0], route.points[route.points.Length - 1], .15f + .14f * i);
            go.transform.SetPositionAndRotation(Ground(start), Quaternion.LookRotation(Flat(route.points[route.points.Length - 1] - route.points[0])));
            go.transform.localScale = Vector3.one * Scale;
            var npc = go.GetComponent<NPC>();
            npc.interactionRadius = 3f * Scale; npc.faceHeight = 1.65f * Scale; npc.cameraDistance = 1.7f * Scale;
            npc.modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/NPCs/" + model + ".fbx");
            npc.RefreshModel();
            foreach (var tag in go.GetComponentsInChildren<NPCNameTag>(true)) Object.DestroyImmediate(tag.gameObject);
            Walker(go.transform.Find("Model"), model);

            var path = new GameObject(go.name + " route").transform; path.SetParent(root, false);
            var wander = go.GetComponent<NPCWander>(); if (wander == null) wander = go.AddComponent<NPCWander>();
            wander.waypoints = route.points.Select((p, k) => { var w = new GameObject("Waypoint " + (k + 1)).transform; w.SetParent(path, false); w.position = Ground(p); return w; }).ToList();
            wander.pattern = route.loop ? NPCWander.Pattern.Loop : NPCWander.Pattern.PingPong;
            wander.walkSpeed = (route.run ? 3.2f : 1.1f) * Scale;
            wander.moveAnimationSpeed = route.run ? 2 : 1;
            wander.pauseSeconds = route.run ? new Vector2(.5f, 1.5f) : new Vector2(1.5f, 4f);
            npc.npcName = DisplayName(model);
            npc.enableDialogueChaining = true;
            npc.dialogueNodes = Dialogue(place == Place.Field ? "tutorial" : place == Place.Venue ? "guest" : route.run ? "jogger" : place == Place.School ? "schoolwalker" : "neighbor", model);
            EditorUtility.SetDirty(go);
        }
        return routes.Count;
    }

    static List<Route> Routes(Place place)
    {
        var list = new List<Route>();
        void Add(bool run, bool loop, params Vector3[] p) => list.Add(new Route { points = p, run = run, loop = loop });
        switch (place)
        {
            case Place.House:
                // Sidewalks only. WorldModelSync's city puts the near pavement at z 22.6-27.6 and the far one at 47.6-52.6 (the road lies between).
                // The lot's entrance ramps cross the near pavement around x -36 to -18, so the near routes stay east of them.
                Add(false, false, new Vector3(-55, 0, 49.0f), new Vector3(50, 0, 49.0f));
                Add(false, false, new Vector3(45, 0, 50.6f), new Vector3(-50, 0, 50.6f));
                Add(true, false, new Vector3(-10, 0, 24.6f), new Vector3(70, 0, 24.6f));
                Add(true, false, new Vector3(55, 0, 49.8f), new Vector3(-55, 0, 49.8f));
                Add(false, false, new Vector3(68, 0, 26.0f), new Vector3(-8, 0, 26.0f));
                break;
            case Place.School:
            {
                var school = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).First(t => t.name == "School grounds").gameObject;
                Bounds corridor = Part(school, "Corridor | floor"), gate = Part(school, "School gate | pillars and arch");
                float cz = corridor.center.z, yardNear = corridor.max.z + 3, yardFar = gate.center.z - Mathf.Sign(gate.center.z - cz) * 5, mid = (yardNear + yardFar) * .5f;
                // Along the covered corridor, and across the yard between the corridor and the gate.
                Add(false, false, new Vector3(corridor.min.x + 10, 0, cz + 1.2f), new Vector3(corridor.max.x - 10, 0, cz + 1.2f));
                Add(true, false, new Vector3(corridor.max.x - 10, 0, cz - 1.2f), new Vector3(corridor.min.x + 10, 0, cz - 1.2f));
                Add(false, false, new Vector3(gate.center.x - 14, 0, yardNear), new Vector3(gate.center.x - 14, 0, yardFar));
                Add(false, false, new Vector3(gate.center.x + 14, 0, yardFar), new Vector3(gate.center.x + 14, 0, yardNear));
                Add(true, false, new Vector3(gate.center.x - 26, 0, mid), new Vector3(gate.center.x + 26, 0, mid));
                break;
            }
            case Place.Venue:
                // One guest strolls along the buffet table; the reunion itself is Justin and four friends.
                Add(false, false, new Vector3(-5.8f, 0, 3.4f), new Vector3(4.8f, 0, 3.4f));
                break;
            case Place.Field:
                Add(true, true, new Vector3(-18, 0, -18), new Vector3(18, 0, -18), new Vector3(18, 0, 18), new Vector3(-18, 0, 18));
                Add(true, true, new Vector3(18, 0, 18), new Vector3(-18, 0, 18), new Vector3(-18, 0, -18), new Vector3(18, 0, -18));
                Add(false, false, new Vector3(-12, 0, -20), new Vector3(-12, 0, 15));
                Add(false, false, new Vector3(14, 0, 15), new Vector3(14, 0, -20));
                Add(false, false, new Vector3(-20, 0, 20), new Vector3(20, 0, 20));
                break;
        }
        return list;
    }

    // Idle / Walk / Run on one "Speed" blend (0, 1, 2), driven by NPCWander.
    static void Walker(Transform model, string modelName)
    {
        if (model == null) return;
        string fbx = "Assets/Characters/NPCs/" + modelName + ".fbx";
        AnimationClip idle = Clip(fbx, "Idle"), walk = Clip(fbx, "Walk"), run = Clip(fbx, "Run");
        if (idle == null || walk == null || run == null) { Debug.LogWarning("Ambient life: " + modelName + " lacks Idle/Walk/Run."); return; }
        Directory.CreateDirectory(AnimatorFolder);
        string path = AnimatorFolder + "/" + modelName + " Walker.controller";
        AssetDatabase.DeleteAsset(path);   // rebuilt each run so it always points at the current clips
        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        var state = controller.CreateBlendTreeInController("Locomotion", out var tree);
        tree.blendType = BlendTreeType.Simple1D; tree.blendParameter = "Speed"; tree.useAutomaticThresholds = false;
        tree.AddChild(idle, 0); tree.AddChild(walk, 1); tree.AddChild(run, 2);
        controller.layers[0].stateMachine.defaultState = state;
        var animator = model.GetComponent<Animator>(); if (animator == null) animator = model.gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
        idle.SampleAnimation(model.gameObject, 0);
    }

    // ================================================================ talking

    // Every animated character in the scene (except the player and the people in a remembered scene) gets an NPC
    // component with small talk. People sitting down keep facing their desk or table while they talk.
    static int MakeEveryoneTalk(Transform root, Place place)
    {
        int count = 0;
        foreach (var animator in Object.FindObjectsByType<Animator>(FindObjectsInactive.Include))
        {
            var smr = animator.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr == null || smr.sharedMesh == null) continue;
            var t = animator.transform;
            if (t.root.name == "Player" || t.GetComponentsInParent<Transform>(true).Any(p => p.name == "Remembered classroom")) continue;
            var person = t.name == "Model" && t.parent != null ? t.parent : t;
            var npc = person.GetComponent<NPC>();
            if (npc == null) npc = person.gameObject.AddComponent<NPC>();
            else if (npc.dialogueNodes.Count > 0 && npc.dialogueNodes[0].npcDialogue != "Hello, traveler." && !person.IsChildOf(root)) { count++; continue; }   // already set up (street neighbors)

            string model = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(smr.sharedMesh));
            string controller = animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "";
            bool seated = controller.Contains("Seated") || controller.Contains("Sitting");
            string name = person.name == "Friend · Sam" ? "Sam" : person.name == "Justin" ? "Justin" : person.name == "Mother" ? "Mom" : person.name == "Father" ? "Dad"
                : person.name == "Tim" || person.name == "Mom" || person.name == "Dad" ? person.name : DisplayName(model);
            string role = name == "Sam" ? "sam" : name == "Justin" ? "justin" : name == "Mom" ? "mom" : name == "Dad" ? "dad" : name == "Tim" ? "tim"
                : model.Contains("Henderson") ? "teacher" : model.Contains("Rosa") ? "cook" : model.Contains("Arthur") ? "officer"
                : person.IsChildOf(root) ? null : place == Place.Venue ? "reunion" : place == Place.School ? "student" : "neighbor";
            if (role == null) { count++; continue; }   // a walker set up above

            npc.npcName = name;
            npc.turnToTalk = !seated;
            npc.interactionRadius = 3f * Scale;
            npc.cameraDistance = 1.7f * Scale;
            var head = person.GetComponentsInChildren<Transform>(true).FirstOrDefault(b => b.name.Equals("head", StringComparison.OrdinalIgnoreCase));
            npc.faceHeight = head != null ? head.position.y - person.position.y + .1f * Scale : (seated ? 1.6f : 2.9f) * (seated ? 1 : 1);
            npc.enableDialogueChaining = true;
            npc.dialogueNodes = Dialogue(role, name + model);
            EditorUtility.SetDirty(npc);
            count++;
        }
        return count;
    }

    // Short, neutral small talk: a greeting, a question back, and one more line. No stress changes.
    static readonly Dictionary<string, string[][]> Lines = new Dictionary<string, string[][]>
    {
        { "student", new[] {
            new[] { "Hi. The room feels busy today.", "What are you working on?", "Just my notes. I like writing things down so I can see them." },
            new[] { "Oh, hey. I was looking out the window.", "What's out there?", "Clouds, mostly. They move slowly when you watch them." },
            new[] { "Hello. I like this seat by the wall.", "Why that one?", "It's quieter here. I can hear myself think." },
            new[] { "Hey. Did you see what's on the board?", "What does it say?", "Today's reminders. I copied them so I don't have to hold them in my head." } } },
        { "teacher", new[] { new[] { "Hello. The room is quiet right now.", "Any advice for today?", "Read one line at a time. Most things make more sense that way." } } },
        { "cook", new[] { new[] { "Hello, dear. Today we have rice and vegetables.", "What do you like to cook?", "Soup. It takes patience, and it always smells like home." } } },
        { "sam", new[] { new[] { "Hey. I'm right here if you want to talk later.", "What are you thinking about?", "Lunch, honestly. And whether I left my umbrella at home." } } },
        { "officer", new[] { new[] { "Hello there. The sidewalk's nice and clear.", "Is it busy around here?", "Not too bad. People come and go at their own pace." } } },
        { "neighbor", new[] {
            new[] { "Hi! Nice day for a walk.", "Where are you headed?", "Just around the block. Walking helps me clear my head." },
            new[] { "Hello. The trees on this street are getting tall.", "Have you lived here long?", "A few years. I still notice something new now and then." } } },
        { "jogger", new[] {
            new[] { "Hey! Just doing a few laps.", "How far do you run?", "It depends on the day. Some days are longer, some are shorter." },
            new[] { "Hi! Keeping a steady pace today.", "Do you run every day?", "Most days. I like the rhythm of it." } } },
        { "schoolwalker", new[] {
            new[] { "Hi! I'm heading to the library.", "What will you read?", "Something about the ocean. I like how big it is." },
            new[] { "Hey. I'm just stretching my legs between classes.", "Where are you going?", "Nowhere in particular. The yard is nice this time of day." } } },
        { "mom", new[] { new[] { "Hi, sweetheart. There's water in the fridge if you want some.", "What are you doing?", "Just tidying the kitchen. It's nice when everything has its place." } } },
        { "dad", new[] { new[] { "Hey, kiddo. I'm just reading for a bit.", "What are you reading?", "An article about old bridges. Some of them have stood for hundreds of years." } } },
        { "tim", new[] { new[] { "Hi! Do you want to see my drawing?", "What did you draw?", "A rocket. It's going to the moon, but it stops for snacks first." } } },
        { "reunion", new[] {
            new[] { "Hey! It's good to see you here.", "What have you been up to?", "A little of everything. Mostly figuring things out as I go." },
            new[] { "Hi! Remember this room? It looks different with the lights on.", "It does. How have you been?", "Busy, in a good way. I'm glad we're all here tonight." } } },
        { "justin", new[] { new[] { "Hey, I'm glad you made it.", "How's the reunion going?", "It's nice. Everyone talks a lot, but it's good noise." } } },
        { "guest", new[] {
            new[] { "Hi! I'm looking for the photo albums.", "Did you find them?", "Not yet. I keep stopping to say hello to people." },
            new[] { "Hello! It's a nice turnout tonight.", "Do you know everyone here?", "Some of them. The rest I'm meeting again for the first time." } } },
        { "tutorial", new[] {
            new[] { "Hi! Are you trying out the controls?", "Any tips?", "Move with W A S D and look around with the mouse. Take it slow at first." },
            new[] { "Hello! I'm just going for a walk around the field.", "It's a big field.", "It is. There's room to try things here." } } },
    };

    static List<NPCDialogueNode> Dialogue(string role, string seed)
    {
        var options = Lines.TryGetValue(role, out var set) ? set : Lines["neighbor"];
        var l = options[Mathf.Abs(seed.GetHashCode()) % options.Length];
        return new List<NPCDialogueNode>
        {
            new NPCDialogueNode { npcDialogue = l[0], playerChoices = new List<NPCDialogueChoice> {
                new NPCDialogueChoice { playerChoice = l[1], nextDialogueIndex = 1 },
                new NPCDialogueChoice { playerChoice = "See you later.", endsDialogue = true, nextDialogueIndex = -1 } } },
            new NPCDialogueNode { npcDialogue = l[2], playerChoices = new List<NPCDialogueChoice> {
                new NPCDialogueChoice { playerChoice = "Thanks. Talk to you later.", endsDialogue = true, nextDialogueIndex = -1 } } }
        };
    }

    // ================================================================ helpers

    static string DisplayName(string model)
    {
        if (model.Contains("Henderson")) return "Mr. Henderson";
        var parts = model.Split('_');
        return parts.Length >= 3 ? string.Join(" ", parts.Skip(2)) : model;
    }

    static AnimationClip Clip(string assetPath, string name) =>
        AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__") && (c.name == name || c.name.EndsWith("|" + name)));

    static Bounds Part(GameObject model, string name) => model.GetComponentsInChildren<Renderer>(true).First(r => r.name == name).bounds;

    static Bounds Bounds(GameObject obj)
    {
        var rs = obj.GetComponentsInChildren<Renderer>(true); var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds); return b;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude < 1e-4f ? Vector3.forward : v; }

    // The walkable surface below a point (ignoring roofs, ceilings and people).
    static Vector3 Ground(Vector3 point)
    {
        float best = float.MinValue;
        foreach (var hit in Physics.RaycastAll(new Vector3(point.x, 8f, point.z), Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore))
            if (hit.collider.GetComponentInParent<NPC>() == null && hit.point.y <= 1.5f && hit.point.y > best) best = hit.point.y;
        point.y = best == float.MinValue ? 0 : best;
        return point;
    }

    // A clear standing spot near 'near' (rings outward), on the floor.
    static Vector3 Free(Vector3 near)
    {
        for (int ring = 0; ring < 5; ring++)
            for (int i = 0; i < (ring == 0 ? 1 : 12); i++)
            {
                float a = i * Mathf.PI / 6, r = ring * .6f;
                var at = Ground(near + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
                if (!Physics.CheckCapsule(at + Vector3.up * .7f, at + Vector3.up * 3f, .55f, ~0, QueryTriggerInteraction.Ignore)) return at;
            }
        return Ground(near);
    }
}
