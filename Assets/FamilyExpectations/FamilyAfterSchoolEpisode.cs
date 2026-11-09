using System.Collections;
using Mindscape.AcademicPressure;
using UnityEngine;
using UnityEngine.InputSystem;
using UIE = UnityEngine.UIElements;
using static UnityEngine.UIElements.UQueryExtensions;

namespace Mindscape.FamilyExpectations
{
    // Episode 1 · The Paper on the Desk. Home after the examination: three objects from today (the examination
    // paper, the school bag, the study notebook) each bring back a memory; the thought journal [J] keeps them.
    // Once all three are seen, the paper takes you back to the morning.
    public sealed class FamilyAfterSchoolEpisode : FamilyEpisode
    {
        public Transform paper, schoolBag, notebook, bedroomSpawn, bedroomView, examMemoryView, outsideStart, outsideEnd;
        public Light deskLight, sun;
        public GameObject morningItems, messyBed, fixedBed;

        public override int Episode => 1;
        protected override float TimeLimit => 480;
        public int SeenMask { get; private set; }

        static readonly string[] Titles = { "Examination paper", "School bag", "Study notebook" };
        static readonly string[] Thoughts =
        {
            "I remember watching the clock while I tried to answer. What if I missed an important question? I don't know my result yet.",
            "Everything was scattered this morning. I rushed to collect my things, already afraid of being late.",
            "I stayed up reviewing one more lesson. I wanted to feel prepared, but by morning I was exhausted."
        };
        static readonly string[] Afterthoughts =
        {
            "The clock kept moving. I tried to remember what I had studied, but all I could think about was the time.",
            "My ID, my examination materials, my bag. I was searching for everything at once.",
            "One more page. One more lesson. I thought studying longer would make the worry disappear."
        };

        bool ended, journalOpen;
        // The journal and its button are one UI Toolkit file (ThoughtJournal.uxml).
        UIE.VisualElement journal, journalButton;
        UIE.Label journalText;

        protected override bool OverlayOpen => journalOpen;
        bool inRoom;
        protected override string RoamHint => !inRoom ? "Open the front door and go to your bedroom." : SeenMask == 7 ? "Return to the examination paper on your desk."
            : "Examine the things from today: the paper and the notebook on the desk, the bag on the floor.\nPress J for your thought journal.";

        Transform Target(int i) => i == 0 ? paper : i == 1 ? schoolBag : notebook;
        bool Seen(int i) => (SeenMask & (1 << i)) != 0;

        protected override IEnumerator Play()
        {
            BuildJournal();
            if (messyBed != null) messyBed.SetActive(false);
            if (fixedBed != null) fixedBed.SetActive(true);
            if (morningItems != null) morningItems.SetActive(false);
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                AddSpot(Target(i), "Examine the " + Titles[i].ToLowerInvariant(), () => inRoom && !Seen(index), () => Memory(index));
            }
            AddSpot(paper, "Pick up the paper  ·  remember the morning", () => SeenMask == 7 && !ended, Ending);

            // Home from school: the bus ride, the walk up to the house, and Mom at the door asking about the examination.
            yield return ComeHome();

            // In through the bedroom door (it opens with its own [ E ]), on foot.
            AddArrival(bedroomSpawn, "Go to your room", () => !inRoom, EnterRoom);
            SetObjective("Go to your room", Check(true, "Home") + Check(false, "To your bedroom"));
            yield return RoamUntil(() => inRoom);

            yield return RoamUntil(() =>
            {
                SetObjective("Examine the things from today", Check(Seen(0), Titles[0] + "  ·  on the desk") + Check(Seen(1), Titles[1] + "  ·  on the floor") + Check(Seen(2), Titles[2] + "  ·  on the desk"));
                return SeenMask == 7;
            });
            SetObjective("Return to the examination paper", Check(true, "3 memories") + Check(false, "The paper on your desk"));
            yield return RoamUntil(() => ended);

            Complete("The paper on the desk",
                "The clock becomes an alarm.\nThe memory takes you back to the morning of the examination.\n\n3 memories examined. Your thought journal keeps them.");
        }

