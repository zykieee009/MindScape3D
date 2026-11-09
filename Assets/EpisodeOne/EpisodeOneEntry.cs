using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mindscape.EpisodeOne
{
    // Keeps the existing menu's cutscene scene link valid; the scene now owns its cinematics.
    public sealed class EpisodeOneEntry : MonoBehaviour
    {
        void Start() { SceneManager.LoadScene(EpisodeOneStory.SceneName); }
    }
}
