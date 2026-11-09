using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mindscape.Data;
using UnityEngine;

namespace Mindscape.SocialDynamics
{
    // The end of Chapter 3, played out instead of told:
    //   Episode 6  the group photograph: walk up onto the stage, the timer counts down, the shutter fires, and the real
    //              picture is kept (it comes back on the walk home and in the dream).
    //   Episode 7  the walk home on a quiet street: memories of the reunion come back at places along the way (no keys),
    //              Joey stops by himself to look at the photograph, greets his family, puts his phone on the desk, lies
    //              down thinking about the day, and falls asleep into a dream.
    //   Episode 8  waking in a dim, open dream with a light far ahead: the music starts with the first slow steps and
    //              good memories light up one by one. Then the mirror: "That's me" on the way up to it, the camera drifting
    //              over memories while he talks, younger Joey fading into the reflection, "It's you!", "Despite everything,
    //              it's still you!", the camera pulling back into the dark, a pencil drawing of the two of them, The End.
    public sealed partial class SocialDynamicsEpisode
    {
        // ================================================================ shared

        // An arrival with no gold marker: walking past it is all it takes, and nothing on screen gives it away.
        Spot Quiet(Spot spot)
        {
            if (spot.marker != null) { Destroy(spot.marker); spot.marker = null; }
            return spot;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude < 1e-4f ? Vector3.forward : v; }

        // The floor (or pavement) under a point, so waypoints sit where the player walks.
        static Vector3 Ground(Vector3 at, float fallbackY)
        {
            // From just above the point, under any ceiling: from higher up the first thing hit was the hall's roof, which
            // put everyone on top of the building for the group photograph.
            foreach (var hit in Physics.RaycastAll(at + Vector3.up * 1.5f, Vector3.down, 10, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                if (hit.collider.GetComponentInParent<CharacterController>() == null) return hit.point;
            return new Vector3(at.x, fallbackY, at.z);
        }

        // A scripted walk (the walk animation plays; nothing collides), ending turned to 'yaw'.
        IEnumerator WalkTo(Vector3 to, float yaw, float seconds)
        {
            if (character != null) character.enabled = false;
            Vector3 from = player.position; Quaternion start = player.rotation;
            Quaternion along = Quaternion.LookRotation(Flat(to - from));
            if (animator != null) animator.SetFloat("Speed", 1);
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                player.position = Vector3.Lerp(from, to, k);
                player.rotation = Quaternion.Slerp(start, k < .85f ? along : Quaternion.Euler(0, yaw, 0), Mathf.Clamp01(k * 5));
                yield return null;
            }
            if (animator != null) animator.SetFloat("Speed", 0);
            Teleport(to, yaw);
        }

        IEnumerator Turn(Transform who, Quaternion to, float seconds)
        {
            if (who == null) yield break;
            Quaternion from = who.rotation;
            for (float t = 0; t < seconds; t += Time.deltaTime) { who.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0, 1, t / seconds)); yield return null; }
            who.rotation = to;
        }

        float PlayerHeight()
        {
            var renderers = player.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return 3.5f;
            var b = renderers[0].bounds; foreach (var r in renderers) b.Encapsulate(r.bounds);
            return Mathf.Max(1, b.size.y);
        }

        // ================================================================ Episode 6 · the group photograph

