using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// School_Grounds.glb furnishes only Room 03 (the story's classroom); Rooms 01, 02 and 04 along the same corridor were
// bare shells with a floor that stops halfway, so walking into one showed an empty box. This copies Room 03's
// furniture (its rows of double desks and seats, the teacher's desk and chair, the bookcase, the books) and its full
// floor into the other three rooms, in every scene with the campus. Room 03's own furniture is untouched. Copies are
// named "Furnished | Room 0N | ..." under the campus, so running it again replaces them.
public static class SchoolSpareClassrooms
{
    const string Prefix = "Furnished | ";
    static readonly (string room, float dx)[] Rooms = { ("Room 04", -20), ("Room 02", 20), ("Room 01", 40) };
    static readonly string[] Furniture = { "Double_Desk", "Seat_", "Teacher_Desk", "Detail_Teacher", "Detail_Bookcase", "Floor" };

    [MenuItem("Mindscape/School/Furnish Empty Classrooms in All Scenes")]
    public static void ApplyAllMenu()
    {
        string active = SceneManager.GetActiveScene().path;
        foreach (var path in SchoolModelSync.FindScenes())
        {
            if (path.Contains("/Prototypes/")) continue;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int n = Apply(scene);
            if (n == 0) continue;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("Classrooms · " + Path.GetFileNameWithoutExtension(path) + ": " + n + " parts");
        }
        if (!string.IsNullOrEmpty(active)) EditorSceneManager.OpenScene(active, OpenSceneMode.Single);
    }

    public static int Apply(Scene scene)
    {
        int made = 0;
        foreach (var campus in SchoolModelSync.FindCampuses(scene))
        {
            var t = campus.transform;
            foreach (var old in t.GetComponentsInChildren<Transform>(true).Where(c => c != null && c.name.StartsWith(Prefix, StringComparison.Ordinal)).ToArray())
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var source = t.GetComponentsInChildren<Transform>(true)
                .Where(c => c.name.StartsWith("Room 03 | ", StringComparison.Ordinal) && Furniture.Any(f => c.name.Substring(10).StartsWith(f, StringComparison.Ordinal)))
                .ToArray();
            if (source.Length == 0) continue;
            foreach (var (room, dx) in Rooms)
            {
                // A room that already has desks (or was removed from this campus) is left alone.
                var parts = t.GetComponentsInChildren<Transform>(true).Where(c => c.name.StartsWith(room + " | ", StringComparison.Ordinal)).ToArray();
                if (parts.Length == 0 || parts.Any(c => c.name.Contains("Desk"))) continue;
                // Its own half floor gives way to the full copy.
                foreach (var floor in parts.Where(c => c.name == room + " | Floor")) floor.gameObject.SetActive(false);
                Vector3 offset = new Vector3(dx, 0, 0);
                foreach (var part in source)
                {
                    var copy = UnityEngine.Object.Instantiate(part.gameObject, part.parent);
                    copy.name = Prefix + room + " | " + part.name.Substring(10);
                    copy.transform.position = part.position + offset;
                    copy.transform.rotation = part.rotation;
                    copy.SetActive(part.gameObject.activeSelf);
                    made++;
                }
            }
        }
        return made;
    }
}
