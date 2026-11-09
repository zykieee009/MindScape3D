using System.Collections;
using System.Linq;
using Mindscape.AcademicPressure;
using UnityEngine;

namespace Mindscape.SocialDynamics
{
    // Conversations cut to whoever is speaking: an over-the-shoulder shot from behind the listener onto the speaker's
    // face, and the speaker plays their talk clip while the others listen. Thoughts keep the shot they are in.
    public sealed partial class SocialDynamicsEpisode
    {
        Transform lastSpeaker;      // the friend who spoke last; "Me" lines are framed over their shoulder
        bool speakerShots = true;
        int shotSide = 1;           // which side of the you-friend line the conversation camera keeps to   // off while a scene runs its own camera (the group photograph countdown)

        // "Justin" -> Justin, "Friend 2" -> Old friend 2, "Me" -> the player, family names -> Mom/Dad/Tim.
        Transform SpeakerOf(string who)
        {
            if (who == "Me") return player;
            var child = transform.Find(who);
            if (child == null && who.StartsWith("Friend ")) child = transform.Find("Old friend " + who.Substring(7));
            if (child == null && (who == "Mom" || who == "Dad" || who == "Tim")) child = HomeWelcome.FindPerson(who, who == "Mom" ? "Mother" : who == "Dad" ? "Father" : "Tim");
            return child != null && child.gameObject.activeInHierarchy ? child : null;
        }

        Vector3 HeadOf(Transform who)
        {
            if (who == player) return player.position + Vector3.up * PlayerHeight() * .82f;
            var npc = who.GetComponentInChildren<NPC>();
            if (npc != null) return npc.FacePosition;
            var head = who.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.ToLowerInvariant() == "head");
            if (head != null) return head.position + Vector3.up * .15f;
            var rs = who.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return who.position + Vector3.up * 2.5f;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            return new Vector3(b.center.x, b.min.y + b.size.y * .82f, b.center.z);
        }

        // The listener for a line: you, when a friend speaks; the friend you are talking with, when you speak.
        Transform ListenerFor(Transform speaker)
        {
            if (speaker != player) return player;
            if (lastSpeaker != null && lastSpeaker.gameObject.activeInHierarchy) return lastSpeaker;
            // Nobody has spoken yet: the nearest friend in the room.
            return transform.Cast<Transform>().Where(t => t.gameObject.activeInHierarchy && (t.name == "Justin" || t.name.StartsWith("Old friend")))
                .OrderBy(t => (t.position - player.position).sqrMagnitude).FirstOrDefault();
        }

        IEnumerator FaceSpeaker(string who)
        {
            if (!speakerShots) yield break;
            var speaker = SpeakerOf(who);
            if (speaker == null) yield break;
            var listener = ListenerFor(speaker);
            // Everyone else listens; the speaker talks.
            foreach (Transform t in transform)
                if (t.name == "Justin" || t.name.StartsWith("Old friend")) HomeWelcome.Play(t, t == speaker ? "Talk" : "Idle");
            foreach (var family in new[] { "Mom", "Dad", "Tim" }.Select(n => SpeakerOf(n)).Where(t => t != null)) HomeWelcome.Play(family, family == speaker ? "Talk" : "Idle");
            if (speaker != player) lastSpeaker = speaker;
            if (listener == null || listener == speaker) yield break;
            Vector3 face = HeadOf(speaker), other = HeadOf(listener);
            float h = PlayerHeight();
            // A three-quarter shot in front of the speaker, turned 42 degrees off the line to the listener, so the listener
            // (big heads!) stays at the edge of the frame instead of in front of the face. The camera keeps to one side of
            // the line between you and your friend for the whole conversation, so the cuts don't flip direction.
            Vector3 toListener = Flat(other - face).normalized;
            float flip = speaker == player ? 1 : -1;
            // A little above eye level, so the shot looks over the heads of anyone standing beside the listener.
            Vector3 Want(int side) => face + Quaternion.AngleAxis(side * flip * 42f, Vector3.up) * toListener * h * .85f + Vector3.up * h * .16f;
            float Room(int side) => (HomeWelcome.ClearView(face, Want(side)) - face).magnitude;
            // Someone else's head right beside the camera would fill a corner of the frame.
            var others = transform.Cast<Transform>().Where(t => t.gameObject.activeInHierarchy && t != speaker && t != listener && (t.name == "Justin" || t.name.StartsWith("Old friend"))).Select(HeadOf).ToArray();
            bool Crowded(int side) { var at = HomeWelcome.ClearView(face, Want(side)); return others.Any(o => (o - at).magnitude < h * .45f || DistanceToLine(o, at, face) < h * .14f); }
            if (Crowded(shotSide) && !Crowded(-shotSide)) shotSide = -shotSide;
            else if (Room(shotSide) < h * .85f * .75f && Room(-shotSide) > Room(shotSide) && !Crowded(-shotSide)) shotSide = -shotSide;
            Vector3 from = HomeWelcome.ClearView(face, Want(shotSide));
            yield return Glide(from, face - Vector3.up * h * .06f, .35f);
        }

        // How far a point is from the segment between the camera and the face it looks at.
        static float DistanceToLine(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a; float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
            return Vector3.Distance(p, a + ab * t);
        }

        // Back to everyone idling once a conversation ends.
        void QuietCast()
        {
            foreach (Transform t in transform)
                if (t.name == "Justin" || t.name.StartsWith("Old friend")) HomeWelcome.Play(t, "Idle");
        }
    }
}
