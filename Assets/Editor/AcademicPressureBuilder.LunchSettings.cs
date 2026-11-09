using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class AcademicPressureBuilder
{
    const string LunchName = "Lunch";

    // A plate of food and a glass of water on the table in front of a seated student (Chapter 1 · Episode 3), so
    // the Sitting_Eating animation has a meal. Finds the tabletop straight ahead and sets the place on its near half;
    // when a "Lunch tray" is under that spot, the meal sits on the tray instead. Safe to re-run: the old one is
    // replaced. Returns false when there is no table in front of the person.
    public static bool LunchOnTable(Transform person)
    {
        if (person == null) return false;
        var old = person.Find(LunchName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        Physics.SyncTransforms();

        // Walk forward from the seat until the ray down meets a tabletop; that is the table's near edge.
        Vector3 forward = Vector3.ProjectOnPlane(person.forward, Vector3.up).normalized;
        RaycastHit top = default; float edge = -1;
        for (float d = .2f; d <= 2f; d += .05f)
        {
            foreach (var hit in Physics.RaycastAll(person.position + forward * d + Vector3.up * 4f, Vector3.down, 6f))
                if (hit.collider.name.EndsWith("_Table") && (edge < 0 || hit.point.y > top.point.y)) { top = hit; edge = d; }
            if (edge >= 0) break;
        }
        if (edge < 0) return false;

        // Sized for the 1.4x cast (about twice real size), like the classroom papers.
        const float plate = .5f, plateHeight = .024f;
        Vector3 centre = person.position + forward * (edge + plate * .5f + .08f);
        float surface = top.point.y;
        var tray = TrayUnder(centre);
        if (tray != null) surface = tray.bounds.max.y;
        centre.y = surface;

        var set = new GameObject(LunchName).transform;
        set.SetParent(person, true);
        set.SetPositionAndRotation(centre, Quaternion.LookRotation(forward));

        // The plate, with rice, a piece of fish or chicken, and greens.
        Round("Plate", set, new Vector3(0, plateHeight * .5f, 0), new Vector3(plate, plateHeight, plate), LunchMaterial("Paper", new Color32(246, 238, 220, 255)));
        Ball("Rice", set, new Vector3(-.07f, plateHeight + .03f, .05f), new Vector3(.2f, .09f, .17f), LunchMaterial("Ivory", new Color32(237, 224, 196, 255)));
        Ball("Main", set, new Vector3(.09f, plateHeight + .025f, .02f), new Vector3(.15f, .06f, .11f), LunchMaterial("Coral", new Color32(196, 112, 92, 255)));
        Ball("Greens", set, new Vector3(.01f, plateHeight + .02f, -.11f), new Vector3(.14f, .05f, .09f), LunchMaterial("Sage", new Color32(122, 143, 105, 255)));
        // A spoon resting on the plate's edge.
        var spoon = Prop("Spoon", set, new Vector3(.2f, plateHeight + .006f, -.04f), new Vector3(.03f, .008f, .22f), LunchMaterial("Spoon steel", new Color32(196, 199, 201, 255)));
        spoon.transform.localRotation = Quaternion.Euler(0, -18, 0);

        // A glass of water at the top right of the plate.
        const float glassHeight = .24f, glassWidth = .13f;
        Vector3 glassAt = new Vector3(plate * .5f + .1f, 0, plate * .25f);
        Round("Glass", set, glassAt + Vector3.up * glassHeight * .5f, new Vector3(glassWidth, glassHeight, glassWidth), LunchMaterial("Glass", new Color32(205, 226, 232, 255)));
        Round("Water", set, glassAt + Vector3.up * (glassHeight * .72f + .002f), new Vector3(glassWidth * 1.01f, .006f, glassWidth * 1.01f), LunchMaterial("Water", new Color32(126, 178, 201, 255)));
        return true;
    }

    // The tray (no collider) whose footprint contains 'at', if any.
    static Renderer TrayUnder(Vector3 at)
    {
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            if (r.name != "Lunch tray") continue;
            var b = r.bounds;
            if (at.x >= b.min.x && at.x <= b.max.x && at.z >= b.min.z && at.z <= b.max.z) return r;
        }
        return null;
    }

    // A cylinder of the given full size (Unity's cylinder is 2 units tall, so its Y scale is halved).
    static GameObject Round(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material)
    {
        var o = Shape(PrimitiveType.Cylinder, name, parent, localPosition, material);
        o.transform.localScale = new Vector3(size.x, size.y * .5f, size.z);
        return o;
    }

    static GameObject Ball(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material)
    {
        var o = Shape(PrimitiveType.Sphere, name, parent, localPosition, material);
        o.transform.localScale = size;
        return o;
    }

    static GameObject Shape(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Material material)
    {
        var o = GameObject.CreatePrimitive(type); o.name = name;
        Object.DestroyImmediate(o.GetComponent<Collider>()); // table props never block the player
        o.transform.SetParent(parent, false);
        o.transform.localPosition = localPosition; o.transform.localRotation = Quaternion.identity;
        var r = o.GetComponent<Renderer>(); r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return o;
    }

    // The shared Chapter 1 material by name, created (once) with this colour if it does not exist yet.
    static Material LunchMaterial(string name, Color color) => AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/" + name + ".mat") ?? Mat(name, color);
}
