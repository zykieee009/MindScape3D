using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

public static class MainMenuNewSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/main_menu_new.unity";

    [MenuItem("Mindscape/Main Menu New/Create Scene")]
    public static void CreateScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        { EditorSceneManager.OpenScene(ScenePath); return; }
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
        { Debug.LogWarning("Save the current scene before creating the new menu."); return; }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var camera = Camera.main; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.06f,.08f,.09f);
        var panel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI Toolkit/PanelSettings.asset"));
        panel.name = "MainMenuNewPanel"; panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panel.referenceResolution = new Vector2Int(1280,720); panel.screenMatchMode = PanelScreenMatchMode.Expand;
        AssetDatabase.CreateAsset(panel, "Assets/UI Toolkit/MainMenuNew/MainMenuNewPanel.asset");
        var go = new GameObject("Main Menu New");
        var doc = go.AddComponent<UIDocument>(); doc.panelSettings = panel;
        doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI Toolkit/MainMenuNew/MainMenuNew.uxml");
        var controller = go.AddComponent<MainMenuNewController>();
        controller.background = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/MenuArt/Background3D.png");
        controller.Rebuild();
        EditorSceneManager.SaveScene(scene, ScenePath);
        // Append without changing the current game's launch scene or existing build indices.
        if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
        AssetDatabase.SaveAssets(); Selection.activeGameObject = go;
        Debug.Log("Created " + ScenePath);
    }
}
