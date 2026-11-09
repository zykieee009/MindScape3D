using System.Collections;
using System.Collections.Generic;
using Mindscape.Minigames;
using UnityEngine;

namespace Mindscape.AcademicPressure
{
    // Episode 3 · Cafeteria, "Everyone Seems Ahead". Walk from Room 03 along the corridor to the cafeteria; overhearing
    // classmates' progress brings up a comparison thought, which the player puts on trial in the Worry Courtroom.
    public sealed class CafeteriaEpisode : AcademicEpisode
    {
        public Transform spawn, table, studentOne, studentTwo, friend, tableView, cafeteriaDoor, inside;
        public GameObject routeMarkers;
        public string friendName = "Sam";
        [Tooltip("The Worry Courtroom minigame (Assets/Prefabs/Minigames/WorryCourtroom.prefab).")]
        public WorryCourtroomMinigame worryCourtroomPrefab;
        [Tooltip("The case on trial (Assets/Minigames/WorryCourtroom/Cases/Chapter1_EveryoneAhead).")]
        public WorryCourtroomCase worryCase;

        public override int Episode => 3;
        protected override float TimeLimit => 420;

        WorryCourtroomMinigame courtroom;
        bool arrived, joined, examined;

        protected override bool OverlayOpen => courtroom != null && courtroom.IsOpen;
        protected override string RoamHint => !arrived ? "Follow the blue markers along the corridor to the cafeteria." : "Your classmates are at the table with the gold marker.";

        protected override IEnumerator Play()
        {
            // Created now so its Start (which wires the buttons and hides the screen) has run long before it opens.
            courtroom = Instantiate(worryCourtroomPrefab, transform);
            courtroom.name = "Worry Courtroom";
            courtroom.startOpen = false; // opened by the story, closes itself when the case is done
            courtroom.cases = new List<WorryCourtroomCase> { worryCase }; courtroom.startCase = 0;
            // The cafeteria door opens with its own [ E ]; walking in past it is what arrives.
            if (cafeteriaDoor != null) AddArrival(inside != null ? inside : cafeteriaDoor, "Go into the cafeteria", () => !arrived, EnterCafeteria, 2.4f);
            else arrived = true;
            AddSpot(table, "Sit with your classmates", () => arrived && !joined, JoinTable, 3.8f);
            if (routeMarkers != null) routeMarkers.SetActive(false);

            Teleport(spawn.position, spawn.eulerAngles.y);
            Shot(spawn.position + spawn.rotation * new Vector3(0, 5, -5), spawn.position + spawn.forward * 10);
            yield return Card("Episode 03  ·  Everyone Seems Ahead", "Begin  [Space]");
            yield return Think("Lunch. " + friendName + " said they'd save a seat.\nThe cafeteria is at the end of the corridor.");

            if (!arrived)
            {
                if (routeMarkers != null) routeMarkers.SetActive(true);
                SetObjective("Go to the cafeteria", Check(false, "Follow the corridor  ·  hold Shift to run"));
                yield return RoamUntil(() => arrived);
            }
            SetObjective("Find a seat for lunch", Check(true, "Cafeteria") + Check(false, "Join your classmates"));
            yield return RoamUntil(() => joined);

            Complete("Everyone seems ahead",
                "You noticed a comparison thought and put it on trial.\n\n" +
                "Balanced thought:\n\"" + Recall(AcademicPressureChapter.BalancedThought, DefaultBalancedThought) + "\"\n\nYour courtroom record is saved.");
        }

        IEnumerator EnterCafeteria()
        {
            if (routeMarkers != null) routeMarkers.SetActive(false);
            yield return Think("Trays, voices, the long tables.\n" + friendName + " is waving from the table near the door.");
            arrived = true;
        }

        IEnumerator JoinTable()
        {
            yield return Fade(0, 1, .3f);
            player.gameObject.SetActive(false);
            if (tableView != null) Shot(tableView.position, table.position + Vector3.up * 1.2f);
            yield return Fade(1, 0, .35f);

            if (studentOne != null) yield return Glide(tableView.position, studentOne.position + Vector3.up * 1.4f, .8f);
            yield return DialogueSequence(
                Say("Student 1", "I already finished my presentation."),
                BeforeDialogue(studentTwo != null ? Glide(tableView.position, studentTwo.position + Vector3.up * 1.4f, .8f) : null,
                    Say("Student 2", "Same. I'm just practicing now.")),
                Think("Everyone is ahead of me."),
                Think("Maybe I'm not good enough."));
            AddStress(15);

            // The screen changes: the worry goes to court. The story panels are UI Toolkit and would sit over it.
            yield return Fade(0, 1, .4f);
            HidePanels();
            string keptThought = null;
            // A loaded save skips the trial and keeps the thought the player reached the first time.
            if (ReplayNext('k', out var kept)) keptThought = kept;
            else
            {
                courtroom.Open(null);
                yield return Fade(1, 0, .2f);
                while (courtroom.IsOpen) yield return null;
                Log('k', courtroom.BalancedThought ?? "");
            }
            if (keptThought != null) yield return Fade(1, 0, .2f);
            // Leaving early (the courtroom's exit button) keeps the case's balanced thought.
            string reached = keptThought ?? courtroom.BalancedThought;
            string balanced = string.IsNullOrEmpty(reached) ? DefaultBalancedThought : reached;
            Record(AcademicPressureChapter.BalancedThought, balanced);
            if (!string.IsNullOrEmpty(courtroom.SmallStep)) Record("small_step", courtroom.SmallStep);
            examined = true;

            if (tableView != null) Shot(tableView.position, table.position + Vector3.up * 1.2f);
            yield return DialogueSequence(
                Think("\"" + balanced + "\""),
                Think("The thought is still there. It just isn't the only one anymore.", () => EaseStress(18)),
                BeforeDialogue(friend != null ? Glide(tableView.position, friend.position + Vector3.up * 1.4f, .8f) : null,
                    Say(friendName, "You okay? You went quiet for a second.")),
                Me(Recall(AcademicPressureChapter.FriendReply) == "fine" ? "Yeah. Just thinking. I'll figure out my next step tonight." : "A little better. I'll work on it tonight, one part at a time."));
            joined = examined;
        }

        // The case's helpful verdict, used if the player leaves the courtroom before choosing one.
        string DefaultBalancedThought
        {
            get
            {
                if (worryCase != null) foreach (var option in worryCase.verdict) if (option.isGood) return option.text;
                return "Other students may be ahead, but I can still work on my project one step at a time.";
            }
        }
    }
}
