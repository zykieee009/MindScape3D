using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// The 3D MindScape3D title (Assets/Models/Title) drawn into a transparent RenderTexture, so the intro's uGUI logo and the
/// main menu's UI Toolkit logo can show it as an ordinary image, fades and layout included. The model sits far below the
/// scene and is seen only by its own camera. Play() runs the model's clip (the intro's build-up once, the menu's glow looping).
/// </summary>
public sealed class TitleModelView : MonoBehaviour
{
    [Tooltip("The model (an .fbx from Assets/Models/Title).")]
    public GameObject model;
    [Tooltip("The model's animation clip (the sub-asset inside the same .fbx).")]
    public AnimationClip clip;
    public bool loop;
    [Tooltip("Plays when the scene starts. Off: call Restart() when the logo should begin.")]
    public bool playOnAwake = true;
    [Tooltip("The model's glow is very bright in URP/Lit; this scales every emission colour down.")]
    [Range(0f, 1f)] public float emissionScale = 0.35f;
    [Tooltip("What the camera frames, in model units: centre, then width. The height follows the picture's aspect.")]
    public Vector2 frameCenter = new Vector2(0.15f, 1.0f);
    public float frameWidth = 11.4f;
    public Vector2Int size = new Vector2Int(1440, 600);

    static readonly Vector3 Spot = new Vector3(0f, -4000f, 0f);

    public RenderTexture Texture { get; private set; }
    public float Aspect => (float)size.x / size.y;
    public bool Ready => Texture != null;

    GameObject instance;
    Camera view;
    PlayableGraph graph;
    AnimationClipPlayable player;
    bool playing;
    float time;

    void Awake() => Build();

    /// <summary>Makes the model, camera and texture. Safe to call early (another script's Awake may run first) and twice.</summary>
    public void Build()
    {
        if (model == null || Texture != null) return;
        Texture = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32) { name = "Title model", antiAliasing = 4 };

        // The clip animates the model's own root position, so it hangs off a holder that stays at the spot.
        var holder = new GameObject("Title model holder");
        holder.transform.SetParent(transform, false);
        holder.transform.position = Spot;
        instance = Instantiate(model, holder.transform);
        instance.name = "Title model instance";
        DimGlow(instance);

        var cameraObject = new GameObject("Title model camera");
        cameraObject.transform.SetParent(transform, false);
        // The model faces +Z; the camera looks at it from the front.
        cameraObject.transform.position = Spot + new Vector3(frameCenter.x, frameCenter.y, 30f);
        cameraObject.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        view = cameraObject.AddComponent<Camera>();
        view.orthographic = true;
        view.orthographicSize = frameWidth / Aspect * 0.5f;
        view.clearFlags = CameraClearFlags.SolidColor;
        view.backgroundColor = new Color(0f, 0f, 0f, 0f);
        view.nearClipPlane = 0.1f;
        view.farClipPlane = 60f;
        view.depth = -100f;
        view.allowHDR = false;
        view.allowMSAA = true;
        view.targetTexture = Texture;

        if (clip != null)
        {
            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            graph = PlayableGraph.Create("Title model");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            player = AnimationClipPlayable.Create(graph, clip);
            AnimationPlayableOutput.Create(graph, "Title", animator).SetSourcePlayable(player);
            graph.Play();
            Evaluate(0f);
        }
        if (playOnAwake) Restart();
    }

    /// <summary>Starts the clip from its first frame.</summary>
    public void Restart() { time = 0f; playing = clip != null; Evaluate(0f); }

    void Update()
    {
        if (!playing || !graph.IsValid()) return;
        time += Time.unscaledDeltaTime;
        if (time >= clip.length)
        {
            if (loop) time %= clip.length;
            else { time = clip.length; playing = false; }
        }
        Evaluate(time);
    }

    void Evaluate(float at)
    {
        if (!graph.IsValid()) return;
        player.SetTime(at);
        graph.Evaluate(0f);
    }

    // The imported materials are shared assets, so every renderer gets its own copy to dim.
    void DimGlow(GameObject root)
    {
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.materials;
            foreach (var material in materials)
            {
                if (!material.HasProperty("_EmissionColor")) continue;
                material.SetColor("_EmissionColor", material.GetColor("_EmissionColor") * emissionScale);
            }
            renderer.materials = materials;
        }
    }

    void OnDestroy()
    {
        if (graph.IsValid()) graph.Destroy();
        if (Texture != null) { Texture.Release(); Destroy(Texture); }
    }
}
