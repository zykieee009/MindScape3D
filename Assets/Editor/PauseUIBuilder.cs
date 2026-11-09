using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

public static class PauseUIBuilder
{
    const string UxmlPath = "Assets/UI Toolkit/PauseMenu.uxml";
    const string PanelSettingsPath = "Assets/UI Toolkit/PanelSettings.asset";

    [MenuItem("Tools/Setup UI Toolkit Pause Menu")]
    public static void SetupPauseMenu()
    {
        VisualTreeAsset uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
        PanelSettings settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);

        if (uxml == null)
        {
            Debug.LogError($"Could not load VisualTreeAsset at {UxmlPath}");
            return;
        }

        GameObject pauseObj = GameObject.Find("PauseMenu");
        if (pauseObj == null)
        {
            pauseObj = new GameObject("PauseMenu");
        }

        // Clean up legacy uGUI children (sprite/hitbox based elements)
        for (int i = pauseObj.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(pauseObj.transform.GetChild(i).gameObject);
        }

        // Remove old Canvas / GraphicRaycaster if attached directly
        var canvas = pauseObj.GetComponent<Canvas>();
        if (canvas != null) Object.DestroyImmediate(canvas);

        var raycaster = pauseObj.GetComponent<UnityEngine.UI.GraphicRaycaster>();
        if (raycaster != null) Object.DestroyImmediate(raycaster);

        var canvasGroup = pauseObj.GetComponent<CanvasGroup>();
        if (canvasGroup != null) Object.DestroyImmediate(canvasGroup);

        UIDocument uiDoc = pauseObj.GetComponent<UIDocument>();
        if (uiDoc == null)
        {
            uiDoc = pauseObj.AddComponent<UIDocument>();
        }

        uiDoc.visualTreeAsset = uxml;
        if (settings != null)
        {
            uiDoc.panelSettings = settings;
        }

        PauseMenu controller = pauseObj.GetComponent<PauseMenu>();
        if (controller == null)
        {
            controller = pauseObj.AddComponent<PauseMenu>();
        }

        // Hook up reference in PlayerControls if present
        PlayerControls player = Object.FindFirstObjectByType<PlayerControls>(FindObjectsInactive.Include);
        if (player != null)
        {
            SerializedObject serializedPlayer = new SerializedObject(player);
            var pauseProp = serializedPlayer.FindProperty("pauseMenu");
            if (pauseProp != null)
            {
                pauseProp.objectReferenceValue = controller;
                serializedPlayer.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(player);
            }
        }

        EditorUtility.SetDirty(pauseObj);
        EditorSceneManager.MarkSceneDirty(pauseObj.scene);
        EditorSceneManager.SaveScene(pauseObj.scene);

        Debug.Log("UI Toolkit Pause Menu successfully configured in scene on 'PauseMenu' GameObject.");
    }
}