        IEnumerator GroupPhoto()
        {
            var place = points[6];
            Vector3 stand = Ground(place.position, place.position.y - 1.5f) + Vector3.up * .05f;
            float yaw = place.eulerAngles.y;
            Vector3 front = Flat(place.forward).normalized;   // from the stage out into the room, where the phone stands
            var friends = new List<Transform>();
            foreach (Transform child in transform) if (child.name == "Justin" || child.name.StartsWith("Old friend")) friends.Add(child);

            // Everyone turns to wave you over while you walk up onto the stage.
            Shot(stand + front * 5 + Vector3.Cross(Vector3.up, front) * 3 + Vector3.up * 3.2f, stand + Vector3.up * 2);
            foreach (var f in friends) StartCoroutine(Turn(f, Quaternion.LookRotation(Flat(player.position - f.position)), .6f));
            yield return WalkTo(stand, yaw, Mathf.Clamp(Vector3.Distance(Flat(player.position), Flat(stand)) / 2.2f, 1.2f, 3.5f));
            foreach (var f in friends) StartCoroutine(Turn(f, Quaternion.LookRotation(front), .5f));

            // The phone on its stand, facing the group.
            Vector3 lens = stand + front * 6.8f + Vector3.up * 2.3f;
            Vector3 group = stand - front * .7f + Vector3.up * 2.1f;
            yield return Glide(lens, group, 1.6f);
            ShotFocus.Clear();   // everyone, you included, is in the picture
            speakerShots = false;   // the phone's view stays put for the countdown
            yield return Lines("Justin|The timer's set! Everyone, squeeze in!", "Friend 2|Wait, wait, I'm not ready!", "Justin|Too late! Smile!");
            speakerShots = true;
            for (int n = 3; n >= 1; n--) { effects.PlayOneShot(softTick, .3f); yield return TimedCard(n.ToString(), .75f); }
            ShotFocus.Clear();
            var photo = SocialStaging.Capture(storyCamera, 1200, 800);
            effects.PlayOneShot(shutter != null ? shutter : (shutter = Tone("Shutter", 2400, .12f)), .5f);
            yield return Flash(.8f);
            SocialStaging.SaveGroupPhoto(photo);
            if (groupPhoto != null) groupPhoto.SetActive(true);
            Record("social.group_photo", "taken");
            ShowPhoto(photo, "all of us, tonight");
            speakerShots = false;
            yield return Lines("Me|We should do this again sometime.", "Justin|Definitely.");
            speakerShots = true;
            HidePhoto();
        }
        AudioClip shutter;

        // ================================================================ Episode 7 · the walk home

        IEnumerator Home()
        {
            QuietStreet();
            StageFamily();
            yield return Lines("Justin|Get home safely, okay? I'm glad you came tonight.", "Me|Thanks, Justin. I'm glad I came, too.");

            // Memories of tonight come back at places along the way: no keys, no markers, just walking.
            Vector3 lights = points[0].position, photoAt = points[1].position;
            float walkY = spawn.position.y;
            Vector3[] memoryAt = { Vector3.Lerp(spawn.position, lights, .4f), Vector3.Lerp(spawn.position, lights, .85f), Vector3.Lerp(lights, photoAt, .5f) };
            (string image, string[] lines)[] memories =
            {
                ("EpisodeThumbnails/Chapter3_Episode2", new[] { "Justin|Hey! You made it!", "I almost turned around at the door. I'm glad I didn't." }),
                ("EpisodeThumbnails/Chapter3_Episode6", new[] { "Friend 1|And somehow, we still finished it!", "We laughed about the smallest things, the way we used to." }),
                ("EpisodeThumbnails/Chapter3_Episode5", new[] { "Justin|You still matter, even when you don't feel proud of yourself.", "I didn't know how much I needed to hear that." }),
            };
            int seen = 0; bool streetNoticed = false, photoSeen = false;
            for (int i = 0; i < memoryAt.Length; i++)
            {
                int n = i;
                var point = Waypoint("Memory on the way home " + (n + 1), Ground(memoryAt[n], walkY));
                Quiet(AddArrival(point, "Remember", () => (seen & (1 << n)) == 0, () => Remember(n), 3.2f));
            }
            Quiet(AddArrival(Waypoint("Under the streetlights", Ground(lights, walkY)), "Follow the streetlights", () => !streetNoticed, StreetQuiet, 3.2f));
            AddArrival(Waypoint("A place to stop", Ground(photoAt, walkY)), "Look at the group photograph", () => !photoSeen, LookAtPhoto, 3.4f);
            SetObjective("The walk home", Check(false, "Follow the streetlights"));
            yield return RoamUntil(() => photoSeen);

            // The front door opens with its own [ E ]; stepping inside is the welcome: the family is right there.
            bool home = false;
            AddArrival(points[2], "Go inside", () => !home, () => Welcome(() => home = true));
            yield return RoamUntil(() => home);
            yield return Visit(3, "Back in your room", "Place your phone on the desk", () => { if (deskItems != null) foreach (var item in deskItems) if (item != null) item.SetActive(true); }, null, "Today wasn't as bad as I thought it would be.");
            yield return FallAsleep();

            IEnumerator Remember(int n)
            {
                seen |= 1 << n;
                yield return FlashbackIn("Earlier tonight  ·  the reunion", null, .35f);
                ShowPhoto(Resources.Load<Texture2D>(memories[n].image), "earlier tonight");
                yield return Lines(memories[n].lines);
                HidePhoto();
                yield return FlashbackOut(null, .35f);
            }
            IEnumerator StreetQuiet()
            {
                streetNoticed = true;
                yield return Think("The street is quiet. I can still hear my friends laughing when I think about tonight.");
            }
            IEnumerator LookAtPhoto()
            {
                photoSeen = true;
                // Joey stops under the light and takes out the photograph; the camera comes round over his shoulder.
                Vector3 forward = Flat(player.forward).normalized, right = Vector3.Cross(Vector3.up, forward);
                float h = PlayerHeight();
                yield return Glide(player.position - forward * 1.5f + right * .8f + Vector3.up * h * .95f, player.position + forward * 1.4f + Vector3.up * h * .55f, 2.2f);
                ShowPhoto(SocialStaging.Picture(SocialStaging.GroupPhotoKey), "the five of us, tonight");
                yield return Lines("I almost spent the whole evening hiding from everyone.", "I thought I had to become someone better before I could stand beside them.", "But they didn't ask me to prove anything. They just wanted me to be there.");
                HidePhoto();
                yield return Glide(player.position - forward * 4.5f + Vector3.up * h * 1.2f, player.position + forward * 3 + Vector3.up * h * .5f, 1.2f);
                SetObjective("A warm welcome", Check(false, "Go home"));
            }
        }

