using UnityEditor;
using UnityEngine;

public static class MinigameMenu
{
    [MenuItem("GameObject/Minigame/Breathing Minigame", false, 10)]
    static void CreateBreathingMinigame(MenuCommand menuCommand)
    {
        GameObject minigameObject = new GameObject("Breathing Minigame", typeof(BreathingMinigame));
        GameObject selected = menuCommand.context as GameObject;
        if (selected != null)
            GameObjectUtility.SetParentAndAlign(minigameObject, selected);

        Undo.RegisterCreatedObjectUndo(minigameObject, "Create Breathing Minigame");
        Selection.activeGameObject = minigameObject;
        EditorGUIUtility.PingObject(minigameObject);
    }
}
