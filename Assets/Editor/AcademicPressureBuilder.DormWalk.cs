using System.Collections.Generic;
using System.Linq;
using Mindscape.AcademicPressure;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class AcademicPressureBuilder
{
    // Episode 4 begins outside the cafeteria after lunch: walk the corridor to the dormitory (gold markers), go in
    // through its door (it opens and closes from both sides, so you can step out and back in), and find three
    // roommates studying at the shared table. Safe to run again: the "Dorm walk" group is rebuilt each time.
    const string DormWalkGroup = "Dorm walk";
    static readonly (string model, string name, int stool, string[] lines)[] Roommates =
    {
        ("NPC_08_Zara", "Zara", 1, new[] { "Hey, welcome back. We're all deep in our notes tonight.", "What are you working on?", "Chemistry review. One chapter, then a short break." }),
        ("NPC_12_Noah", "Noah", 5, new[] { "Oh, hi. There's water by the shelves if you want some.", "How's your studying going?", "Slow, honestly. But slow still moves." }),
        ("NPC_14_Mei", "Mei", 7, new[] { "Hi. I kept your desk lamp from being borrowed.", "Thanks. Are you almost done?", "Not really. I'm just finishing this page first." }),
    };

    [MenuItem(Menu + "Episode 4 - Add the Walk From the Cafeteria", priority = 40)]
    public static void MenuDormWalk()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play mode first."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath(4), OpenSceneMode.Single);
        var story = Object.FindAnyObjectByType<DormitoryEpisode>(FindObjectsInactive.Include);
        if (story == null) { Debug.LogError("No DormitoryEpisode in " + scene.path); return; }
        root = story.transform;
        Materials();
        PrepareCast();
        var school = story.transform.Find("School grounds");
        AddDormWalk(story, school != null ? school.gameObject : null);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Episode 4: walk from the cafeteria and roommates added to " + scene.path);
    }

    static void AddDormWalk(DormitoryEpisode story, GameObject school)
    {
        if (school == null) throw new System.InvalidOperationException("School grounds not found under the episode.");
        var old = root.Find(DormWalkGroup);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var group = Pivot(DormWalkGroup, Vector3.zero);
        Physics.SyncTransforms();

        Bounds cafFloor = Part(school, "Cafeteria | Floor"), cafHeader = Part(school, "Cafeteria | Front_Door_Header");
        Bounds dormFloor = Part(school, "Dormitory | Floor"), dormHeader = Part(school, "Dormitory | Front_Door_Header");
        Bounds corridor = Part(school, "Corridor | floor");
        Vector3 cafOut = Axis(cafHeader.center - cafFloor.center), dormOut = Axis(dormHeader.center - dormFloor.center);
        Vector3 cafDoor = new Vector3(cafHeader.center.x, 0, cafHeader.center.z), dormDoor = new Vector3(dormHeader.center.x, 0, dormHeader.center.z);

        // Just outside the cafeteria door, facing along the corridor toward the dormitory.
        Vector3 start = Surface(cafDoor.x + cafOut.x * 2.6f, cafDoor.z + cafOut.z * 2.6f, 0, 3);
        story.walkStart = Pivot("Outside the cafeteria", start + Vector3.up * .1f);
        story.walkStart.rotation = Quaternion.LookRotation(Flat(new Vector3(dormDoor.x, 0, corridor.center.z) - start));
        story.walkStart.SetParent(group, true);

        // Inside the dorm, past the door: the spot the old episode started on.
        story.dormInside = story.spawn;

        story.routeMarkers = Route("Route to the dormitory", start, new Vector3(start.x, 0, corridor.center.z),
            new Vector3(dormDoor.x, 0, corridor.center.z), dormDoor + dormOut * 1.6f);
        story.routeMarkers.transform.SetParent(group, true);

        // Roommates studying at the shared table (the player's own seat and the one opposite stay free).
        var seats = new List<Transform>();
        var tableTop = Part(school, "Dormitory | Detail_Study_Table_Top");
        foreach (var mate in Roommates)
        {
            var stool = Part(school, "Dormitory | Detail_Study_Stool_" + mate.stool);
            Vector3 seat = new Vector3(stool.center.x, stool.max.y, stool.center.z);
            Vector3 toTable = Flat(new Vector3(tableTop.center.x, 0, stool.center.z) - seat);
            var person = Person("Roommate · " + mate.name, mate.model, seat, Quaternion.LookRotation(toTable), true, ClassroomClip);
            person.SetParent(group, true);
            var npc = person.gameObject.AddComponent<NPC>();
            npc.npcName = mate.name;
            npc.turnToTalk = false;
            npc.interactionRadius = 3f * PeopleScale;
            npc.cameraDistance = 1.7f * PeopleScale;
            var head = person.GetComponentsInChildren<Transform>(true).FirstOrDefault(b => b.name.Equals("head", System.StringComparison.OrdinalIgnoreCase));
            npc.faceHeight = head != null ? head.position.y - person.position.y + .1f * PeopleScale : 1.6f;
            npc.enableDialogueChaining = true;
            npc.dialogueNodes = new List<NPCDialogueNode>
            {
                new NPCDialogueNode { npcDialogue = mate.lines[0], playerChoices = new List<NPCDialogueChoice> {
                    new NPCDialogueChoice { playerChoice = mate.lines[1], nextDialogueIndex = 1 },
                    new NPCDialogueChoice { playerChoice = "I'll let you focus.", endsDialogue = true, nextDialogueIndex = -1 } } },
                new NPCDialogueNode { npcDialogue = mate.lines[2], playerChoices = new List<NPCDialogueChoice> {
                    new NPCDialogueChoice { playerChoice = "Good luck. Talk later.", endsDialogue = true, nextDialogueIndex = -1 } } }
            };
            EditorUtility.SetDirty(npc);
            seats.Add(person);
            // An open notebook in front of each roommate.
            Vector3 onTable = new Vector3(Mathf.Lerp(seat.x, tableTop.center.x, .55f), tableTop.max.y, seat.z);
            Box("Notebook", onTable + Vector3.up * .02f, new Vector3(.5f, .03f, .65f), paper, person, false).transform.rotation = Quaternion.LookRotation(toTable) * Quaternion.Euler(0, 8, 0);
        }
        story.roommates = seats.ToArray();

        var player = story.player;
        if (player != null) player.SetPositionAndRotation(story.walkStart.position, story.walkStart.rotation);
        if (story.storyCamera != null)
            story.storyCamera.transform.SetPositionAndRotation(story.walkStart.position + Vector3.up * 5 - story.walkStart.forward * 5, Quaternion.LookRotation(story.walkStart.forward));
        EditorUtility.SetDirty(story);
    }
}
