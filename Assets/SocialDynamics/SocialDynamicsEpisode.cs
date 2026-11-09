using System;
using System.Collections;
using Mindscape.AcademicPressure;
using Mindscape.Data;
using UnityEngine;

namespace Mindscape.SocialDynamics
{
    public sealed partial class SocialDynamicsEpisode : AcademicEpisode
    {
        public int episodeNumber = 1;
        public Transform spawn, openingView;
        public Transform bedroomReturn;   // Episode 7: the old arrival point in your room (you now walk there through the doors)
        public Transform[] points;
        public Light warmth;
        public float mirrorCameraYaw = -55f;   // Episode 8: degrees the follow camera starts to the side of the player-to-mirror line
        public GameObject groupPhoto;
        public GameObject[] deskItems;    // Episode 7: the phone and the photograph, hidden until the phone is placed on the desk
        public override int Chapter => 3;
        public override int Episode => episodeNumber;
        protected override string CompletionKey => SocialDynamicsChapter.SaveKey(Episode);
        protected override string NextScene => Episode < 8 ? SocialDynamicsChapter.Scene(Episode + 1) : "";
        protected override string ContinueLabel => "Continue";
        protected override bool HasFinale => Episode == 8;
        protected override string FinaleHeading => "CHAPTER 3  /  SOCIAL DYNAMICS";
        protected override string FinaleBody => "Thank you for walking through these stories with us.";
        protected override string CompletionNext => Episode == 8 ? "CHAPTER 3 COMPLETE · SOCIAL DYNAMICS" : "NEXT: " + SocialDynamicsChapter.Titles[Episode].ToUpperInvariant();
        protected override string RoamHint => "Follow the gold markers. E interacts; Space continues; number keys choose.";
        public int SeenMask { get; private set; }
        public int Response { get; private set; } = -1;
        public int Stage { get; private set; }

        protected override IEnumerator Play()
        {
            NameTheCast();
            // Episode 8 opens in the dream (SocialDynamicsEnding.cs); the room and the mirror come after it.
            if (Episode == 8) BeginDream();
            else
            {
                Teleport(spawn.position, spawn.eulerAngles.y);
                Shot(openingView.position, openingView.position + openingView.forward * 8);
            }
            yield return Card("CHAPTER 3 · SOCIAL DYNAMICS\n<size=60%>Episode " + Episode.ToString("00") + " · " + SocialDynamicsChapter.Titles[Episode - 1] + "</size>");
            switch (Episode)
            {
                case 1: yield return Invitation(); break;
                case 2: yield return Arrival(); break;
                case 3: yield return Conversations(); break;
                case 4: yield return Corner(); break;
                case 5: yield return CheckIn(); break;
                case 6: yield return Rejoin(); break;
                case 7: yield return Home(); break;
                case 8: yield return Mirror(); break;
            }
            Complete(SocialDynamicsChapter.Titles[Episode - 1], Episode == 8
                ? "It's you!\nDespite everything, it's still you!\n\nYou do not have to become someone else to deserve kindness, friendship, and the chance to keep growing."
                : "You took another step through the evening. Your choices are saved.\n\nContinue when you are ready.");
        }

        IEnumerator Lines(params string[] lines)
        {
            var sequence = new IEnumerator[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                int split = line.IndexOf('|');
                if (split < 0) { sequence[i] = Think(line); continue; }
                string who = line.Substring(0, split), words = line.Substring(split + 1);
                // The camera cuts to whoever speaks (SocialSpeakers.cs).
                sequence[i] = BeforeDialogue(FaceSpeaker(who), who == "Me" ? Me(words) : Say(SocialDynamicsChapter.Speaker(who), words));
            }
            yield return DialogueSequence(sequence);
            QuietCast();
        }
        IEnumerator Visit(int index, string objective, string label, params string[] lines) => Visit(index, objective, label, false, null, null, lines);
        IEnumerator Visit(int index, string objective, string label, Action open, Action close, params string[] lines) => Visit(index, objective, label, false, open, close, lines);
        // open/close run around the lines, e.g. to hold the phone up while the thoughts play. onArrival: reached on foot (through
        // a door), with no [ E ].
        IEnumerator Visit(int index, string objective, string label, bool onArrival, Action open, Action close, params string[] lines)
        {
            bool done = false;
            if (onArrival) AddArrival(points[index], label, () => !done, Act); else AddSpot(points[index], label, () => !done, Act);
            SetObjective(objective, Check(false, label));
            yield return RoamUntil(() => done);
            // Episode 8 stands back from the mirror so the whole of it, and your reflection, is in frame.
            IEnumerator Act() { if (Episode == 8) ShotOver(points[index], 4.4f, .7f); else ShotOver(points[index]); open?.Invoke(); yield return Lines(lines); close?.Invoke(); done = true; Stage++; }
        }

