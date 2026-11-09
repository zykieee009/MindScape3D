using System.Collections;
using Mindscape.Data;
using UnityEngine;
using UIE = UnityEngine.UIElements;
using static UnityEngine.UIElements.UQueryExtensions;

namespace Mindscape.AcademicPressure
{
    // Episode 1 · House. Wake up to reminders of school: the phone's deadline, the unfinished project
    // (first decision + thought identification), packing, two chores, then the walk to the bus stop.
    public sealed class HouseEpisode : AcademicEpisode
    {
        public Transform phone, laptop, books, schoolBag, bed, plate, sink, frontDoor, busStop, bus;
        public GameObject messyBed, fixedBed, carriedBag, carriedPlate, routeMarkers, laptopScreen;
        public Vector3 outsideSpawn = new Vector3(-22.1f, .7f, 8.2f);
        public float outsideYaw = 0;

        public override int Episode => 1;
        protected override bool AsksForName => true;
        protected override float TimeLimit => 720;

        bool phoneChecked, laptopChecked, booksPacked, bagPacked, bedMade, plateCleared, plateWashed, dressed, leftHouse, atStop;
        AudioClip alarm;

        protected override string RoamHint => !phoneChecked ? "Your phone is on the bedside table." :
            !laptopChecked ? "Your laptop is on the desk." :
            !(booksPacked && bagPacked) ? "Pack your books and your school bag." :
            !(bedMade && plateWashed) ? "Finish your chores before you leave." :
            !dressed ? "The bathroom is down the hall. Change into your school uniform there." :
            !leftHouse ? "The front door is in the living room. Open it and step outside." : "Follow the blue markers to the bus stop.";

        protected override IEnumerator Play()
        {
            alarm = Tone("Morning alarm", 760, 1.6f, true);
            if (messyBed != null) messyBed.SetActive(true);
            if (fixedBed != null) fixedBed.SetActive(false);
            if (carriedBag != null) carriedBag.SetActive(false);
            if (carriedPlate != null) carriedPlate.SetActive(false);
            if (routeMarkers != null) routeMarkers.SetActive(false);
            if (bus != null) bus.gameObject.SetActive(false);

            AddSpot(phone, "Check your phone", () => !phoneChecked, CheckPhone);
            AddSpot(laptop, "Open your laptop", () => phoneChecked && !laptopChecked, OpenLaptop);
            AddSpot(books, "Pack your books", () => laptopChecked && !booksPacked, PackBooks);
            AddSpot(schoolBag, "Pack your school bag", () => laptopChecked && !bagPacked, PackBag);
            AddSpot(bed, "Make your bed", () => booksPacked && bagPacked && !bedMade, MakeBed, 3.6f);
            AddSpot(plate, "Clear your breakfast plate", () => booksPacked && bagPacked && !plateCleared, ClearPlate);
            AddSpot(sink, "Wash the plate", () => plateCleared && !plateWashed, WashPlate, 3.6f);
            // The front door opens with its own [ E ]; stepping out onto the path is what leaves the house.
            AddArrival(Waypoint("Outside the front door", outsideSpawn), "Leave for school", () => bedMade && plateWashed && dressed && !leftHouse, LeaveHouse);
            // Into the bathroom to change into the school uniform (a house without one skips the step).
            dressed = AddBathroom("Change into your school uniform", () => bedMade && plateWashed && !dressed, Outfit.SchoolUniform,
                "Uniform on. Now I look ready, even if I don't feel it.", () => dressed = true) == null;
            AddSpot(busStop, "Wait for the bus", () => leftHouse && !atStop, WaitForBus, 4.5f);

            // Opening: who is this story about?
            Shot(bed.position + new Vector3(-3.2f, 3.4f, 3.6f), bed.position);
            yield return Card("CHAPTER 1  ·  ACADEMIC PRESSURE\n<size=60%>Episode 01  ·  Tomorrow Already?</size>", "Begin  [Space]");
            string chosen = PlayerName;
            yield return Ask("Before we begin", "What name should the story use for you?", "Your name", MindscapeDatabase.HasPlayerName ? PlayerName : "", false, 24, "Start", n => chosen = n);
            MindscapeDatabase.PlayerName = chosen;
            PlayerName = MindscapeDatabase.PlayerName;

            effects.clip = alarm; effects.loop = true; effects.volume = .14f; effects.Play();
            yield return Glide(bed.position + new Vector3(-2.4f, 2.6f, 2.4f), bed.position + Vector3.up * .4f, 2.2f);
            yield return DialogueSequence(
                Say("Morning", "An alarm. The room is still half dark.", () => { effects.Stop(); effects.loop = false; }),
                Think("Morning already.\nBefore anything else, I should check my phone."));

            SetObjective("Check your phone", Check(false, "Phone on the bedside table"));
            yield return RoamUntil(() => phoneChecked);

            SetObjective("Look at your project", Check(true, "Phone") + Check(false, "Laptop on the desk"));
            yield return RoamUntil(() => laptopChecked);

            yield return RoamUntil(() =>
            {
                SetObjective("Get ready for school", Check(booksPacked, "Pack your books") + Check(bagPacked, "Pack your school bag"));
                return booksPacked && bagPacked;
            });

            yield return Think("Before I go, there are a few things to do around the house.");
            yield return RoamUntil(() =>
            {
                SetObjective("Chores before you leave", Check(bedMade, "Make your bed") + Check(plateWashed, plateCleared ? "Wash the plate at the kitchen sink" : "Clear your plate from the dining table"));
                return bedMade && plateWashed;
            });

            if (!dressed)
            {
                SetObjective("Get dressed", Check(true, "Chores done") + Check(false, "Change into your uniform in the bathroom"));
                yield return RoamUntil(() => dressed);
            }

            SetObjective("Leave for school", Check(true, "Packed") + Check(true, "Chores done") + Check(true, "School uniform") + Check(false, "Front door"));
            yield return RoamUntil(() => leftHouse);

            SetObjective("Walk to the bus stop", Check(false, "Follow the blue markers"));
            yield return RoamUntil(() => atStop);

            string plan = Recall(AcademicPressureChapter.LaptopPlan) == "one_task" ? "You decided to start with one task first." : "You decided to finish everything tonight.";
            Complete("Tomorrow already?",
                "The reminder, the unfinished introduction, and a full morning.\n\n" + plan +
                "\nYou named a feeling: " + Recall(AcademicPressureChapter.Feeling, "—") + ".\n\nYour choices are saved.");
        }

