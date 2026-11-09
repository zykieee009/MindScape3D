using System.Collections;
using Mindscape.AcademicPressure;
using UnityEngine;

namespace Mindscape.FamilyFinale
{
    // The final two episodes share the established exploration, dialogue and pause UI.
    public sealed partial class FamilyFinaleEpisode : AcademicEpisode
    {
        public int episodeNumber = 4;
        public Transform bedroomSpawn, paper, dinner, mother, father, tim, bed;
        public Transform bedroomView, dinnerView, bedView;
        public Transform[] keepsakes;
        public Light eveningLight;
        public override int Episode => episodeNumber;
        public override int Chapter => 2;
        protected override float TimeLimit => 480;
        public static string Scene(int episode) => "Chapter 2 - Episode " + episode;
        public static string SaveKey(int episode) => Mindscape.Data.MindscapeDatabase.ProfileKey("Mindscape.FamilyExpectations.Episode" + episode);
        protected override string CompletionKey => SaveKey(Episode);
        protected override string NextScene => Episode == 4 ? Scene(5) : Mindscape.SocialDynamics.SocialDynamicsChapter.Scene(1);
        protected override string ContinueLabel => Episode == 4 ? "Continue to Episode 5" : "Begin Chapter 3";
        protected override string CompletionNext => Episode == 4 ? "NEXT: WHAT I LEFT BEHIND" : "CHAPTER 2 COMPLETE  •  FAMILY EXPECTATIONS";
        protected override string RoamHint => Episode == 4 ? "Follow the gold marker to continue the evening." : "Explore the keepsakes on your desk. Press E to examine.";
        public int SeenMask { get; private set; }
        public bool DinnerFinished { get; private set; }
        public int WorryChoice { get; private set; } = -1;
        public int DinnerChoice { get; private set; } = -1;
        public int ReflectionChoice { get; private set; } = -1;
        bool paperSeen, returned, resting;
        static readonly string[] KeepsakeNames = { "Old certificate", "Old baseball set", "Old notebook" };

        protected override IEnumerator Play()
        {
            Teleport(bedroomSpawn.position, bedroomSpawn.eulerAngles.y);
            Shot(bedroomView.position, paper.position);
            if (Episode == 4) yield return Evening();
            else yield return Remembering();
        }

        IEnumerator Evening()
        {
            AddSpot(paper, "Return to the examination paper", () => !paperSeen, Worry);
            AddSpot(dinner, "Join your family for dinner", () => paperSeen && !DinnerFinished, Dinner, 4f);
            AddSpot(paper, "Return to your bedroom", () => DinnerFinished && !returned, ReturnToRoom);
            yield return Card("CHAPTER 2 · FAMILY EXPECTATIONS\n<size=60%>Episode 04 · The Weight Behind the Worry</size>", "Return to the present  [Space]");
            yield return Think("That's what happened. I studied all night, woke up late, and then the traffic made everything worse.");
            SetObjective("Back in the present", Check(false, "Examine the paper on your desk"));
            yield return RoamUntil(() => paperSeen);
            yield return DialogueSequence(
                Say("Mother", PlayerName + ", dinner is ready! Come down before the food gets cold."),
                Think("I'll put the paper inside my notebook for now."));
            SetObjective("Family dinner", Check(false, "Join your family at the dining table"));
            yield return RoamUntil(() => DinnerFinished);
            SetObjective("A quiet moment", Check(false, "Return to the bedroom desk"));
            yield return RoamUntil(() => returned);
            Complete("The weight behind the worry", "You named a worry and chose how to speak at dinner.\n\nYour family wants a good future for you, but the pressure still feels real. Your choices are saved.\n\nNext: revisit the things you once enjoyed.");
        }

        IEnumerator Worry()
        {
            Shot(bedroomView.position, paper.position);
            yield return DialogueSequence(
                Think("If I had slept earlier, packed my things, or left sooner, maybe everything would have gone differently."),
                Think("I don't even know my result yet, but I already feel like I failed."));
            yield return Choose("Name the worry", "Which thought feels strongest right now?", new[] {
                "What if I fail the examination?", "What if my family is disappointed in me?", "What if everyone else gets a better score than I do?"
            }, i => WorryChoice = i);
            Record("family.main_worry", WorryChoice.ToString());
            AddStress(10);
            string[] responses = { "I'm scared that all the time I spent studying won't be enough.", "I'm worried my family will think I didn't try hard enough.", "I keep imagining everyone doing better, even though I don't know their results." };
            yield return DialogueSequence(
                Think(responses[WorryChoice]),
                Think("Maybe I'm not only worried about the exam. I'm worried about what the result might mean about me."),
                Think("I want my family to be proud of me. I want to feel proud of myself, too. Sometimes one mistake feels like it could ruin everything."));
            yield return Think("Before I decide what this result means, I can pause and breathe.");
            bool worryBreathCompleted = false;
            yield return Breathe(done => worryBreathCompleted = done);
            Record("family.worry_breathing", worryBreathCompleted ? "completed" : "closed_early");
            yield return Think("I still don't know the result. I can notice this worry without calling it the truth.");
            yield return Card("A thought is something you can notice.\nIt is not a result, a diagnosis, or a score.", "Continue  [Space]");
            EaseStress(8);
            paperSeen = true;
        }

