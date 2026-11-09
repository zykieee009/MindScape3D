using UnityEditor;
using UnityEngine;

// Editor only: adds GameObject > Collectible and a custom Inspector for CollectibleItem.
public static class CollectibleMenu
{
    const string DefaultModelPath = "Assets/Prefabs/Collectibles/DefaultCollectibleModel.prefab";

    [MenuItem("GameObject/Collectible", false, 7)]
    static void CreateCollectible(MenuCommand menuCommand)
    {
        GameObject selected = menuCommand.context as GameObject;
        CollectiblesManager manager = Object.FindAnyObjectByType<CollectiblesManager>();

        if (manager == null)
        {
            GameObject managerObject = new GameObject("collectibles", typeof(CollectiblesManager));
            Undo.RegisterCreatedObjectUndo(managerObject, "Create Collectibles Manager");
            manager = managerObject.GetComponent<CollectiblesManager>();
        }

        GameObject collectibleObject = new GameObject("Collectible");
        Transform parent = selected != null ? selected.transform : manager.transform;
        GameObjectUtility.SetParentAndAlign(collectibleObject, parent.gameObject);

        SphereCollider collider = collectibleObject.AddComponent<SphereCollider>();
        collider.radius = 0.75f;

        CollectibleItem collectible = collectibleObject.AddComponent<CollectibleItem>();
        collectible.collectibleName = "Collectible";
        collectible.modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultModelPath);
        collectible.RefreshModel();

        Undo.RegisterCreatedObjectUndo(collectibleObject, "Create Collectible");
        Selection.activeGameObject = collectibleObject;
        EditorGUIUtility.PingObject(collectibleObject);
    }
}

[CustomEditor(typeof(CollectibleItem))]
public class CollectibleItemEditor : Editor
{
    SerializedProperty itemId;
    SerializedProperty collectibleName;
    SerializedProperty modelPrefab;
    SerializedProperty interactionRadius;
    SerializedProperty collected;

    void OnEnable()
    {
        itemId = serializedObject.FindProperty("itemId");
        collectibleName = serializedObject.FindProperty("collectibleName");
        modelPrefab = serializedObject.FindProperty("modelPrefab");
        interactionRadius = serializedObject.FindProperty("interactionRadius");
        collected = serializedObject.FindProperty("collected");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Collectible", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(modelPrefab, new GUIContent("3D Model"));
        bool modelChanged = EditorGUI.EndChangeCheck();
        EditorGUILayout.PropertyField(collectibleName, new GUIContent("Collectible Name"));
        EditorGUILayout.PropertyField(itemId, new GUIContent("Item ID"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Interaction", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(interactionRadius);

        if (Application.isPlaying)
            EditorGUILayout.PropertyField(collected);

        serializedObject.ApplyModifiedProperties();
        bool refreshClicked = GUILayout.Button("Refresh 3D Model");

        if (modelChanged || refreshClicked)
        {
            foreach (Object selectedTarget in targets)
            {
                CollectibleItem collectible = selectedTarget as CollectibleItem;
                if (collectible == null) continue;

                Undo.RecordObject(collectible, "Update Collectible Model");
                collectible.RefreshModel();
                EditorUtility.SetDirty(collectible);
            }
        }

        EditorGUILayout.HelpBox(
            "Assign a prefab or imported model to 3D Model. The collectible root keeps the interaction component and collider.",
            MessageType.Info);
    }
}
