using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

namespace Mindscape.SocialDynamics
{
    // Pieces the end of Chapter 3 builds at runtime, so the scenes need no extra set-up:
    //   MemoryFrame  a framed picture with a caption that lights up out of the dark.
    //   DreamSpace   Episode 8's dim, open dream: a path toward a far light, memory frames along both sides.
    //   Capture / Sketch / group photo save: the Episode 6 photograph and the pencil drawing at the very end.
    public static class SocialStaging
    {
        // Good things Joey did without noticing, one per frame: (picture in Resources, words).
        public static readonly (string image, string words)[] Memories =
        {
            ("EpisodeThumbnails/Chapter1_Episode4", "I took one step at a time, even when the deadline felt too big."),
            ("EpisodeThumbnails/Chapter1_Episode3", "I questioned a worried thought, and found a kinder one."),
            ("EpisodeThumbnails/Chapter2_Episode4", "I told my family how I really felt."),
            ("FamilyFinale/Keepsake_Certificate", "I was proud of myself once, just for trying."),
            ("EpisodeThumbnails/Chapter3_Episode2", "I went to the reunion, even though I was scared."),
            ("EpisodeThumbnails/Chapter3_Episode5", "I let a friend in when he checked on me."),
            (GroupPhotoKey, "I came back to the group, just as I am."),
        };
        public const string GroupPhotoKey = "@group-photo";

        static Material litDark, frameGold, unlitBase;
        static Shader Lit => Shader.Find("Universal Render Pipeline/Lit");
        static Shader Unlit => Shader.Find("Universal Render Pipeline/Unlit");

        public static Material Solid(Color color, bool unlit = false)
        {
            var m = new Material(unlit ? Unlit : Lit);
            m.SetColor("_BaseColor", color); m.color = color;
            return m;
        }

        public static Texture2D Picture(string key)
        {
            if (key == GroupPhotoKey) return LoadGroupPhoto() ?? Resources.Load<Texture2D>("SocialDynamics/OldGroupPhoto");
            return Resources.Load<Texture2D>(key);
        }

        // ---------------------------------------------------------------- memory frames

        public static MemoryFrame Frame(Transform parent, Vector3 at, Vector3 facing, Texture2D picture, string words, float width = 2.4f)
        {
            // Quads and TMP text face -Z, so the frame's +Z points away from the viewer ('facing' points toward them).
            var root = new GameObject("Memory · " + words).transform;
            root.SetParent(parent, true); root.position = at; root.rotation = Quaternion.LookRotation(-facing.normalized);
            float height = width * 9f / 16f;
            if (picture != null && picture.width > 0) height = width * picture.height / (float)picture.width;
            if (frameGold == null) frameGold = Solid(new Color(.86f, .7f, .42f), true);
            var border = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.Destroy(border.GetComponent<Collider>());
            border.name = "Frame"; border.transform.SetParent(root, false);
            border.transform.localPosition = new Vector3(0, 0, .01f); border.transform.localScale = new Vector3(width + .16f, height + .16f, 1);
            var borderMat = new Material(frameGold); border.GetComponent<Renderer>().sharedMaterial = borderMat;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.Destroy(quad.GetComponent<Collider>());
            quad.name = "Picture"; quad.transform.SetParent(root, false); quad.transform.localScale = new Vector3(width, height, 1);
            var pictureMat = Solid(Color.white, true);
            if (picture != null) { pictureMat.SetTexture("_BaseMap", picture); pictureMat.mainTexture = picture; }
            quad.GetComponent<Renderer>().sharedMaterial = pictureMat;
            foreach (var r in root.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }

            var caption = new GameObject("Caption").AddComponent<TextMeshPro>();
            caption.transform.SetParent(root, false); caption.transform.localPosition = new Vector3(0, -height * .5f - .38f, 0);
            caption.text = words; caption.fontSize = 2.2f; caption.alignment = TextAlignmentOptions.Center;
            caption.rectTransform.sizeDelta = new Vector2(width + 1.4f, 1.2f); caption.color = new Color(1, .95f, .85f, 0);

            var glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(root, false); glow.transform.localPosition = new Vector3(0, 0, -1.2f);
            glow.type = LightType.Point; glow.color = new Color(1, .86f, .62f); glow.range = 6; glow.intensity = 0; glow.shadows = LightShadows.None;

            var frame = root.gameObject.AddComponent<MemoryFrame>();
            frame.Init(pictureMat, borderMat, caption, glow);
            return frame;
        }

