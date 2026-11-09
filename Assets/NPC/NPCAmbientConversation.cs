using System.Collections;
using TMPro;
using UnityEngine;

// A light-weight street conversation: two NPCs periodically pause, face one another,
// and exchange world-space lines. Player dialogue always takes priority.
[DisallowMultipleComponent]
public sealed class NPCAmbientConversation : MonoBehaviour
{
    public NPC first;
    public NPC second;
    [TextArea(1, 3)] public string[] lines = { "Morning!", "Morning. Beautiful day for a walk." };
    [Min(1f)] public float quietSeconds = 7f;
    [Min(0.5f)] public float lineSeconds = 2.8f;

    TextMeshPro bubble;
    NPCWander firstWander, secondWander;
    Coroutine routine;

    void Start()
    {
        if (first == null || second == null || lines == null || lines.Length == 0) return;
        firstWander = first.GetComponent<NPCWander>();
        secondWander = second.GetComponent<NPCWander>();
        bubble = CreateBubble();
        routine = StartCoroutine(ConversationLoop());
    }

    IEnumerator ConversationLoop()
    {
        int line = 0;
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(quietSeconds * .8f, quietSeconds * 1.2f));
            if (NPCInteractionManager.DialogueActive || PlayerIsNearEither()) continue;

            SetWandering(false);
            for (int exchange = 0; exchange < Mathf.Min(4, lines.Length); exchange++)
            {
                if (NPCInteractionManager.DialogueActive || PlayerIsNearEither()) break;
                FaceEachOther();
                NPC speaker = exchange % 2 == 0 ? first : second;
                Show(speaker, lines[line++ % lines.Length]);
                yield return new WaitForSeconds(lineSeconds);
            }
            Hide();
            SetWandering(true);
        }
    }

    bool PlayerIsNearEither()
    {
        var player = FindAnyObjectByType<PlayerControls>();
        return player != null && (first.IsInRange(player.transform) || second.IsInRange(player.transform));
    }

    void FaceEachOther()
    {
        Vector3 a = second.transform.position - first.transform.position; a.y = 0f;
        if (a.sqrMagnitude < .01f) return;
        first.transform.rotation = Quaternion.LookRotation(a.normalized, Vector3.up);
        second.transform.rotation = Quaternion.LookRotation(-a.normalized, Vector3.up);
    }

    TextMeshPro CreateBubble()
    {
        var go = new GameObject("Ambient speech", typeof(TextMeshPro));
        go.transform.SetParent(transform, false);
        var text = go.GetComponent<TextMeshPro>();
        text.fontSize = .52f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color32(54, 48, 40, 255);
        text.outlineColor = new Color32(247, 236, 210, 255);
        text.outlineWidth = .18f;
        text.enableWordWrapping = true;
        text.rectTransform.sizeDelta = new Vector2(4.2f, .8f);
        text.gameObject.SetActive(false);
        return text;
    }

    void Show(NPC speaker, string line)
    {
        bubble.text = Mindscape.Data.MindscapeDatabase.Personalize(line);
        bubble.transform.position = speaker.FacePosition + Vector3.up * .75f;
        bubble.gameObject.SetActive(true);
    }

    void LateUpdate()
    {
        if (bubble == null || !bubble.gameObject.activeSelf || Camera.main == null) return;
        Vector3 towardCamera = bubble.transform.position - Camera.main.transform.position;
        if (towardCamera.sqrMagnitude > .01f)
            bubble.transform.rotation = Quaternion.LookRotation(towardCamera.normalized, Vector3.up);
    }

    void SetWandering(bool enabled)
    {
        if (firstWander != null) firstWander.enabled = enabled;
        if (secondWander != null) secondWander.enabled = enabled;
    }

    void Hide() { if (bubble != null) bubble.gameObject.SetActive(false); }

    void OnDisable()
    {
        Hide();
        SetWandering(true);
        if (routine != null) StopCoroutine(routine);
    }
}
