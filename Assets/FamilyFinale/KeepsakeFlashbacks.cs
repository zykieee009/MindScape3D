using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mindscape.AcademicPressure;
using UnityEngine;

namespace Mindscape.FamilyFinale
{
    // Episode 5's keepsakes open into memories. Examining one zooms in on it, then the screen fades into the past,
    // played out with a younger Joey (Resources/Flashback, built by Mindscape > Flashbacks > Build Flashback Assets):
    //   the certificate   Senior high recognition day: home with the Academic Achiever certificate, the family around him.
    //   the baseball set  Saturday afternoons: playing catch with Justin and Aaron on the lawn out front.
    //   the notebook      A younger Joey at this desk, writing his plans down one by one.
    // The memory fades out again onto the keepsake, and the present thought follows.
    public sealed partial class FamilyFinaleEpisode
    {
        static readonly string[] Labels = { "Senior high  ·  Recognition day", "Grade school  ·  Saturday afternoons", "Junior high  ·  A notebook full of plans" };
        static readonly string[] Opening = {
            "Academic Achiever. Senior high. I remember walking home with this, holding it with both hands.",
            "My old glove and bat. Saturday afternoons were for baseball.",
            "This notebook is full of plans and ideas. Sometimes I wonder what would have happened if I'd continued." };
        static readonly string[] Afterward = {
            "Back then, I was proud of myself just for trying my best. That moment mattered, even if it didn't lead to a bigger award.",
            "I wasn't trying to be the best. I just loved playing. When did everything I do start needing to become something impressive?",
            "Maybe I could have become better at it. Maybe I could have achieved more by now." };

        readonly List<GameObject> stage = new List<GameObject>();
        readonly List<(Transform who, Vector3 at, Quaternion turn)> moved = new List<(Transform, Vector3, Quaternion)>();
        readonly List<Behaviour> paused = new List<Behaviour>();
        Light sun; float sunIntensity; Color sunColor, savedAmbient; Quaternion sunTurn;
        bool memoryPlaying;
        float lampWas = -1;
        Coroutine drift;   // the slow camera move inside a memory; stopped when the memory ends

        IEnumerator Inspect(int index)
        {
            var item = keepsakes[index];
            Vector3 c = Centre(item);
            Vector3 away = Flat(bedroomView.position - c);
            // Close in on the keepsake.
            yield return Glide(c + away * 1.6f + Vector3.up * 1.1f, c, 1.3f);
            yield return Think(Opening[index]);
            yield return FlashbackIn(Labels[index], () => { player.gameObject.SetActive(false); Stage(index); }, .6f);
            memoryPlaying = true;
            if (index == 0) yield return RecognitionDay();
            else if (index == 1) yield return Saturdays();
            else yield return Plans();
            memoryPlaying = false;
            yield return FlashbackOut(() => { ClearStage(); player.gameObject.SetActive(true); Shot(c + away * 1.6f + Vector3.up * 1.1f, c); }, .6f);
            yield return Think(Afterward[index]);
            AddStress(index == 0 ? -4 : 4);
            SeenMask |= 1 << index;
            SetObjective("Remembering the past", Check((SeenMask & 1) != 0, KeepsakeNames[0]) + Check((SeenMask & 2) != 0, KeepsakeNames[1]) + Check((SeenMask & 4) != 0, KeepsakeNames[2]));
        }

        // ---------------------------------------------------------------- the memories

        Transform young, friendA, friendB, ball, certificate;
        Vector3 catchSide, catchStreet, certificateView, certificateEnd;
        Vector3 scenePoint;