        // ---------------------------------------------------------------- the dream

        public static DreamSpace Dream(Vector3 origin, float length)
        {
            var root = new GameObject("Dream space").transform; root.position = origin;
            if (litDark == null) litDark = Solid(new Color(.07f, .09f, .15f));
            // The floor: a long dark path with no walls or ceiling; it fades into the dark at its edges.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Dream floor";
            floor.transform.SetParent(root, false); floor.transform.localPosition = new Vector3(0, -.25f, length * .5f - 6);
            floor.transform.localScale = new Vector3(40, .5f, length + 24); floor.GetComponent<Renderer>().sharedMaterial = litDark;
            var path = GameObject.CreatePrimitive(PrimitiveType.Cube); path.name = "Faint path"; Object.Destroy(path.GetComponent<Collider>());
            path.transform.SetParent(root, false); path.transform.localPosition = new Vector3(0, .005f, length * .5f);
            path.transform.localScale = new Vector3(3.2f, .01f, length); path.GetComponent<Renderer>().sharedMaterial = Solid(new Color(.16f, .2f, .32f));
            // Invisible walls keep you on the way forward without boxing the view in.
            void Wall(Vector3 at, Vector3 size)
            {
                var w = new GameObject("Dream edge", typeof(BoxCollider)).transform; w.SetParent(root, false);
                w.localPosition = at; w.GetComponent<BoxCollider>().size = size;
            }
            Wall(new Vector3(-7, 3, length * .5f), new Vector3(.5f, 6, length + 24));
            Wall(new Vector3(7, 3, length * .5f), new Vector3(.5f, 6, length + 24));
            Wall(new Vector3(0, 3, -6), new Vector3(14, 6, .5f));
            Wall(new Vector3(0, 3, length + 4), new Vector3(14, 6, .5f));
            // The light far ahead: an open, glowing doorway standing on its own (not a tunnel), with a few steps up to it.
            var door = new GameObject("Distant light").transform; door.SetParent(root, false); door.localPosition = new Vector3(0, 0, length);
            var white = Solid(new Color(1.6f, 1.55f, 1.4f), true);
            var stone = Solid(new Color(.35f, .45f, .7f), true);
            for (int i = 0; i < 4; i++)
            {
                var step = GameObject.CreatePrimitive(PrimitiveType.Cube); step.name = "Step"; Object.Destroy(step.GetComponent<Collider>());
                step.transform.SetParent(door, false); step.transform.localPosition = new Vector3(0, .1f + i * .2f, -2.4f + i * .55f);
                step.transform.localScale = new Vector3(5 - i * .5f, .2f, .6f); step.GetComponent<Renderer>().sharedMaterial = stone;
            }
            foreach (float x in new[] { -1.7f, 1.7f })
            {
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube); pillar.name = "Pillar"; Object.Destroy(pillar.GetComponent<Collider>());
                pillar.transform.SetParent(door, false); pillar.transform.localPosition = new Vector3(x, 3.4f, 0); pillar.transform.localScale = new Vector3(.4f, 5.6f, .4f);
                pillar.GetComponent<Renderer>().sharedMaterial = stone;
            }
            var arch = GameObject.CreatePrimitive(PrimitiveType.Cube); arch.name = "Arch"; Object.Destroy(arch.GetComponent<Collider>());
            arch.transform.SetParent(door, false); arch.transform.localPosition = new Vector3(0, 6.3f, 0); arch.transform.localScale = new Vector3(3.8f, .4f, .4f);
            arch.GetComponent<Renderer>().sharedMaterial = stone;
            var glowPane = GameObject.CreatePrimitive(PrimitiveType.Quad); glowPane.name = "Light in the doorway"; Object.Destroy(glowPane.GetComponent<Collider>());
            glowPane.transform.SetParent(door, false); glowPane.transform.localPosition = new Vector3(0, 3.4f, .05f); glowPane.transform.localScale = new Vector3(3, 5.6f, 1);
            glowPane.GetComponent<Renderer>().sharedMaterial = white;
            var light = new GameObject("Doorway light").AddComponent<Light>(); light.transform.SetParent(door, false); light.transform.localPosition = new Vector3(0, 3, -2);
            light.type = LightType.Point; light.color = new Color(.85f, .9f, 1); light.intensity = 6; light.range = 30; light.shadows = LightShadows.None;
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var dream = root.gameObject.AddComponent<DreamSpace>();
            dream.start = new GameObject("Dream start").transform; dream.start.SetParent(root, false); dream.start.localPosition = new Vector3(0, .1f, 0);
            dream.end = door; dream.length = length; dream.doorwayLight = light;
            // Memory frames on alternating sides of the path, angled toward you as you walk.
            var frames = new List<MemoryFrame>();
            for (int i = 0; i < Memories.Length; i++)
            {
                float z = 10 + i * (length - 18) / Mathf.Max(1, Memories.Length - 1);
                float x = i % 2 == 0 ? -3.6f : 3.6f;
                Vector3 at = origin + new Vector3(x, 2.6f, z);
                Vector3 facing = new Vector3(-x * .6f, 0, -1);   // toward the path and back toward you
                frames.Add(Frame(root, at, facing, Picture(Memories[i].image), Memories[i].words));
            }
            dream.frames = frames.ToArray();
            return dream;
        }