        // ---------------------------------------------------------------- coming home

        bool walkedUp, greeted;
        Transform mom, tim;

        IEnumerator ComeHome()
        {
            // Mom (and Tim) wait just inside the front door, out of sight from the street.
            mom = HomeWelcome.FindPerson("Mom", "Mother"); tim = HomeWelcome.FindPerson("Tim");
            HomeWelcome.FrontDoor(bedroomSpawn.position, out var door, out var inward);
            float floorY = bedroomSpawn.position.y - .1f;
            if (Physics.Raycast(new Vector3(door.x, door.y + 3, door.z) + inward * 2.2f, Vector3.down, out var floorHit, 8, ~0, QueryTriggerInteraction.Ignore)) floorY = floorHit.point.y;
            Vector3 entry = new Vector3(door.x, floorY, door.z), side = Vector3.Cross(Vector3.up, inward);
            if (mom != null) HomeWelcome.Place(mom, HomeWelcome.ClearFloor(entry + inward * 4.6f + side * .4f, floorY), entry);
            if (tim != null) HomeWelcome.Place(tim, HomeWelcome.ClearFloor(entry + inward * 4.4f + side * 2.2f, floorY), entry);
            var inside = Waypoint("Just inside the front door", entry + inward * 2.2f + Vector3.up * .1f);

            var ride = BusRide.Find();
            if (ride != null)
            {
                // On the bus: it comes down the street with you on board and pulls in at the shelter.
                ride.Place(-70);
                Teleport(new Vector3(ride.kerb.x, ride.kerb.y + 30, ride.kerb.z), 0);   // out of the way while you ride
                player.gameObject.SetActive(false);
                Vector3 side2 = Vector3.Cross(Vector3.up, ride.heading);
                bool arrived = false;
                StartCoroutine(Drive(-70, 0, 9f, () => arrived = true, side2));
                yield return Card(TitleCard, "Begin  [Space]");
                yield return DialogueSequence(
                    Think("The bus ride home feels longer than usual."),
                    Think("The examination is over, but I keep going over every question in my head."));
                while (!arrived) yield return null;
                // Off at the kerb; the bus pulls away.
                Vector3 kerb = ride.kerb;
                if (Physics.Raycast(kerb + Vector3.up * 5, Vector3.down, out var hit, 12, ~0, QueryTriggerInteraction.Ignore)) kerb = hit.point;
                player.gameObject.SetActive(true);
                Vector3 toHouse = Flat(outsideStart.position - kerb).normalized;
                Teleport(kerb + Vector3.up * .1f, Quaternion.LookRotation(toHouse).eulerAngles.y);
                effects.PlayOneShot(softTick, .2f);
                // From the pavement beside you, never out in the road: the bus is still pulling away there.
                Shot(kerb + side2 * 5.5f - toHouse * .5f + Vector3.up * 3.2f, kerb + toHouse * 5 + Vector3.up * 1.6f);
                StartCoroutine(Drive(0, 70, 5f, () => ride.bus.gameObject.SetActive(false), side2, follow: false));
            }
            else
            {
                Teleport(outsideStart.position, outsideStart.eulerAngles.y);
                Shot(outsideStart.position + new Vector3(2.6f, 4.2f, 4.5f), outsideEnd.position + Vector3.up * 2.2f);
                yield return Card(TitleCard, "Begin  [Space]");
            }
            yield return Say("After school", "The examination is over, but the worry has followed you home.");
            AddStress(10);

            // The walk home, then in through the front door (it opens with its own [ E ]).
            AddArrival(outsideStart, "Walk home", () => !walkedUp, () => { walkedUp = true; return Nothing(); }, 3f);
            AddArrival(inside, "Go inside", () => !greeted, () => Greeting(inward), 2f);
            SetObjective("Walk home", Check(false, "Up the path to your front door"));
            yield return RoamUntil(() => walkedUp || greeted);
            if (!greeted) SetObjective("Go inside", Check(true, "Front door") + Check(false, "Open the door and go in"));
            yield return RoamUntil(() => greeted);

            IEnumerator Drive(float from, float to, float seconds, System.Action done, Vector3 side3, bool follow = true)
            {
                for (float t = 0; t < seconds; t += Time.deltaTime)
                {
                    float k = follow ? Mathf.Sin(t / seconds * Mathf.PI * .5f) : (t / seconds) * (t / seconds);   // slows into the stop / speeds away
                    ride.Place(Mathf.Lerp(from, to, k));
                    if (follow) Shot(ride.bus.position + side3 * 11 + Vector3.up * 5 - ride.heading * 6, ride.bus.position + Vector3.up * 1.5f);
                    yield return null;
                }
                ride.Place(to);
                done?.Invoke();
            }
        }

