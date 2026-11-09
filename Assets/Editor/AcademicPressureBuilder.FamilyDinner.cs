using System.Linq;
using Mindscape.FamilyFinale;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class AcademicPressureBuilder
{
    const string DinnerSetName = "Dinner set";

    // Chapter 2 · Episode 4's family dinner: the house's dining table, made longer, with four of its chairs (two per side),
    // Mom, Dad and Tim sitting and eating (Sitting_Eating), a plate and a glass at every place, and the fourth seat left for
    // you. The house's own table and two chairs are hidden in this episode only. Safe to re-run on the saved scene.
    [MenuItem(FamilyMenu + "Episode 4 - Update the Dinner Table")]
    public static void UpdateDinnerTable()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play mode first."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(FamilyPath(4), OpenSceneMode.Single);
        var story = Object.FindAnyObjectByType<FamilyFinaleEpisode>();
        if (story == null || story.episodeNumber != 4) { Debug.LogWarning("Chapter 2 - Episode 4 has no Episode 4 story."); return; }
        DinnerTable(story);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("Chapter 2 · Episode 4 dinner table updated.");
    }

    public static void DinnerTable(FamilyFinaleEpisode story)
    {
        var house = GameObject.Find("House And Lot").transform;
        var table = house.Find("Dining table"); var north = house.Find("Dining chair | north"); var south = house.Find("Dining chair | south");
        foreach (var t in new[] { table, north, south }) t.gameObject.SetActive(true); // inactive renderers report empty bounds
        Physics.SyncTransforms();
        Bounds tb = table.GetComponent<Renderer>().bounds, nb = north.GetComponent<Renderer>().bounds, sb = south.GetComponent<Renderer>().bounds;
        foreach (var t in new[] { table, north, south }) t.gameObject.SetActive(false);

        // Start clean: the previous dinner set, and the first build's loose plates and bowls.
        var old = story.transform.Find(DinnerSetName); if (old != null) Object.DestroyImmediate(old.gameObject);
        foreach (var t in story.transform.Cast<Transform>().Where(t => t.name == "Dinner plate" || t.name == "Rice bowl").ToArray()) Object.DestroyImmediate(t.gameObject);
        var set = new GameObject(DinnerSetName).transform; set.SetParent(story.transform, false);

        // Longer along the room (east-west), nudged west so the walkway by the kitchen counter stays open.
        const float stretch = 1.65f, westShift = .5f;
        float centreX = tb.center.x - westShift, floorY = tb.min.y, topY = tb.max.y;
        var newTable = CopyFurniture(table, "Dinner_Table", set, new Vector3(stretch, 1, 1));   // "_Table": LunchOnTable looks for it
        Physics.SyncTransforms();
        newTable.position += new Vector3(centreX - newTable.GetComponent<Renderer>().bounds.center.x, 0, 0);

        // Two chairs per long side. Seats: [north west, north east, south west, south east].
        float spread = tb.size.x * stretch * .25f;
        var chairs = new[]
        {
            Chair(north, set, "Dinner chair | north west", new Vector3(centreX - spread, 0, nb.center.z), nb),
            Chair(north, set, "Dinner chair | north east", new Vector3(centreX + spread, 0, nb.center.z), nb),
            Chair(south, set, "Dinner chair | south west", new Vector3(centreX - spread, 0, sb.center.z), sb),
            Chair(south, set, "Dinner chair | south east", new Vector3(centreX + spread, 0, sb.center.z), sb)
        };
        Physics.SyncTransforms();
        Vector3 tableCentre = new Vector3(centreX, topY, tb.center.z);
        Vector3 Seat(Transform chair) => SeatTop(chair, tableCentre);
        Quaternion Facing(Transform chair) => Quaternion.LookRotation(new Vector3(0, 0, Mathf.Sign(tableCentre.z - chair.GetComponent<Renderer>().bounds.center.z)));

        // You sit north west, nearest the bedroom door; Tim beside you; Mom and Dad across the table, facing you.
        SitDown(story.mother, FamilyModels[0], Seat(chairs[2]), Facing(chairs[2]), AdultHeight);
        SitDown(story.father, FamilyModels[1], Seat(chairs[3]), Facing(chairs[3]), AdultHeight);
        SitDown(story.tim, FamilyModels[2], Seat(chairs[1]), Facing(chairs[1]), ChildHeight);
        var yours = new GameObject("Your place").transform; yours.SetParent(set, false);
        yours.SetPositionAndRotation(Seat(chairs[0]), Facing(chairs[0]));
        // A plate of food and a glass at each place; dinner plates are bigger than the cafeteria's, so they read from the camera.
        const float dinnerware = 1.7f;
        foreach (var person in new[] { story.mother, story.father, story.tim, yours })
        {
            if (!LunchOnTable(person)) { Debug.LogWarning("Dinner: no table in front of " + person.name); continue; }
            var meal = person.Find(LunchName);
            meal.localScale = Vector3.one * dinnerware;
            meal.position += person.forward * .25f * (dinnerware - 1) / .7f;   // keep the bigger plate on the table, clear of its edge
            // LunchOnTable takes its height at the table's edge; this table's edge is rounded, so sit the plate on the top itself.
            if (newTable.GetComponent<Collider>().Raycast(new Ray(meal.position + Vector3.up * 3, Vector3.down), out var top, 6))
                meal.position = new Vector3(meal.position.x, top.point.y, meal.position.z);
        }

        // Something to share in the middle.
        var dish = new GameObject("Serving dish").transform; dish.SetParent(set, false); dish.position = tableCentre;
        Round("Dish", dish, new Vector3(0, .02f, 0), new Vector3(.75f, .04f, .75f), LunchMaterial("Paper", new Color32(246, 238, 220, 255)));
        Ball("Vegetables", dish, new Vector3(-.1f, .07f, .05f), new Vector3(.36f, .12f, .3f), LunchMaterial("Sage", new Color32(122, 143, 105, 255)));
        Ball("Main", dish, new Vector3(.13f, .07f, -.06f), new Vector3(.3f, .1f, .24f), LunchMaterial("Coral", new Color32(196, 112, 92, 255)));
        var pot = new GameObject("Rice pot").transform; pot.SetParent(set, false); pot.position = tableCentre + new Vector3(-spread * 1.05f, 0, 0);
        Round("Pot", pot, new Vector3(0, .13f, 0), new Vector3(.5f, .26f, .5f), LunchMaterial("Ink pot", new Color32(58, 74, 78, 255)));
        Ball("Rice", pot, new Vector3(0, .26f, 0), new Vector3(.44f, .1f, .44f), LunchMaterial("Ivory", new Color32(237, 224, 196, 255)));

        // Join from behind your chair; the camera looks over it at the family.
        Vector3 yourSeat = yours.position, away = -yours.forward;
        story.dinner.position = new Vector3(yourSeat.x, floorY + 1.4f, yourSeat.z) + away * 1.3f;
        story.dinnerView.position = new Vector3(yourSeat.x, floorY, yourSeat.z) + away * 3.4f + Vector3.up * 5.6f + Vector3.left * .6f;
        story.dinnerView.LookAt(tableCentre + Vector3.up * .9f);
        var light = story.transform.Find("Warm dinner light"); if (light != null) light.position = tableCentre + Vector3.up * 2.8f;
        EditorUtility.SetDirty(story);
    }

    // A copy of a piece of the house's furniture (its mesh, materials and a collider), scaled on top of its own size.
    static Transform CopyFurniture(Transform source, string name, Transform parent, Vector3 extraScale)
    {
        var copy = new GameObject(name).transform; copy.SetParent(parent, true);
        copy.SetPositionAndRotation(source.position, source.rotation);
        copy.localScale = Vector3.Scale(source.lossyScale, extraScale);
        var mesh = source.GetComponent<MeshFilter>().sharedMesh;
        copy.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        copy.gameObject.AddComponent<MeshRenderer>().sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
        copy.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
        return copy;
    }

    // A chair copy whose centre lands on 'at' (x/z), keeping its height and the way it faces.
    static Transform Chair(Transform source, Transform parent, string name, Vector3 at, Bounds sourceBounds)
    {
        var chair = CopyFurniture(source, name, parent, Vector3.one);
        chair.position += new Vector3(at.x - sourceBounds.center.x, 0, at.z - sourceBounds.center.z);
        return chair;
    }

    // The top of a chair's seat: straight down onto the chair, a little toward the table from its middle (clear of the backrest).
    static Vector3 SeatTop(Transform chair, Vector3 tableCentre)
    {
        var collider = chair.GetComponent<Collider>();
        var b = chair.GetComponent<Renderer>().bounds;
        Vector3 toTable = new Vector3(0, 0, Mathf.Sign(tableCentre.z - b.center.z));
        Vector3 from = new Vector3(b.center.x, b.max.y + 1, b.center.z) + toTable * b.size.z * .15f;
        if (collider.Raycast(new Ray(from, Vector3.down), out var hit, b.size.y + 2)) return hit.point;
        Debug.LogWarning("Dinner: no seat found on " + chair.name);
        return new Vector3(b.center.x, b.min.y + b.size.y * .4f, b.center.z);
    }

    // Moves a family member onto a seat and gives them the Sitting_Eating pose, keeping their NPC setup (name, small talk).
    static void SitDown(Transform person, string modelPath, Vector3 seat, Quaternion facing, float height)
    {
        person.SetPositionAndRotation(seat, facing);
        foreach (var block in person.GetComponents<BoxCollider>()) Object.DestroyImmediate(block);   // the chair is in the way already
        var old = person.Find("Model"); if (old != null) Object.DestroyImmediate(old.gameObject);
        foreach (var meal in person.Cast<Transform>().Where(t => t.name == LunchName).ToArray()) Object.DestroyImmediate(meal.gameObject);

        var body = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), person); body.name = "Model";
        body.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        var rest = BoundsOf(body); body.transform.localScale *= height / Mathf.Max(.01f, rest.size.y);
        var motion = Clip(modelPath, LunchClip);
        if (motion != null)
        {
            motion.SampleAnimation(body, 0);
            var animator = body.GetComponent<Animator>(); if (animator == null) animator = body.AddComponent<Animator>();
            animator.runtimeAnimatorController = Controller(System.IO.Path.GetFileNameWithoutExtension(modelPath), LunchClip, motion);
            animator.applyRootMotion = false;
        }
        else Debug.LogWarning(modelPath + " has no '" + LunchClip + "' clip.");
        var hips = Hips(body.transform);
        if (hips != null) body.transform.position += seat + Vector3.up * height * .05f - person.forward * height * .03f - hips.position;   // as Person() seats people
        foreach (var c in body.GetComponentsInChildren<Collider>(true)) c.enabled = false;

        var npc = person.GetComponent<NPC>();
        if (npc != null) { npc.turnToTalk = false; EditorUtility.SetDirty(npc); }   // seated: keep facing the table while talking
        EditorUtility.SetDirty(person.gameObject);
    }
}