        // ---------------------------------------------------------------- the terrace

        // Episode 5's terrace: a timber deck outside the wall behind 'window' (the lounge window spot inside the hall),
        // with a railing at its far edge, planters, and warm string lights. Built for the scene and destroyed after it.
        public static TerraceSpace Terrace(Transform parent, Vector3 window)
        {
            // The wall: straight out from the window spot, wherever the hall's wall is nearest.
            Vector3 eye = window + Vector3.up * 1.2f, outward = Vector3.left; float best = float.MaxValue;
            // The hall's own walls (its Architecture mesh), not the sofa or a table that happens to be closer.
            foreach (var dir in new[] { Vector3.left, Vector3.right, Vector3.forward, Vector3.back })
                foreach (var hit in Physics.RaycastAll(eye, dir, 8, ~0, QueryTriggerInteraction.Ignore))
                    if (hit.collider.name.Contains("Architecture") && hit.distance < best) { best = hit.distance; outward = dir; }
            if (best == float.MaxValue) best = 1.5f;
            float floorY = window.y;
            if (Physics.Raycast(window + Vector3.up * 1, Vector3.down, out var floorHit, 4, ~0, QueryTriggerInteraction.Ignore)) floorY = floorHit.point.y;
            Vector3 along = Vector3.Cross(Vector3.up, outward);
            Vector3 wall = new Vector3(window.x, floorY, window.z) + outward * best;
            const float depth = 5.2f, width = 9f;
            Vector3 start = wall + outward * .45f;   // past the wall's thickness
            var root = new GameObject("Terrace").transform; root.SetParent(parent, true);
            root.position = start; root.rotation = Quaternion.LookRotation(outward);   // local +Z: out over the city, +X along
            var deck = Solid(new Color(.47f, .33f, .23f)); var rail = Solid(new Color(.2f, .17f, .15f)); var glassy = Solid(new Color(.55f, .66f, .74f));
            var planter = Solid(new Color(.75f, .69f, .6f)); var leaf = Solid(new Color(.26f, .45f, .3f)); var bulb = Solid(new Color(1.6f, 1.2f, .6f), true);
            GameObject Box(string name, Vector3 local, Vector3 size, Material m, PrimitiveType type = PrimitiveType.Cube)
            {
                var go = GameObject.CreatePrimitive(type); go.name = name; Object.Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(root, false); go.transform.localPosition = local; go.transform.localScale = size;
                go.GetComponent<Renderer>().sharedMaterial = m; return go;
            }
            Box("Deck", new Vector3(0, -.12f, depth * .5f), new Vector3(width, .24f, depth), deck);
            for (int i = 0; i < 9; i++) Box("Deck board line", new Vector3(-width * .5f + .5f + i * 1f, .005f, depth * .5f), new Vector3(.03f, .01f, depth), rail);
            // Railing: posts, a top rail and glass panels on the far edge and both sides.
            void RailRun(Vector3 a, Vector3 b)
            {
                Vector3 mid = (a + b) * .5f, d = b - a; bool x = Mathf.Abs(d.x) > Mathf.Abs(d.z); float len = d.magnitude;
                Box("Top rail", mid + Vector3.up * 1.75f, x ? new Vector3(len, .1f, .14f) : new Vector3(.14f, .1f, len), rail);
                Box("Glass panel", mid + Vector3.up * .9f, x ? new Vector3(len, 1.6f, .04f) : new Vector3(.04f, 1.6f, len), glassy);
                for (float t = 0; t <= 1.001f; t += 1f / Mathf.Max(1, Mathf.Round(len / 2.2f)))
                    Box("Rail post", a + d * t + Vector3.up * .9f, new Vector3(.14f, 1.8f, .14f), rail);
            }
            RailRun(new Vector3(-width * .5f, 0, depth), new Vector3(width * .5f, 0, depth));
            RailRun(new Vector3(-width * .5f, 0, .1f), new Vector3(-width * .5f, 0, depth));
            RailRun(new Vector3(width * .5f, 0, .1f), new Vector3(width * .5f, 0, depth));
            // Supports down to the ground.
            foreach (float x in new[] { -width * .5f + .3f, 0f, width * .5f - .3f })
            {
                float down = 6;
                if (Physics.Raycast(root.TransformPoint(new Vector3(x, -.3f, depth - .3f)), Vector3.down, out var g, 12, ~0, QueryTriggerInteraction.Ignore)) down = g.distance + .3f;
                Box("Support", new Vector3(x, -down * .5f, depth - .3f), new Vector3(.3f, down, .3f), rail);
            }
            foreach (float x in new[] { -width * .5f + .7f, width * .5f - .7f })
            {
                Box("Planter", new Vector3(x, .45f, .8f), new Vector3(.9f, .9f, .9f), planter);
                Box("Plant", new Vector3(x, 1.25f, .8f), new Vector3(1.1f, 1.1f, 1.1f), leaf, PrimitiveType.Sphere);
            }
            // A strand of warm bulbs from the wall out to the rail corners.
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i <= 6; i++)
                {
                    float t = i / 6f;
                    var p = Vector3.Lerp(new Vector3(side * width * .35f, 4.6f, .2f), new Vector3(side * width * .5f, 2.2f, depth), t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * .35f;
                    Box("String bulb", p, Vector3.one * .16f, bulb, PrimitiveType.Sphere);
                }
            var glow = new GameObject("Terrace glow").AddComponent<Light>(); glow.transform.SetParent(root, false);
            glow.transform.localPosition = new Vector3(0, 3.6f, depth * .45f); glow.type = LightType.Point; glow.color = new Color(1, .78f, .5f);
            glow.intensity = 2.4f; glow.range = 11; glow.shadows = LightShadows.None;
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var space = root.gameObject.AddComponent<TerraceSpace>();
            space.outward = outward; space.rail = root.TransformPoint(new Vector3(0, 0, depth));
            return space;
        }

