using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Shows one slide at a time; the player advances manually with Next (there is no
// autoplay and no going back). To add, remove, or reorder slides, edit the Slides
// list in the Inspector — no code changes needed.
public class CutscenePlayer : MonoBehaviour
{
    [Header("Slides — drag each slide's CanvasGroup here, in play order")]
    [SerializeField] CanvasGroup[] slides;

    [Header("Timing (seconds)")]
    [SerializeField] float fadeInDuration = 1f;
    [SerializeField] float fadeOutDuration = 1f;

    [Header("Controls — assign the button objects here")]
    [SerializeField] Button nextButton;
    [SerializeField] Button playEpisodeButton;

    [Header("Episode")]
    [SerializeField] string episodeSceneName = "Chapter 1 - Episode 1";

    int _currentSlide = -1;
    Coroutine _transitionRoutine;

    void Awake()
    {
        foreach (CanvasGroup slide in slides)
        {
            if (slide == null) continue;
            slide.alpha = 0f;
            slide.gameObject.SetActive(false);
        }

        if (nextButton != null) nextButton.onClick.AddListener(Next);
        if (playEpisodeButton != null) playEpisodeButton.onClick.AddListener(PlayEpisode);
    }

    void Start()
    {
        if (slides.Length == 0)
        {
            PlayEpisode();
            return;
        }

        _transitionRoutine = StartCoroutine(TransitionTo(0));
    }

    // Fades out the current slide (if any) and fades in the requested one.
    IEnumerator TransitionTo(int index)
    {
        if (_currentSlide >= 0 && _currentSlide < slides.Length && slides[_currentSlide] != null)
        {
            CanvasGroup current = slides[_currentSlide];
            yield return Fade(current, current.alpha, 0f, fadeOutDuration);
            current.gameObject.SetActive(false);
        }

        _currentSlide = index;
        UpdateControlsVisibility();

        CanvasGroup next = slides[index];
        next.gameObject.SetActive(true);
        yield return Fade(next, 0f, 1f, fadeInDuration);
    }

    // Next shows on every slide except the last; Play Episode only shows on the last slide.
    void UpdateControlsVisibility()
    {
        bool isLastSlide = _currentSlide == slides.Length - 1;
        if (nextButton != null) nextButton.gameObject.SetActive(!isLastSlide);
        if (playEpisodeButton != null) playEpisodeButton.gameObject.SetActive(isLastSlide);
    }

    IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            group.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        group.alpha = to;
    }

    // Advances to the next slide. On the last slide, this starts the episode instead.
    public void Next()
    {
        int nextIndex = _currentSlide + 1;
        if (nextIndex >= slides.Length)
        {
            PlayEpisode();
            return;
        }

        if (_transitionRoutine != null)
            StopCoroutine(_transitionRoutine);
        _transitionRoutine = StartCoroutine(TransitionTo(nextIndex));
    }

    // Jumps directly to the given slide (used by CutsceneNextButton). Out-of-range
    // indices are ignored.
    public void GoToSlide(int index)
    {
        if (index < 0 || index >= slides.Length) return;

        if (_transitionRoutine != null)
            StopCoroutine(_transitionRoutine);
        _transitionRoutine = StartCoroutine(TransitionTo(index));
    }

    // Skips the rest of the cutscene entirely and starts the episode.
    public void PlayEpisode()
    {
        if (_transitionRoutine != null)
            StopCoroutine(_transitionRoutine);

        if (!string.IsNullOrEmpty(episodeSceneName))
            EpisodeLoadingScreen.Load(episodeSceneName, 1);
    }
}
