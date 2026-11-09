using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Populates the Chapter 1 house street with reusable NPC prefabs, walk routes,
// ambient chatter, and player-facing dialogue. Safe to run repeatedly.
public static class StreetLifeBuilder
{
    const string ScenePath = "Assets/Scenes/Chapter 1 - Episode 1.unity";
    const string RootName = "Street Life";

    struct StreetNPC
    {
        public string prefab, name;
        public Vector3 position;
        public float yaw;
        public string greeting, detail;
        public StreetNPC(string prefab, string name, Vector3 position, float yaw, string greeting, string detail)
        { this.prefab = prefab; this.name = name; this.position = position; this.yaw = yaw; this.greeting = greeting; this.detail = detail; }
    }

    static readonly StreetNPC[] People =
    {
        new StreetNPC("NPC_01_Maya", "Maya", new Vector3(-17f, .25f, 21.8f), 90f,
            "Hey! The neighborhood is already waking up.", "I like walking before school. It gives my thoughts a little room."),
        new StreetNPC("NPC_03_Leo", "Leo", new Vector3(-7f, .25f, 23.1f), 90f,
            "Morning! I'm taking the long way to the bus stop.", "A short walk helps me feel ready for the day."),
        new StreetNPC("NPC_02_Nia", "Nia", new Vector3(7.5f, .25f, 21.8f), 90f,
            "Hi, Joey! Hana and I were talking about today's presentation.", "We're both nervous, so we promised to encourage each other."),
        new StreetNPC("NPC_04_Hana", "Hana", new Vector3(10.2f, .25f, 21.8f), 270f,
            "Good morning, Joey. One step at a time today.", "Preparation matters, but being kind to yourself matters too."),
        new StreetNPC("NPC_07_Malik", "Malik", new Vector3(15f, .25f, 23.4f), 90f,
            "Nice morning for some fresh air, isn't it?", "The street feels calmer before everyone reaches school."),
        new StreetNPC("NPC_20_Officer_Arthur", "Officer Arthur", new Vector3(24.2f, .25f, 22f), 180f,
            "Morning! The school bus will pull in here shortly.", "Stay on the sidewalk and wait behind the curb. I'll let you know when it's safe to board.")
    };

    [MenuItem("Mindscape/Chapter 1 - Academic Pressure/Populate Episode 1 Street", priority = 24)]
    public static void PopulateOpenScene()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play mode first."); return; }
        var scene = SceneManager.GetActiveScene().path == ScenePath
            ? SceneManager.GetActiveScene()
            : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Populate(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    public static void Populate(Scene scene)
    {
        var player = Object.FindAnyObjectByType<PlayerControls>()?.transform;
        if (player == null) { Debug.LogError("Street life: no player in scene."); return; }

        var old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);
        foreach (var manager in Object.FindObjectsByType<NPCInteractionManager>(FindObjectsInactive.Include))
            Object.DestroyImmediate(manager.gameObject);

        var root = new GameObject(RootName).transform;
        var managerRoot = new GameObject("NPC System", typeof(NPCInteractionManager));
        managerRoot.transform.SetParent(root, false);

        var npcs = new Dictionary<string, NPC>();
        foreach (var person in People)
        {
            string path = "Assets/Prefabs/NPCs/" + person.prefab + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogWarning("Street life: missing " + path); continue; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            go.name = "Street NPC - " + person.name;
            go.transform.SetPositionAndRotation(Ground(person.position), Quaternion.Euler(0f, person.yaw, 0f));
            go.transform.localScale = Vector3.one * AcademicPressureBuilder.PeopleScale;   // same scale as the player
            var npc = go.GetComponent<NPC>();
            npc.interactionRadius *= AcademicPressureBuilder.PeopleScale;
            npc.faceHeight *= AcademicPressureBuilder.PeopleScale;
            npc.cameraDistance *= AcademicPressureBuilder.PeopleScale;
            npc.npcName = person.name;
            npc.modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/NPCs/" + person.prefab + ".fbx");
            if (npc.modelPrefab == null)
                npc.modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/NPCs/" + person.prefab + ".glb");
            npc.enableDialogueChaining = true;
            npc.dialogueNodes = Dialogue(person.greeting, person.detail);
            npc.RefreshModel();
            Animate(go.transform.Find("Model"), person.prefab);
            // No floating name tags on the street; the "[ E ] Talk to" prompt already shows who you are facing.
            foreach (var tag in go.GetComponentsInChildren<NPCNameTag>(true)) Object.DestroyImmediate(tag.gameObject);
            npcs[person.name] = npc;
            EditorUtility.SetDirty(go);
        }

        AddRoute(root, npcs, "Maya", new[] { new Vector3(-17, 0, 21.8f), new Vector3(-8, 0, 21.8f), new Vector3(1, 0, 21.8f) }, .95f);
        AddRoute(root, npcs, "Leo", new[] { new Vector3(-7, 0, 23.1f), new Vector3(2, 0, 23.1f), new Vector3(12, 0, 23.1f) }, 1.15f);
        AddRoute(root, npcs, "Malik", new[] { new Vector3(15, 0, 23.4f), new Vector3(20, 0, 23.4f), new Vector3(27, 0, 23.4f) }, 1.05f);

        if (npcs.TryGetValue("Nia", out var nia) && npcs.TryGetValue("Hana", out var hana))
        {
            var talk = new GameObject("Nia and Hana ambient conversation", typeof(NPCAmbientConversation));
            talk.transform.SetParent(root, false);
            var ambient = talk.GetComponent<NPCAmbientConversation>();
            ambient.first = nia; ambient.second = hana;
            ambient.lines = new[]
            {
                "Did you finish your notes?",
                "Almost. I'm trying not to rush.",
                "We can practice together on the bus.",
                "Good idea. That makes it feel manageable."
            };
        }

        // Keep the currently imported character as the player's visible model and refresh its animator link.
        var movement = player.GetComponent<PlayerMovement>();
        var latestModel = player.Find("BaldCharacter");
        if (movement != null && latestModel != null) movement.animator = latestModel.GetComponentInChildren<Animator>(true);

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Street life populated: " + npcs.Count + " interactable NPCs, 3 walking routes, and 1 ambient conversation pair.");
    }