        // Justin and the old friends stand in an arc round a spot, facing it; returns where each was, to put them back.
        System.Collections.Generic.List<(Transform who, Vector3 at, Quaternion turn)> GatherAround(Transform spot)
        {
            var before = new System.Collections.Generic.List<(Transform, Vector3, Quaternion)>();
            var friends = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in transform) if (child.gameObject.activeInHierarchy && (child.name == "Justin" || child.name.StartsWith("Old friend"))) friends.Add(child);
            if (friends.Count == 0) return before;
            float floorY = friends[0].position.y;
            Vector3 centre = new Vector3(spot.position.x, floorY, spot.position.z);
            // The arc opens toward the player (they walk up into the gap), so nobody stands between you and the group.
            Vector3 ahead = Vector3.ProjectOnPlane(centre - player.position, Vector3.up);
            if (ahead.sqrMagnitude < .01f) ahead = Vector3.ProjectOnPlane(spot.forward, Vector3.up);
            if (ahead.sqrMagnitude < .01f) ahead = Vector3.forward;
            ahead.Normalize();
            for (int i = 0; i < friends.Count; i++)
            {
                var f = friends[i];
                before.Add((f, f.position, f.rotation));
                // Spread over 140 degrees on the far side of the spot, with room between them for the conversation camera.
                float angle = friends.Count == 1 ? 0 : -70 + 140f * i / (friends.Count - 1);
                Vector3 at = centre + Quaternion.AngleAxis(angle, Vector3.up) * ahead * 3.4f;
                HomeWelcome.Place(f, HomeWelcome.ClearFloor(at, floorY), centre);
                HomeWelcome.Play(f, "Idle");
            }
            return before;
        }

