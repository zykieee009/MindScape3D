using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Gives every doorway in a scene a real, animated door (Assets/Prefabs/Doors, see DoorPrefabBuilder) in place of the static panel the
// models carry. The panel is switched off and renamed "<name> (old panel)" (a model instance cannot lose children), and the door is
// fitted to the opening the panel filled: its leaf is as wide and tall as the panel was.
//   House And Lot:  Door_Entrance, Door_Bathroom, Door_Bedroom_Left, Door_Bedroom_Right
//   School grounds: "Room 01-04 | Door" (Reunion Hall copies lack some panels; the door header marks the doorway then), "Cafeteria |
//                   Front_Door", "Dormitory | Front_Door". The cafeteria's own closed door set (AcademicPressureBuilder.CafeteriaDoors)
//                   is switched off for the new door.
// A doorway whose panel was switched off (the story walks through it) gets a door that starts open, so the way stays free and the
// door can still be shut. Every door is usable; the story no longer locks any.
// Safe to re-run: a doorway that already has an animated door is left alone. Called by WorldModelSync.Apply, so every builder keeps them.
public static class DoorSetup
{
    const float DoorwayHeight = 4.4f;   // school doorways, floor to header, in world units (the school is 2x in every scene)
    static readonly Regex SchoolPart = new Regex(@"^(Room \d+|Cafeteria|Dormitory) \| (Door|Front_Door|Door_Header|Front_Door_Header)$");

    static string HousePrefab(string name) => name == "Door_Bathroom" ? "Door_Modern" : "Door_Classic";
    static string SchoolPrefab(string building) => building.StartsWith("Room") ? "Door_Modern" : building == "Cafeteria" ? "Door_Rustic" : "Door_Classic";

    class Slot
    {
        public string name, prefab;
        public Transform parent;
        public Vector3 bottom, normal;
        public float width, height, range = -1f;
        public bool startOpen;
        public Transform[] retire = new Transform[0];   // panels (and handles) the door replaces
    }

