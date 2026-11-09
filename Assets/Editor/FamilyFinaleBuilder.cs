using System;
using System.Linq;
using Mindscape.FamilyFinale;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class FamilyFinaleBuilder
{
    const string Source = "Assets/Scenes/Chapter 2 - Episode 1.unity";
    static Transform root;
    static Material paper, wood, teal, gold, ink;
    // Superseded: Chapter 2 is built on Chapter 1's scenes by Mindscape > Chapter 2 - Family Expectations > Build All Episodes
    // (FamilyExpectationsBuilder.cs). The original scenes are kept in Assets/Scenes/Prototypes.

    // [MenuItem("Mindscape/Chapter 2 - Family Expectations/Build Episodes 4 and 5")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before building.");
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the open scene before building.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try { BuildEpisode(4); BuildEpisode(5); RegisterScenes(); AssetDatabase.SaveAssets(); }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        Debug.Log("Family finale built: Episodes 4 and 5, previews, and build settings.");
    }

    static void BuildEpisode(int episode)
    {
        // Work on a copy; the original bedroom scene remains untouched.
        var scene = EditorSceneManager.OpenScene(Source);
        BedroomLayout.Apply(scene);
        var original = scene.GetRootGameObjects().First(x => x.name == "Episode 1 - Family Expectations");
        var sourceStory = original.GetComponent<Mindscape.EpisodeOne.EpisodeOneStory>();
        var memory = sourceStory.memoryObjects.ToArray();
        Object.DestroyImmediate(sourceStory);
        foreach (var go in scene.GetRootGameObjects())
            if (!new[] { "Main Camera", "Player", "House And Lot", "Directional Light", "Global Volume", "EventSystem", original.name }.Contains(go.name))
                Object.DestroyImmediate(go);
        foreach (Transform t in original.transform.Cast<Transform>().ToArray())
            if (t.name.Contains("tableau") || t.name.Contains("camera mark") || t.name.Contains("Morning search")) Object.DestroyImmediate(t.gameObject);
        foreach (var t in original.GetComponentsInChildren<Transform>(true).Where(x => x.name == "Memory marker").ToArray()) Object.DestroyImmediate(t.gameObject);
        root = original.transform; root.name = "Episode " + episode + " - Family Finale";
        System.IO.Directory.CreateDirectory("Assets/FamilyFinale/Materials");
        System.IO.Directory.CreateDirectory("Assets/Resources/FamilyFinale");
        paper = Mat("Paper", new Color32(237, 224, 197, 255)); wood = Mat("Walnut", new Color32(105, 68, 47, 255));
        teal = Mat("Keepsake teal", new Color32(48, 99, 103, 255)); gold = Mat("Brass", new Color32(218, 170, 87, 255)); ink = Mat("Ink", new Color32(35, 42, 51, 255));
        var story = root.gameObject.AddComponent<FamilyFinaleEpisode>(); story.episodeNumber = episode;
        story.player = GameObject.Find("Player").transform; story.storyCamera = Camera.main;
        story.paper = memory[0]; story.bedroomSpawn = Pivot("Bedroom start", BedroomLayout.PlayerStart);
        story.bedroomSpawn.rotation = Quaternion.Euler(0, BedroomLayout.PlayerStartYaw, 0);
        story.bedroomView = Pivot("Bedroom view", new Vector3(-14.7f, 4.3f, -8.9f)); story.bedroomView.LookAt(story.paper.position);
        story.player.SetPositionAndRotation(story.bedroomSpawn.position, story.bedroomSpawn.rotation);
        story.player.gameObject.SetActive(true);
        var controls = story.player.GetComponent<PlayerControls>(); controls.enabled = false;
        controls.tasksOpen = controls.pauseOpen = controls.questOpen = controls.dialogueOpen = controls.minigameOpen = false;
        var movement = story.player.GetComponent<PlayerMovement>(); movement.enabled = true; movement.cameraTransform = story.storyCamera.transform;
        // Keep the bedroom doorway unobstructed for the walk to dinner and back: the doors stand open.
        var house = GameObject.Find("House And Lot");
        DoorSetup.LeaveOpen(house.transform, "Door_Bedroom_Left", "Door_Bedroom_Right");
        var sun = GameObject.Find("Directional Light").GetComponent<Light>(); sun.intensity = episode == 4 ? .65f : .15f;
        sun.color = new Color(1, .72f, .49f); sun.transform.rotation = Quaternion.Euler(12, -35, 0);
        story.eveningLight = original.GetComponentsInChildren<Light>(true).FirstOrDefault();
        if (story.eveningLight != null) story.eveningLight.intensity = 2;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.32f, .35f, .43f);
        RenderSettings.fog = false;
        story.bed = Pivot("Bed interaction", new Vector3(-13.2f, 1.4f, -12));
        story.bedView = Pivot("Night camera", new Vector3(-16.8f, 4.6f, -10)); story.bedView.LookAt(story.bed.position);

        // Use the house's authored dining table when present.
        var table = house.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name.ToLowerInvariant().Contains("dining") && r.name.ToLowerInvariant().Contains("table") && !r.name.ToLowerInvariant().Contains("leg"));
        Vector3 center = table != null ? table.bounds.center : new Vector3(-24, 2, -1);
        if (table != null) center.y = table.bounds.max.y;
        if (table == null) Box("Dinner table", center, new Vector3(3.2f, .18f, 2), wood, true);
        story.dinner = Pivot("Family dinner interaction", center + new Vector3(0, -.8f, -3.4f));
        story.dinnerView = Pivot("Dinner camera", center + new Vector3(0, 2.4f, -5.5f)); story.dinnerView.LookAt(center + Vector3.up * .5f);
        story.mother = Person("Mother", "Assets/FamilySet/Mother.fbx", center + new Vector3(-2.1f, 0, 0), 90);
        story.father = Person("Father", "Assets/FamilySet/Father.fbx", center + new Vector3(2.1f, 0, 0), 270);
        story.tim = Person("Tim", "Assets/FamilySet/Boy.fbx", center + new Vector3(0, 0, 2.7f), 180);
        for (int i = 0; i < 3; i++)
        {
            Vector3 p = center + new Vector3((i - 1) * .8f, .15f, -.25f);
            Box("Dinner plate", p, new Vector3(.6f, .035f, .55f), paper);
            Box("Rice bowl", p + Vector3.up * .07f, new Vector3(.25f, .12f, .25f), gold);
        }
        var light = new GameObject("Warm dinner light", typeof(Light)).GetComponent<Light>(); light.transform.SetParent(root); light.transform.position = center + Vector3.up * 2.8f;
        light.type = LightType.Point; light.color = new Color(1, .78f, .53f); light.range = 9; light.intensity = 2;

        story.keepsakes = new[] {
            Pivot("Old certificate", new Vector3(-15.3f, 2.48f, -6.65f)),   // clear of the photograph frame once it stands up
            Pivot("Old hobby item", new Vector3(-14.7f, 2.48f, -6.65f)),
            Pivot("Old notebook", new Vector3(-14, 2.48f, -6.65f))
        };
        var certificate = story.keepsakes[0];
        Box("Certificate frame", certificate.position, new Vector3(.62f, .045f, .8f), gold, false, certificate);
        Box("Certificate paper", certificate.position + Vector3.up * .025f, new Vector3(.55f, .012f, .72f), paper, false, certificate);
        for (int i = 0; i < 4; i++) Box("Certificate lettering", certificate.position + new Vector3(0, .035f, -.15f + i * .09f), new Vector3(.35f, .006f, .018f), ink, false, certificate);
        var hobby = story.keepsakes[1];
        Box("Sketchbook", hobby.position, new Vector3(.55f, .09f, .65f), teal, false, hobby);
        Box("Sketch page", hobby.position + Vector3.up * .05f, new Vector3(.5f, .015f, .6f), paper, false, hobby);
        for (int i = 0; i < 3; i++) Box("Drawing pencil", hobby.position + new Vector3(-.15f + i * .11f, .08f, 0), new Vector3(.035f, .035f, .48f), i == 1 ? gold : ink, false, hobby);
        var notebook = story.keepsakes[2]; Box("Old notebook cover", notebook.position, new Vector3(.55f, .13f, .72f), wood, false, notebook);
        Box("Notebook pages", notebook.position + Vector3.up * .02f, new Vector3(.51f, .07f, .68f), paper, false, notebook);
        Box("Notebook elastic", notebook.position + new Vector3(.15f, .075f, 0), new Vector3(.035f, .012f, .72f), teal, false, notebook);
        // Photograph is background dressing, not an extra required interaction.
        Box("Old photograph frame", new Vector3(-15.7f, 2.7f, -6.35f), new Vector3(.35f, .5f, .045f), wood);
        Box("Old photograph", new Vector3(-15.7f, 2.7f, -6.38f), new Vector3(.29f, .43f, .012f), teal);
        foreach (var k in story.keepsakes) k.gameObject.SetActive(episode == 5);
        if (episode == 5) { memory[0].gameObject.SetActive(false); memory[2].gameObject.SetActive(false); }
        story.storyCamera.transform.SetPositionAndRotation(story.bedroomView.position, story.bedroomView.rotation);
        CollectibleModels.Apply(scene);
        var path = "Assets/Scenes/" + FamilyFinaleEpisode.Scene(episode) + ".unity";
        EditorSceneManager.SaveScene(scene, path);
        Preview(story, episode);
    }

    static Transform Person(string name, string path, Vector3 at, float yaw)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) throw new InvalidOperationException("Missing family model: " + path);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, root); go.name = name;
        var rs = go.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        go.transform.localScale *= (name == "Tim" ? 3.1f : 4f) / Mathf.Max(.01f, b.size.y); go.transform.rotation = Quaternion.Euler(0, yaw, 0);
        b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        go.transform.position += new Vector3(at.x, .55f, at.z) - new Vector3(b.center.x, b.min.y, b.center.z);
        foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
        return go.transform;
    }
    static Transform Pivot(string name, Vector3 at) { var t = new GameObject(name).transform; t.SetParent(root); t.position = at; return t; }
    static GameObject Box(string name, Vector3 at, Vector3 size, Material mat, bool collider = false, Transform parent = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent != null ? parent : root);
        go.transform.position = at; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
    }
    static Material Mat(string name, Color color)
    {
        string path = "Assets/FamilyFinale/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path); if (m != null) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color }; AssetDatabase.CreateAsset(m, path); return m;
    }
    static void Preview(FamilyFinaleEpisode story, int episode)
    {
        var camera = story.storyCamera; var pos = camera.transform.position; var rot = camera.transform.rotation;
        if (episode == 4) camera.transform.SetPositionAndRotation(story.dinnerView.position, story.dinnerView.rotation);
        var rt = RenderTexture.GetTemporary(960, 540, 24); var old = RenderTexture.active; var target = camera.targetTexture;
        var texture = new Texture2D(960, 540, TextureFormat.RGB24, false);
        try { camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); texture.Apply(); System.IO.File.WriteAllBytes("Assets/Resources/FamilyFinale/Episode" + episode + ".png", texture.EncodeToPNG()); }
        finally { camera.targetTexture = target; RenderTexture.active = old; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(texture); camera.transform.SetPositionAndRotation(pos, rot); }
        AssetDatabase.ImportAsset("Assets/Resources/FamilyFinale/Episode" + episode + ".png");
    }
    static void RegisterScenes()
    {
        var list = EditorBuildSettings.scenes.ToList();
        foreach (int n in new[] { 4, 5 }) { var path = "Assets/Scenes/" + FamilyFinaleEpisode.Scene(n) + ".unity"; var e = list.FirstOrDefault(x => x.path == path); if (e == null) list.Add(new EditorBuildSettingsScene(path, true)); else e.enabled = true; }
        EditorBuildSettings.scenes = list.ToArray();
        foreach (var guid in AssetDatabase.FindAssets("t:BuildProfile"))
        {
            var profile = AssetDatabase.LoadAssetAtPath<UnityEditor.Build.Profile.BuildProfile>(AssetDatabase.GUIDToAssetPath(guid));
            if (profile == null || !profile.overrideGlobalScenes) continue;
            var scenes = profile.scenes.ToList();
            foreach (var e in list.Where(x => x.path.Contains("Chapter 2 - Episode 4") || x.path.Contains("Chapter 2 - Episode 5")))
            { var existing = scenes.FirstOrDefault(x => x.path == e.path); if (existing == null) scenes.Add(new EditorBuildSettingsScene(e.path, true)); else existing.enabled = true; }
            profile.scenes = scenes.ToArray(); EditorUtility.SetDirty(profile);
        }
    }
}
