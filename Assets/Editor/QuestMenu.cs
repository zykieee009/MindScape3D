using UnityEditor;
using UnityEngine;

// Editor only: adds GameObject > Quest and a custom Inspector for Quest.
public static class QuestMenu
{
    [MenuItem("GameObject/Quest", false, 9)]
    static void CreateQuest(MenuCommand menuCommand)
    {
        GameObject questObject = new GameObject("Quest", typeof(Quest));
        GameObject selected = menuCommand.context as GameObject;
        if (selected != null)
            GameObjectUtility.SetParentAndAlign(questObject, selected);

        Undo.RegisterCreatedObjectUndo(questObject, "Create Quest");
        QuestHUDBuilder.EnsureQuestHUD();
        Selection.activeGameObject = questObject;
        EditorGUIUtility.PingObject(questObject);
    }
}

[CustomEditor(typeof(Quest))]
public class QuestEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Quest Setup", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("questId"), new GUIContent("Quest ID"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("questName"), new GUIContent("Quest Name"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("activeOnStart"), new GUIContent("Active On Start"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Criteria", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("requiredCollectibles"), new GUIContent("Required Collectibles"), true);

        SerializedProperty requireStress = serializedObject.FindProperty("requireStressRange");
        EditorGUILayout.PropertyField(requireStress, new GUIContent("Require Stress Range"));
        if (requireStress.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("minimumStress"), new GUIContent("Minimum"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maximumStress"), new GUIContent("Maximum"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("requiredNPCNames"), new GUIContent("NPC Names To Talk To"), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Minigames", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("requiredMinigames"), new GUIContent("Required Minigames"), true);
        EditorGUILayout.HelpBox("Every configured minigame is required. Each item can trigger at quest start, a timer (MM:SS), a stress threshold, an NPC conversation, or manually through TriggerMinigame(index).", MessageType.None);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Episode Timer", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("episodeDurationMinutes"), new GUIContent("Duration Minutes"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("episodeDurationSeconds"), new GUIContent("Duration Seconds"));
        EditorGUILayout.HelpBox("The round ends when this timer reaches 00:00. Set both values to 0 to disable the limit.", MessageType.None);

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("onQuestCompleted"), new GUIContent("On Quest Completed"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("onEpisodeFailed"), new GUIContent("On Episode Failed"));
        serializedObject.ApplyModifiedProperties();

        Quest quest = (Quest)target;
        if (!quest.HasCriteria)
            EditorGUILayout.HelpBox("Add at least one collectible, stress range, NPC name, or minigame. An empty quest will not auto-complete.", MessageType.Info);

        if (Application.isPlaying)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Progress", EditorStyles.boldLabel);
            string status = quest.IsCompleted ? "Completed" : quest.IsActive ? "Active" : "Inactive";
            EditorGUILayout.HelpBox(status, quest.IsCompleted ? MessageType.Info : MessageType.None);

            for (int i = 0; i < quest.requiredCollectibles.Count; i++)
            {
                QuestCollectibleRequirement requirement = quest.requiredCollectibles[i];
                if (requirement == null) continue;
                EditorGUILayout.LabelField(
                    string.IsNullOrEmpty(requirement.DisplayName) ? "Unassigned Collectible" : requirement.DisplayName,
                    quest.GetCollectibleProgress(requirement) + " / " + Mathf.Max(1, requirement.requiredQuantity));
            }

            for (int i = 0; i < quest.requiredNPCNames.Count; i++)
            {
                string npcName = quest.requiredNPCNames[i];
                EditorGUILayout.LabelField(npcName, quest.HasTalkedToNPC(npcName) ? "Talked" : "Not Talked");
            }
        }
    }
}