        IEnumerator Dinner()
        {
            Shot(dinnerView.position, dinnerView.position + dinnerView.forward * 5);
            yield return DialogueSequence(
                Say("Mother", "How was school today? Wasn't your examination this morning?"),
                Me("It was... okay, I guess."),
                Say("Father", "Did you manage to answer everything?"),
                Me("Not everything. I arrived late, so I had less time than I expected."),
                Say("Father", "What happened? You knew the examination was important."),
                Me("I studied late, and then I woke up late. The traffic made it worse."),
                Say("Mother", "Try to rest properly before an examination. You can't study well when you're too tired."),
                Me("I know, Mom."),
                Say("Mother", "Your cousin received an academic award recently. I'm happy to see young people working hard for their future."),
                Think("They didn't say I was a failure. They didn't compare our grades directly. Why does it feel like I have to prove I'm doing enough?"));
            AddStress(12);
            yield return Choose("At the dinner table", "How do you respond?", new[] {
                "I'm just tired. I want to rest for a while.", "I'm happy for my cousin. I just feel worried about my own progress."
            }, i => DinnerChoice = i);
            Record("family.dinner_response", DinnerChoice.ToString());
            EaseStress(DinnerChoice == 1 ? 8 : 4);
            string afterDinner = "I know they want me to have a good future. I wish I knew how to stop feeling like I'm falling behind.";
            if (DinnerChoice == 0)
            {
                yield return DialogueSequence(
                    Me("I'm just tired. I want to rest for a while."),
                    Say("Mother", "All right. Finish your dinner and get some rest."),
                    Think(afterDinner));
            }
            else
            {
                yield return DialogueSequence(
                    Me("I'm happy for my cousin. I just feel worried about my own progress."),
                    Say("Mother", "I understand school can be stressful. Just do your best, okay?"),
                    Me("I'll try, Mom."),
                    Think(afterDinner));
            }
            DinnerFinished = true;
        }

        IEnumerator ReturnToRoom()
        {
            Shot(bedroomView.position, paper.position);
            yield return Think("The room is quiet again. On my desk are things from a time when trying felt like enough.");
            returned = true;
        }

        IEnumerator Remembering()
        {
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                AddSpot(keepsakes[i], "Examine " + KeepsakeNames[i], () => (SeenMask & (1 << index)) == 0, () => Inspect(index));
            }
            AddSpot(bed, "Lie down for the night", () => SeenMask == 7 && !resting, Rest);
            yield return Card("CHAPTER 2 · FAMILY EXPECTATIONS\n<size=60%>Episode 05 · What I Left Behind</size>", "Begin  [Space]");
            yield return Think("After dinner, I notice my old certificate and notebook on the desk, and my baseball things by the wardrobe.");
            SetObjective("Remembering the past", "Examine the old certificate, the baseball set, and the notebook.\nYou can explore them in any order.");
            yield return RoamUntil(() => SeenMask == 7);
            Shot(bedroomView.position, keepsakes[2].position);
            yield return Choose("Reflection", "What am I focusing on right now?", new[] {
                "The progress I have already made.", "The milestones I imagine I should have reached by now.", "One small step I can still take in the present."
            }, i => ReflectionChoice = i);
            Record("family.past_reflection", ReflectionChoice.ToString());
            string[] responses = { "There has been progress. It's hard to see when I only look at what is missing.", "I keep comparing my life to a version of it that only exists in my mind.", "I could make a little time for something I enjoy. It doesn't have to become an achievement." };
            yield return DialogueSequence(
                Think(responses[ReflectionChoice]),
                Think("I can't change the past. But I keep using it to judge where I am today."),
                Think("I wish I could see my progress without always thinking about what I haven't achieved."));
            SetObjective("The end of the day", Check(false, "Go to bed when you are ready"));
            yield return RoamUntil(() => resting);
            // No task or choice during this sequence: the story lets the thoughts pass.
            player.gameObject.SetActive(false);
            Shot(bedView.position, bed.position);
            if (eveningLight != null) eveningLight.intensity = .35f;
            yield return DialogueSequence(
                Think("I should have done more when I was younger."),
                Think("What if I never become successful?"),
                Think("What if my family eventually realizes I'm not as capable as they think?"),
                Think("Everyone expects me to have a plan, but I don't even know what will happen next."),
                Think("I know I can't change the examination anymore. Worrying won't give me the result. So why can't I stop thinking about it?"));
            AddStress(8);
            yield return Fade(0, 1, 2.5f);
            yield return Fade(1, 0, .4f);
            yield return Say("Narration", "Sometimes, the mind keeps searching for answers even when there is nothing left to change. " + PlayerName + " falls asleep, but the worries are still waiting for the next day.");
            PlayerPrefs.SetInt(Mindscape.Data.MindscapeDatabase.ProfileKey("Mindscape.FamilyExpectations.Complete"), 1);
            PlayerPrefs.SetInt(Mindscape.Data.MindscapeProgress.ChapterUnlockedKey, Mathf.Max(3, PlayerPrefs.GetInt(Mindscape.Data.MindscapeProgress.ChapterUnlockedKey, 1)));
            Complete("What I left behind", "You explored three keepsakes and noticed how expectations shape the way you see your past.\n\nRest ends the day; it does not magically resolve the worry.\nYour reflection choice and Chapter 2 completion are saved.");
        }

        // Inspect(index): the keepsake memories are in KeepsakeFlashbacks.cs.
        IEnumerator Rest() { resting = true; yield break; }
    }
}
