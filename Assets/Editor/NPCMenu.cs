using UnityEditor;
using UnityEngine;

// Editor only: adds GameObject > NPC and a custom Inspector for NPC dialogue trees.
public static class NPCMenu
{
    const string DefaultModelPath = "Assets/Prefabs/NPC/DefaultNPCModel.prefab";

    [MenuItem("GameObject/NPC", false, 8)]
    static void CreateNPC(MenuCommand menuCommand)
    {
        NPCInteractionManager manager = Object.FindAnyObjectByType<NPCInteractionManager>();
        if (manager == null)
        {
            GameObject managerObject = new GameObject("NPC System", typeof(NPCInteractionManager));
            Undo.RegisterCreatedObjectUndo(managerObject, "Create NPC System");
            manager = managerObject.GetComponent<NPCInteractionManager>();
        }

        GameObject npcObject = new GameObject("NPC", typeof(CapsuleCollider), typeof(NPC));
        GameObject selected = menuCommand.context as GameObject;
        Transform parent = selected != null ? selected.transform : manager.transform;
        GameObjectUtility.SetParentAndAlign(npcObject, parent.gameObject);

        CapsuleCollider collider = npcObject.GetComponent<CapsuleCollider>();
        collider.height = 1.8f;
        collider.center = new Vector3(0f, 0.9f, 0f);
        collider.radius = 0.35f;

        NPC npc = npcObject.GetComponent<NPC>();
        npc.modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultModelPath);
        npc.modelOffset = new Vector3(0f, 0.9f, 0f);
        npc.RefreshModel();

        Undo.RegisterCreatedObjectUndo(npcObject, "Create NPC");
        Selection.activeGameObject = npcObject;
    }
}

[CustomEditor(typeof(NPC))]
public class NPCEditor : Editor
{
    SerializedProperty npcName;
    SerializedProperty modelPrefab;
    SerializedProperty modelOffset;
    SerializedProperty interactionRadius;
    SerializedProperty turnDuration;
    SerializedProperty faceFocus;
    SerializedProperty faceHeight;
    SerializedProperty cameraDistance;
    SerializedProperty cameraHeightOffset;
    SerializedProperty enableDialogueChaining;
    SerializedProperty dialogueNodes;

    void OnEnable()
    {
        npcName = serializedObject.FindProperty("npcName");
        modelPrefab = serializedObject.FindProperty("modelPrefab");
        modelOffset = serializedObject.FindProperty("modelOffset");
        interactionRadius = serializedObject.FindProperty("interactionRadius");
        turnDuration = serializedObject.FindProperty("turnDuration");
        faceFocus = serializedObject.FindProperty("faceFocus");
        faceHeight = serializedObject.FindProperty("faceHeight");
        cameraDistance = serializedObject.FindProperty("cameraDistance");
        cameraHeightOffset = serializedObject.FindProperty("cameraHeightOffset");
        enableDialogueChaining = serializedObject.FindProperty("enableDialogueChaining");
        dialogueNodes = serializedObject.FindProperty("dialogueNodes");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField("NPC", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(modelPrefab, new GUIContent("3D Model"));
        bool modelChanged = EditorGUI.EndChangeCheck();
        EditorGUILayout.PropertyField(modelOffset, new GUIContent("Model Offset"));
        EditorGUILayout.PropertyField(npcName, new GUIContent("NPC Name"));
        EditorGUILayout.PropertyField(interactionRadius);
        EditorGUILayout.PropertyField(turnDuration);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Conversation Camera", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(faceFocus);
        EditorGUILayout.PropertyField(faceHeight);
        EditorGUILayout.PropertyField(cameraDistance);
        EditorGUILayout.PropertyField(cameraHeightOffset);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Dialogue", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(enableDialogueChaining, new GUIContent("Enable Dialogue Chaining"));
        EditorGUILayout.PropertyField(dialogueNodes, new GUIContent("Dialogue Nodes"), true);

        serializedObject.ApplyModifiedProperties();
        bool refreshClicked = GUILayout.Button("Refresh 3D Model");
        if (modelChanged || refreshClicked)
        {
            foreach (Object selectedTarget in targets)
            {
                NPC npc = selectedTarget as NPC;
                if (npc == null) continue;
                Undo.RecordObject(npc, "Update NPC Model");
                npc.RefreshModel();
                EditorUtility.SetDirty(npc);
            }
        }
    }
}