    static List<NPCDialogueNode> Dialogue(string greeting, string detail) => new List<NPCDialogueNode>
    {
        new NPCDialogueNode
        {
            npcDialogue = greeting,
            playerChoices = new List<NPCDialogueChoice>
            {
                new NPCDialogueChoice { playerChoice = "How is your morning going?", nextDialogueIndex = 1 },
                new NPCDialogueChoice { playerChoice = "See you later.", endsDialogue = true, nextDialogueIndex = -1 }
            }
        },
        new NPCDialogueNode
        {
            npcDialogue = detail,
            playerChoices = new List<NPCDialogueChoice>
            {
                new NPCDialogueChoice { playerChoice = "Thanks for telling me.", stressChange = -1f, endsDialogue = true, nextDialogueIndex = -1 }
            }
        }
    };

    // The roster FBXs import without an Animator. Give each street model one whose Idle and Walk clips
    // (from its own FBX) follow the "Speed" value NPCWander sets while it walks.
    static void Animate(Transform model, string modelName)
    {
        if (model == null) return;
        string fbx = "Assets/Characters/NPCs/" + modelName + ".fbx";
        if (AssetImporter.GetAtPath(fbx) is ModelImporter importer && importer.clipAnimations.Length == 0)
        {
            // Imported clips do not loop by default; loop everything except the one-shot sit-down.
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips) clip.loopTime = !clip.name.Contains("Sit_Down");
            importer.clipAnimations = clips; importer.SaveAndReimport();
        }
        AnimationClip Clip(string name)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(fbx))
                if (asset is AnimationClip c && !c.name.StartsWith("__preview__") && (c.name == name || c.name.EndsWith("|" + name))) return c;
            return null;
        }
        AnimationClip idle = Clip("Idle"), walk = Clip("Walk");
        if (idle == null || walk == null) { Debug.LogWarning("Street life: " + modelName + " has no Idle/Walk clip; it stays in its rest pose."); return; }

        const string folder = "Assets/AcademicPressure/Animators";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/AcademicPressure", "Animators");
        string path = folder + "/" + modelName + " Street.controller";
        AssetDatabase.DeleteAsset(path);   // rebuilt each run so it always points at the current clips
        var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        var machine = controller.layers[0].stateMachine;
        var idleState = machine.AddState("Idle"); idleState.motion = idle;
        var walkState = machine.AddState("Walk"); walkState.motion = walk;
        machine.defaultState = idleState;
        var start = idleState.AddTransition(walkState); start.hasExitTime = false; start.duration = .15f;
        start.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Greater, .1f, "Speed");
        var stop = walkState.AddTransition(idleState); stop.hasExitTime = false; stop.duration = .2f;
        stop.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Less, .1f, "Speed");

        var animator = model.GetComponent<Animator>();
        if (animator == null) animator = model.gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
        idle.SampleAnimation(model.gameObject, 0);   // stand naturally in the editor instead of the bind pose
    }

    static void AddRoute(Transform root, Dictionary<string, NPC> npcs, string npcName, Vector3[] points, float speed)
    {
        if (!npcs.TryGetValue(npcName, out var npc)) return;
        var route = new GameObject(npcName + " sidewalk route").transform;
        route.SetParent(root, false);
        var waypoints = new List<Transform>();
        for (int i = 0; i < points.Length; i++)
        {
            var point = new GameObject("Waypoint " + (i + 1)).transform;
            point.SetParent(route, false);
            point.position = Ground(points[i]);
            waypoints.Add(point);
        }
        var wander = npc.GetComponent<NPCWander>();
        if (wander == null) wander = npc.gameObject.AddComponent<NPCWander>();
        wander.waypoints = waypoints;
        wander.pattern = NPCWander.Pattern.PingPong;
        wander.walkSpeed = speed * AcademicPressureBuilder.PeopleScale;
        wander.pauseSeconds = new Vector2(1.5f, 4f);
    }

    static Vector3 Ground(Vector3 point)
    {
        float best = point.y;
        foreach (var hit in Physics.RaycastAll(new Vector3(point.x, 8f, point.z), Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore))
            if (hit.collider.GetComponentInParent<NPC>() == null && hit.point.y <= 1.5f && hit.point.y > best) best = hit.point.y;
        point.y = best;
        return point;
    }
}
