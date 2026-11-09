using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class AcademicPressureBuilder
{
    const string DeskPaperName = "Desk paper";

    // A sheet of paper and a pencil on the desk in front of a seated student (Chapter 1 · Episode 2), so the
    // Sitting_Writing animation writes on something. Finds the desktop straight ahead of the person and lays the
    // sheet on its near half, where the writing hands rest. Safe to re-run: the old sheet is replaced.
    // Returns false when there is no desk in front of the person.
    public static bool PaperOnDesk(Transform person)
    {
        if (person == null) return false;
        var old = person.Find(DeskPaperName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        Physics.SyncTransforms();

        // Walk forward from the seat until the ray down meets a desktop; that is the desk's near edge.
        Vector3 forward = Vector3.ProjectOnPlane(person.forward, Vector3.up).normalized;
        RaycastHit top = default; float edge = -1;
        for (float d = .3f; d <= 2f; d += .05f)
        {
            foreach (var hit in Physics.RaycastAll(person.position + forward * d + Vector3.up * 4f, Vector3.down, 6f))
                if (hit.collider.name.Contains("Desk") && hit.collider.name.EndsWith("_Top") && (edge < 0 || hit.point.y > top.point.y)) { top = hit; edge = d; }
            if (edge >= 0) break;
        }
        if (edge < 0) return false;

        // Sized for the 1.4x cast: roughly an A4 sheet in their hands.
        const float width = .42f, length = .58f, thickness = .006f;
        // A small, stable tilt per student so the room does not look stamped out.
        float tilt = (Mathf.Abs(person.name.GetHashCode()) % 17 - 8) * 1.1f;
        Quaternion facing = Quaternion.LookRotation(forward) * Quaternion.Euler(0, tilt, 0);
        Vector3 centre = person.position + forward * (edge + length * .5f + .06f);
        centre.y = top.point.y + thickness * .5f;

        var set = new GameObject(DeskPaperName).transform;
        set.SetParent(person, true);
        set.SetPositionAndRotation(centre, facing);
        Prop("Sheet", set, Vector3.zero, new Vector3(width, thickness, length), DeskMaterial("Paper"));
        // A pencil lying beside the sheet, on the writing-hand side.
        var pencil = Prop("Pencil", set, new Vector3(width * .5f + .07f, .016f, -.02f), new Vector3(.032f, .032f, .34f), DeskMaterial("Brass"));
        pencil.transform.localRotation = Quaternion.Euler(0, 12, 0);
        Prop("Lead", pencil.transform, new Vector3(0, 0, .53f), new Vector3(.6f, .6f, .06f), DeskMaterial("Ink"));
        return true;
    }

    static GameObject Prop(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material)
    {
        var o = GameObject.CreatePrimitive(PrimitiveType.Cube); o.name = name;
        Object.DestroyImmediate(o.GetComponent<Collider>()); // props never block the player
        o.transform.SetParent(parent, false);
        o.transform.localPosition = localPosition; o.transform.localRotation = Quaternion.identity; o.transform.localScale = size;
        var r = o.GetComponent<Renderer>(); r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return o;
    }

    static Material DeskMaterial(string name) => AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/" + name + ".mat");
}
