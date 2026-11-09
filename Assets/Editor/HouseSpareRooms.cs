using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// The house had two empty rooms off the hallway: the room behind Door_Bathroom (where Joey changes into his uniform)
// and the room behind Door_Bedroom_Right. This furnishes them in every scene that has the house: a bathroom (shower,
// toilet, vanity and mirror, tiles, towel rail, mat) and a second bedroom (copies of the house's own bed, bedside
// table, lamp, wardrobe, rug and posters, plus a desk and chair). The middle of the bathroom stays clear: that is where
// AcademicEpisode.ChangeClothes stands Joey after he changes.
//
// Positions are written in Chapter 1 · Episode 1's world coordinates (house origin below, scale 2.5, no rotation) and
// carried to each scene's house through its transform. Everything made here is named "Bathroom | ..." or
// "Spare room | ..." under "House And Lot", so running it again replaces it. WorldModelSync.Apply calls it, so the
// episode builders keep the rooms.
public static class HouseSpareRooms
{
    const string BathroomPrefix = "Bathroom | ", BedroomPrefix = "Spare room | ";
    const string MaterialFolder = "Assets/Models/House Rooms";
    static readonly Vector3 Origin = new Vector3(-22.08f, .53f, -4.90f);   // House And Lot in Ch1 Ep1
    const float Floor = .53f;

    [MenuItem("Mindscape/House/Furnish Spare Rooms (open scene)")]
    static void ApplyOpenMenu()
    {
        var scene = SceneManager.GetActiveScene();
        Debug.Log("Spare rooms: " + Apply(scene));
        EditorSceneManager.MarkSceneDirty(scene);
    }

