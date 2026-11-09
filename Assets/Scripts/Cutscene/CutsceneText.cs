using UnityEngine;
using TMPro;

// Holds one line of cutscene dialogue. Edit Speaker Name and Dialogue in the
// Inspector — the text renders live on the attached TextMeshProUGUI, both in
// Edit Mode and Play Mode.
[RequireComponent(typeof(TextMeshProUGUI))]
public class CutsceneText : MonoBehaviour
{
    [Header("Dialogue")]
    [SerializeField] string speakerName;
    [SerializeField, TextArea(3, 10)] string dialogue;

    TextMeshProUGUI _label;

    public string SpeakerName => speakerName;
    // In Play mode the default protagonist name in the line becomes the active player's name.
    public string Dialogue => Application.isPlaying ? Mindscape.Data.MindscapeDatabase.Personalize(dialogue) : dialogue;

    void Awake()
    {
        _label = GetComponent<TextMeshProUGUI>();
        Apply();
    }

    void OnValidate()
    {
        if (_label == null) _label = GetComponent<TextMeshProUGUI>();
        Apply();
    }

    void Apply()
    {
        if (_label != null) _label.text = Dialogue;
    }
}
