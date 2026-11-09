using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Chapter 2 Episode 5's hobby keepsake is Joey's old baseball set (it used to be a sketchbook on the desk): the glove
// with the ball in it on the floor beside the wardrobe, and the bat leaning against the wardrobe's side.
// FamilyExpectationsBuilder calls Place when it builds the episode; the menu item fixes an already-built scene.
public static class KeepsakeBaseball
{
    const string ScenePath = "Assets/Scenes/Chapter 2 - Episode 5.unity";
    const float Scale = 2.4f;   // the house is built at ~2.5x (desk tops at 1.86 m)

    [MenuItem("Mindscape/Chapter 2 - Family Expectations/Episode 5 · Baseball Keepsake by the Wardrobe")]
    public static void ApplyToScene()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play mode first."); return; }
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
        var hobby = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "Old hobby item");
        if (hobby == null) { Debug.LogWarning(ScenePath + " has no 'Old hobby item'."); return; }
        Place(hobby);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Baseball keepsake placed at " + hobby.position);
    }

    public static void Place(Transform hobby)
    {
        bool wasActive = hobby.gameObject.activeSelf;
        hobby.gameObject.SetActive(true);
        var wardrobe = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(r => r.name == "West bedroom | wardrobe" && r.gameObject.activeInHierarchy);
        if (wardrobe == null) { Debug.LogWarning("No 'West bedroom | wardrobe' to stand the baseball set beside."); return; }
        var w = wardrobe.bounds;
        // The wardrobe's back is on the north wall; its west side faces the free floor toward the bedroom door.
        Vector3 floor = new Vector3(w.min.x - .75f, w.min.y, w.min.z + .55f);
        hobby.position = floor + Vector3.up * .05f;
        for (int i = hobby.childCount - 1; i >= 0; i--) Object.DestroyImmediate(hobby.GetChild(i).gameObject);

        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(FlashbackAssets.BaseballModel);
        if (asset == null) { Debug.LogWarning("Missing " + FlashbackAssets.BaseballModel); return; }
        var set = (GameObject)PrefabUtility.InstantiatePrefab(asset, hobby);
        set.name = "Collectible model";   // the name CollectibleModels leaves alone
        set.transform.SetPositionAndRotation(floor, Quaternion.identity);
        set.transform.localScale = Vector3.one * Scale;
        foreach (var c in set.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        Transform Piece(string n) => set.GetComponentsInChildren<Transform>(true).First(t => t.name == n);
        var glove = Piece("Baseball glove"); var ball = Piece("Baseball"); var bat = Piece("Baseball bat");
        // Glove on the floor, turned a little; the ball resting in its palm.
        glove.SetPositionAndRotation(floor + new Vector3(-.15f, 0, -.1f), Quaternion.Euler(0, 35, 0));
        glove.position += Vector3.up * (floor.y - glove.GetComponent<Renderer>().bounds.min.y);
        var g = glove.GetComponent<Renderer>().bounds;
        ball.position = new Vector3(g.center.x, g.max.y - .02f, g.center.z) + Vector3.up * (ball.position.y - ball.GetComponent<Renderer>().bounds.min.y);
        // The bat: knob on the floor, leaning back against the wardrobe's side.
        Vector3 foot = new Vector3(w.min.x - .55f, w.min.y, w.min.z + 1.05f);
        bat.SetPositionAndRotation(foot, Quaternion.AngleAxis(-14, Vector3.forward));
        hobby.gameObject.SetActive(wasActive);
    }
}