    [MenuItem("Mindscape/House/Furnish Spare Rooms (all house scenes)")]
    static void ApplyAllMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string active = SceneManager.GetActiveScene().path;
        foreach (var path in Directory.GetFiles("Assets/Scenes", "*.unity"))
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            string result = Apply(scene);
            if (result == null) continue;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("Spare rooms · " + Path.GetFileNameWithoutExtension(path) + ": " + result);
        }
        if (!string.IsNullOrEmpty(active)) EditorSceneManager.OpenScene(active, OpenSceneMode.Single);
    }

    static Transform house;
    static Material ceramic, tile, grout, chrome, glass, mirror, towel, mat, wood, darkWood, cushion;

    // Null when the scene has no house with these rooms.
    public static string Apply(Scene scene)
    {
        house = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            var t = root.name == "House And Lot" ? root.transform : FindDeep(root.transform, "House And Lot");
            if (t != null && FindDeep(t, "Door_Bathroom") != null) { house = t; break; }
        }
        if (house == null) return null;
        for (int i = house.childCount - 1; i >= 0; i--)
        {
            var child = house.GetChild(i);
            if (child.name.StartsWith(BathroomPrefix) || child.name.StartsWith(BedroomPrefix)) Object.DestroyImmediate(child.gameObject);
        }
        Materials();
        int bathroom = Bathroom(), bedroom = Bedroom();
        return bathroom + " bathroom parts, " + bedroom + " bedroom parts";
    }

    // ---------------------------------------------------------------- the bathroom
    // Room: x -25.66 .. -18.51, z -15.60 .. -9.07 (door in the north wall at x -22.1). Sizes are about 1.7x real life,
    // like the rest of the house.
    static int Bathroom()
    {
        int n = 0;
        // Floor tiles and a tiled wall band (wainscot) round the room.
        n += Part(BathroomPrefix + "floor tiles", P(-22.09f, Floor + .012f, -12.33f), new Vector3(7.12f, .02f, 6.5f), tile, false);
        for (float x = -25.4f; x < -18.6f; x += .8f) n += Part(BathroomPrefix + "floor grout", P(x, Floor + .025f, -12.33f), new Vector3(.03f, .005f, 6.5f), grout, false);
        for (float z = -15.4f; z < -9.2f; z += .8f) n += Part(BathroomPrefix + "floor grout", P(-22.09f, Floor + .025f, z), new Vector3(7.12f, .005f, .03f), grout, false);
        n += Part(BathroomPrefix + "wall tiles back", P(-22.09f, Floor + 1.4f, -15.57f), new Vector3(7.12f, 2.8f, .05f), tile, false);
        n += Part(BathroomPrefix + "wall tiles west", P(-25.63f, Floor + 1.4f, -12.33f), new Vector3(.05f, 2.8f, 6.5f), tile, false);
        n += Part(BathroomPrefix + "wall tiles east", P(-18.54f, Floor + 1.4f, -12.33f), new Vector3(.05f, 2.8f, 6.5f), tile, false);
        foreach (var (x, z, sx, sz) in new[] { (-22.09f, -15.53f, 7.12f, .04f), (-25.59f, -12.33f, .04f, 6.5f), (-18.58f, -12.33f, .04f, 6.5f) })
            n += Part(BathroomPrefix + "tile trim", P(x, Floor + 2.82f, z), new Vector3(sx, .1f, sz), grout, false);

        // Shower in the back-left corner: tray, two glass panels, a chrome frame, the pipe and head.
        n += Part(BathroomPrefix + "shower tray", P(-24.5f, Floor + .09f, -14.45f), new Vector3(2.3f, .18f, 2.3f), ceramic);
        n += Part(BathroomPrefix + "shower drain", P(-24.5f, Floor + .185f, -14.45f), new Vector3(.25f, .01f, .25f), chrome, false);
        n += Part(BathroomPrefix + "shower glass side", P(-23.36f, Floor + 2.1f, -14.75f), new Vector3(.05f, 3.9f, 1.7f), glass);
        n += Part(BathroomPrefix + "shower glass front", P(-24.9f, Floor + 2.1f, -13.31f), new Vector3(1.5f, 3.9f, .05f), glass);
        n += Part(BathroomPrefix + "shower frame", P(-23.36f, Floor + 2.1f, -13.31f), new Vector3(.08f, 3.9f, .08f), chrome, false);
        n += Part(BathroomPrefix + "shower frame top", P(-24.5f, Floor + 4.05f, -13.31f), new Vector3(2.3f, .06f, .06f), chrome, false);
        n += Part(BathroomPrefix + "shower pipe", P(-25.55f, Floor + 3.1f, -14.9f), new Vector3(.08f, 2.2f, .08f), chrome, false);
        n += Part(BathroomPrefix + "shower arm", P(-25.3f, Floor + 4.2f, -14.9f), new Vector3(.5f, .07f, .07f), chrome, false);
        n += Round(BathroomPrefix + "shower head", P(-25.05f, Floor + 4.12f, -14.9f), new Vector3(.42f, .05f, .42f), chrome);
        n += Part(BathroomPrefix + "shower valve", P(-25.58f, Floor + 2.1f, -14.9f), new Vector3(.06f, .3f, .3f), chrome, false);

        // Toilet against the back wall, facing the door.
        n += Part(BathroomPrefix + "toilet base", P(-20.5f, Floor + .3f, -14.8f), new Vector3(.62f, .6f, .9f), ceramic);
        n += Round(BathroomPrefix + "toilet bowl", P(-20.5f, Floor + .62f, -14.55f), new Vector3(.78f, .08f, 1.0f), ceramic);
        n += Round(BathroomPrefix + "toilet seat", P(-20.5f, Floor + .7f, -14.55f), new Vector3(.8f, .03f, 1.02f), cushion);
        n += Part(BathroomPrefix + "toilet tank", P(-20.5f, Floor + 1.05f, -15.35f), new Vector3(.82f, .8f, .32f), ceramic);
        n += Part(BathroomPrefix + "toilet lid", P(-20.5f, Floor + 1.47f, -15.35f), new Vector3(.88f, .05f, .38f), ceramic, false);
        n += Part(BathroomPrefix + "flush button", P(-20.5f, Floor + 1.5f, -15.35f), new Vector3(.16f, .02f, .1f), chrome, false);
        n += Round(BathroomPrefix + "toilet paper", P(-19.1f, Floor + 1.2f, -15.35f), new Vector3(.24f, .12f, .24f), towel, Quaternion.Euler(90, 0, 0));
        n += Part(BathroomPrefix + "toilet paper holder", P(-19.1f, Floor + 1.2f, -15.5f), new Vector3(.32f, .04f, .1f), chrome, false);

        // Vanity on the east wall: cabinet, counter, basin, tap, mirror and a small shelf.
        n += Part(BathroomPrefix + "vanity cabinet", P(-19.0f, Floor + .7f, -11.6f), new Vector3(.9f, 1.4f, 1.8f), darkWood);
        n += Part(BathroomPrefix + "vanity door line", P(-19.46f, Floor + .7f, -11.6f), new Vector3(.01f, 1.2f, .02f), wood, false);
        n += Part(BathroomPrefix + "vanity handle", P(-19.47f, Floor + .95f, -11.75f), new Vector3(.03f, .2f, .04f), chrome, false);
        n += Part(BathroomPrefix + "vanity handle", P(-19.47f, Floor + .95f, -11.45f), new Vector3(.03f, .2f, .04f), chrome, false);
        n += Part(BathroomPrefix + "vanity counter", P(-19.0f, Floor + 1.43f, -11.6f), new Vector3(1.0f, .08f, 1.95f), ceramic, false);
        n += Round(BathroomPrefix + "basin", P(-19.05f, Floor + 1.48f, -11.6f), new Vector3(.66f, .04f, .8f), glass);
        n += Part(BathroomPrefix + "tap", P(-18.68f, Floor + 1.62f, -11.6f), new Vector3(.08f, .28f, .08f), chrome, false);
        n += Part(BathroomPrefix + "tap spout", P(-18.8f, Floor + 1.75f, -11.6f), new Vector3(.26f, .06f, .06f), chrome, false);
        n += Part(BathroomPrefix + "mirror frame", P(-18.55f, Floor + 2.9f, -11.6f), new Vector3(.05f, 1.75f, 1.45f), wood, false);
        n += Part(BathroomPrefix + "mirror", P(-18.52f, Floor + 2.9f, -11.6f), new Vector3(.03f, 1.6f, 1.3f), mirror, false);
        n += Part(BathroomPrefix + "shelf", P(-18.7f, Floor + 2.0f, -10.25f), new Vector3(.3f, .05f, .7f), wood, false);
        n += Round(BathroomPrefix + "soap bottle", P(-18.7f, Floor + 2.15f, -10.4f), new Vector3(.12f, .12f, .12f), towel);
        n += Round(BathroomPrefix + "cup", P(-18.7f, Floor + 2.12f, -10.1f), new Vector3(.12f, .09f, .12f), cushion);

        // A towel rail by the door, a bath mat in front of the shower, a small bin.
        n += Part(BathroomPrefix + "towel rail", P(-25.55f, Floor + 2.4f, -11.0f), new Vector3(.06f, .06f, 1.3f), chrome, false);
        n += Part(BathroomPrefix + "towel", P(-25.5f, Floor + 1.95f, -11.0f), new Vector3(.08f, .95f, 1.0f), towel, false);
        n += Part(BathroomPrefix + "bath mat", P(-24.4f, Floor + .03f, -12.6f), new Vector3(1.5f, .03f, .9f), mat, false);
        n += Round(BathroomPrefix + "bin", P(-19.0f, Floor + .25f, -14.0f), new Vector3(.4f, .25f, .4f), chrome);
        return n;
    }

    // ---------------------------------------------------------------- the second bedroom
    // Room: x -34.03 .. -25.66, z -15.60 .. -6.08; window in the west wall (z -9 .. -13), door at the north-east corner.
    // The house's own bedroom furniture (Joey's room, x -18.5 .. -9.9) is copied across, so it matches.
    static int Bedroom()
    {
        int n = 0;
        // Joey's bed is centred at x -11.6 with its head on the back wall; this one sits at x -30.2.
        float dx = -30.2f - -11.6f;
        foreach (var name in new[] { "West bedroom | bed fixed", "West bedroom | bed", "West bedroom | bedside table", "West bedroom | bedside lamp", "West bedroom | wall poster 1", "West bedroom | woven bedside rug" })
            n += CopyPart(name, new Vector3(dx, 0, 0), false);
        // The wardrobe against the north wall, west of the door.
        n += CopyPart("West bedroom | wardrobe", new Vector3(-31.0f - -11.89f, 0, 0), false);
        n += CopyPart("West bedroom | wall poster 2", new Vector3(-27.6f - -13.81f, 0, 0), false);

        // A desk under the east wall with a chair, a lamp and a few books.
        n += Part(BedroomPrefix + "desk top", P(-26.5f, Floor + 1.75f, -12.6f), new Vector3(1.1f, .1f, 2.3f), wood);
        foreach (var (x, z) in new[] { (-26.95f, -11.55f), (-26.95f, -13.65f), (-26.05f, -11.55f), (-26.05f, -13.65f) })
            n += Part(BedroomPrefix + "desk leg", P(x, Floor + .85f, z), new Vector3(.1f, 1.7f, .1f), darkWood);
        n += Part(BedroomPrefix + "desk drawer", P(-26.35f, Floor + 1.45f, -13.1f), new Vector3(.8f, .4f, .9f), darkWood, false);
        n += Part(BedroomPrefix + "chair seat", P(-27.55f, Floor + 1.0f, -12.6f), new Vector3(.85f, .1f, .85f), darkWood);
        foreach (var (x, z) in new[] { (-27.9f, -12.25f), (-27.9f, -12.95f), (-27.2f, -12.25f), (-27.2f, -12.95f) })
            n += Part(BedroomPrefix + "chair leg", P(x, Floor + .5f, z), new Vector3(.08f, 1.0f, .08f), darkWood, false);
        n += Part(BedroomPrefix + "chair back", P(-27.95f, Floor + 1.65f, -12.6f), new Vector3(.08f, 1.2f, .85f), darkWood, false);
        n += Part(BedroomPrefix + "chair cushion", P(-27.55f, Floor + 1.08f, -12.6f), new Vector3(.75f, .06f, .75f), cushion, false);
        n += Round(BedroomPrefix + "desk lamp base", P(-26.4f, Floor + 1.83f, -11.8f), new Vector3(.3f, .03f, .3f), chrome);
        n += Part(BedroomPrefix + "desk lamp arm", P(-26.4f, Floor + 2.15f, -11.8f), new Vector3(.05f, .6f, .05f), chrome, false);
        n += Part(BedroomPrefix + "desk lamp shade", P(-26.55f, Floor + 2.45f, -11.85f), new Vector3(.35f, .18f, .3f), cushion, false);
        string[] covers = { "towel", "cushion", "mat" };
        for (int i = 0; i < 3; i++)
            n += Part(BedroomPrefix + "book", P(-26.4f, Floor + 1.86f + i * .09f, -13.3f), new Vector3(.55f, .08f, .75f - i * .08f),
                covers[i] == "towel" ? towel : covers[i] == "cushion" ? cushion : mat, false);
        return n;
    }

    // A copy of one of the house's own parts, moved by offset (world metres, Ch1 Ep1 frame). With hide, the copy is
    // switched off (the messy bed, when the made one is also copied).
    static int CopyPart(string name, Vector3 offset, bool hide)
    {
        var original = FindDeep(house, name);
        if (original == null) return 0;
        var copy = Object.Instantiate(original.gameObject, house);
        copy.name = BedroomPrefix + name.Replace("West bedroom | ", "");
        copy.transform.localPosition = house.InverseTransformPoint(original.position + house.rotation * offset);
        copy.transform.localRotation = original.localRotation;
        copy.transform.localScale = original.localScale;
        copy.SetActive(!hide && original.gameObject.activeSelf);
        // A copy keeps no story roles: no scripts beyond rendering and collision.
        foreach (var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
        return 1;
    }

    // Ch1 Ep1 world point -> this scene's world point.
    static Vector3 P(float x, float y, float z) => house.TransformPoint((new Vector3(x, y, z) - Origin) / 2.5f);

    static int Part(string name, Vector3 at, Vector3 size, Material material, bool collider = true)
    {
        var o = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Place(o, name, at, size, material, collider, Quaternion.identity);
        return 1;
    }

    static int Round(string name, Vector3 at, Vector3 size, Material material, Quaternion? turn = null)
    {
        var o = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(o.GetComponent<Collider>());
        Place(o, name, at, size, material, false, turn ?? Quaternion.identity);
        return 1;
    }

    static void Place(GameObject o, string name, Vector3 at, Vector3 size, Material material, bool collider, Quaternion turn)
    {
        o.name = name;
        o.transform.SetParent(house, false);
        o.transform.position = at;
        o.transform.rotation = house.rotation * turn;
        o.transform.localScale = size / 2.5f;
        o.GetComponent<Renderer>().sharedMaterial = material;
        o.isStatic = true;
        if (!collider) { var c = o.GetComponent<Collider>(); if (c != null) Object.DestroyImmediate(c); }
    }

    static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    static void Materials()
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Models", "House Rooms");
        ceramic = Mat("Ceramic", new Color(.95f, .95f, .93f), .7f);
        tile = Mat("Bathroom Tile", new Color(.72f, .84f, .83f), .55f);
        grout = Mat("Tile Grout", new Color(.88f, .9f, .88f), .2f);
        chrome = Mat("Chrome", new Color(.78f, .8f, .82f), .85f, .9f);
        glass = Mat("Shower Glass", new Color(.8f, .92f, .95f, .28f), .9f, 0, true);
        mirror = Mat("Mirror", new Color(.82f, .88f, .9f), .95f, .95f);
        towel = Mat("Towel", new Color(.93f, .78f, .55f), .1f);
        mat = Mat("Bath Mat", new Color(.36f, .55f, .55f), .05f);
        wood = Mat("Light Wood", new Color(.72f, .56f, .38f), .25f);
        darkWood = Mat("Dark Wood", new Color(.45f, .32f, .22f), .25f);
        cushion = Mat("Soft Coral", new Color(.86f, .55f, .47f), .1f);
    }

    static Material Mat(string name, Color color, float smoothness, float metallic = 0, bool transparent = false)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        m.SetColor("_BaseColor", color); m.color = color;
        m.SetFloat("_Smoothness", smoothness); m.SetFloat("_Metallic", metallic);
        if (transparent)
        {
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        EditorUtility.SetDirty(m);
        return m;
    }
}
