using System.Collections;
using System.Linq;
using UnityEngine;

namespace Mindscape.AcademicPressure
{
    // Coming home through the front door with the family there to greet you (Chapter 2 Episode 1, Chapter 3 Episode 7).
    // The family is moved, before you get there, to a clear spot just inside the front door; when you walk in they turn
    // to you, step closer, and talk (their "Talk" clip), while the camera frames everyone from inside the house rather
    // than from behind you, where the door and the wall are.
    public static class HomeWelcome
    {
        // Talk controllers (Idle, Talk = Conversation, Walk) live in Resources/FamilyTalk, one per cast model, built by
        // Mindscape > NPCs > Build Family Talk Controllers.
        static readonly string[] Models = { "NPC_11_Amara", "NPC_13_David", "NPC_16_Tariq", "NPC_06_Ethan", "NPC_01_Maya", "NPC_03_Leo", "NPC_05_Chloe", "NPC_08_Zara" };

        public static Transform FindPerson(params string[] names)
        {
            foreach (var n in names)
            {
                var npc = Object.FindObjectsByType<NPC>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(x => x.npcName == n || x.name == n);
                if (npc != null) return npc.transform;
                var go = GameObject.Find(n);
                if (go != null) return go.transform;
            }
            return null;
        }

        // The entrance door (Door_Entrance), and the way into the house from it, toward 'insidePoint' (any point in the house).
        public static bool FrontDoor(Vector3 insidePoint, out Vector3 door, out Vector3 inward)
        {
            var doors = Object.FindObjectsByType<DoorInteraction>(FindObjectsSortMode.None);
            var front = doors.Where(d => d.name.Contains("Entrance")).OrderBy(d => (d.transform.position - insidePoint).sqrMagnitude).FirstOrDefault()
                ?? doors.OrderBy(d => (d.transform.position - insidePoint).sqrMagnitude).FirstOrDefault();
            if (front == null) { door = insidePoint; inward = Vector3.back; return false; }
            door = front.transform.position;
            // Doors are axis-aligned: the way in is the main axis toward the inside point.
            Vector3 d = insidePoint - door; d.y = 0;
            inward = Mathf.Abs(d.x) > Mathf.Abs(d.z) ? new Vector3(Mathf.Sign(d.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(d.z));
            return true;
        }

        // Clear floor for a standing person near 'around' (no furniture, no wall), searching outward in rings.
        public static Vector3 ClearFloor(Vector3 around, float floorY, float radius = .5f, float height = 3f)
        {
            for (int ring = 0; ring < 5; ring++)
                for (int i = 0; i < (ring == 0 ? 1 : 12); i++)
                {
                    float a = i * Mathf.PI / 6, r = ring * .6f;
                    Vector3 c = around + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                    if (!Physics.Raycast(new Vector3(c.x, floorY + 2.5f, c.z), Vector3.down, out var hit, 5, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (Mathf.Abs(hit.point.y - floorY) > .4f) continue;   // furniture, not floor
                    var blockers = Physics.OverlapCapsule(hit.point + Vector3.up * (radius + .1f), hit.point + Vector3.up * height, radius, ~0, QueryTriggerInteraction.Ignore);
                    if (blockers.Any(b => b.GetComponentInParent<CharacterController>() == null && b.GetComponentInParent<NPC>() == null)) continue;
                    return hit.point;
                }
            return new Vector3(around.x, floorY, around.z);
        }

        // Puts someone at a spot, facing a point, and stops them wandering off.
        public static void Place(Transform who, Vector3 at, Vector3 face)
        {
            if (who == null) return;
            foreach (var w in who.GetComponentsInChildren<NPCWander>(true)) w.enabled = false;
            Vector3 look = face - at; look.y = 0;
            who.SetPositionAndRotation(at, look.sqrMagnitude > .001f ? Quaternion.LookRotation(look) : who.rotation);
            who.gameObject.SetActive(true);
        }

        static Animator Body(Transform who) => who == null ? null : who.GetComponentInChildren<Animator>(true);

        // Swaps in the talk controller (once) so "Idle", "Talk" and "Walk" can be played by name.
        static bool Ready(Animator a)
        {
            if (a == null) return false;
            if (a.runtimeAnimatorController != null && a.runtimeAnimatorController.name.EndsWith(" Talk")) return true;
            string current = (a.runtimeAnimatorController != null ? a.runtimeAnimatorController.name : "") + " " + (a.avatar != null ? a.avatar.name : "") + " " + a.gameObject.name;
            foreach (var m in Models)
            {
                string person = m.Substring(m.LastIndexOf('_') + 1);   // Amara, David, Tariq
                if (!current.Contains(m) && !current.Contains(person)) continue;
                var controller = Resources.Load<RuntimeAnimatorController>("FamilyTalk/" + m + " Talk");
                if (controller == null) return false;
                a.runtimeAnimatorController = controller; a.applyRootMotion = false;
                return true;
            }
            return false;
        }

        public static void Play(Transform who, string state)
        {
            var a = Body(who);
            if (Ready(a) && a.HasState(0, Animator.StringToHash(state))) a.CrossFade(state, .25f);
        }

        // A few steps toward someone, with the walk clip.
        public static IEnumerator StepToward(Transform who, Vector3 to, float seconds)
        {
            if (who == null) yield break;
            Vector3 from = who.position; to.y = from.y;
            if ((to - from).sqrMagnitude < .01f) yield break;
            Play(who, "Walk");
            Quaternion face = Quaternion.LookRotation(Vector3.ProjectOnPlane(to - from, Vector3.up));
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                who.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, t / seconds));
                who.rotation = Quaternion.Slerp(who.rotation, face, Time.deltaTime * 8);
                yield return null;
            }
            who.position = to;
            Play(who, "Idle");
        }

        public static IEnumerator TurnTo(Transform who, Vector3 point, float seconds = .4f)
        {
            if (who == null) yield break;
            Vector3 look = point - who.position; look.y = 0;
            if (look.sqrMagnitude < .001f) yield break;
            Quaternion from = who.rotation, to = Quaternion.LookRotation(look);
            for (float t = 0; t < seconds; t += Time.deltaTime) { who.rotation = Quaternion.Slerp(from, to, t / seconds); yield return null; }
            who.rotation = to;
        }

        // A camera position near 'desired' that has a clear line to 'target' (pulled in before any wall).
        public static Vector3 ClearView(Vector3 target, Vector3 desired)
        {
            Vector3 d = desired - target;
            foreach (var hit in Physics.RaycastAll(target, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            {
                if (hit.collider.GetComponentInParent<CharacterController>() != null || hit.collider.GetComponentInParent<NPC>() != null) continue;
                return target + d.normalized * Mathf.Max(.5f, hit.distance - .35f);
            }
            return desired;
        }
    }
}
