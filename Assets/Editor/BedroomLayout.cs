using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Joey's bedroom (the "West bedroom" of the house). The door is in the west wall; the study desk used
// to start right at that wall and covered half the doorway. This lays the room out so the door has a
// clear landing: the wardrobe sits in the north-east corner, the desk beside it, and the school bag by
// the bed. Works on either the Episode 1 or the Episode 2 scene and can be re-run safely (it sets
// absolute positions, it never nudges by an offset it can't undo).
public static class BedroomLayout
{
    // Interior of the room, world units: x -18.16 .. -10.13, z -15.60 .. -6.08. Door: west wall, z -8.6 .. -6.3.
    const float WardrobeMaxX = -10.15f, WardrobeMaxZ = -6.10f;   // back and right side flush with the walls
    static readonly Vector3 DeskCenter = new Vector3(-14.7f, 2.3f, -6.6f);          // desk top
    static readonly Vector3 DeskScale = new Vector3(2.2f, 0.18f, 1.0f);              // x -15.8 .. -13.6, z -7.1 .. -6.1
    static readonly Vector3 ChairFloorPosition = new Vector3(-14.7f, 0.55f, -7.5f);   // tucked in under the desk
    static readonly Vector3 PaperPosition = new Vector3(-15.25f, 2.41f, -6.7f);
    static readonly Vector3 LampPosition = new Vector3(-14.65f, 2.43f, -6.3f);
    static readonly Vector3 NotebookPosition = new Vector3(-14.0f, 2.46f, -6.65f);
    static readonly Vector3 BagPosition = new Vector3(-17.3f, 1.2f, -10.5f);
    public static readonly Vector3 PlayerStart = new Vector3(-16.4f, 0.65f, -8.6f);
    public const float PlayerStartYaw = 35f;                                          // faces the desk

    [MenuItem("Mindscape/Chapter 2 - Family Expectations/Episode 1/Redesign Bedroom Layout (open scene)")]
    public static void ApplyToOpenScene()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play mode first."); return; }
        var scene = SceneManager.GetActiveScene();
        string report = Apply(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Bedroom layout applied to '" + scene.name + "':\n" + report);
    }

    // Absolute: the wardrobe's back meets the north wall and its right side the east wall. Returns how far it moved.
    public static float WardrobeToCorner(Transform house)
    {
        string[] wardrobeParts = { "West bedroom | wardrobe", "West bedroom | cabinet door", "West bedroom | cabinet door.001", "West bedroom | cabinet pull", "West bedroom | cabinet pull.001" };
        var parts = house.Cast<Transform>().Where(t => wardrobeParts.Contains(t.name)).ToArray();
        var wardrobe = parts.FirstOrDefault(t => t.name == "West bedroom | wardrobe");
        if (wardrobe == null) return 0f;
        var renderers = wardrobe.GetComponentsInChildren<Renderer>();
        var shift = new Vector3(WardrobeMaxX - renderers.Max(r => r.bounds.max.x), 0, WardrobeMaxZ - renderers.Max(r => r.bounds.max.z));
        if (shift.sqrMagnitude > 1e-6f) foreach (var p in parts) p.position += shift;
        Physics.SyncTransforms();
        return shift.magnitude;
    }

