using UnityEditor;
using UnityEngine;

/// <summary>
/// Checks that a story shot never starts inside the player's head. AcademicEpisode.ShotOver backs the camera off from its
/// target toward the player, so it lands on them whenever they stand about that far away; Shot() now passes the position through
/// ShotFocus.ClearOfBody. This walks the player around every target at every distance an interaction allows, for every
/// (back, up) pair the episodes use, and fails if any shot ends up inside the body's clearance.
/// </summary>
public static class StoryShotClearanceCheck
{
    // Every ShotOver(target, back, up) pair used by the episodes, plus the default (2.4, 1.6).
    static readonly float[,] Shots =
    {
        { 2.4f, 1.6f }, { 1.6f, 1.2f }, { 1.6f, 1.3f }, { 1.7f, 1.1f }, { 1.8f, 1.2f }, { 1.8f, 1.3f },
        { 2f, 1.3f }, { 2f, 1.4f }, { 2.2f, 1.6f }, { 2.6f, 1.4f }, { 3.2f, 2.4f }, { 4.4f, .7f },
    };

    [MenuItem("Mindscape/Verify/Story Shot Clearance")]
    public static void Run()
    {
        // The player capsule: 3.5 tall, 0.4 wide, at the 1.4 scale the story scenes use.
        const float scale = 1.4f, height = 3.5f * scale, radius = .4f * scale, clearance = .9f, reach = 3.2f;
        Vector3 feet = Vector3.zero;
        int checkedShots = 0, moved = 0, failures = 0;
        float worst = float.MaxValue;

        for (int s = 0; s < Shots.GetLength(0); s++)
            for (float distance = .3f; distance <= reach + .2f; distance += .1f)
                for (int angle = 0; angle < 360; angle += 15)
                {
                    float a = angle * Mathf.Deg2Rad;
                    Vector3 target = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * distance + Vector3.up * .8f;
                    // Same maths as AcademicEpisode.ShotOver.
                    Vector3 from = Vector3.ProjectOnPlane(feet - target, Vector3.up);
                    if (from.sqrMagnitude < .01f) from = Vector3.back;
                    Vector3 camera = target + from.normalized * Shots[s, 0] + Vector3.up * Shots[s, 1];

                    Vector3 cleared = ShotFocus.ClearOfBody(camera, target, feet, height, radius, clearance);
                    checkedShots++;
                    if (cleared != camera) moved++;

                    // A shot right beside the player is of the player; those are left alone on purpose.
                    float targetOffAxis = Vector2.Distance(new Vector2(target.x, target.z), Vector2.zero);
                    if (targetOffAxis < radius + .4f) continue;

                    float y = Mathf.Clamp(cleared.y, feet.y, feet.y + height);
                    float gap = Vector3.Distance(cleared, new Vector3(0, y, 0)) - radius;
                    worst = Mathf.Min(worst, gap);
                    if (gap < clearance - .001f) failures++;
                }

        string summary = $"Story shot clearance: {checkedShots} shots, {moved} moved clear, closest camera to the body {worst:F2} m (needs {clearance:F2} m).";
        if (failures == 0) Debug.Log(summary + " PASS");
        else Debug.LogError(summary + $" FAIL: {failures} shots start inside the player.");
    }
}
