using UnityEngine;

// One [ E ] at a time. Story spots (AcademicEpisode), doors (DoorInteraction) and NPCs (NPCInteractionManager) each offer the
// thing in reach every frame with a score (about its distance, lower is better); the best offer of the frame owns the key and the
// prompt on the next frame, so a press never opens a door and starts a conversation at once, and only one prompt shows.
public static class InteractFocus
{
    // A story spot on someone (Talk to Sam) wins over that person's own small talk.
    public const float StoryBonus = 1f;

    static int frame = -1;
    static object best, owner;
    static float bestScore;

    static void Tick()
    {
        if (frame == Time.frameCount) return;
        owner = frame == Time.frameCount - 1 ? best : null;
        best = null; bestScore = float.MaxValue; frame = Time.frameCount;
    }

    public static void Offer(object who, float score)
    {
        Tick();
        if (who != null && score < bestScore) { bestScore = score; best = who; }
    }

    // Whether this owns the key now (it had the best offer last frame).
    public static bool Has(object who)
    {
        Tick();
        return who != null && ReferenceEquals(owner, who);
    }
}
