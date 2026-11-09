using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UIE = UnityEngine.UIElements;

// A door the player opens and closes: E plays the door model's own swing (Assets/Models/Doors, the hinge and handle clips
// authored in Blender) and E again swings it shut. The clips are baked into poses (tracks) when the prefab is built: they are
// generic clips, and AnimationClip.SampleAnimation does nothing with those in a player build. The leaf's collider moves with it, so an open door is a free doorway and
// a closed one shuts it. The door always opens away from whoever uses it; the clips only swing one way, so the other way is
// the same pose turned back (the hinge rotation inverted). A door that cannot be closed (the player stands in it) says nothing.
// Doors are built by DoorPrefabBuilder (prefabs) and placed in the scenes by DoorSetup.
public class DoorInteraction : MonoBehaviour
{
    [Header("Interaction")]
    public float interactRange = 2.4f;      // from the middle of the door, flat on the floor

    [Header("Model")]
    public GameObject model;                // the imported door the swing moves
    public Track[] tracks;                  // the hinge (and handle) actions, baked by DoorPrefabBuilder and played together
    public float sampleRate = 30f;          // poses per second in the tracks
    public Transform[] hinges;              // the leaves that swing; their rotation is inverted when the door opens the other way
    public Renderer[] leaves;               // what is lit when the door is in reach, and what blocks the doorway
    public Vector2 openSpan = new Vector2(.6f, 2f);     // seconds of the clips that open the door
    public Vector2 closeSpan = new Vector2(2.6f, 3.9f); // seconds of the clips that close it again
    public float speed = 1f;
    public bool startOpen;                  // an open doorway the story walks through; the door can still be shut
    public Vector2 leafSize = new Vector2(1f, 2.1f);    // closed leaf, width x height at scale 1 (DoorSetup fits it to the opening)

    [Header("Highlight")]
    public Color highlightColor = new Color(1f, 0.85f, 0.3f, 1f);
    public float highlightIntensity = 0.4f;

    // One moving part of the door, posed at every 1 / sampleRate seconds of the clips.
    [System.Serializable]
    public class Track
    {
        public Transform target;
        public Vector3[] positions;
        public Quaternion[] rotations;
    }

    static readonly List<DoorInteraction> Active = new List<DoorInteraction>();
    static UIE.VisualElement _prompt;   // the shared bottom-centre key prompt (InteractPrompt.uxml), one for all doors
    static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    private Transform _player;
    private CharacterController _character;
    private PlayerMovement _movement;
    private PlayerControls _controls;
    private readonly List<Material> _lit = new List<Material>();
    private bool _playerInRange;
    private bool _open, _moving, _mirror;
    private Vector3 _center;
    private Vector3 _normal;        // across the doorway, flat
    private float _swingSide;       // which side of the doorway the clips swing the leaf to (+1 along _normal, or -1)
    private Bounds _closed;         // the shut leaf, for "is someone standing in the doorway"

    public bool IsOpen => _open;

    void OnEnable() => Active.Add(this);

    void OnDisable()
    {
        Active.Remove(this);
        if (_playerInRange) { _playerInRange = false; SetHighlight(false); }
        RefreshPrompt();
    }

    void Awake()
    {
        if (model == null || leaves == null || leaves.Length == 0 || tracks == null || tracks.Length == 0) return;

        // The model stands closed. Measure it, and which way the clips swing it, then put it back.
        _closed = LeafBounds();
        _center = _closed.center;
        _normal = _closed.size.x < _closed.size.z ? Vector3.right : Vector3.forward;
        Pose(openSpan.y);
        _swingSide = Mathf.Sign(Vector3.Dot(LeafBounds().center - _center, _normal) + 1e-4f);
        Pose(0f);

        foreach (var leaf in leaves)
            foreach (var material in leaf.materials)    // instances, so one lit door does not light the others
            {
                if (!material.HasProperty(EmissionColorID) || material.name.Contains("Glass")) continue;
                material.EnableKeyword("_EMISSION");
                material.SetColor(EmissionColorID, Color.black);
                _lit.Add(material);
            }

        if (startOpen) { Pose(openSpan.y); _open = true; }
    }

    void Start()
    {
        _controls = FindObjectOfType<PlayerControls>();
        if (_controls == null) return;
        _player = _controls.transform;
        _character = _player.GetComponent<CharacterController>();
        _movement = _player.GetComponent<PlayerMovement>();
    }

