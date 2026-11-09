using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Renders the main menu's chapter and episode thumbnails, also the loading screens' backdrops, from the episode scenes
// themselves, so both show what the game actually looks like. Each shot opens its scene, poses the animated characters on their first frame (Edit mode leaves
// them in the bind pose), and renders a temporary camera to Assets/Resources/EpisodeThumbnails/<file>.png (1920x1080, full screen behind the loading screen).
// Scenes are opened, never saved.
public static class EpisodeThumbnails
{
    public const string Folder = "Assets/Resources/EpisodeThumbnails";
    const int Width = 1920, Height = 1080;

    public struct Shot
    {
        public string scene, file;
        public Vector3 eye, target;
        public float fov;
        public Shot(string scene, string file, Vector3 eye, Vector3 target, float fov = 50f) { this.scene = scene; this.file = file; this.eye = eye; this.target = target; this.fov = fov; }
    }

    // Views picked by trying a few per scene (Trial) and keeping the one that shows the episode's place and people best.
    public static readonly Shot[] Shots =
    {
        new Shot("Tutorial", "Tutorial", new Vector3(3f, 2.4f, 5.5f), new Vector3(-1f, 1.3f, -1f), 55f), // the park, notebook and ID on the path
        new Shot("Chapter 1 - Episode 1", "Chapter1_Episode1", new Vector3(-20f, 3.4f, -5.5f), new Vector3(-30f, 1f, 1f), 55f), // living room, the family at home
        new Shot("Chapter 1 - Episode 2", "Chapter1_Episode2", new Vector3(-1f, 3.8f, -21f), new Vector3(-9f, 1f, -8f), 55f), // Room 03 from the teacher's corner
        new Shot("Chapter 1 - Episode 3", "Chapter1_Episode3", new Vector3(-73f, 3.6f, -1f), new Vector3(-56f, 1f, -12f), 55f), // cafeteria at lunch
        new Shot("Chapter 1 - Episode 4", "Chapter1_Episode4", new Vector3(67f, 3.8f, -15.5f), new Vector3(58f, 1.2f, -5f), 55f), // dorm study table
        new Shot("Chapter 2 - Episode 1", "Chapter2_Episode1", new Vector3(-22f, 4.5f, 30f), new Vector3(-22f, 1.5f, 8f), 50f), // coming home, the front walk
        new Shot("Chapter 2 - Episode 2", "Chapter2_Episode2", new Vector3(-20f, 3.4f, -5.5f), new Vector3(-12f, 1.3f, 3f), 55f), // kitchen in the morning
        new Shot("Chapter 2 - Episode 3", "Chapter2_Episode3", new Vector3(-16.5f, 3.4f, -20f), new Vector3(-5f, 1f, -6f), 55f), // the exam, from the front corner
        new Shot("Chapter 2 - Episode 4", "Chapter2_Episode4", new Vector3(-22.5f, 3.6f, -4.5f), new Vector3(-16f, 1.3f, 1.5f), 55f), // family dinner
        new Shot("Chapter 2 - Episode 5", "Chapter2_Episode5", new Vector3(-24f, 3.4f, -4.5f), new Vector3(-16f, 1.3f, 1.5f), 55f), // the family talk by the dining table
        new Shot("Chapter 3 - Episode 1", "Chapter3_Episode1", new Vector3(-17.2f, 3f, -15f), new Vector3(-11f, 1.5f, -9.5f), 55f), // bedroom desk, the invitation
        new Shot("Chapter 3 - Episode 2", "Chapter3_Episode2", new Vector3(-6f, 3.4f, -5.2f), new Vector3(1f, 1.2f, 0f), 55f), // arriving at the Reunion Hall
        new Shot("Chapter 3 - Episode 3", "Chapter3_Episode3", new Vector3(9f, 3f, -5f), new Vector3(-3f, 1.2f, 1f), 55f), // the two groups of classmates
        new Shot("Chapter 3 - Episode 4", "Chapter3_Episode4", new Vector3(-4f, 3f, -3f), new Vector3(-11.5f, 1.2f, 3f), 55f), // the quiet corner by the window
        new Shot("Chapter 3 - Episode 5", "Chapter3_Episode5", new Vector3(-4f, 3f, -3f), new Vector3(-11.5f, 1.2f, 3f), 55f), // talking with Justin in the corner
        new Shot("Chapter 3 - Episode 6", "Chapter3_Episode6", new Vector3(2f, 2.6f, 0.8f), new Vector3(9.8f, 1.6f, 0.8f), 55f), // group photo on the stage
        new Shot("Chapter 3 - Episode 7", "Chapter3_Episode7", new Vector3(36f, 5f, 16f), new Vector3(26f, 1f, 28f), 55f), // the street at night
        new Shot("Chapter 3 - Episode 8", "Chapter3_Episode8", new Vector3(-13.5f, 2.8f, -9.5f), new Vector3(-17.5f, 1.8f, -14.5f), 55f), // back in the bedroom, the mirror
    };