        // Built while the screen is black.
        void Stage(int index)
        {
            float me = player.lossyScale.x * .86f;                       // a younger, slightly smaller Joey
            float friend = player.lossyScale.x * (2.45f / 2.53f) * .86f;
            if (index == 0)
            {
                // Just home through the front door, certificate in hand; the family comes to meet him in the hall.
                HomeWelcome.FrontDoor(bedroomSpawn.position, out var door, out var inward);
                float floorY = mother != null ? mother.position.y : player.position.y;
                Vector3 entry = new Vector3(door.x, floorY, door.z), side = Vector3.Cross(Vector3.up, inward);
                scenePoint = HomeWelcome.ClearFloor(entry + inward * 3.4f, floorY);
                Vector3 family = entry + inward * 6.6f;
                young = Spawn("Joey", scenePoint, family, me);
                PlayerOutfit.Wear(young, Outfit.SchoolUniform);
                Gather(mother, HomeWelcome.ClearFloor(family + side * 1.7f, floorY), scenePoint);
                Gather(father, HomeWelcome.ClearFloor(family - side * 1.7f, floorY), scenePoint);
                Gather(tim, HomeWelcome.ClearFloor(family - inward * .9f + side * .2f + side * 3.1f, floorY), scenePoint);
                certificate = Certificate(young);
                Vector3 mid = (scenePoint + family) * .5f + Vector3.up * 2;
                certificateView = HomeWelcome.ClearView(mid, mid + side * 7f + Vector3.up * 2.2f);
                certificateEnd = HomeWelcome.ClearView(mid, mid - side * 6f + inward * 2.5f + Vector3.up * 1.6f);
                Shot(certificateView, mid);
            }
            else if (index == 1)
            {
                // The front lawn (beside the path to the door, away from the driveway), on a bright afternoon.
                HomeWelcome.FrontDoor(bedroomSpawn.position, out var door, out var inward);
                Vector3 street = -inward, lawnSide = LawnSide(door, street);
                Vector3 lawn = Ground(door + street * 6f + lawnSide * 10f);
                scenePoint = lawn;
                Vector3 a = Ground(lawn - lawnSide * 3.4f), b = Ground(lawn + lawnSide * 3f - street * 1.8f), d = Ground(lawn + lawnSide * .6f + street * 3.4f);
                young = Spawn("Joey", a, lawn, me);
                friendA = Spawn("Justin", b, lawn, friend);
                friendB = Spawn("Aaron", d, lawn, friend);
                catchSide = lawnSide; catchStreet = street;
                Afternoon();
                var set = Instantiate(Resources.Load<GameObject>("Flashback/Baseball set")); stage.Add(set);
                Transform Piece(string n) => set.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);
                float s = player.lossyScale.x * 1.6f;
                Hold(young, Piece("Baseball glove"), "hand.L", s);
                Hold(friendA, Piece("Baseball bat"), "hand.R", s, Quaternion.Euler(0, 0, 0));
                ball = Piece("Baseball"); if (ball != null) { ball.SetParent(null, true); ball.localScale = Vector3.one * s * 1.4f; stage.Add(ball.gameObject); }
                // From the path, across the lawn.
                Vector3 eye = lawn + Vector3.up * 1.6f;
                Shot(HomeWelcome.ClearView(eye, lawn - lawnSide * 8.5f + street * 1.5f + Vector3.up * 4.6f), eye);
            }
            else
            {
                // This room, years ago: at the desk, writing.
                var seat = SeatTop(out var facing);
                young = Spawn("Joey", seat, seat + facing, me);
                PlayerOutfit.Wear(young, Outfit.Home);
                Sit(young, seat, facing);
                Vector3 side = Vector3.Cross(Vector3.up, facing);
                Shot(seat - facing * 2.4f + side * 2.6f + Vector3.up * 3.4f, seat + facing * .9f + Vector3.up * 1.3f);
                if (eveningLight != null) { lampWas = eveningLight.intensity; eveningLight.intensity *= 1.4f; }
            }
        }

