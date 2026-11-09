using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

public static class QuestHUDBuilder
{
    const string UxmlPath = "Assets/UI Toolkit/QuestHUD.uxml";
    const string PanelSettingsPath = "Assets/UI Toolkit/PanelSettings.asset";

    [MenuItem("GameObject/Quest HUD", false, 10)]
    [MenuItem("Tools/Setup UI Toolkit Quest HUD")]
    public static void SetupQuestHUD()
    {
        EnsureQuestHUD(true);
    }

    public static GameObject EnsureQuestHUD(bool saveScene = false)
    {
        VisualTreeAsset uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
        PanelSettings settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
        if (uxml == null)
        {
            Debug.LogError("Could not load Quest HUD UXML at " + UxmlPath);
            return null;
        }

        GameObject hudObject = GameObject.Find("UI_Toolkit_QuestHUD");
        if (hudObject == null)
        {
            hudObject = new GameObject("UI_Toolkit_QuestHUD");
            Undo.RegisterCreatedObjectUndo(hudObject, "Create Quest HUD");
        }

        UIDocument document = hudObject.GetComponent<UIDocument>();
        if (document == null) document = Undo.AddComponent<UIDocument>(hudObject);
        document.visualTreeAsset = uxml;
        document.panelSettings = settings;
        document.sortingOrder = 30;

        if (hudObject.GetComponent<QuestHUDController>() == null)
            Undo.AddComponent<QuestHUDController>(hudObject);

        EditorUtility.SetDirty(hudObject);
        EditorSceneManager.MarkSceneDirty(hudObject.scene);
        if (saveScene) EditorSceneManager.SaveScene(hudObject.scene);
        Selection.activeGameObject = hudObject;
        return hudObject;
    }
}