        IEnumerator Greeting(Vector3 inward)
        {
            greeted = true;
            SetLocked(true);
            float yawIn = Quaternion.LookRotation(inward).eulerAngles.y;
            Teleport(player.position, yawIn);
            Vector3 side = Vector3.Cross(Vector3.up, inward);
            Vector3 target = mom != null ? mom.position : player.position + inward * 3;
            Vector3 middle = (player.position + target) * .5f + Vector3.up * 2;
            // From inside the house, beside the two of you, not from behind you at the door.
            Shot(HomeWelcome.ClearView(middle, middle + side * 4.6f + Vector3.up * 1.1f - inward * .6f), middle);
            ShotFocus.Clear();
            if (mom != null) { StartCoroutine(HomeWelcome.TurnTo(mom, player.position)); StartCoroutine(HomeWelcome.StepToward(mom, player.position + inward * 1.9f + side * .3f, 1.1f)); }
            if (tim != null) StartCoroutine(HomeWelcome.TurnTo(tim, player.position));
            yield return new WaitForSeconds(1.2f);
            if (mom != null) StartCoroutine(HomeWelcome.TurnTo(mom, player.position, .3f));
            yield return DialogueSequence(
                Line(tim, "Tim", "You're back!"),
                Line(mom, "Mom", PlayerName + ", you're home! How did the examination go?"),
                Line(null, null, "I'm not sure yet, Mom. I arrived late, so I didn't get to answer everything."),
                Line(mom, "Mom", "Late? What happened this morning?"),
                Line(null, null, "Can I tell you later? I just want to rest for a bit."),
                Line(mom, "Mom", "All right. Go and change, then rest. We can talk at dinner."),
                BeforeDialogue(null, Think("She only asked how it went. So why does it feel so heavy?")));
            foreach (var f in new[] { mom, tim }) HomeWelcome.Play(f, "Idle");
            Teleport(player.position, yawIn);   // the follow camera comes back behind you, inside

            IEnumerator Line(Transform who, string name, string words)
            {
                foreach (var f in new[] { mom, tim }) if (f != null) HomeWelcome.Play(f, f == who ? "Talk" : "Idle");
                if (name != null && who == null && name == "Tim") yield break;   // no Tim in this scene
                yield return who == null && name == null ? Me(words) : Say(name, words);
            }
        }