        IEnumerator RecognitionDay()
        {
            // Slowly round from beside the hall to the family's side, onto his face.
            drift = StartCoroutine(Glide(certificateEnd, young.position + Vector3.up * 2.1f, 11f));
            Play(young, "Talk");
            foreach (var f in new[] { mother, father, tim }) HomeWelcome.Play(f, "Talk");
            yield return DialogueSequence(
                Say("Mother", "Academic Achiever! Come here, let me see it."),
                Say("Father", "All those late nights. We're proud of you."),
                BeforeDialogue(Talking(tim), Say("Tim", "Can I hold it? I'll be careful!")),
                BeforeDialogue(Talking(young), Me("I just tried my best.")),
                BeforeDialogue(Talking(mother), Say("Mother", "And that's exactly what we're proud of.")));
            foreach (var f in new[] { mother, father, tim }) HomeWelcome.Play(f, "Idle");
        }

        IEnumerator Saturdays()
        {
            StartCoroutine(PlayCatch());
            Vector3 eye = scenePoint + Vector3.up * 1.6f;
            drift = StartCoroutine(Glide(HomeWelcome.ClearView(eye, scenePoint - catchSide * 6.5f + catchStreet * 3.5f + Vector3.up * 5f), eye, 14f));
            yield return new WaitForSeconds(2.2f);
            yield return DialogueSequence(
                Say("Justin", "Over here! Throw it to me!"),
                Say("Aaron", "Nice catch, " + PlayerName + "!"),
                Me("Again! Higher this time!"),
                Think("We played until the streetlights came on. Nobody kept score."));
        }

        IEnumerator Plans()
        {
            Play(young, "Writing");
            Vector3 head = young.position + Vector3.up * player.lossyScale.x * 1.3f;
            drift = StartCoroutine(Glide(head + Flat(keepsakes[2].position - head) * -1.6f + Vector3.up * 1.2f + Vector3.Cross(Vector3.up, Flat(keepsakes[2].position - head)) * 1.4f, keepsakes[2].position, 10f));
            yield return DialogueSequence(
                Think("Plans: learn to draw comics. Join the science fair. See the ocean. Maybe become an engineer, or a pilot."),
                Think("I wrote every idea down, as if every one of them was possible. Back then, it felt like they were."));
        }

        // Three of them throwing a ball round in turns while the memory plays.
        IEnumerator PlayCatch()
        {
            var people = new[] { young, friendA, friendB }.Where(p => p != null).ToArray();
            if (ball == null || people.Length < 2) yield break;
            int from = 0;
            while (memoryPlaying)
            {
                int to = (from + 1) % people.Length;
                var thrower = people[from]; var catcher = people[to];
                StartCoroutine(TurnTowards(thrower, catcher.position, .25f));
                StartCoroutine(TurnTowards(catcher, thrower.position, .25f));
                Play(thrower, "Talk");
                yield return new WaitForSeconds(.35f);
                Vector3 a = HandOf(thrower), b = HandOf(catcher);
                float arc = 1.6f + Vector3.Distance(a, b) * .18f;
                for (float t = 0; t < 1.05f && ball != null; t += Time.deltaTime)
                {
                    float k = t / 1.05f;
                    ball.position = Vector3.Lerp(a, HandOf(catcher), k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * arc;
                    ball.Rotate(720 * Time.deltaTime, 0, 0, Space.Self);
                    yield return null;
                }
                Play(thrower, "Idle");
                if (catcher != young) Play(catcher, "Talk");
                yield return new WaitForSeconds(.6f);
                Play(catcher, "Idle");
                from = to;
            }
        }

        // ---------------------------------------------------------------- staging helpers

        Transform Spawn(string prefab, Vector3 at, Vector3 face, float scale)
        {
            var asset = Resources.Load<GameObject>("Flashback/" + prefab);
            if (asset == null) { Debug.LogWarning("Missing Resources/Flashback/" + prefab + " (Mindscape > Flashbacks > Build Flashback Assets)."); return null; }
            var go = Instantiate(asset); go.name = prefab + " (memory)"; stage.Add(go);
            go.transform.localScale = Vector3.one * scale;
            Vector3 look = Flat(face - at);
            // The rig's feet sit 0.12 below its origin at rest.
            go.transform.SetPositionAndRotation(at + Vector3.up * .12f * scale, Quaternion.LookRotation(look));
            var a = go.GetComponent<Animator>(); if (a != null) a.Update(0);
            return go.transform;
        }

        static void Play(Transform who, string state)
        {
            var a = who != null ? who.GetComponent<Animator>() : null;
            if (a != null && a.HasState(0, Animator.StringToHash(state))) a.CrossFade(state, .2f);
        }

        static IEnumerator Talking(Transform who) { Play(who, "Talk"); HomeWelcome.Play(who, "Talk"); yield break; }

        static Transform Bone(Transform who, string name) => who == null ? null : who.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == name);
        Vector3 HandOf(Transform who) { var h = Bone(who, "hand.R") ?? Bone(who, "hand.L"); return h != null ? h.position : who.position + Vector3.up * 1.4f; }

