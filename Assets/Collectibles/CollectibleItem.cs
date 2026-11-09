using UnityEngine;
using UnityEngine.Serialization;

// A pickup placed in the scene. itemId is what quests match on; collectibleName is shown to the player.
// Create with GameObject > Collectible. CollectiblesManager handles the pickup itself.
public class CollectibleItem : MonoBehaviour
{
    [Header("Collectible")]
    public string itemId = "collectible";

    [FormerlySerializedAs("displayName")]
    public string collectibleName = "Collectible";

    [Tooltip("Prefab or imported model displayed for this collectible.")]
    public GameObject modelPrefab;

    [Header("Interaction")]
    public float interactionRadius = 2.25f;
    public bool collected;

    [HideInInspector] public CollectiblesManager manager;
    [SerializeField, HideInInspector] GameObject modelInstance;

    public string DisplayName => string.IsNullOrWhiteSpace(collectibleName)
        ? "Collectible"
        : collectibleName;

    void Awake()
    {
        if (manager == null)
            manager = FindAnyObjectByType<CollectiblesManager>();

        if (modelPrefab != null && modelInstance == null)
            RefreshModel();
    }

    public bool CanCollect(Transform player)
    {
        if (collected || player == null) return false;
        return Vector3.Distance(transform.position, player.position) <= interactionRadius;
    }

    public void Collect()
    {
        if (collected) return;
        collected = true;
        if (manager != null)
            manager.Collect(this);
        else
            gameObject.SetActive(false);
    }

    public void RefreshModel()
    {
        if (modelInstance != null)
        {
            if (Application.isPlaying)
                Destroy(modelInstance);
            else
                DestroyImmediate(modelInstance);
        }

        // Existing primitive collectibles keep their root MeshRenderer until a
        // replacement model is assigned. Once assigned, the child model becomes
        // the visible representation and the legacy root mesh is hidden.
        MeshRenderer rootRenderer = GetComponent<MeshRenderer>();
        if (rootRenderer != null)
            rootRenderer.enabled = modelPrefab == null;

        if (modelPrefab == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            modelInstance = UnityEditor.PrefabUtility.InstantiatePrefab(modelPrefab, transform) as GameObject;
        else
            modelInstance = Instantiate(modelPrefab, transform);
#else
        modelInstance = Instantiate(modelPrefab, transform);
#endif

        if (modelInstance == null)
            return;

        modelInstance.name = "Model";
        modelInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        modelInstance.transform.localScale = Vector3.one;

        // The collectible root owns interaction. Visual prefabs should not add
        // extra colliders that can block the player or camera.
        Collider[] visualColliders = modelInstance.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < visualColliders.Length; i++)
            visualColliders[i].enabled = false;
    }
}