        static IEnumerator Nothing() { yield break; }
        static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude < 1e-4f ? Vector3.forward : v; }

        IEnumerator EnterRoom()
        {
            Shot(bedroomView.position, paper.position);
            yield return DialogueSequence(
                Think("It's finally over. I should be relieved, but I still feel worried."),
                Think("I studied for this exam. I stayed up late because I didn't want to fail.\nBut I arrived after everyone had started."),
                Think("What if I made mistakes because I was rushing?\nI don't know my result yet, but I'm already imagining the worst."));
            inRoom = true;
        }

                IEnumerator Walk(Vector3 from, Vector3 to, float seconds)
        {
            if (character != null) character.enabled = false;
            if (animator != null) animator.SetFloat("Speed", 1);
            for (float t = 0; t < seconds; t += Time.deltaTime) { player.position = Vector3.Lerp(from, to, t / seconds); yield return null; }
            if (animator != null) animator.SetFloat("Speed", 0);
            Teleport(to, player.eulerAngles.y);
        }

        IEnumerator Memory(int i)
        {
            var target = Target(i);
            ShotOver(target, 2f, 1.4f);
            yield return DialogueSequence(
                Say("Memory  /  " + Titles[i], Thoughts[i], () => AddStress(8)),
                RecallMemory(),
                Think("I'll put it in my journal. Seeing it written down makes it a little smaller."));
            // Writing it down helps a little.
            EaseStress(4);
            SeenMask |= 1 << i;
            effects.PlayOneShot(chime, .1f);

            // Each memory is a short flashback inside the present-day episode.
            IEnumerator RecallMemory()
            {
                if (i == 0 && examMemoryView != null)
                {
                    yield return FlashbackIn("Earlier today  ·  the examination", () => Shot(examMemoryView.position, examMemoryView.position + examMemoryView.forward * 6), .35f);
                    yield return Say("Earlier  /  the examination", Afterthoughts[0]);
                    yield return FlashbackOut(() => ShotOver(target, 2f, 1.4f), .35f);
                }
                else
                {
                    float sunWas = sun != null ? sun.intensity : 0, lampWas = deskLight != null ? deskLight.intensity : 0;
                    yield return FlashbackIn(i == 2 ? "The night before" : "Examination morning", () =>
                    {
                        if (i == 2) { if (deskLight != null) deskLight.intensity = 4; if (sun != null) sun.intensity = .08f; }
                        if (i == 1 && morningItems != null) morningItems.SetActive(true);
                    }, .35f);
                    yield return Say(i == 2 ? "The night before  /  one more lesson" : "Examination morning  /  already late", Afterthoughts[i]);
                    yield return FlashbackOut(() =>
                    {
                        if (sun != null) sun.intensity = sunWas;
                        if (deskLight != null) deskLight.intensity = lampWas;
                        if (morningItems != null) morningItems.SetActive(false);
                    }, .35f);
                }
            }
        }

        IEnumerator Ending()
        {
            ShotOver(paper, 1.6f, 1.3f);
            yield return DialogueSequence(
                Think("Maybe if I had done things differently, the examination would have gone better."),
                Think("If I had slept earlier. Packed my bag. Left sooner..."));
            AddStress(10);
            yield return Fade(0, 1, 2);
            effects.PlayOneShot(Tone("Morning alarm", 760, 1.6f, true), .23f);
            ended = true;
        }

        // ---------------------------------------------------------------- thought journal

        protected override void Update()
        {
            base.Update();
            if (journalButton != null) journalButton.EnableInClassList("hidden", !(IsRoaming && !journalOpen && !IsPaused && !IsFailed));
            var k = Keyboard.current;
            if (k == null) return;
            if (journalOpen && (k.jKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame)) ToggleJournal();
            else if (!journalOpen && IsRoaming && !IsPaused && k.jKey.wasPressedThisFrame) ToggleJournal();
        }

        public void ToggleJournal()
        {
            if (!journalOpen && (!IsRoaming || IsPaused)) return;
            journalOpen = !journalOpen;
            journal.EnableInClassList("hidden", !journalOpen);
            SetLocked(journalOpen);
            if (!journalOpen) return;
            journalText.text = "";
            for (int i = 0; i < 3; i++)
                journalText.text += "<color=#DBB36F>" + Titles[i].ToUpperInvariant() + "</color>\n" + (Seen(i) ? Thoughts[i] : "Not examined yet.") + "\n\n";
        }

        void BuildJournal()
        {
            var root = Overlay(EpisodeUI.Load().thoughtJournal, "thought journal");
            journal = root.Q("journal-screen"); journalButton = root.Q("journal-button");
            journalText = root.Q<UIE.Label>("journal-text");
            root.Q<UIE.Button>("journal-close").clicked += ToggleJournal;
            root.Q<UIE.Button>("journal-button").clicked += ToggleJournal;
            root.Query<UIE.Button>().ForEach(b => b.focusable = false); // Space and Enter belong to the story
            journal.EnableInClassList("hidden", true);
            journalButton.EnableInClassList("hidden", true);
        }
    }
}
