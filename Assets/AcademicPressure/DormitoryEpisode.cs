using System.Collections;
using Mindscape.Data;
using UnityEngine;
using UIE = UnityEngine.UIElements;
using static UnityEngine.UIElements.UQueryExtensions;

namespace Mindscape.AcademicPressure
{
    // Episode 4 · Dormitory, "One Step at a Time". The emotional conclusion of Chapter 1. After lunch, walk the
    // corridor from the cafeteria to the dorm (its door opens from both sides; roommates are studying inside), then
    // sit at the desk. The plan chosen in Episode 1 shapes the evening:
    //   "Finish everything tonight": work through all four tasks back to back; it wears you out, and the breathing
    //   exercise comes when you are exhausted.
    //   "Start with one task": take a breath first, then finish one small task.
    // Then the reflection journal entry and the self-reflection.
    public sealed class DormitoryEpisode : AcademicEpisode
    {
        public Transform spawn, desk, deskView, laptop, notebook, phone, unfinishedProject;
        public Light deskLamp;
        [Header("Walk from the cafeteria")]
        [Tooltip("Where the episode starts: outside the cafeteria. Empty: start inside the dorm (spawn).")]
        public Transform walkStart;
        [Tooltip("Walking past this point (inside the dorm door) is arriving.")]
        public Transform dormInside;
        public GameObject routeMarkers;
        public Transform[] roommates;

        public override int Episode => 4;
        protected override float TimeLimit => 480;

        public const string JournalPrompt = "What did you learn about how you handle pressure today?";
        public static readonly string[] Tasks = { "Finish introduction", "Review references", "Prepare presentation slides", "Practice presentation" };

        bool seated, inDorm;
        // The plan from Episode 1: true for "I'll finish everything tonight."
        bool everything;
        // The laptop's task list is one UI Toolkit file (LaptopTasks.uxml).
        UIE.VisualElement taskPanel, progressFill;
        UIE.Label taskStatus;
        UIE.Button startButton;
        readonly UIE.Button[] taskButtons = new UIE.Button[4];
        readonly UIE.Label[] taskLabels = new UIE.Label[4];
        int taskPick = -1, doneMask;
        bool taskStarted, taskClosed, canClose;

        protected override string RoamHint => !inDorm ? "Follow the gold markers along the corridor to the dormitory." : "Your desk is under the window.";

        protected override IEnumerator Play()
        {
            everything = Recall(AcademicPressureChapter.LaptopPlan) != "one_task";
            BuildTaskList();
            AddSpot(desk, "Sit at your desk", () => inDorm && !seated, SitDown, 3.6f);

            if (walkStart != null && dormInside != null)
            {
                if (routeMarkers != null) routeMarkers.SetActive(false);
                AddArrival(dormInside, "Go into the dorm", () => !inDorm, EnterDorm, 2.4f);
                Teleport(walkStart.position, walkStart.eulerAngles.y);
                Shot(walkStart.position + walkStart.rotation * new Vector3(0, 5, -5), walkStart.position + walkStart.forward * 10);
                yield return Card("Episode 04  ·  One Step at a Time", "Begin  [Space]");
                yield return Think("Lunch is over and so are classes.\nThe dorm is at the other end of the corridor.");
                if (routeMarkers != null) routeMarkers.SetActive(true);
                SetObjective("Walk back to the dorm", Check(false, "Follow the corridor  ·  hold Shift to run"));
                yield return RoamUntil(() => inDorm);
                if (routeMarkers != null) routeMarkers.SetActive(false);
            }
            else
            {
                inDorm = true;
                Teleport(spawn.position, spawn.eulerAngles.y);
                Shot(spawn.position + spawn.rotation * new Vector3(0, 4.5f, -4), desk.position);
                yield return Card("Episode 04  ·  One Step at a Time", "Begin  [Space]");
                yield return Think("Back in the dorm. It's quiet in here.", () => AddStress(10));
            }
            yield return Think("The deadline. The announcement. What they said at lunch.\nEverything from today is louder now that I'm sitting still.");

            if (everything)
            {
                // This morning's plan: no stopping until everything is done.
                yield return Think("I said I'd finish everything tonight. No breaks. If I stop now, I'll fall behind.", () => AddStress(5));
            }
            else
            {
                yield return Think("Before I decide what to do next, I can take a moment to breathe.");
                bool deskBreathCompleted = false;
                yield return Breathe(done => deskBreathCompleted = done);
                Record("academic.desk_breathing", deskBreathCompleted ? "completed" : "closed_early");
                yield return Think("The deadline is still there. I can meet it one part at a time.");
            }

            SetObjective("Sit at your desk", Check(false, "The desk under the window"));
            yield return RoamUntil(() => seated);

            // At the desk: look at what's there, then open the laptop for a task list instead of "finish entire project".
            bool laptopOpened = false;
            while (!laptopOpened)
            {
                int pick = 0;
                yield return Choose("At the desk", "What do you look at?", new[] { "The unfinished project", "The notebook", "The phone", "Open the laptop" }, i => pick = i);
                if (pick == 0) { yield return Glide(deskView.position, unfinishedProject.position, .6f); yield return Think("A draft with gaps in it. Seeing it all at once makes it look bigger than it is."); }
                else if (pick == 1) { yield return Glide(deskView.position, notebook.position, .6f); yield return Think("My notes from class. Some of them already make sense."); }
                else if (pick == 2)
                {
                    yield return Glide(deskView.position, phone.position, .6f);
                    OpenPhoneLock("9:40", "Monday");
                    PhoneNotification("Reminder", "Research Project Submission", "Tomorrow  —  11:59 PM");
                    yield return Think("The same reminder from this morning.\nResearch Project Submission. Tomorrow, 11:59 PM.");
                    ClosePhone();
                }
                else laptopOpened = true;
            }
            yield return Glide(deskView.position, laptop.position, .6f);
            yield return ShowTasks(everything);

            if (everything) yield return Exhausted();
            else
            {
                yield return Think("One part is done. The rest can wait its turn.");
                EaseStress(15);
            }

            // The reflection journal and self-assessment follow when the episode completes (AcademicEpisode.Reflect).
            Shot(deskView.position, desk.position + Vector3.up * .3f);
            yield return Think("I don't have to solve everything at once.\nI can take one step at a time.");

            Complete("One step at a time", everything
                ? "From the reminder this morning to the desk tonight, you noticed your thoughts, examined a comparison, and pushed through every task in one night.\n\nIt got done, and it wore you out. The breath helped you come back to yourself."
                : "From the reminder this morning to the desk tonight, you noticed your thoughts, took a breath, examined a comparison, and finished one small task.");
        }

