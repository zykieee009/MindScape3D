using System;
using System.Collections.Generic;
using UnityEngine;

// NPC data: name, a branching dialogue tree (nodes + choices, each choice can change stress) and camera
// framing. Author everything in the Inspector. NPCInteractionManager does the talking and the UI.
[Serializable]
public class NPCDialogueChoice
{
    public string playerChoice = "Continue";
    [Tooltip("Signed stress adjustment when selected. Negative reduces stress; positive increases it.")]
    public float stressChange;
    [Tooltip("Dialogue node index to open next. Use -1 to end the conversation.")]
    public int nextDialogueIndex = -1;
    public bool endsDialogue;
}

[Serializable]
public class NPCDialogueNode
{
    [TextArea(3, 8)] public string npcDialogue = "Hello, traveler.";
    public List<NPCDialogueChoice> playerChoices = new List<NPCDialogueChoice>();
}

public class NPC : MonoBehaviour
{
    [Header("NPC")]
    public string npcName = "NPC";
    [Tooltip("Prefab or imported model displayed for this NPC.")]
    public GameObject modelPrefab;
    public Vector3 modelOffset;
    [Min(0.01f)] public float modelScale = 1f;

    [Header("Interaction")]
    public float interactionRadius = 3f;
    [Tooltip("Seconds used to turn toward the player and return to the resting direction.")]
    public float turnDuration = 0.3f;
    [Tooltip("Turn the whole character toward the player while talking. Off for people sitting at a desk or table.")]
    public bool turnToTalk = true;

    [Header("Conversation Camera")]
    [Tooltip("Optional head/face transform. When empty, Face Height is used.")]
    public Transform faceFocus;
    public float faceHeight = 1.65f;
    public float cameraDistance = 1.7f;
    public float cameraHeightOffset = 0.08f;

    [Header("Dialogue")]
    public bool enableDialogueChaining;
    public List<NPCDialogueNode> dialogueNodes = new List<NPCDialogueNode>
    {
        new NPCDialogueNode()
    };

    [SerializeField, HideInInspector] GameObject modelInstance;

    // With no Face Focus set, the conversation camera aims just above the model's "head" bone, the way the family
    // NPCs (Mom, Dad, Tim) were set up, so NPCs of every size are framed alike. Face Height is only the fallback for
    // a model without a head bone (a fixed height can't follow the rescaled street and school NPCs).
    public Vector3 FacePosition
    {
        get
        {
            if (faceFocus != null) return faceFocus.position;
            if (!headSearched)
            {
                headSearched = true;
                foreach (var bone in GetComponentsInChildren<Transform>(true))
                    if (bone.name.Equals("head", System.StringComparison.OrdinalIgnoreCase)) { head = bone; break; }
            }
            return head != null
                ? head.position + Vector3.up * (.1f * transform.lossyScale.y)
                : transform.position + Vector3.up * faceHeight;
        }
    }

    Transform head;
    bool headSearched;

    void Awake()
    {
        // Existing scene instances may have been authored with a moved model child.
        // Dialogue rotates this NPC root, so an offset child appears to fly/orbit
        // around the character instead of turning in place.
        NormalizeModelTransform();
    }

    public bool IsInRange(Transform player)
    {
        return player != null && Vector3.Distance(transform.position, player.position) <= interactionRadius;
    }

    public void RefreshModel()
    {
        if (modelInstance != null)
        {
            if (Application.isPlaying) Destroy(modelInstance);
            else DestroyImmediate(modelInstance);
        }

        if (modelPrefab == null) return;

#if UNITY_EDITOR
        modelInstance = !Application.isPlaying
            ? UnityEditor.PrefabUtility.InstantiatePrefab(modelPrefab, transform) as GameObject
            : Instantiate(modelPrefab, transform);
#else
        modelInstance = Instantiate(modelPrefab, transform);
#endif

        if (modelInstance == null) return;
        modelInstance.name = "Model";
        NormalizeModelTransform();

        Collider[] modelColliders = modelInstance.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < modelColliders.Length; i++)
            modelColliders[i].enabled = false;
    }

    void NormalizeModelTransform()
    {
        if (modelInstance == null) return;
        modelInstance.transform.SetLocalPositionAndRotation(modelOffset, Quaternion.identity);
        modelInstance.transform.localScale = Vector3.one * Mathf.Max(0.01f, modelScale);

        // Imported character files do not share a consistent origin. Snap the
        // visible feet to the NPC root so an asset cannot hover above the floor
        // or sink into it just because its source pivot is different.
        Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        modelInstance.transform.position += Vector3.up * (transform.position.y - bounds.min.y);
    }
}
