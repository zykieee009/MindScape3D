using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Attach to a Button. Set Scene Name in the Inspector to the scene this button
// should load when clicked — independent of CutscenePlayer's own episode scene,
// so different buttons (e.g. branching choices) can target different scenes.
[RequireComponent(typeof(Button))]
public class CutscenePlayEpisodeButton : MonoBehaviour
{
    [Header("Target Scene")]
    [SerializeField] string sceneName;

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(PlayEpisode);
    }

    public void PlayEpisode()
    {
        if (!string.IsNullOrEmpty(sceneName))
            SceneManager.LoadScene(sceneName);
    }
}