        // Something held in a hand: it follows the hand from now on.
        void Hold(Transform who, Transform item, string hand, float scale, Quaternion? turn = null)
        {
            if (who == null || item == null) return;
            var bone = Bone(who, hand); if (bone == null) return;
            item.SetParent(null, true); item.localScale = Vector3.one * scale;
            item.SetPositionAndRotation(bone.position, who.rotation * (turn ?? Quaternion.Euler(0, 0, 90)));
            item.SetParent(bone, true);
            stage.Add(item.gameObject);
        }

        // The certificate in a simple gold frame, held up in his right hand.
        Transform Certificate(Transform who)
        {
            var root = new GameObject("Academic Achiever certificate").transform; stage.Add(root.gameObject);
            var gold = Mindscape.SocialDynamics.SocialStaging.Solid(new Color(.86f, .68f, .32f));
            var paper = Mindscape.SocialDynamics.SocialStaging.Solid(new Color(.97f, .94f, .86f));
            void Part(Vector3 local, Vector3 size, Material m)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(p.GetComponent<Collider>());
                p.transform.SetParent(root, false); p.transform.localPosition = local; p.transform.localScale = size; p.GetComponent<Renderer>().sharedMaterial = m;
            }
            Part(Vector3.zero, new Vector3(.62f, .8f, .04f), gold);
            Part(new Vector3(0, 0, -.025f), new Vector3(.52f, .7f, .01f), paper);
            Part(new Vector3(0, -.2f, -.032f), new Vector3(.12f, .12f, .01f), gold);   // the seal
            var hand = Bone(who, "hand.R");
            if (hand != null)
            {
                root.SetPositionAndRotation(hand.position + who.forward * .25f + Vector3.up * .25f, who.rotation * Quaternion.Euler(-10, 180, 0));
                root.SetParent(hand, true);
            }
            else root.SetPositionAndRotation(who.position + who.forward * .6f + Vector3.up * 1.6f, who.rotation * Quaternion.Euler(-10, 180, 0));
            return root;
        }

        // A family member stepped into the memory; put back afterwards.
        void Gather(Transform who, Vector3 at, Vector3 face)
        {
            if (who == null) return;
            moved.Add((who, who.position, who.rotation));
            foreach (var b in who.GetComponentsInChildren<NPCWander>(true).Where(b => b.enabled)) paused.Add(b);
            HomeWelcome.Place(who, at, face);
        }

        // The study chair's seat (or the floor in front of the desk), and the way it faces the desk.
        Vector3 SeatTop(out Vector3 facing)
        {
            var desk = keepsakes[2].position;
            var seat = FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.name == "Seat" && r.transform.parent != null && r.transform.parent.name.Contains("chair"))
                .OrderBy(r => (r.bounds.center - desk).sqrMagnitude).FirstOrDefault();
            if (seat == null) { facing = Vector3.right; return Ground(desk - Vector3.right * 1.2f); }
            facing = Flat(desk - seat.bounds.center);
            facing = Mathf.Abs(facing.x) > Mathf.Abs(facing.z) ? new Vector3(Mathf.Sign(facing.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(facing.z));
            var b = seat.bounds; return new Vector3(b.center.x, b.max.y, b.center.z);
        }

        // Seated: the hips on the seat top, as the classroom builder seats people.
        void Sit(Transform who, Vector3 seat, Vector3 facing)
        {
            if (who == null) return;
            Play(who, "Writing");
            var a = who.GetComponent<Animator>(); if (a != null) { a.Play("Writing", 0, 0); a.Update(0); }
            var hips = Bone(who, "hips");
            float h = 2.54f * who.lossyScale.y;
            Vector3 want = seat + Vector3.up * h * .05f - facing * h * .03f;
            if (hips != null) who.position += want - hips.position;
        }

        void Afternoon()
        {
            sun = FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
            savedAmbient = RenderSettings.ambientLight;
            if (sun != null) { sunIntensity = sun.intensity; sunColor = sun.color; sunTurn = sun.transform.rotation; sun.intensity = 1.1f; sun.color = new Color(1, .88f, .7f); sun.transform.rotation = Quaternion.Euler(38, -35, 0); }
            RenderSettings.ambientLight = new Color(.55f, .55f, .55f);
        }

        void ClearStage()
        {
            if (drift != null) { StopCoroutine(drift); drift = null; }
            foreach (var go in stage) if (go != null) Destroy(go);
            stage.Clear();
            foreach (var (who, at, turn) in moved) if (who != null) { who.SetPositionAndRotation(at, turn); HomeWelcome.Play(who, "Idle"); }
            moved.Clear();
            foreach (var b in paused) if (b != null) b.enabled = true;
            paused.Clear();
            if (sun != null) { sun.intensity = sunIntensity; sun.color = sunColor; sun.transform.rotation = sunTurn; sun = null; RenderSettings.ambientLight = savedAmbient; }
            if (eveningLight != null && lampWas >= 0) { eveningLight.intensity = lampWas; lampWas = -1; }
            young = friendA = friendB = ball = certificate = null;
        }

        static IEnumerator TurnTowards(Transform who, Vector3 point, float seconds)
        {
            if (who == null) yield break;
            Quaternion from = who.rotation, to = Quaternion.LookRotation(Flat(point - who.position));
            for (float t = 0; t < seconds; t += Time.deltaTime) { if (who == null) yield break; who.rotation = Quaternion.Slerp(from, to, t / seconds); yield return null; }
            if (who != null) who.rotation = to;
        }

        // Which side of the front path the open lawn is on: the side with more open ground (the other is the driveway).
        static Vector3 LawnSide(Vector3 door, Vector3 street)
        {
            Vector3 across = Vector3.Cross(Vector3.up, street);
            float Grass(Vector3 dir)
            {
                int n = 0;
                for (int i = 4; i <= 14; i += 2)
                    if (Physics.Raycast(door + street * 6 + dir * i + Vector3.up * 8, Vector3.down, out var hit, 12, ~0, QueryTriggerInteraction.Ignore)
                        && (hit.collider.name.ToLowerInvariant().Contains("lawn") || hit.collider.name.ToLowerInvariant().Contains("grass"))) n++;
                return n;
            }
            return Grass(-across) >= Grass(across) ? -across : across;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude < 1e-4f ? Vector3.forward : v.normalized; }

        static Vector3 Centre(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return t.position;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            return b.center;
        }

        static Vector3 Ground(Vector3 at)
        {
            foreach (var hit in Physics.RaycastAll(at + Vector3.up * 8, Vector3.down, 20, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                if (hit.collider.GetComponentInParent<CharacterController>() == null) return hit.point;
            return at;
        }
    }
}