    [MenuItem("Mindscape/Main Menu/Capture Episode Thumbnails")]
    public static void CaptureAll() => Capture(0, Shots.Length);

    // A few at a time from tooling (each scene takes a few seconds to open); the menu item does them all.
    public static void Capture(int from, int count)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string back = EditorSceneManager.GetActiveScene().path;
        Directory.CreateDirectory(Folder);
        try
        {
            for (int i = from; i < Mathf.Min(Shots.Length, from + count); i++)
            {
                EditorSceneManager.OpenScene("Assets/Scenes/" + Shots[i].scene + ".unity");
                Debug.Log("Thumbnail " + Shots[i].file + ": " + Render(Shots[i], Folder + "/" + Shots[i].file + ".png"));
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(back)) EditorSceneManager.OpenScene(back);
            AssetDatabase.Refresh();
        }
    }

    // Tries out views of one scene: "ex ey ez tx ty tz [fov]; ..." (eye, target), written to dir/<scene>_<n>.png.
    public static void Trial(string scene, string dir, string views)
    {
        EditorSceneManager.OpenScene("Assets/Scenes/" + scene + ".unity");
        Directory.CreateDirectory(dir);
        var list = views.Split(';');
        for (int i = 0; i < list.Length; i++)
        {
            var v = System.Array.ConvertAll(list[i].Trim().Split(' '), float.Parse);
            var shot = new Shot(scene, "trial", new Vector3(v[0], v[1], v[2]), new Vector3(v[3], v[4], v[5]), v.Length > 6 ? v[6] : 50f);
            Render(shot, Path.Combine(dir, scene.Replace(" ", "") + "_" + i + ".png"));
        }
    }

    // Renders a shot of the open scene to any PNG path (also used to try out views).
    public static string Render(Shot shot, string path)
    {
        foreach (var animator in Object.FindObjectsOfType<Animator>())
            if (animator.runtimeAnimatorController != null && animator.isActiveAndEnabled) { animator.Rebind(); animator.Update(0f); animator.Update(.25f); }

        var source = Camera.main;
        var go = new GameObject("Thumbnail camera");
        // Rendered at twice the size and scaled down for smooth edges (an MSAA target renders black from Camera.Render here).
        var texture = new RenderTexture(Width * 2, Height * 2, 24, RenderTextureFormat.ARGB32);
        var small = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        try
        {
            var camera = go.AddComponent<Camera>();
            if (source != null) camera.CopyFrom(source);
            camera.transform.SetPositionAndRotation(shot.eye, Quaternion.LookRotation(shot.target - shot.eye));
            camera.fieldOfView = shot.fov;
            camera.nearClipPlane = .05f;
            camera.targetTexture = texture;
            camera.aspect = (float)Width / Height;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camera.Render();

            Graphics.Blit(texture, small);
            RenderTexture.active = small;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            return path;
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(go);
            texture.Release(); small.Release();
            Object.DestroyImmediate(texture); Object.DestroyImmediate(small);
        }
    }
}