        // The street is empty tonight: the walkers and joggers have gone home (the family inside the house stays).
        static bool IsFamily(NPC npc) => npc != null && (npc.npcName == "Mom" || npc.npcName == "Dad" || npc.npcName == "Tim" || npc.name == "Mother" || npc.name == "Father" || npc.name == "Tim");

        void QuietStreet()
        {
            foreach (var wander in FindObjectsByType<NPCWander>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (IsFamily(wander.GetComponentInParent<NPC>()) || IsFamily(wander.GetComponentInChildren<NPC>())) { wander.enabled = false; continue; }
                wander.gameObject.SetActive(false);
            }
            foreach (var npc in FindObjectsByType<NPC>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (!npc.transform.IsChildOf(transform) && !IsFamily(npc)) npc.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- the welcome home

        Transform mom, dad, tim;
        Vector3 houseIn = Vector3.back;

        // Before you reach the house: the family waits just inside the front door, facing it.
        void StageFamily()
        {
            mom = Mindscape.AcademicPressure.HomeWelcome.FindPerson("Mom", "Mother");
            dad = Mindscape.AcademicPressure.HomeWelcome.FindPerson("Dad", "Father");
            tim = Mindscape.AcademicPressure.HomeWelcome.FindPerson("Tim");
            Vector3 inside = points[3] != null ? points[3].position : points[2].position;
            Mindscape.AcademicPressure.HomeWelcome.FrontDoor(inside, out var door, out houseIn);
            float floorY = Ground(points[2].position, points[2].position.y - 1.4f).y;
            Vector3 side = Vector3.Cross(Vector3.up, houseIn);
            Vector3 entry = new Vector3(door.x, floorY, door.z);
            void Put(Transform who, Vector3 offset)
            {
                if (who == null) return;
                var at = Mindscape.AcademicPressure.HomeWelcome.ClearFloor(entry + offset, floorY);
                Mindscape.AcademicPressure.HomeWelcome.Place(who, at, entry);
            }
            Put(mom, houseIn * 4.6f + side * .4f);
            Put(dad, houseIn * 5.2f - side * 1.5f);
            Put(tim, houseIn * 4.2f + side * 2.0f);
        }

        IEnumerator Welcome(System.Action done)
        {
            SetLocked(true);
            float yawIn = Quaternion.LookRotation(houseIn).eulerAngles.y;
            Teleport(player.position, yawIn);
            var family = new[] { mom, dad, tim }.Where(f => f != null).ToArray();
            Vector3 side = Vector3.Cross(Vector3.up, houseIn);
            float h = PlayerHeight();
            Vector3 centre = family.Length > 0 ? family.Aggregate(Vector3.zero, (a, f) => a + f.position) / family.Length : player.position + houseIn * 3;
            Vector3 middle = (player.position + centre) * .5f + Vector3.up * h * .55f;
            // From inside the house, off to the side: the family and you, not the back of the front door.
            Shot(Mindscape.AcademicPressure.HomeWelcome.ClearView(middle, middle + side * 4.6f + Vector3.up * 1.1f - houseIn * .6f), middle);
            ShotFocus.Clear();
            foreach (var f in family) StartCoroutine(Mindscape.AcademicPressure.HomeWelcome.TurnTo(f, player.position));
            if (mom != null) StartCoroutine(Mindscape.AcademicPressure.HomeWelcome.StepToward(mom, player.position + houseIn * 1.9f + side * .3f, 1.1f));
            if (tim != null) StartCoroutine(Mindscape.AcademicPressure.HomeWelcome.StepToward(tim, player.position + houseIn * 1.7f + side * 1.6f, 1.0f));
            yield return new WaitForSeconds(1.2f);
            foreach (var f in family) StartCoroutine(Mindscape.AcademicPressure.HomeWelcome.TurnTo(f, player.position, .3f));
            yield return DialogueSequence(
                FamilyLine(mom, "Mom", "Hey! You're home! How was the reunion?"),
                FamilyLine(null, null, "It was nice. I got to see everyone again."),
                FamilyLine(dad, "Dad", "That's good! Did you have fun?"),
                FamilyLine(null, null, "Yeah, I did. It was nice catching up with them."),
                FamilyLine(mom, "Mom", "That's great. You should get some rest now. You must be tired."),
                BeforeDialogue(null, Think("It's nice to be home.")),
                FamilyLine(tim, "Tim", "Good night! Don't stay up too late!"),
                FamilyLine(null, null, "Good night!"));
            foreach (var f in family) Mindscape.AcademicPressure.HomeWelcome.Play(f, "Idle");
            // Back to walking, with the camera behind you inside the house, on to your room.
            Teleport(player.position, yawIn);
            SetObjective("Back in your room", Check(false, "Place your phone on the desk"));
            done();

            IEnumerator FamilyLine(Transform who, string name, string words)
            {
                foreach (var f in family) Mindscape.AcademicPressure.HomeWelcome.Play(f, f == who ? "Talk" : "Idle");
                yield return who == null ? Me(words) : Say(name, words);   // the animation changes as the line comes up
            }
        }

        // The old friends get their names; other boys take the names from the capstone list.
        void NameTheCast()
        {
            for (int i = 1; i <= SocialDynamicsChapter.FriendNames.Length; i++)
            {
                var friend = transform.Find("Old friend " + i);
                var npc = friend != null ? (friend.GetComponent<NPC>() ?? friend.GetComponentInChildren<NPC>()) : null;
                if (npc != null) npc.npcName = SocialDynamicsChapter.FriendNames[i - 1];
            }
            foreach (var npc in FindObjectsByType<NPC>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (SocialDynamicsChapter.BoyNames.TryGetValue(npc.npcName, out var boy)) npc.npcName = boy;
        }

        IEnumerator FallAsleep()
        {
            yield return Fade(0, 1, .8f);
            SetLocked(true);
            // The bed in Joey's room: its long side runs head to foot, the head is the end nearest the bedside table.
            var renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var bed = renderers.FirstOrDefault(r => r.name == "West bedroom | bed fixed" && r.gameObject.activeInHierarchy)
                ?? renderers.FirstOrDefault(r => r.name == "West bedroom | bed" && r.gameObject.activeInHierarchy);
            var table = renderers.FirstOrDefault(r => r.name == "West bedroom | bedside table");
            float h = PlayerHeight();
            Vector3 head, centre;
            if (bed != null)
            {
                var b = bed.bounds; centre = b.center;
                Vector3 along = b.size.x >= b.size.z ? Vector3.right : Vector3.forward;
                if (table != null && Vector3.Dot(table.bounds.center - centre, along) < 0) along = -along;
                head = along;
                float top = b.max.y;
                if (Physics.Raycast(new Vector3(centre.x, b.max.y + 2, centre.z), Vector3.down, out var hit, 6, ~0, QueryTriggerInteraction.Ignore)) top = hit.point.y;
                centre.y = top;
            }
            else { centre = player.position; head = Flat(player.forward).normalized; }
            if (character != null) character.enabled = false;
            // Lying on his back, head on the pillow: the body's up runs toward the head of the bed, its front faces the ceiling.
            player.SetPositionAndRotation(centre - head * h * .5f + Vector3.up * .05f, Quaternion.LookRotation(Vector3.up, head));
            if (animator != null) animator.SetFloat("Speed", 0);
            Vector3 side = Vector3.Cross(Vector3.up, head);
            Vector3 face = centre + head * h * .38f + Vector3.up * .3f;
            Shot(centre + Vector3.up * h * 1.2f - head * h * .6f + side * h * .5f, face);
            ShotFocus.Clear();
            if (warmth != null) warmth.intensity *= .6f;
            yield return Fade(1, 0, 1.4f);
            // The camera settles slowly toward him while he thinks back over the day.
            StartCoroutine(Glide(face + Vector3.up * h * .55f - head * h * .25f + side * h * .25f, face, 16f));
            yield return Lines(
                "Justin waited for me at the door. Everyone made room for me in the photograph.",
                "I laughed tonight. Really laughed.",
                "I was so worried about what they'd think of me. They just wanted me there.",
                "Maybe tomorrow I can remember this, too.",
                "My eyes are getting heavy...");
            ShotFocus.Clear();
            for (float t = 0; t < 2; t += Time.deltaTime) { if (warmth != null) warmth.intensity = Mathf.Lerp(warmth.intensity, .15f, Time.deltaTime); yield return null; }
            yield return Fade(0, 1, 3f);
            SetFadeColor(new Color(.05f, .07f, .14f));
            yield return Fade(1, .6f, 1.5f);
            yield return Say("Narration", PlayerName + " falls asleep, still smiling. Somewhere in a dream, a soft light is waiting.");
            yield return Fade(.6f, 1, 1f);
            SetFadeColor(null);
            yield return Fade(1, 0, .1f);
        }

        // ================================================================ Episode 8 · the dream

        DreamSpace dream;
        Color savedAmbient, savedFogColor, savedBackground; bool savedFog; float savedFogDensity, savedFar; FogMode savedFogMode;
        readonly List<(Light light, float intensity)> savedSuns = new List<(Light, float)>();
        float savedWalk = -1, savedRun = -1;

        // Called before the title card, so the card already sits over the dark.
        void BeginDream()
        {
            dream = SocialStaging.Dream(new Vector3(1400, 0, 0), 96);
            savedAmbient = RenderSettings.ambientLight; savedFog = RenderSettings.fog; savedFogColor = RenderSettings.fogColor;
            savedFogDensity = RenderSettings.fogDensity; savedFogMode = RenderSettings.fogMode;
            savedBackground = storyCamera.backgroundColor; savedFar = storyCamera.farClipPlane;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { savedSuns.Add((l, l.intensity)); l.intensity *= .08f; }
            var night = new Color(.025f, .032f, .07f);
            RenderSettings.ambientLight = new Color(.1f, .12f, .2f);
            RenderSettings.fog = true; RenderSettings.fogColor = night; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = .028f;
            storyCamera.backgroundColor = night; storyCamera.farClipPlane = 140;
            if (movement != null) { savedWalk = movement.moveSpeed; savedRun = movement.runSpeed; movement.moveSpeed = movement.runSpeed = savedWalk * .45f; }
            MusicManager.FadeTo(0, .05f);   // silence until the first steps
            Teleport(dream.start.position, 0);
            if (movement != null) movement.ResetView(0, 12f);
            Shot(dream.start.position + new Vector3(0, 4.2f, -5.5f), dream.start.position + new Vector3(0, 2.2f, 20));
        }

        void EndDream()
        {
            RenderSettings.ambientLight = savedAmbient; RenderSettings.fog = savedFog; RenderSettings.fogColor = savedFogColor;
            RenderSettings.fogDensity = savedFogDensity; RenderSettings.fogMode = savedFogMode;
            storyCamera.backgroundColor = savedBackground; storyCamera.farClipPlane = savedFar;
            foreach (var (light, intensity) in savedSuns) if (light != null) light.intensity = intensity;
            if (movement != null && savedWalk > 0) { movement.moveSpeed = savedWalk; movement.runSpeed = savedRun; }
            if (dream != null) dream.gameObject.SetActive(false);
        }

        IEnumerator Mirror()
        {
            yield return Dream();
            yield return MirrorScene();
        }

        IEnumerator Dream()
        {
            yield return Think("I open my eyes. Everything is dim... but not completely dark.");
            yield return Think("There's a light, far ahead.");
            bool reached = false;
            StartCoroutine(Watch());
            SetObjective("A light in the distance", Check(false, "Walk toward the light"));
            yield return RoamUntil(() => reached);
            yield return Think("The light is so warm.");
            // The light fills the view, and he wakes in his own room.
            SetFadeColor(new Color(1, .98f, .93f));
            yield return Fade(0, 1, 2.4f);
            EndDream();
            Teleport(spawn.position, spawn.eulerAngles.y);
            // The follow camera starts off to one side, so the player does not stand between you and the mirror.
            if (movement != null) movement.ResetView(spawn.eulerAngles.y + mirrorCameraYaw, 12f);
            Shot(openingView.position, openingView.position + openingView.forward * 8);
            yield return Fade(1, 0, 2f);
            SetFadeColor(null);
            yield return Think("My room. It was a dream... but I still remember every one of those moments.");

            IEnumerator Watch()
            {
                Vector3 from = player.position;
                bool music = false;
                // A loaded save saved after this walk goes straight through it.
                if (ReplayAhead) reached = true;
                while (!reached)
                {
                    if (!music && Vector3.Distance(Flat(player.position), Flat(from)) > .6f) { music = true; MusicManager.FadeTo(1, 6f); }
                    float z = player.position.z - dream.start.position.z;
                    foreach (var f in dream.frames)
                        if (!f.IsShown && f.transform.position.z - dream.start.position.z < z + 9) { f.Show(2.4f); effects.PlayOneShot(chime, .06f); }
                    if (z > dream.length - 4.5f) reached = true;
                    yield return null;
                }
                if (!music) MusicManager.FadeTo(1, 3f);
            }
        }

        // ================================================================ Episode 8 · the mirror

        IEnumerator MirrorScene()
        {
            var mirrorSpot = points[0];
            var mirror = FindAnyObjectByType<SocialMirror>();
            Vector3 toRoom = Flat(spawn.position - mirrorSpot.position).normalized;
            Vector3 floor = new Vector3(mirrorSpot.position.x, spawn.position.y, mirrorSpot.position.z);
            bool near = false, facing = false;
            // On the way up to it, he starts to see himself.
            Quiet(AddArrival(Waypoint("Approaching the mirror", floor + toRoom * 2.8f), "Approach the mirror", () => !near, Near, 1.3f));
            AddArrival(Waypoint("In front of the mirror", floor + toRoom * 1.3f), "Look into the mirror", () => near && !facing, () => { facing = true; return Nothing(); }, 1.1f);
            SetObjective("The person standing here", Check(false, "Walk up to the mirror"));
            yield return RoamUntil(() => facing);

            // Face the mirror squarely. Younger Joey stands beside you, seen only in the reflection.
            SetLocked(true);
            Teleport(floor + toRoom * 1.3f, Quaternion.LookRotation(-toRoom).eulerAngles.y);
            var younger = Younger(toRoom);
            if (mirror != null) { mirror.ShowYounger(); mirror.Blend = 0; }
            storyCamera.cullingMask &= ~(1 << SocialMirror.YoungerLayer);
            ShotOver(mirrorSpot, 4.4f, .7f);

            // Memories float up around the mirror while he talks; the camera drifts slowly over them.
            Vector3 along = Vector3.Cross(Vector3.up, toRoom);
            var frames = new List<MemoryFrame>();
            Vector3[] spots = MemorySpots(floor, toRoom, along);
            int[] memoryIndex = { 0, 2, 4, 6 };
            for (int i = 0; i < spots.Length; i++)
            {
                var m = SocialStaging.Memories[memoryIndex[i]];
                frames.Add(SocialStaging.Frame(transform, spots[i], toRoom, SocialStaging.Picture(m.image), m.words, 1.4f));
            }
            bool drifting = true;
            Vector3 eyeLevel = floor + toRoom * 4.2f + Vector3.up * 3f;
            float sideRoom = RoomReach(eyeLevel, along), otherRoom = RoomReach(eyeLevel, -along);
            StartCoroutine(Drift());

            string[] lines = {
                "I've spent so much time looking at other people. Their achievements, their plans, and their happiness. I kept wondering why I wasn't like them.",
                "I thought I needed to become someone more successful and more confident, someone who never feels left behind.",
                "But I never stopped to appreciate the person standing here right now.",
                "I've made mistakes. I've worried about my future and thought about the things I could have done differently.",
                "Sometimes, I was so afraid of disappointing others that I forgot what I wanted for myself.",
                "When I saw other people moving forward, I treated my own life like it wasn't worth anything yet.",
                "But the person who got scared, made mistakes, felt lost, and still kept going...",
                "That person was me.",
                "I still don't have all the answers. I'm still afraid of what comes next, and I know I have things to improve.",
                "But I don't have to become a completely different person to keep growing. I can make mistakes without treating myself like a failure.",
                "I may not be where I thought I'd be by now, but my life is still mine. I've been searching for someone I could be proud of...",
                "And I've been here the whole time." };
            var sequence = new IEnumerator[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                int n = i;
                // Every few lines another memory lights up, and younger Joey shows a little more in the glass.
                sequence[i] = BeforeDialogue(Step(n), Think(lines[n]));
            }
            yield return DialogueSequence(sequence);
            drifting = false;

            // "It's you!": the younger self is fully there, smiling back.
            yield return Glide(floor + toRoom * 3.4f + Vector3.up * 2.6f, mirrorSpot.position + Vector3.up * .1f, 1.6f);
            if (warmth != null) warmth.intensity *= 1.5f;
            if (mirror != null) mirror.Glow = 1.15f;
            yield return BlendYounger(mirror, 1, 1.6f);
            yield return Card("IT'S YOU!");
            yield return Card("Despite everything, it's still you!");
            yield return Lines("I'm still the person who was afraid to enter that room. I'm still the person who compared my life with everyone else's. I don't know exactly what will happen next.", "But I'm also the person who showed up, who tried, and who chose to keep going.", "I don't have to prove that my life is better than someone else's. I can keep learning without rejecting who I am today.", "Maybe I don't have everything figured out yet. And maybe, for now, that's enough.");

            // The two of them in the glass; the camera pulls slowly back into the dark.
            Shot(floor + toRoom * 2.6f + Vector3.up * 2.7f, mirrorSpot.position + Vector3.up * .15f);
            yield return null;
            var drawing = SocialStaging.Sketch(storyCamera);
            // Back toward the far wall of the room, but not through it.
            float depth = RoomReach(floor + toRoom * .5f + Vector3.up * 3.6f, toRoom) + .5f;
            StartCoroutine(Glide(floor + toRoom * Mathf.Min(7.5f, depth - .7f) + Vector3.up * 3.6f, mirrorSpot.position + Vector3.up * .15f, 7f));
            yield return new WaitForSeconds(2.5f);
            yield return Fade(0, 1, 4f);
            // A drawing of the two of them, smiling.
            ShowItem(drawing, "Despite everything, it's still you!");
            yield return FadePhoto(0, 1, 1.8f);
            yield return new WaitForSeconds(4.5f);
            yield return FadePhoto(1, 0, 1.8f);
            HideItem();
            // The End, on black.
            int mask = storyCamera.cullingMask;
            storyCamera.cullingMask = 0;
            var background = storyCamera.backgroundColor; storyCamera.backgroundColor = Color.black;
            yield return Fade(1, 0, .6f);
            yield return Card("THE END");
            storyCamera.cullingMask = mask; storyCamera.backgroundColor = background;
            if (younger != null) Destroy(younger);
            foreach (var f in frames) if (f != null) f.Hide(.1f);
            PlayerPrefs.SetInt(MindscapeDatabase.ProfileKey("Mindscape.SocialDynamics.Complete"), 1);

            IEnumerator Near()
            {
                near = true;
                yield return Think("That's me.");
            }
            IEnumerator Step(int n)
            {
                if (n % 3 == 0 && n / 3 < frames.Count) frames[n / 3].Show(2f);
                if (mirror != null) StartCoroutine(BlendYounger(mirror, Mathf.Min(.45f, (n + 1) * .04f), 2.5f));
                yield break;
            }
            IEnumerator Drift()
            {
                int k = 0;
                while (drifting)
                {
                    // Alternate between the mirror and whichever memory is lit, each move slow enough to read over.
                    var lit = frames.Where(f => f.IsShown).ToList();
                    if (k % 2 == 1 && lit.Count > 0)
                    {
                        var f = lit[(k / 2) % lit.Count].transform;
                        yield return Glide(f.position + toRoom * 2.6f - Vector3.up * .3f + along * .6f, f.position, 6.5f);
                    }
                    else
                    {
                        Vector3 side = along * (k % 4 == 0 ? Mathf.Min(1.4f, sideRoom - .7f) : -Mathf.Min(1.1f, otherRoom - .7f));
                        yield return Glide(floor + toRoom * 4.2f + side + Vector3.up * 3f, mirrorSpot.position + Vector3.up * .1f, 6.5f);
                    }
                    k++;
                }
            }
        }

        // Four places for the memory frames on the mirror's wall, either side of the mirror and inside the room: the side
        // walls are measured, so no frame (or its words) ends up half inside a wall. Higher and lower in turn.
        static Vector3[] MemorySpots(Vector3 floor, Vector3 toRoom, Vector3 along)
        {
            Vector3 wallFace = floor + toRoom * .5f + Vector3.up * 3.6f;
            float right = RoomReach(wallFace, along), left = RoomReach(wallFace, -along);
            var offsets = new List<float>();
            // Clear of the mirror (1.7 m from its middle) and of the side walls (1 m), 1.6 m apart.
            for (float o = 1.9f; o <= right - 1f && offsets.Count < 4; o += 1.6f) offsets.Add(o);
            for (float o = 1.9f; o <= left - 1f && offsets.Count < 8; o += 1.6f) offsets.Add(-o);
            if (offsets.Count == 0) offsets.Add(0);
            // Alternate sides, nearest first.
            var ordered = offsets.OrderBy(o => Mathf.Abs(o)).ThenBy(o => o < 0 ? 1 : 0).ToList();
            var spots = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                float o = ordered[i % ordered.Count];
                // A room too narrow for four in a row stacks the extras above the first ones.
                float lift = (i % 2 == 0 ? 3.3f : 4.4f) + (i >= ordered.Count ? 1.2f : 0);
                spots[i] = floor + toRoom * .5f + along * o + Vector3.up * lift;
            }
            return spots;
        }

        // How far the room's walls are from a point in one direction (people ignored), up to 20 m.
        static float RoomReach(Vector3 from, Vector3 direction)
        {
            bool backfaces = Physics.queriesHitBackfaces; Physics.queriesHitBackfaces = true;
            float best = 20;
            foreach (var hit in Physics.RaycastAll(from, direction, 20, ~(1 << 2), QueryTriggerInteraction.Ignore))
                if (!(hit.collider is CharacterController) && hit.collider.GetComponentInParent<Animator>() == null && hit.distance < best) best = hit.distance;
            Physics.queriesHitBackfaces = backfaces;
            return best;
        }

        static IEnumerator Nothing() { yield break; }

        IEnumerator BlendYounger(SocialMirror mirror, float to, float seconds)
        {
            if (mirror == null) yield break;
            float from = mirror.Blend;
            for (float t = 0; t < seconds; t += Time.deltaTime) { mirror.Blend = Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, t / seconds)); yield return null; }
            mirror.Blend = to;
        }

        // A slightly younger Joey (a little smaller, the same face) beside you, on the layer only the mirror sees.
        GameObject Younger(Vector3 toRoom)
        {
            var body = player.GetComponentsInChildren<Animator>(true).FirstOrDefault(a => a.transform != player)?.gameObject;
            if (body == null) return null;
            var copy = Instantiate(body);
            copy.name = "Younger Joey (mirror only)";
            copy.transform.localScale = body.transform.lossyScale * .84f;
            Vector3 beside = Vector3.Cross(Vector3.up, toRoom).normalized;
            copy.transform.SetPositionAndRotation(player.position + beside * 1.15f, Quaternion.LookRotation(-toRoom));
            foreach (var c in copy.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            SocialStaging.SetLayer(copy, SocialMirror.YoungerLayer);
            var a = copy.GetComponent<Animator>(); if (a != null) a.SetFloat("Speed", 0);
            return copy;
        }
    }
}