        IEnumerator EnterDorm()
        {
            inDorm = true;
            AddStress(10);
            yield return Think(roommates != null && roommates.Length > 0
                ? "Back in the dorm. My roommates are already studying.\nIt's quiet, except for pages turning."
                : "Back in the dorm. It's quiet in here.");
        }

        IEnumerator SitDown()
        {
            yield return Fade(0, 1, .35f);
            player.gameObject.SetActive(false);
            if (deskLamp != null) deskLamp.intensity = 2.4f;
            Shot(deskView.position, desk.position);
            yield return Fade(1, 0, .4f);
            // The player sees the desk: unfinished project, laptop, notebook, phone.
            foreach (var item in new[] { unfinishedProject, laptop, notebook, phone })
            {
                if (item == null) continue;
                yield return Glide(deskView.position, item.position, .7f);
            }
            if (everything)
                yield return DialogueSequence(
                    Me("I still have a lot to do."),
                    BeforeDialogue(new WaitForSeconds(1.2f), Think("This morning I wanted to finish everything tonight. That's still the plan.")));
            else
                yield return DialogueSequence(
                    Me("I still have a lot to do."),
                    BeforeDialogue(new WaitForSeconds(1.2f), Me("But I don't have to finish everything at once.")),
                    Think("This morning I said I'd start with one task. That still feels right."));
            seated = true;
        }

        // Choice A's cost: four tasks in a row leave you drained, and the breathing exercise is where you catch up with yourself.
        IEnumerator Exhausted()
        {
            if (deskLamp != null) deskLamp.intensity = 1.6f;
            Shot(deskView.position, desk.position + Vector3.up * .3f);
            yield return DialogueSequence(
                Think("It's done. All of it."),
                Think("So why do I feel this heavy?"),
                Think("My eyes sting. My shoulders are tight. I've read the same sentence three times and none of it stayed."),
                Think("I pushed through everything at once, and now I can barely think."));
            yield return Think("I need to stop for a moment and breathe.");
            bool completed = false;
            yield return Breathe(done => completed = done, 20);
            Record("academic.exhaustion_breathing", completed ? "completed" : "closed_early");
            yield return DialogueSequence(
                Think("A little steadier now."),
                Think("Finishing everything tonight cost me a lot. Next time, starting with one part might be kinder to me."));
        }

        // ---------------------------------------------------------------- task list

        bool Done(int i) => (doneMask & (1 << i)) != 0;

