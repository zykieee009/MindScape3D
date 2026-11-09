using System;
using System.Linq;
using Mindscape.EpisodeThree;
using Mindscape.Minigames;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class FamilyEpisodeThreeBuilder
{
    public const string ScenePath = "Assets/Scenes/" + EpisodeThreeStory.SceneName + ".unity";
    static Transform root;
    static Material cream, teal, dark, gold, skin, wood, sage;    // Superseded: Chapter 2 is built on Chapter 1's scenes by Mindscape > Chapter 2 - Family Expectations > Build All Episodes
    // (FamilyExpectationsBuilder.cs). The original scenes are kept in Assets/Scenes/Prototypes.

    // [MenuItem("Mindscape/Chapter 2 - Family Expectations/Episode 3/Build Running Out of Time")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before building.");
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the open scene before building Episode 3.");
        foreach (var dir in new[]{"Assets/EpisodeThree/Materials", "Assets/Resources/EpisodeThree"}) System.IO.Directory.CreateDirectory(dir);
        // A copied player carries the exact same model and controller as the previous episode.
        var source = EditorSceneManager.OpenScene("Assets/Scenes/" + Mindscape.EpisodeTwo.EpisodeTwoStory.SceneName + ".unity");
        var sourcePlayer = source.GetRootGameObjects().First(x => x.name == "Player");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
        var player = Object.Instantiate(sourcePlayer); player.name = "Player"; SceneManager.MoveGameObjectToScene(player, scene);
        var camera = Object.Instantiate(source.GetRootGameObjects().First(x => x.name == "Main Camera")); camera.name = "Main Camera"; SceneManager.MoveGameObjectToScene(camera, scene);
        EditorSceneManager.CloseScene(source, true);
        root = new GameObject("Episode 3 - Running Out of Time").transform;
        cream = Mat("Ivory", new Color32(237, 224, 196, 255)); teal = Mat("School teal", new Color32(53, 98, 100, 255));
        dark = Mat("Ink", new Color32(34, 45, 49, 255)); gold = Mat("Brass", new Color32(220, 174, 94, 255));
        skin = Mat("Warm skin", new Color32(179, 126, 91, 255)); wood = Mat("Cedar", new Color32(123, 83, 56, 255)); sage = Mat("Garden sage", new Color32(122, 143, 105, 255));
        var story = root.gameObject.AddComponent<EpisodeThreeStory>(); story.player = player.transform; story.storyCamera = camera.GetComponent<Camera>();
        var classroom = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Classroom_LowPoly_Furnished.glb"), root);
        classroom.name = "Furnished classroom"; classroom.transform.localScale = Vector3.one * 2;
        Transform Find(string name) => classroom.GetComponentsInChildren<Transform>(true).First(x => x.name == name);
        // Normalize the imported classroom: entry faces the negative Z corridor, board faces the room.
        var door = Find("Door"); if (door.position.z > 0) classroom.transform.rotation = Quaternion.Euler(0, 180, 0);
        Vector3 doorway = door.position; door.gameObject.SetActive(false); Find("Door_Handle").gameObject.SetActive(false);
        foreach (var filter in classroom.GetComponentsInChildren<MeshFilter>())
            if (filter.sharedMesh != null && filter.GetComponent<Collider>() == null) filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
        var sun = new GameObject("Directional Light", typeof(Light)).GetComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.35f; sun.color = new Color(1, .91f, .77f); sun.transform.rotation = Quaternion.Euler(48, -32, 0); sun.shadows = LightShadows.Soft;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.68f, .73f, .75f); RenderSettings.fog = true; RenderSettings.fogColor = new Color(.79f, .83f, .78f); RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = .006f;
        story.storyCamera.backgroundColor = new Color(.79f, .83f, .78f); story.storyCamera.clearFlags = CameraClearFlags.SolidColor; story.storyCamera.farClipPlane = 150;
        var hall = Pivot("School corridor", Vector3.zero);
        Box("Corridor floor", new Vector3(0, -.17f, -11), new Vector3(24, .3f, 6), cream, hall);
        Box("Corridor ceiling", new Vector3(0, 6.2f, -11), new Vector3(24, .18f, 6.4f), cream, hall);
        Box("Garden", new Vector3(0, -.35f, -22), new Vector3(80, .35f, 18), sage, hall);
        Box("Arrival lane", new Vector3(0, -.12f, -20), new Vector3(75, .2f, 5), dark, hall);
        Box("Hall railing", new Vector3(0, 1, -13.9f), new Vector3(24, .16f, .12f), teal, hall);
        foreach (float x in new[]{-11.5f, -6, 0, 6, 11.5f})
        { Box("Colonnade", new Vector3(x, 3, -13.75f), new Vector3(.35f, 6, .35f), teal, hall); Box("Railing post", new Vector3(x, .5f, -13.9f), new Vector3(.12f, 1, .12f), teal, hall); }
        Boundary("Corridor garden boundary", new Vector3(0, 3, -14), new Vector3(24, 6, .1f));
        Boundary("West end", new Vector3(-12, 3, -11), new Vector3(.15f, 6, 6)); Boundary("East end", new Vector3(12, 3, -11), new Vector3(.15f, 6, 6));
        Box("West corridor return", new Vector3(-11, 3, -8), new Vector3(2.1f, 6, .2f), cream, hall);
        Box("East corridor return", new Vector3(11, 3, -8), new Vector3(2.1f, 6, .2f), cream, hall);
        Box("Room plaque", new Vector3(doorway.x, 4.8f, -8.05f), new Vector3(2.8f, .7f, .12f), teal);
        Label("03 / EXAMINATION", new Vector3(doorway.x, 4.8f, -8.13f), Quaternion.identity, 2.6f, .5f, 2.4f, cream.color);
        Label("QUIET PLEASE", new Vector3(doorway.x - 2.8f, 3.1f, -8.15f), Quaternion.identity, 2.3f, .6f, 2.2f, dark.color);
        story.entrance = Pivot("Knock here", new Vector3(doorway.x, 1.6f, -8.9f));
        story.spawn = Pivot("Corridor start", new Vector3(-doorway.x, .1f, -11)); story.spawn.rotation = Quaternion.Euler(0, doorway.x > 0 ? 90 : 270, 0);
        player.transform.SetPositionAndRotation(story.spawn.position, story.spawn.rotation);
        var movement = player.GetComponent<PlayerMovement>(); movement.cameraTransform = camera.transform; movement.enabled = true; movement.cameraOffset = new Vector3(0, .45f, -3.5f); movement.cameraTargetHeight = 2.1f; movement.minDistance = 1.5f; movement.maxDistance = 5;
        player.GetComponent<PlayerControls>().enabled = false;
        // Choose the back-row end chair nearest the entrance, leaving a clear central aisle.
        var chairs = classroom.GetComponentsInChildren<Transform>().Where(x => x.name.EndsWith("Wood_Seat")).OrderBy(x => Vector3.Distance(x.position, doorway)).ToArray();
        var ownChair = chairs[0]; var ownPos = ownChair.GetComponent<Renderer>().bounds.center;
        story.seat = Pivot("Joey seat interaction", new Vector3(ownPos.x, 1.6f, ownPos.z - 1.6f));
        story.seatView = Pivot("Seated examination view", ownPos + new Vector3(0, 2.4f, -.15f)); story.seatView.LookAt(Find("Chalkboard").position + Vector3.down * .6f);
        story.seatMarker = Box("Empty seat gold marker", ownPos + Vector3.up * 2.5f, Vector3.one * .25f, gold, null, false); story.seatMarker.transform.rotation = Quaternion.Euler(0, 45, 45);
        foreach (var chair in chairs.Skip(1)) SeatedStudent(chair.GetComponent<Renderer>().bounds.center, chair.GetSiblingIndex());
        var teacher = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FamilySet/Father.fbx"), root); teacher.name = "Teacher";
        var bounds = BoundsOf(teacher); teacher.transform.localScale *= 3.3f / Mathf.Max(.01f, bounds.size.y);
        bounds = BoundsOf(teacher); teacher.transform.position += new Vector3(doorway.x - Mathf.Sign(doorway.x) * 1.5f, 0, -5.7f) - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        teacher.transform.rotation = Quaternion.Euler(0, 180, 0);
        story.teacherView = Pivot("Teacher conversation view", new Vector3(doorway.x, 3.2f, -9.5f)); story.teacherView.LookAt(teacher.transform.position + Vector3.up * 2.3f);
        var board = Find("Chalkboard").GetComponent<Renderer>().bounds;
        Label("GENERAL STUDIES\n\nBegin with what you know.\nReview your work before handing it in.", board.center + Vector3.back * .12f, Quaternion.identity, board.size.x * .9f, board.size.y * .8f, 3.8f, cream.color);
        var clock = GameObject.CreatePrimitive(PrimitiveType.Cylinder); clock.name = "Classroom clock"; clock.transform.SetParent(root); clock.transform.SetPositionAndRotation(new Vector3(0, 5.55f, 7.48f), Quaternion.Euler(90, 0, 0)); clock.transform.localScale = new Vector3(.9f, .035f, .9f); clock.GetComponent<Renderer>().sharedMaterial = cream; Object.DestroyImmediate(clock.GetComponent<Collider>());
        Box("Clock minute hand", new Vector3(0, 5.7f, 7.38f), new Vector3(.035f, .3f, .02f), dark, null, false); Box("Clock hour hand", new Vector3(.1f, 5.55f, 7.37f), new Vector3(.2f, .035f, .02f), dark, null, false);
        var bag = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EpisodeTwo/Prefabs/School bag.prefab"), root); bag.name = "Joey school bag"; bag.transform.position = ownPos + new Vector3(-.75f, -.5f, .1f); RemoveGlints(bag);
        var kit = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EpisodeTwo/Prefabs/Examination materials.prefab"), root); kit.name = "Joey examination materials"; kit.transform.position = ownPos + new Vector3(.8f, .55f, .25f); kit.transform.localScale = Vector3.one * .7f; RemoveGlints(kit);
        var bus = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EpisodeTwo/Prefabs/Commuter bus.prefab"), root); bus.name = "Arrival bus"; bus.SetActive(true); bus.transform.position = new Vector3(-6, 0, -20); story.bus = bus.transform;
        story.openingView = Pivot("School arrival view", new Vector3(12, 9, -26)); story.openingView.LookAt(new Vector3(0, 2, -9));
        story.storyCamera.transform.SetPositionAndRotation(story.openingView.position, story.openingView.rotation);
        MinigameUI.EnsureInput();
        CollectibleModels.Apply(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        var scenes = EditorBuildSettings.scenes.ToList(); var entry = scenes.FirstOrDefault(x => x.path == ScenePath); if (entry == null) scenes.Add(new EditorBuildSettingsScene(ScenePath, true)); else entry.enabled = true; EditorBuildSettings.scenes = scenes.ToArray();
        foreach (var guid in AssetDatabase.FindAssets("t:BuildProfile"))
        {
            var profile = AssetDatabase.LoadAssetAtPath<UnityEditor.Build.Profile.BuildProfile>(AssetDatabase.GUIDToAssetPath(guid)); if (profile == null || !profile.overrideGlobalScenes) continue;
            var list = profile.scenes.Select(x => new EditorBuildSettingsScene(x.path, x.enabled)).ToList(); var match = list.FirstOrDefault(x => x.path == ScenePath);
            if (match == null) list.Add(new EditorBuildSettingsScene(ScenePath, true)); else match.enabled = true;
            profile.scenes = new EditorBuildSettingsScene[0]; profile.scenes = list.ToArray(); EditorUtility.SetDirty(profile);
        }
        AssetDatabase.SaveAssets(); CapturePreview(); Debug.Log("Episode 3 built: " + ScenePath);
    }
    static void SeatedStudent(Vector3 seat, int index)
    {
        var p = Pivot("Student at desk " + index, seat);
        Box("Uniform", seat + Vector3.up * .65f, new Vector3(.78f, 1.1f, .5f), index % 2 == 0 ? cream : teal, p, false);
        Box("Hair", seat + Vector3.up * 1.54f, new Vector3(.64f, .25f, .59f), dark, p, false);
        Box("Head", seat + Vector3.up * 1.28f, new Vector3(.58f, .53f, .55f), skin, p, false);
        foreach (float x in new[]{-.24f, .24f})
        {
            Box("Trouser leg", seat + new Vector3(x, -.28f, .5f), new Vector3(.29f, .72f, .3f), dark, p, false);
            Box("Knee", seat + new Vector3(x, .025f, .29f), new Vector3(.29f, .3f, .65f), dark, p, false);
            Box("Shoe", seat + new Vector3(x, -.63f, .63f), new Vector3(.33f, .19f, .48f), dark, p, false);
            Box("Sleeve", seat + new Vector3(x * 1.8f, .64f, .1f), new Vector3(.23f, .63f, .26f), cream, p, false);
            Box("Writing arm", seat + new Vector3(x * 1.8f, .40f, .44f), new Vector3(.21f, .2f, .66f), skin, p, false);
        }
        Box("Exam paper", seat + new Vector3(.74f, .53f, .35f), new Vector3(.5f, .015f, .6f), cream, p, false);
    }
    static void RemoveGlints(GameObject obj) { foreach (var t in obj.GetComponentsInChildren<Transform>()) if (t.name == "Objective glint") Object.DestroyImmediate(t.gameObject); }
    static Bounds BoundsOf(GameObject obj) { var renderers = obj.GetComponentsInChildren<Renderer>(); var b = renderers[0].bounds; foreach (var r in renderers) b.Encapsulate(r.bounds); return b; }
    static Transform Pivot(string name, Vector3 position) { var t = new GameObject(name).transform; t.SetParent(root); t.position = position; return t; }
    static void Boundary(string name, Vector3 position, Vector3 size) { var p = Pivot(name, position); p.gameObject.AddComponent<BoxCollider>().size = size; }
    static GameObject Box(string name, Vector3 position, Vector3 size, Material material, Transform parent = null, bool collider = true)
    { var o = GameObject.CreatePrimitive(PrimitiveType.Cube); o.name = name; o.transform.SetParent(parent == null ? root : parent); o.transform.position = position; o.transform.localScale = size; o.GetComponent<Renderer>().sharedMaterial = material; if (!collider) Object.DestroyImmediate(o.GetComponent<Collider>()); return o; }
    static void Label(string words, Vector3 position, Quaternion rotation, float width, float height, float size, Color color)
    { var o = new GameObject(words, typeof(TextMeshPro)); o.transform.SetParent(root); o.transform.SetPositionAndRotation(position, rotation); var t = o.GetComponent<TextMeshPro>(); t.font = MinigameUI.Font(false); t.text = words; t.fontSize = size; t.alignment = TextAlignmentOptions.Center; t.color = color; t.rectTransform.sizeDelta = new Vector2(width, height); }
    static Material Mat(string name, Color color)
    { string path = "Assets/EpisodeThree/Materials/" + name + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path); if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); } material.color = color; material.SetFloat("_Smoothness", .12f); return material; }
    // [MenuItem("Mindscape/Chapter 2 - Family Expectations/Episode 3/Capture Classroom Preview")]
    public static void CapturePreview()
    {
        if (EditorApplication.isPlaying) return;
        var story = Object.FindAnyObjectByType<EpisodeThreeStory>(); if (story == null) return;
        var cam = story.storyCamera; var oldPosition = cam.transform.position; var oldRotation = cam.transform.rotation; var oldTarget = cam.targetTexture; var oldActive = RenderTexture.active;
        var rt = new RenderTexture(1280, 720, 24); var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            cam.transform.position = new Vector3(-7.8f, 5.5f, -6.6f); cam.transform.LookAt(new Vector3(0, 2.5f, 2)); cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
            const string path = "Assets/Resources/EpisodeThree/ClassroomPreview.png"; System.IO.File.WriteAllBytes(path, texture.EncodeToPNG()); AssetDatabase.ImportAsset(path);
        }
        finally { cam.targetTexture = oldTarget; RenderTexture.active = oldActive; cam.transform.SetPositionAndRotation(oldPosition, oldRotation); Object.DestroyImmediate(rt); Object.DestroyImmediate(texture); }
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }
}
