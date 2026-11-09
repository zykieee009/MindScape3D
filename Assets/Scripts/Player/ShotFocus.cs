using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Keeps the player's own body out of the way of story shots and NPC conversations. Story shots (AcademicEpisode.Shot,
// ShotOver, Glide) and the conversation camera say what they are looking at; while such a shot is up, the player is
// hidden whenever their body stands between the camera and that point (their shadow stays). Shots of the player
// themselves, and the follow camera while roaming, are left alone.
public class ShotFocus : MonoBehaviour
{
    static ShotFocus instance;
    static Vector3? focus;

    /// <summary>The follow camera is at the player's eyes (first person), so their own body is hidden from it.</summary>
    public static bool FirstPerson;

    PlayerMovement movement;
    Renderer[] body;
    ShadowCastingMode[] modes;
    bool hidden;

    /// <summary>A story or conversation shot is looking at this point.</summary>
    public static void Set(Vector3 point) => focus = point;

    /// <summary>The camera is back to following the player.</summary>
    public static void Clear() => focus = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("ShotFocus");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<ShotFocus>();
        SceneManager.sceneLoaded += (scene, mode) => { focus = null; FirstPerson = false; instance.Forget(); };
    }

    void Forget()
    {
        Show();
        movement = null; body = null; modes = null;
    }

    void LateUpdate()
    {
        if (movement == null)
        {
            movement = FindAnyObjectByType<PlayerMovement>();
            if (movement == null) return;
            body = movement.GetComponentsInChildren<Renderer>(true);
            modes = new ShadowCastingMode[body.Length];
            for (int i = 0; i < body.Length; i++) modes[i] = body[i].shadowCastingMode;
        }
        if (ShouldHide()) Hide(); else Show();
    }

    bool ShouldHide()
    {
        var cam = Camera.main;
        if (FirstPerson && cam != null && movement.gameObject.activeInHierarchy)
        {
            if (movement.enabled && !NPCInteractionManager.DialogueActive) return true;
            // Locked with no story shot up (a line of dialogue after using a door, say): the camera is still at the eyes,
            // inside the head, so the body stays hidden rather than showing the face from behind.
            if (!focus.HasValue) return true;
        }
        if (!focus.HasValue || cam == null || !movement.gameObject.activeInHierarchy) return false;
        if (FirstPerson && ShotFromPlayerView(cam.transform.position)) return true;
        // The follow camera while you walk around always looks at you.
        if (movement.enabled && !NPCInteractionManager.DialogueActive) return false;

        // The body as a vertical segment from the feet to the top of the head.
        Transform player = movement.transform;
        var controller = movement.GetComponent<CharacterController>();
        float scale = player.lossyScale.x;
        float radius = (controller != null ? controller.radius : .4f) * scale + .25f;
        float height = controller != null ? controller.height * player.lossyScale.y : 2.4f;
        Vector3 feet = player.position, head = feet + Vector3.up * height;

        Vector3 target = focus.Value, eye = cam.transform.position;
        // The shot is of the player themselves (the target is on their body): keep them in it. An item right beside them
        // is not: the camera backs off toward the player, so their body can fill the frame and must step aside.
        // (The cartoon head reaches above the capsule, so the top of the body counts a little higher for this.)
        if (DistanceToSegment(target, feet, head + Vector3.up * height * .35f) < radius - .25f) return false;
        if (DistanceToSegment(eye, feet, head) < radius) return true;   // camera inside the body
        // Does the line from the camera to the subject pass through the body?
        const int steps = 24;
        for (int i = 1; i < steps; i++)
            if (DistanceToSegment(Vector3.Lerp(eye, target, i / (float)steps), feet, head) < radius) return true;
        return false;
    }

    // In first person a story shot (washing the plate at the sink, an examine close-up) still reads as the player's own
    // view when the camera is near them, so their body and whatever they carry (the plate) stay out of it, whichever
    // side of them the camera lands. A shot of the player themselves (the mirror) is left alone.
    bool ShotFromPlayerView(Vector3 eye)
    {
        Transform player = movement.transform;
        var controller = movement.GetComponent<CharacterController>();
        float height = controller != null ? controller.height * player.lossyScale.y : 2.4f;
        Vector3 feet = player.position, head = feet + Vector3.up * height;
        float radius = (controller != null ? controller.radius : .4f) * player.lossyScale.x + .25f;
        if (DistanceToSegment(focus.Value, feet, head + Vector3.up * height * .35f) < radius - .25f) return false;
        return DistanceToSegment(eye, feet, head) < height;
    }

    static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b) => Vector3.Distance(p, ClosestOnSegment(p, a, b));

    static Vector3 ClosestOnSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
        return a + ab * t;
    }

    /// <summary>
    /// Keeps a story camera out of the player's own head. A shot that backs off from its subject toward the player lands
    /// inside the body when the player stands about that far away, and the view is blocked. If the camera is within
    /// <paramref name="clearance"/> of the body (the capsule from the feet up <paramref name="height"/>, <paramref name="radius"/> wide),
    /// it is pushed sideways, away from the body, to that distance. A shot of the player themselves is left alone;
    /// an item beside them is not, since the camera backs off toward the player.
    /// </summary>
    public static Vector3 ClearOfBody(Vector3 camera, Vector3 lookAt, Vector3 feet, float height, float radius, float clearance = .9f)
    {
        Vector3 head = feet + Vector3.up * height;
        if (DistanceToSegment(lookAt, feet, head) < radius) return camera;   // a shot of the player themselves
        Vector3 axis = ClosestOnSegment(camera, feet, head);
        float wanted = radius + clearance;
        if (Vector3.Distance(camera, axis) >= wanted) return camera;

        Vector3 away = Vector3.ProjectOnPlane(camera - axis, Vector3.up);
        if (away.sqrMagnitude < .0001f) away = Vector3.ProjectOnPlane(feet - lookAt, Vector3.up);   // dead centre: step back from the subject
        if (away.sqrMagnitude < .0001f) away = Vector3.back;
        return new Vector3(axis.x, camera.y, axis.z) + away.normalized * wanted;
    }

    /// <summary>Same, for the player's own CharacterController capsule.</summary>
    public static Vector3 ClearOfBody(Vector3 camera, Vector3 lookAt, Transform player, CharacterController controller)
    {
        float height = controller != null ? controller.height * player.lossyScale.y : 2.4f;
        float radius = (controller != null ? controller.radius : .4f) * player.lossyScale.x;
        return ClearOfBody(camera, lookAt, player.position, height, radius);
    }

    void Hide()
    {
        if (hidden) return;
        hidden = true;
        foreach (var r in body) if (r != null) r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
    }

    void Show()
    {
        if (!hidden) return;
        hidden = false;
        if (body == null) return;
        for (int i = 0; i < body.Length; i++) if (body[i] != null) body[i].shadowCastingMode = modes[i];
    }
}