        IEnumerator ShowTasks(bool all)
        {
            doneMask = 0; taskClosed = canClose = false;
            SetProgress(0);
            taskPanel.EnableInClassList("hidden", false);
            // Each task in the push-through plan takes longer than the last: the tiredness adds up.
            string[][] steps =
            {
                new[] { "Opening the document...", "Writing one paragraph at a time...", "Reading it back once...", "Done." },
                new[] { "Opening the next file...", "Checking every source...", "Rereading the same line...", "Done." },
                new[] { "Starting the next part...", "Eyes getting heavy...", "Pushing through...", "Done." },
                new[] { "One more...", "Losing track of the words...", "Almost there...", "Done." },
            };
            string[] after = { "One down. Three left. Keep going.", "Two done. My neck is getting stiff.", "Three done. I can barely focus.", "All four done. I'm exhausted." };
            for (int round = 0; round < (all ? Tasks.Length : 1); round++)
            {
                taskPick = -1; taskStarted = false;
                for (int i = 0; i < Tasks.Length; i++) DrawTask(i);
                startButton.text = all ? "Choose the next task" : "Choose one task";
                startButton.SetEnabled(false);
                taskStatus.text = all
                    ? (round == 0 ? "Finish all four tonight. Pick where to start." : after[round - 1] + "  Pick the next one.")
                    : "Pick one small task to finish tonight.";
                if (ReplayNext('t', out var keptTask)) { taskPick = int.Parse(keptTask); taskStarted = true; }
                while (!taskStarted) yield return null;
                Log('t', taskPick.ToString());

                foreach (var b in taskButtons) b.SetEnabled(false);
                startButton.SetEnabled(false);
                var words = steps[all ? round : 0];
                float seconds = all ? 5 + round * 1.5f : 5;
                for (float t = 0; t < seconds; t += Time.deltaTime)
                {
                    SetProgress(t / seconds);
                    taskStatus.text = Tasks[taskPick] + "  ·  " + words[Mathf.Min(words.Length - 1, (int)(t / seconds * words.Length))];
                    yield return null;
                }
                SetProgress(1);
                doneMask |= 1 << taskPick;
                if (round == 0) Record(AcademicPressureChapter.FirstTask, Tasks[taskPick]);
                DrawTask(taskPick);
                effects.PlayOneShot(chime, .16f);
                if (all) AddStress(7);
                foreach (var b in taskButtons) b.SetEnabled(true);
            }
            if (all) Record("academic.tasks_finished", "all");
            taskStatus.text = all ? after[Tasks.Length - 1] : "One task done: " + Tasks[taskPick] + ".";
            foreach (var b in taskButtons) b.SetEnabled(false);
            startButton.text = "Close laptop";
            canClose = true;
            startButton.SetEnabled(true);
            if (Replaying) taskClosed = true;
            while (!taskClosed) yield return null;
            taskPanel.EnableInClassList("hidden", true);
        }

        void PickTask(int index)
        {
            if (taskStarted || IsPaused || Done(index)) return;
            taskPick = index;
            for (int i = 0; i < Tasks.Length; i++) DrawTask(i);
            startButton.text = "Start: " + Tasks[index];
            startButton.SetEnabled(true);
        }

        void OnStartButton()
        {
            if (IsPaused) return;
            if (canClose) taskClosed = true;
            else if (!taskStarted && taskPick >= 0 && !Done(taskPick)) taskStarted = true;
        }

        void DrawTask(int i)
        {
            bool done = Done(i), picked = i == taskPick;
            taskLabels[i].text = Tasks[i] + (done ? "   ·  done" : "");
            taskButtons[i].EnableInClassList("done", done);
            taskButtons[i].EnableInClassList("picked", picked && !done);
        }

        void SetProgress(float amount) => progressFill.style.width = UIE.Length.Percent(Mathf.Clamp01(amount) * 100f);

        void BuildTaskList()
        {
            taskPanel = Overlay(EpisodeUI.Load().laptopTasks, "laptop task list").Q("laptop-root");
            var list = taskPanel.Q("laptop-tasks"); list.Clear();
            for (int i = 0; i < Tasks.Length; i++)
            {
                int id = i;
                // A row of the Tasks window: a checkbox (with its tick) and the task's name.
                var button = new UIE.Button(() => PickTask(id)) { focusable = false };
                button.AddToClassList("task");
                var check = new UIE.VisualElement(); check.AddToClassList("check");
                var tick = new UIE.VisualElement(); tick.AddToClassList("tick"); check.Add(tick);
                var label = new UIE.Label(Tasks[i]); label.AddToClassList("task-label");
                button.Add(check); button.Add(label);
                taskButtons[i] = button; taskLabels[i] = label; list.Add(button);
            }
            progressFill = taskPanel.Q("laptop-progress");
            taskStatus = taskPanel.Q<UIE.Label>("laptop-status");
            startButton = taskPanel.Q<UIE.Button>("laptop-start"); startButton.clicked += OnStartButton; startButton.focusable = false;
            taskPanel.EnableInClassList("hidden", true);
        }
    }
}