    // How many doors were placed.
    public static int Apply(Scene scene)
    {
        var roots = scene.GetRootGameObjects().Where(g => g.name != WorldModelSync.InstanceName).ToArray();
        var placed = roots.SelectMany(g => g.GetComponentsInChildren<DoorInteraction>(true)).Where(d => d.model != null).ToList();
        int count = 0;
        foreach (var slot in HouseSlots(roots).Concat(SchoolSlots(roots)))
        {
            var door = placed.FirstOrDefault(d => Vector3.Distance(Flat(d.transform.position), Flat(slot.bottom)) < .3f);
            if (door != null && Mathf.Abs(door.leafSize.y * door.transform.lossyScale.y - slot.height) > slot.height * .05f)
            {
                // Placed with the wrong measurements by an earlier pass: stand it again (its panel is gone, so it keeps its open flag).
                slot.startOpen |= door.startOpen;
                placed.Remove(door);
                Object.DestroyImmediate(door.gameObject);
                door = null;
            }
            if (door == null)
            {
                door = Place(slot);
                if (door == null) continue;
                placed.Add(door); count++;
            }
            door.enabled = true;    // an earlier builder pass may have locked it
        }
        // The cafeteria's old closed door set stands where the new door does.
        foreach (var set in roots.SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => t.name == "Cafeteria entrance doors"))
            set.gameObject.SetActive(false);
        if (count > 0) Physics.SyncTransforms();
        return count;
    }

    // The story walks through these doorways (the bedroom to the dinner table and back): the doors start open. A door not placed yet
    // is still the model's panel, which is switched off so the opening it leaves is read as open when the door is placed.
    public static void LeaveOpen(Transform root, params string[] names)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(t => names.Contains(t.name)).ToArray())
        {
            var door = t.GetComponent<DoorInteraction>();
            if (door != null && door.model != null) door.startOpen = true;
            else t.gameObject.SetActive(false);
        }
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    static System.Collections.Generic.IEnumerable<Slot> HouseSlots(GameObject[] roots)
    {
        foreach (var house in roots.Where(g => g.name == "House And Lot"))
            foreach (var panel in house.transform.Cast<Transform>().Where(t => t.name.StartsWith("Door_") && t.GetComponent<MeshRenderer>() != null).ToArray())
            {
                var b = panel.GetComponent<MeshRenderer>().bounds;
                bool acrossX = b.size.x < b.size.z;     // the panel is thin across the doorway
                var legacy = panel.GetComponent<DoorInteraction>();
                yield return new Slot
                {
                    name = panel.name, prefab = HousePrefab(panel.name), parent = house.transform,
                    bottom = new Vector3(b.center.x, b.min.y, b.center.z),
                    normal = acrossX ? Vector3.right : Vector3.forward,
                    width = acrossX ? b.size.z : b.size.x, height = b.size.y,
                    range = legacy != null ? legacy.interactRange : -1f,
                    startOpen = !panel.gameObject.activeSelf,
                    retire = new[] { panel },
                };
            }
    }

    static System.Collections.Generic.IEnumerable<Slot> SchoolSlots(GameObject[] roots)
    {
        var parts = roots.SelectMany(g => g.GetComponentsInChildren<Renderer>(true)).Where(r => SchoolPart.IsMatch(r.name)).ToArray();
        bool cafeteriaSet = roots.SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Any(t => t.name == "Cafeteria entrance doors" && t.gameObject.activeInHierarchy);
        foreach (var header in parts.Where(r => r.name.EndsWith("Header")))
        {
            string building = header.name.Substring(0, header.name.IndexOf(" |"));
            var mine = parts.Where(r => r.transform.parent == header.transform.parent && r.name.StartsWith(building + " |")).ToArray();
            var panel = mine.FirstOrDefault(r => !r.name.EndsWith("Header"));
            var hb = header.bounds;
            bool acrossX = hb.size.x < hb.size.z;
            float bottomY = panel != null ? panel.bounds.min.y : hb.min.y - DoorwayHeight;
            var handles = header.transform.parent.Cast<Transform>().Where(t => t.name.StartsWith(building + " | Door_Handle")).ToArray();
            yield return new Slot
            {
                name = building + " | Door", prefab = SchoolPrefab(building), parent = header.transform.parent,
                bottom = new Vector3(hb.center.x, bottomY, hb.center.z),
                normal = acrossX ? Vector3.right : Vector3.forward,
                width = (acrossX ? hb.size.z : hb.size.x) * .92f,   // the panel is a little narrower than the header over it
                height = hb.min.y - bottomY,
                startOpen = panel != null && !panel.gameObject.activeSelf && !(building == "Cafeteria" && cafeteriaSet),
                retire = (panel != null ? new[] { panel.transform } : new Transform[0]).Concat(handles).ToArray(),
            };
        }
    }

    static DoorInteraction Place(Slot slot)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabBuilder.PrefabPath(slot.prefab));
        if (prefab == null) { Debug.LogWarning("Doors: " + slot.prefab + " is missing, run Mindscape > Doors > Rebuild Door Prefabs."); return null; }
        var size = prefab.GetComponent<DoorInteraction>().leafSize;

        foreach (var old in slot.retire)
        {
            if (old.name.EndsWith("(old panel)")) continue;
            old.gameObject.SetActive(false);
            old.name += " (old panel)";
        }
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, slot.parent);
        instance.name = slot.name;
        instance.transform.SetPositionAndRotation(slot.bottom, Quaternion.LookRotation(slot.normal));
        // The leaf fills the opening; width and depth scale together (a swinging leaf shears if they differ), height on its own.
        float across = slot.width / size.x;
        Vector3 world = new Vector3(across, slot.height / size.y, across), parent = slot.parent.lossyScale;
        instance.transform.localScale = new Vector3(world.x / parent.x, world.y / parent.y, world.z / parent.z);
        var door = instance.GetComponent<DoorInteraction>();
        door.startOpen = slot.startOpen;
        if (slot.range > 0f) door.interactRange = slot.range;
        return door;
    }

    [MenuItem("Mindscape/Doors/Apply Doors to Open Scene")]
    public static void ApplyOpenSceneMenu()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play mode first."); return; }
        var scene = SceneManager.GetActiveScene();
        int placed = Apply(scene);
        Debug.Log("Doors, " + scene.name + ": " + placed + " placed.");
        if (placed > 0) EditorSceneManager.MarkSceneDirty(scene);
    }

    [MenuItem("Mindscape/Doors/Apply Doors to All Story Scenes")]
    public static void ApplyAllMenu()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play mode first."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string active = SceneManager.GetActiveScene().path;
        foreach (var path in Directory.GetFiles("Assets/Scenes", "*.unity").Where(p => Path.GetFileName(p).StartsWith("Chapter ")).OrderBy(p => p))
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int placed = Apply(scene);
            Debug.Log("Doors, " + scene.name + ": " + placed + " placed.");
            if (placed > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
        }
        if (File.Exists(active)) EditorSceneManager.OpenScene(active, OpenSceneMode.Single);
    }
}