        IEnumerator Invitation()
        {
            string[] labels = { "Read Justin's message", "Look at the old photograph", "Examine your desk" };
            // Sample of the new look: Justin's message glows blue instead of having a gold marker (the others keep theirs).
            if (points[0].GetComponentInChildren<InteractOutline>(true) == null) points[0].gameObject.AddComponent<InteractOutline>();
            for (int i = 0; i < 3; i++) { int n = i; AddSpot(points[n], labels[n], () => (SeenMask & (1 << n)) == 0, () => Inspect(n)); }
            SetObjective("An invitation from an old friend", "Explore the phone, photograph, and desk in any order.");
            yield return RoamUntil(() => SeenMask == 7);
            // Justin writes again, and the reply is sent from the phone.
            OpenPhone("Justin");
            effects.PlayOneShot(chime, .2f);
            yield return PhoneMessage(false, "Don't forget to let me know! I really hope you can come.", wait: false);
            yield return PhoneChoose(new[] { "Sure! I'll try to come.", "I'm not sure yet. I have a few things to think about." }, i => Response = i);
            Record("social.invitation", Response.ToString());
            if (Response == 0)
            {
                yield return PhoneMessage(true, "Sure! I'll try to come. It's been a while.", wait: false);
                yield return PhoneMessage(false, "Great! See you there!");
            }
            else
            {
                yield return PhoneMessage(true, "I'm not sure yet, but I'll think about it.", wait: false);
                yield return PhoneMessage(false, "No pressure! Just let me know. We'd be happy to see you.");
                yield return PhoneMessage(true, "I've thought about it. I'd like to come.");
            }
            ClosePhone();
            yield return Think("I want to go. I really do. I just hope I don't spend the whole time worrying about what everyone thinks of me.");
            // Getting ready and the bus to the reunion (SocialJourney.cs).
            yield return GetReadyAndGo();
            IEnumerator Inspect(int n)
            {
                ShotOver(points[n]);
                if (n == 0)
                {
                    // The invitation itself, as a text from Justin.
                    OpenPhone("Justin");
                    yield return PhoneMessage(false, "Hey! Our old group is planning a reunion this weekend. Are you coming? It would be nice to see everyone again!");
                    ClosePhone();   // the phone fills the screen, so the thought comes once it is put down
                    yield return Think("It's been a long time since I last saw everyone.");
                }
                else if (n == 1)
                {
                    ShowPhoto(Resources.Load<Texture2D>("SocialDynamics/OldGroupPhoto"), "the five of us, after class");
                    yield return Lines("I miss those days. We used to laugh about the smallest things. Nobody cared that much about what we had or what we were going to become.", "But things are different now. Everyone has their own life. I wonder what they'll think when they see me again.");
                    HidePhoto();
                }
                else yield return Lines("What if they ask about my achievements? What if they ask about my plans? What if I don't have anything interesting to say?");
                SeenMask |= 1 << n;
                SetObjective("An invitation from an old friend", Check((SeenMask & 1) != 0, labels[0]) + Check((SeenMask & 2) != 0, labels[1]) + Check((SeenMask & 4) != 0, labels[2]));
            }
        }
        IEnumerator Arrival()
        {
            yield return Think("There they are.");
            yield return Visit(0, "A familiar face", "Greet Justin", "Justin|Hey! You made it! It's been so long! How have you been?", "Me|I'm okay. How about you?", "Justin|I'm doing pretty well. A lot has happened since we last saw each other.", "Justin|Come on. Everyone's here. You should say hi!");
            yield return Visit(1, "Old friends", "Join the group", "Friend 1|Hey! I almost didn't recognize you!", "Friend 2|It's been ages! Do you remember when we used to stay after class just to talk?", "Me|Of course. I remember that.", "Friend 1|Those were good times.", "For a moment, I begin to relax. We talk about school activities and the smallest things that made us laugh.", "Friend 2|I'm currently working at a company I really wanted to join. It's been tiring, but I'm learning a lot.", "Friend 1|That's great! I've been busy with my studies, too. I'm hoping to get an opportunity abroad after graduation.", "Friend 3|I finally started working on that project I told you about before. I didn't think I'd actually do it, but here I am.", "Everyone has something new to share.", "They've been doing so much. I wonder if I have anything worth talking about.");
        }
        IEnumerator Conversations()
        {
            for (int i = 0; i < 2; i++) { int n = i; AddSpot(points[n + 2], n == 0 ? "Listen to the career conversation" : "Listen to the travel conversation", () => (SeenMask & (1 << n)) == 0, () => Listen(n)); }
            SetObjective("Everyone seems to be moving forward", "Listen to both groups in any order.");
            yield return RoamUntil(() => SeenMask == 3);
            yield return Lines("They have plans. They have stories. They have things they're proud of.", "What am I supposed to say when it's my turn?");
            yield return Think("The room feels loud. I can take a breath before I speak to anyone.");
            bool conversationBreathCompleted = false;
            yield return Breathe(done => conversationBreathCompleted = done);
            Record("social.conversation_breathing", conversationBreathCompleted ? "completed" : "closed_early");
            yield return Think("I don't need a perfect story to talk to a friend.");
            yield return Visit(0, "A check-in", "Talk with Justin", "Justin|Hey, are you enjoying yourself?", "Me|Yeah. It's nice seeing everyone again.", "Justin|That's good! I was hoping you'd have fun tonight. You've been pretty quiet, though. Is everything okay?");
            yield return Choose("How do you respond?", "You can share as much as you want.", new[] { "I'm just listening. Everyone has a lot of interesting stories.", "I feel like everyone has moved forward, and I'm not sure where I fit anymore." }, i => Response = i);
            Record("social.check_in", Response.ToString());
            if (Response == 0) yield return Lines("Justin|That's okay. You don't have to talk all the time. Just enjoy being here.", "Me|Yeah. I'll try.");
            else yield return Lines("Justin|Hey, you don't have to compare your life with theirs. Everyone's been going through different things.", "Me|I know, but it's hard not to think about it.", "Justin|I understand. We can talk more later, okay? You don't have to explain everything right now.");
            IEnumerator Listen(int n)
            {
                ShotOver(points[n + 2]);
                if (n == 0) yield return Lines("Friend 1|I've been preparing my portfolio because I want to apply for a better opportunity.", "Friend 2|That's a good idea. I've been doing the same thing. I feel like we have to prepare early if we want to get somewhere.");
                else yield return Lines("Friend 3|I finally got to visit the place I've always wanted to see.", "Friend 4|That's amazing! I hope I can do that someday, too.");
                SeenMask |= 1 << n;
                SetObjective("Everyone seems to be moving forward", Check((SeenMask & 1) != 0, "Career conversation") + Check((SeenMask & 2) != 0, "Travel conversation"));
            }
        }
        IEnumerator Corner()
        {
            yield return Visit(4, "A little space", "Sit by the window", "Maybe I shouldn't have come.", "I thought seeing everyone again would make me happy. Instead, I keep thinking about everything I haven't done.");
            yield return Glide(points[4].position + new Vector3(2, 2, -3), player.position + Vector3.up * 2, 2);
            yield return Lines("I don't even think anyone has said anything bad about me. They're just talking about their lives.", "So why does it feel like I'm being left behind?", "Maybe I don't belong here anymore.");
            yield return Visit(5, "A quiet moment", "Check your phone", () => OpenPhoneLock("8:17", "Saturday", dim: true), ClosePhone, "There are no new messages. My reflection is faint on the dark screen.", "Everyone else seems to know who they're becoming.", "I don't even know how to explain who I am right now.");
            yield return Say("Narration", "You turn off the phone. A few quiet moments pass. Then footsteps approach.");
        }
        IEnumerator CheckIn()
        {
            // Justin finds you by the window; the talk happens out on the terrace (SocialJourney.cs).
            bool talked = false;
            AddSpot(points[4], "Talk with Justin by the window", () => !talked, () => Talked(TerraceTalk(), () => talked = true));
            SetObjective("A friend checks in", Check(false, "Talk with Justin by the window"));
            yield return RoamUntil(() => talked);
        }
        static IEnumerator Talked(IEnumerator scene, Action done) { yield return scene; done(); }
        IEnumerator Rejoin()
        {
            yield return Lines("I'm still worried about what people might think of me.", "But maybe I don't have to wait until my life is perfect before I can enjoy being with them.");
            yield return Think("I can pause here, then go back in as I am.");
            bool rejoinBreathCompleted = false;
            yield return Breathe(done => rejoinBreathCompleted = done);
            Record("social.rejoin_breathing", rejoinBreathCompleted ? "completed" : "closed_early");
            yield return Think("I can return to the group without proving anything.");
            // The friends were standing by the stage for the photograph; for this talk they gather round the table, so
            // the conversation's cuts stay at the table instead of crossing the hall. They go back while the journal card is up.
            var stageSpots = GatherAround(points[1]);
            yield return Visit(1, "There is room for you", "Rejoin the group", "Justin|Come sit here!", "Friend 1|There you are! We were just talking about our old school days.", "Friend 2|Do you remember that time we all got confused during the group activity?", "Me|Of course! I still remember how we kept arguing about what to do.", "Friend 1|And somehow, we still finished it!", "Me|Yeah. It wasn't perfect, but we got through it.", "Friend 3|So, what have you been up to lately?", "Me|I've been figuring things out. Some days are good, and some days are difficult. I'm still learning what I want to do next.", "Friend 3|Honestly, same. I think most of us are still figuring things out.", "Friend 2|Yeah. We don't have to have everything planned already.", "Me|That actually makes me feel better.");
            yield return Choose(SocialDynamicsChapter.JournalPrompt, "Choose one thing you appreciate about yourself.", SocialDynamicsChapter.Appreciations, i => Response = i);
            Record("social.appreciation", SocialDynamicsChapter.Appreciations[Response]);
            MindscapeDatabase.SaveJournal(3, 6, SocialDynamicsChapter.JournalPrompt, SocialDynamicsChapter.Appreciations[Response]);
            foreach (var (who, at, turn) in stageSpots) if (who != null) who.SetPositionAndRotation(at, turn);
            yield return Card("Saved to your reflection journal\n\n" + SocialDynamicsChapter.Appreciations[Response]);
            yield return Think("Maybe I don't have to be the most successful person in the room to have a place here.");
            yield return Visit(6, "A memory together", "Join the group photograph", "Justin|Everyone, come on! Let's take a picture before somebody leaves!", "Friend 1|Come closer! There's still room!", "Justin|You're part of this, too. Come here!");
            yield return GroupPhoto();
        }
    }
}