    public static string Apply(Scene scene)
    {
        var sb = new System.Text.StringBuilder();
        var roots = scene.GetRootGameObjects();
        var house = roots.FirstOrDefault(g => g.name == "House And Lot");
        var storyGo = roots.FirstOrDefault(g => g.name.StartsWith("Episode 1 - Family") || g.name.StartsWith("Episode 2 - Before"));
        if (house == null || storyGo == null) return "Nothing to do: this scene has no house or story root.";
        var story = storyGo.transform;

        // 1. Wardrobe to the north-east corner.
        sb.AppendLine("wardrobe moved " + WardrobeToCorner(house.transform).ToString("0.00") + " m");

        // 2. Desk, legs, lamp and light.
        var desk = Find(story, "Study desk");
        if (desk != null) { desk.position = DeskCenter; desk.localScale = DeskScale; }
        var legs = story.Cast<Transform>().Where(t => t.name == "Desk leg").ToArray();
        float left = DeskCenter.x - DeskScale.x / 2f + 0.15f, right = DeskCenter.x + DeskScale.x / 2f - 0.15f;
        float south = DeskCenter.z - DeskScale.z / 2f + 0.1f, north = DeskCenter.z + DeskScale.z / 2f - 0.1f;
        for (int i = 0; i < legs.Length && i < 4; i++)
            legs[i].position = new Vector3(i < 2 ? left : right, legs[i].position.y, i % 2 == 0 ? south : north);
        var lamp = Find(story, "Desk lamp base"); if (lamp != null) lamp.position = LampPosition;
        var deskLight = Find(story, "Evening desk light"); if (deskLight != null) deskLight.position = new Vector3(DeskCenter.x, 3.1f, DeskCenter.z + 0.1f);
        sb.AppendLine("desk centred at x " + DeskCenter.x + ", " + legs.Length + " legs");

        // 3. Things that sit on the desk or on the floor.
        var paper = Find(story, "Examination paper interaction"); if (paper != null) paper.position = PaperPosition;
        var materials = Find(story, "Examination materials"); if (materials != null) materials.position = new Vector3(PaperPosition.x, 2.43f, PaperPosition.z);
        var notebook = Find(story, "Study notebook interaction"); if (notebook != null) notebook.position = NotebookPosition;
        var bag = Find(story, "School bag memory interaction"); if (bag != null) bag.position = BagPosition;
        var fragments = Find(story, "Morning search fragments");
        if (fragments != null)
        {
            var id = Find(fragments, "Student ID"); if (id != null) id.position = new Vector3(-17.65f, id.position.y, -9.6f);
            var photo = Find(fragments, "ID photo"); if (photo != null) photo.position = new Vector3(-17.65f, photo.position.y, -9.5f);
            var notes = Find(fragments, "Dropped revision notes"); if (notes != null) notes.position = new Vector3(-17.0f, notes.position.y, -11.3f);
        }
        var studyView = Find(story, "Late study camera");
        if (studyView != null) studyView.LookAt(new Vector3(DeskCenter.x - 0.1f, 2.5f, DeskCenter.z));

        // 4. A chair, tucked in at the desk so the floor in front of the bed stays open.
        if (Find(story, "Study chair") == null && desk != null)
        {
            var wood = desk.GetComponent<Renderer>().sharedMaterial;
            var teal = AssetDatabase.LoadAssetAtPath<Material>("Assets/EpisodeOne/Materials/Notebook teal.mat");
            var chair = new GameObject("Study chair").transform; chair.SetParent(story); chair.position = ChairFloorPosition;
            Part(chair, "Seat", new Vector3(0, 1.07f, 0), new Vector3(.95f, .14f, .95f), teal != null ? teal : wood);
            Part(chair, "Back", new Vector3(0, 1.72f, -.44f), new Vector3(.95f, 1.1f, .12f), wood);
            foreach (float x in new[] { -.4f, .4f }) foreach (float z in new[] { -.4f, .4f })
                Part(chair, "Leg", new Vector3(x, .5f, z), new Vector3(.12f, 1.0f, .12f), wood);
            sb.AppendLine("chair added");
        }

        var chairNow = Find(story, "Study chair"); if (chairNow != null) chairNow.position = ChairFloorPosition;

        // 5. Joey's start (the story script also teleports here when the episode begins).
        var player = roots.FirstOrDefault(g => g.name == "Player");
        if (player != null && story.name.StartsWith("Episode 1"))
        {
            var cc = player.GetComponent<CharacterController>(); bool was = cc != null && cc.enabled; if (cc != null) cc.enabled = false;
            player.transform.SetPositionAndRotation(PlayerStart, Quaternion.Euler(0, PlayerStartYaw, 0));
            if (cc != null) cc.enabled = was;
        }
        return sb.ToString();
    }

    static Transform Find(Transform parent, string name) { return parent.Cast<Transform>().FirstOrDefault(t => t.name == name); }

    static void Part(Transform parent, string name, Vector3 localOffset, Vector3 scale, Material material)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = name;
        cube.transform.SetParent(parent, false); cube.transform.localPosition = localOffset; cube.transform.localScale = scale;
        if (material != null) cube.GetComponent<Renderer>().sharedMaterial = material;
    }
}