        // ---------------------------------------------------------------- pictures

        public static Texture2D Capture(Camera camera, int width, int height)
        {
            var rt = RenderTexture.GetTemporary(width, height, 24);
            var previous = RenderTexture.active; var oldTarget = camera.targetTexture;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt; camera.Render();
                RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            }
            finally { camera.targetTexture = oldTarget; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); }
            return image;
        }

        // A pencil drawing of what the camera sees: soft graphite shading plus dark edges on warm paper.
        public static Texture2D Sketch(Camera camera, int width = 960, int height = 540)
        {
            var photo = Capture(camera, width, height);
            var src = photo.GetPixels(); Object.Destroy(photo);
            var lum = new float[src.Length];
            for (int i = 0; i < src.Length; i++) lum[i] = src[i].r * .3f + src[i].g * .59f + src[i].b * .11f;
            // Normalise the brightness so a dim room still draws with a full range of tones.
            float lo = 1, hi = 0; foreach (var l in lum) { lo = Mathf.Min(lo, l); hi = Mathf.Max(hi, l); }
            float span = Mathf.Max(.05f, hi - lo);
            var outPx = new Color[src.Length];
            var paper = new Color(.96f, .93f, .86f); var graphite = new Color(.22f, .2f, .19f);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    float L(int dx, int dy) => (lum[Mathf.Clamp(y + dy, 0, height - 1) * width + Mathf.Clamp(x + dx, 0, width - 1)] - lo) / span;
                    float gx = -L(-1, -1) - 2 * L(-1, 0) - L(-1, 1) + L(1, -1) + 2 * L(1, 0) + L(1, 1);
                    float gy = -L(-1, -1) - 2 * L(0, -1) - L(1, -1) + L(-1, 1) + 2 * L(0, 1) + L(1, 1);
                    float edge = Mathf.Clamp01(Mathf.Sqrt(gx * gx + gy * gy) * 1.6f - .08f);
                    float tone = L(0, 0);
                    // Light hatching in the darker areas: diagonal lines whose density follows the shade.
                    float hatch = tone < .55f && ((x + y) % 6 == 0) ? (.55f - tone) * .9f : 0;
                    hatch += tone < .3f && ((x - y + 1000) % 6 == 0) ? (.3f - tone) * .9f : 0;
                    float ink = Mathf.Clamp01(edge + hatch + (1 - tone) * .12f);
                    // A soft vignette, like a drawing that fades toward the edges of the page.
                    float vx = x / (float)width - .5f, vy = y / (float)height - .5f;
                    float fade = Mathf.Clamp01(1 - (vx * vx + vy * vy) * 2.2f);
                    outPx[i] = Color.Lerp(paper, graphite, ink * fade);
                }
            var sketch = new Texture2D(width, height, TextureFormat.RGB24, false);
            sketch.SetPixels(outPx); sketch.Apply();
            return sketch;
        }

        // The Episode 6 photograph is kept for the walk home and the dream (per profile, outside the build).
        static string GroupPhotoPath => Path.Combine(Application.persistentDataPath, Mindscape.Data.MindscapeDatabase.ProfileKey("group-photo").Replace(':', '_').Replace('/', '_') + ".png");

        public static void SaveGroupPhoto(Texture2D photo)
        {
            try { File.WriteAllBytes(GroupPhotoPath, photo.EncodeToPNG()); }
            catch (System.Exception e) { Debug.LogWarning("Could not keep the group photograph: " + e.Message); }
        }

        public static Texture2D LoadGroupPhoto()
        {
            try
            {
                if (!File.Exists(GroupPhotoPath)) return null;
                var t = new Texture2D(2, 2, TextureFormat.RGB24, false);
                return t.LoadImage(File.ReadAllBytes(GroupPhotoPath)) ? t : null;
            }
            catch { return null; }
        }

        public static void SetLayer(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }
    }

    // A framed memory that is dark until Show lights it up.
    public sealed class MemoryFrame : MonoBehaviour
    {
        Material picture, border; TextMeshPro caption; Light glow;
        float shown, target, speed = 1;
        Color pictureColor = Color.white, borderColor;

        public bool IsShown => target > 0;

        public void Init(Material picture, Material border, TextMeshPro caption, Light glow)
        {
            this.picture = picture; this.border = border; this.caption = caption; this.glow = glow;
            borderColor = border.GetColor("_BaseColor");
            Apply();
        }

        public void Show(float seconds = 1.6f) { target = 1; speed = 1 / Mathf.Max(.05f, seconds); }
        public void Hide(float seconds = 1f) { target = 0; speed = 1 / Mathf.Max(.05f, seconds); }

        void Update()
        {
            if (Mathf.Approximately(shown, target)) return;
            shown = Mathf.MoveTowards(shown, target, speed * Time.deltaTime);
            Apply();
        }

        void Apply()
        {
            float k = Mathf.SmoothStep(0, 1, shown);
            picture.SetColor("_BaseColor", pictureColor * k); picture.color = pictureColor * k;
            border.SetColor("_BaseColor", borderColor * k); border.color = borderColor * k;
            if (caption != null) { var c = caption.color; c.a = k; caption.color = c; }
            if (glow != null) glow.intensity = 1.6f * k;
            transform.localScale = Vector3.one * Mathf.Lerp(.85f, 1, k);
        }
    }

    // Episode 8's dream: where it starts, the far light, and the frames along the way.
    public sealed class DreamSpace : MonoBehaviour
    {
        public Transform start, end;
        public float length;
        public Light doorwayLight;
        public MemoryFrame[] frames;
    }

    // Episode 5's terrace: the railing's middle (on the deck) and the way out over the city.
    public sealed class TerraceSpace : MonoBehaviour
    {
        public Vector3 rail, outward;
    }
}