    void Update()
    {
        if (_player == null || model == null) return;

        bool inRange = CanAct() && this == Nearest() && !(_open && Blocked());
        // Story spots and NPCs share the [ E ] key: the nearest of everything in reach gets it (InteractFocus).
        if (inRange)
        {
            Vector3 d = _center - _player.position; d.y = 0;
            InteractFocus.Offer(this, d.magnitude);
            inRange = InteractFocus.Has(this);
        }
        if (inRange != _playerInRange)
        {
            _playerInRange = inRange;
            SetHighlight(_playerInRange);
            RefreshPrompt();
        }

        bool interactPressed = (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) || MobileInput.InteractPressed;
        if (_playerInRange && !_moving && interactPressed)
            StartCoroutine(Swing(!_open));
    }

    // Not while a conversation, menu or story shot has the player held.
    bool CanAct() => (_movement == null || _movement.enabled) && (_controls == null || !_controls.IsOverlayOpen());

    float FlatDistance()
    {
        Vector3 d = _player.position - _center; d.y = 0f;
        return d.magnitude;
    }

    // Two doors can be in reach at once (a hallway); only the closer one answers.
    DoorInteraction Nearest()
    {
        DoorInteraction best = null; float bestDistance = float.MaxValue;
        foreach (var door in Active)
        {
            if (door._player == null || door.model == null) continue;
            float d = door.FlatDistance();
            if (d <= door.interactRange && d < bestDistance) { best = door; bestDistance = d; }
        }
        return best;
    }

    // Shutting the door on the player would trap them in the leaf.
    bool Blocked()
    {
        if (_character == null) return false;
        var zone = _closed; zone.Expand(new Vector3(.3f, 0f, .3f));
        return _character.bounds.Intersects(zone);
    }

    IEnumerator Swing(bool opening)
    {
        _moving = true;
        if (opening)
            _mirror = _player != null && Mathf.Sign(Vector3.Dot(_player.position - _center, _normal) + 1e-4f) == _swingSide;   // away from the user
        Vector2 span = opening ? openSpan : closeSpan;
        float duration = Mathf.Max(.05f, (span.y - span.x) / Mathf.Max(.1f, speed));
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            Pose(Mathf.Lerp(span.x, span.y, t / duration));
            yield return null;
        }
        Pose(opening ? span.y : 0f);
        _open = opening;
        _moving = false;
        RefreshPrompt();
    }

    // The clips at one moment, the swing turned round if the door is opening toward the other side.
    void Pose(float time)
    {
        foreach (var track in tracks)
        {
            if (track.target == null || track.rotations == null || track.rotations.Length == 0) continue;
            int last = track.rotations.Length - 1;
            float frame = Mathf.Clamp(time * sampleRate, 0f, last);
            int i = Mathf.Min((int)frame, last), j = Mathf.Min(i + 1, last);
            float k = frame - i;
            track.target.localPosition = Vector3.Lerp(track.positions[i], track.positions[j], k);
            track.target.localRotation = Quaternion.Slerp(track.rotations[i], track.rotations[j], k);
        }
        if (_mirror)
            foreach (var hinge in hinges) hinge.localRotation = Quaternion.Inverse(hinge.localRotation);
    }

    Bounds LeafBounds()
    {
        var b = leaves[0].bounds;
        for (int i = 1; i < leaves.Length; i++) b.Encapsulate(leaves[i].bounds);
        return b;
    }

    void SetHighlight(bool active)
    {
        Color color = active ? highlightColor * highlightIntensity : Color.black;
        foreach (var material in _lit) if (material != null) material.SetColor(EmissionColorID, color);
    }

    // "[ E ]  Open Door" / "[ E ]  Close Door", shown while the door has the [ E ] key (InteractFocus). It sits a row above the
    // other key prompt (.doors in InteractPrompt.uss), though only one of them has the key at a time.
    static void RefreshPrompt()
    {
        DoorInteraction focused = null;
        foreach (var door in Active) if (door._playerInRange) { focused = door; break; }
        bool show = focused != null;
        if (_prompt == null || _prompt.panel == null)
        {
            if (!show) return;
            var ui = Mindscape.AcademicPressure.EpisodeUI.Load();
            var document = Mindscape.AcademicPressure.EpisodeUI.Spawn("Door prompt", ui != null ? ui.interactPrompt : null, 0);
            if (document == null) return;
            _prompt = UIE.UQueryExtensions.Q(document.rootVisualElement, "prompt");
            _prompt.AddToClassList("doors");
        }
        if (show) UIE.UQueryExtensions.Q<UIE.Label>(_prompt, "prompt-text").text = focused._open ? "[ E ]  Close Door" : "[ E ]  Open Door";
        _prompt.EnableInClassList("hidden", !show);
    }

    void OnDestroy()
    {
        foreach (var material in _lit) if (material != null) Destroy(material);
    }
}