        // ---------------------------------------------------------------- interactions

        IEnumerator CheckPhone()
        {
            ShotOver(phone, 1.6f, 1.2f);
            OpenPhoneLock("7:12", "Monday");
            PhoneNotification("Reminder", "Research Project Submission", "Tomorrow  —  11:59 PM");
            yield return DialogueSequence(
                Think("Tomorrow already?"),
                Think("I knew it was close. Seeing the date makes it feel closer."),
                Think("There's still so much left to do on it.\nIt feels like the whole day is already counting down."));
            AddStress(12);
            ClosePhone();
            phoneChecked = true;
        }

        IEnumerator OpenLaptop()
        {
            ShotOver(laptop, 1.7f, 1.1f);
            if (laptopScreen != null) laptopScreen.SetActive(true);
            ShowLaptopDocument(true);
            yield return Me("I haven't even finished the first part.");
            ShowLaptopDocument(false);   // the decision gets the whole view
            int plan = 0;
            yield return Choose("Decision", "What will you do?", new[] { "\"I'll finish everything tonight.\"", "\"I'll start with one task first.\"" }, i => plan = i);
            Record(AcademicPressureChapter.LaptopPlan, plan == 0 ? "finish_everything" : "one_task");
            yield return plan == 0 ? Me("I have to finish everything tonight. I can't fall behind.") : Me("Maybe I should just start with one part.");

            // First CBT feature: thought identification.
            yield return Card("\"What if I fail this project?\"", "A thought appears  ·  Continue  [Space]");
            AddStress(10);
            string[] feelings = { "Worried", "Overwhelmed", "Frustrated", "Calm" };
            int felt = 0;
            yield return Choose("Thought identification", "What are you feeling?", feelings, i => felt = i);
            Record(AcademicPressureChapter.Feeling, feelings[felt]);
            EaseStress(6);   // naming the feeling
            yield return Think(felt == 3
                ? "Calm. The deadline is still there, and I can notice it without rushing."
                : feelings[felt] + ". That's what this feels like right now.\nNoticing it doesn't change the deadline, but it helps me see what's happening.");
            if (laptopScreen != null) laptopScreen.SetActive(false);
            laptopChecked = true;
        }

        // The laptop up close: the same laptop screen as Chapter 1 · Episode 4 (LaptopTasks.uxml), showing the project as a
        // document window with its three parts instead of the task list.
        UIE.VisualElement laptopDocument;

