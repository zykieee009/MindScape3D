using TMPro;
using UnityEngine;

// A name plate that floats above an NPC's head and always faces the camera.
// It is a normal child object of the NPC (see the Hierarchy), so move, resize, restyle or
// remove it freely. The text follows NPC > NPC Name automatically.
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Mindscape/NPC Name Tag")]
public class NPCNameTag : MonoBehaviour
{
    [Tooltip("The NPC this tag names. Found automatically on the parent if empty.")]
    public NPC npc;
    [Tooltip("The text object that shows the name.")]
    public TMP_Text label;
    [Tooltip("Keep the tag at 'Height' metres above the NPC's feet. Untick to place it by hand.")]
    public bool controlHeight = true;
    [Tooltip("Metres above the NPC's feet.")]
    public float height = 2.4f;
    [Tooltip("Turn the tag toward the camera while playing.")]
    public bool faceCamera = true;

    void OnEnable() { Sync(); }
    void OnValidate() { Sync(); }

    void Sync()
    {
        if (npc == null) npc = GetComponentInParent<NPC>();
        if (npc != null && label != null && label.text != npc.npcName) label.text = npc.npcName;
        if (controlHeight && transform.parent != null)
        {
            var p = transform.localPosition;
            if (!Mathf.Approximately(p.y, height)) transform.localPosition = new Vector3(0f, height, 0f);
        }
    }

    void LateUpdate()
    {
        if (!Application.isPlaying) return;
        if (npc != null && label != null && label.text != npc.npcName) label.text = npc.npcName;
        if (!faceCamera) return;
        var cam = Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation;
    }
}
