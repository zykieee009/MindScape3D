using UnityEngine;
using UnityEngine.UI;

// Attach to a Button under a CutscenePlayer's canvas. Set Target Slide Index in
// the Inspector to the slide this button should jump to when clicked (0-based,
// matching the Slides list on CutscenePlayer).
[RequireComponent(typeof(Button))]
public class CutsceneNextButton : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] CutscenePlayer player;
    [SerializeField] int targetSlideIndex;

    void Awake()
    {
        if (player == null) player = FindAnyObjectByType<CutscenePlayer>();
        GetComponent<Button>().onClick.AddListener(GoToTargetSlide);
    }

    public void GoToTargetSlide()
    {
        if (player != null) player.GoToSlide(targetSlideIndex);
    }
}