        void ShowLaptopDocument(bool show)
        {
            if (laptopDocument == null)
            {
                if (!show) return;
                laptopDocument = Overlay(EpisodeUI.Load().laptopTasks, "laptop project").Q("laptop-root");
                // Smaller than the task list so the whole laptop sits above the dialogue bar.
                var lap = laptopDocument.Q(className: "laptop");
                lap.style.transformOrigin = new UIE.TransformOrigin(UIE.Length.Percent(50), UIE.Length.Percent(0));
                lap.style.scale = new UIE.Scale(new Vector3(.76f, .76f, 1));
                laptopDocument.Q<UIE.Label>(className: "menubar-app").text = "Documents";
                laptopDocument.Q<UIE.Label>(className: "titlebar-title").text = "Research Project.docx";
                laptopDocument.Q(className: "removed").style.display = UIE.DisplayStyle.None;
                laptopDocument.Q<UIE.Label>(className: "window-title").text = "Research Project  ·  Draft";
                laptopDocument.Q(className: "laptop-start").style.display = UIE.DisplayStyle.None;
                laptopDocument.Q<UIE.Label>("laptop-status").text = "Due tomorrow.  Only the first part has been started.";
                laptopDocument.Q<UIE.Label>("laptop-status").style.width = UIE.Length.Percent(100);
                laptopDocument.Q("laptop-progress").style.width = UIE.Length.Percent(12);
                var list = laptopDocument.Q("laptop-tasks"); list.Clear();
                foreach (var (part, note) in new[] { ("1. Introduction", "unfinished"), ("2. References", "not started"), ("3. Slides", "not started") })
                {
                    var row = new UIE.VisualElement { pickingMode = UIE.PickingMode.Ignore };
                    row.AddToClassList("task");
                    var check = new UIE.VisualElement(); check.AddToClassList("check");
                    var tick = new UIE.VisualElement(); tick.AddToClassList("tick"); check.Add(tick);
                    var label = new UIE.Label(part + "   ·  " + note); label.AddToClassList("task-label");
                    row.Add(check); row.Add(label); list.Add(row);
                }
            }
            laptopDocument.EnableInClassList("hidden", !show);
        }

        IEnumerator PackBooks()
        {
            ShotOver(books, 1.8f, 1.2f);
            yield return Think("My reference books and research notes. I'll bring them with me.");
            books.gameObject.SetActive(false);
            effects.PlayOneShot(chime, .1f);
            booksPacked = true;
        }

        IEnumerator PackBag()
        {
            ShotOver(schoolBag, 2f, 1.3f);
            yield return Think("ID, notebook, charger. Everything for the day goes in here.");
            schoolBag.gameObject.SetActive(false);
            if (carriedBag != null) carriedBag.SetActive(true);
            effects.PlayOneShot(chime, .1f);
            bagPacked = true;
        }

        IEnumerator MakeBed()
        {
            yield return Fade(0, 1, .3f);
            if (messyBed != null) messyBed.SetActive(false);
            if (fixedBed != null) fixedBed.SetActive(true);
            ShotOver(bed, 3.2f, 2.4f);
            yield return Fade(1, 0, .3f);
            yield return Think("Pillow, blanket, done. One small thing finished.");
            EaseStress(4);
            bedMade = true;
        }

        IEnumerator ClearPlate()
        {
            ShotOver(plate, 1.8f, 1.3f);
            plate.gameObject.SetActive(false);
            if (carriedPlate != null) carriedPlate.SetActive(true);
            yield return Think("Breakfast is done. The sink is in the kitchen.");
            plateCleared = true;
        }

        IEnumerator WashPlate()
        {
            ShotOver(sink, 2.2f, 1.6f);
            yield return Fade(0, 1, .3f);
            if (carriedPlate != null) carriedPlate.SetActive(false);
            yield return Fade(1, 0, .3f);
            yield return Think("Washed and drying. The kitchen is quiet again.");
            plateWashed = true;
        }

        IEnumerator LeaveHouse()
        {
            if (routeMarkers != null) routeMarkers.SetActive(true);
            yield return Think("Fresh air. The bus stop is just down the street.");
            leftHouse = true;
        }

        IEnumerator WaitForBus()
        {
            atStop = true;
            if (routeMarkers != null) routeMarkers.SetActive(false);
            Shot(busStop.position + new Vector3(-6, 3.5f, -6), busStop.position + Vector3.up);
            yield return Think("The reminder keeps replaying in my head.\nTomorrow, 11:59 PM.");
            if (bus == null) yield break;
            // The trip to school: the bus pulls in, picks you up, and drives on.
            Vector3 stopAt = bus.position;
            bus.gameObject.SetActive(true);
            Vector3 from = stopAt + Vector3.left * 30;
            for (float t = 0; t < 3; t += Time.deltaTime) { bus.position = Vector3.Lerp(from, stopAt, Mathf.SmoothStep(0, 1, t / 3)); yield return null; }
            yield return Fade(0, 1, .35f);
            player.gameObject.SetActive(false);
            Shot(stopAt + new Vector3(4, 6, -14), stopAt + Vector3.up * 1.5f);
            yield return Fade(1, 0, .35f);
            for (float t = 0; t < 4; t += Time.deltaTime) { bus.position += Vector3.right * Time.deltaTime * (2 + t * 3); yield return null; }
            yield return Card("On the way to school", "The deadline rides along  ·  Continue  [Space]");
        }
    }
}
