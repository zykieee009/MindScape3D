using System.Collections;
using System.Linq;
using Mindscape.AcademicPressure;
using UnityEngine;
using UnityEngine.InputSystem;
using UIE = UnityEngine.UIElements;
using static UnityEngine.UIElements.UQueryExtensions;

namespace Mindscape.FamilyExpectations
{
    // Episode 3 · Running Out of Time. The bus arrives at school after the examination has begun: cross the yard to
    // Room 03, speak to the teacher, find the empty seat, and answer what you can in the time that remains. A short
    // grounding break steadies the clock-watching; the paper is handed in as it is. No score is shown.
    public sealed class FamilyExamEpisode : FamilyEpisode
    {
        public Transform entrance, landing, seat, seatView, teacher, openingView, spawn, bus;
        public GameObject routeMarkers;
        [Min(30)] public float examinationSeconds = 180;

        public override int Episode => 3;
        protected override float TimeLimit => 420;
        protected override string FlashbackLabel => "Examination morning";

        static readonly string[] Questions =
        {
            "A notebook costs 24 pesos. How much do three notebooks cost?",
            "Read: ‘Even though it rained, Ana brought the seedlings outside after the clouds cleared.’\n\nWhen did Ana bring the seedlings outside?",
            "A bus travels 12 kilometers in 30 minutes. At the same speed, how far will it travel in one hour?",
            "Which sentence is an observation rather than a prediction?",
            "A class plants 40 seeds. Three-quarters of them sprout. How many seeds sprout?"
        };
        static readonly string[][] Options =
        {
            new[] { "48 pesos", "72 pesos", "96 pesos" },
            new[] { "Before the rain", "While it was raining", "After the clouds cleared" },
            new[] { "18 kilometers", "24 kilometers", "36 kilometers" },
            new[] { "The clock shows 8:15.", "Everyone will finish before me.", "This will be my worst examination." },
            new[] { "10 seeds", "20 seeds", "30 seeds" }
        };
        // The 5-4-3-2-1 grounding technique: five things you see, four you feel, three you hear, two you smell, one you taste.
        static readonly string[] GroundingSense = { "5  ·  SEE", "4  ·  TOUCH", "3  ·  HEAR", "2  ·  SMELL", "1  ·  TASTE" };
        static readonly string[] GroundingWords =
        {
            "Look around and find five things you can see.\nThe desk, the paper, your pencil, the window, the clock.",
            "Notice four things you can feel.\nYour feet on the floor, the chair, the pencil in your hand, your sleeves.",
            "Listen for three sounds.\nThe clock ticking, a page turning, your own breathing.",
            "Notice two things you can smell.\nThe paper, the air in the room.",
            "Notice one thing you can taste.\nA sip of water, or just the taste in your mouth."
        };
        static readonly string[] GroundingActions = { "I see five things", "I feel four things", "I hear three sounds", "I smell two things", "I notice one taste" };

        bool admitted, seated, submitted, examOpen, groundingOpen, confirmOpen, grounded, timeUp;
        readonly int[] answers = { -1, -1, -1, -1, -1 };
        readonly bool[] flagged = new bool[5];
        int selected, groundingStep;
        float remaining, groundingAt, tickAt;
        // The paper, the grounding break and the hand-in prompt are one UI Toolkit file (ExamPaper.uxml).
        UIE.VisualElement examRoot, examScreen, groundingScreen, confirmScreen;
        UIE.Label clock, thought, question, progress, groundingText, groundingCount, confirmationText;
        UIE.Button[] questionButtons, answerButtons;
        UIE.Button flagButton, groundingButton;
        AudioClip tick, page, bell;

        int Answered => answers.Count(a => a >= 0);
        protected override bool OverlayOpen => examOpen;
        protected override string RoamHint => !admitted ? "Follow the blue markers across the yard to Room 03. Hold Shift to run." : "Find the empty seat with the gold marker.";

        protected override IEnumerator Play()
        {
            remaining = examinationSeconds;
            tick = Tone("Classroom clock", 1100, .045f); page = Tone("Paper", 180, .16f); bell = Tone("School bell", 660, 1.3f);
            BuildExam();
            // Room 03's door opens with its own [ E ]; stepping inside is where the teacher speaks to you.
            AddArrival(landing != null ? landing : entrance, "Enter Room 03", () => !admitted, Admission, 2.2f);
            AddSpot(seat, "Sit down and begin", () => admitted && !seated, Sit);

            if (routeMarkers != null) routeMarkers.SetActive(false);
            if (openingView != null) Shot(openingView.position, openingView.position + openingView.forward * 10);
            player.gameObject.SetActive(false);
            yield return Card(TitleCard, "Begin  [Space]");
            if (bus != null)
            {
                Vector3 stopAt = bus.position, from = stopAt + Vector3.left * 45;
                bus.gameObject.SetActive(true);
                for (float t = 0; t < 3.5f; t += Time.deltaTime) { bus.position = Vector3.Lerp(from, stopAt, Mathf.SmoothStep(0, 1, t / 3.5f)); yield return null; }
                bus.position = stopAt;
            }
            yield return Fade(0, 1, .3f);
            player.gameObject.SetActive(true);
            Teleport(spawn.position, spawn.eulerAngles.y);
            Shot(spawn.position + spawn.right * 5 - spawn.forward * .5f + Vector3.up * 3, spawn.position + spawn.forward * 8 + Vector3.up * 1.5f);
            yield return Fade(1, 0, .3f);
            if (bus != null) StartCoroutine(DriveOff());
            yield return Me("I'm here. I can't get those minutes back.\nI just need to reach my classroom.");
            AddStress(15);
            if (routeMarkers != null) routeMarkers.SetActive(true);

            SetObjective("Reach Room 03", Check(false, "Across the yard, along the corridor  ·  hold Shift to run"));
            yield return RoamUntil(() => admitted);
            SetObjective("Find your seat", Check(true, "Room 03") + Check(false, "The empty seat with the gold marker"));
            yield return RoamUntil(() => seated);

            yield return TakeExamination();

            yield return DialogueSequence(
                Say("Teacher", "Thank you, " + PlayerName + ". You can leave your paper here."),
                Think("I did what I could with the time I had.\nI don't know how it will turn out yet."),
                Think("But the thought follows me out of the room.\nWhat will my family say if it isn't enough?"));
            AddStress(8);
            Record("family.exam_answered", Answered.ToString());
            Record("family.exam_grounding", grounded ? "taken" : "skipped");
            yield return Fade(0, 1, .7f);
            Complete("I used the time I had.",
                "You arrived, found your place, and handed in your examination.\n\n" + Answered + " of 5 questions answered. " +
                (grounded ? "You took a moment to steady yourself." : "You kept going at your own pace.") + "\n\nYour grade is still unknown.");
        }

        IEnumerator DriveOff()
        {
            for (float t = 0; t < 4; t += Time.deltaTime) { bus.position += Vector3.right * Time.deltaTime * (2 + t * 4); yield return null; }
            bus.gameObject.SetActive(false);
        }

        IEnumerator Admission()
        {
            if (routeMarkers != null) routeMarkers.SetActive(false);
            if (teacher != null)
            {
                Vector3 toTeacher = Vector3.ProjectOnPlane(teacher.position - player.position, Vector3.up).normalized;
                Shot(player.position - toTeacher * 2.6f + Vector3.up * 4.4f, teacher.position);
            }
            yield return DialogueSequence(
                Me("I'm sorry I'm late. May I still take the examination?"),
                Say("Teacher", "Come in quietly, " + PlayerName + ". Take the empty seat.\nStart with what you know and use the time that remains."),
                Think("It feels like everyone noticed. They're looking at their papers now.\nOne step. Find my seat."));
            AddStress(10);
            admitted = true;
        }

        IEnumerator Sit()
        {
            yield return Fade(0, 1, .3f);
            player.gameObject.SetActive(false);
            Shot(seatView.position, seatView.position + seatView.forward * 6);
            yield return Fade(1, 0, .4f);
            yield return Think("The words look familiar, but my thoughts keep racing.\nI'll begin with one question I understand.");
            seated = true;
        }

        // ---------------------------------------------------------------- the examination paper

        IEnumerator TakeExamination()
        {
            SetLocked(true); HidePanels();
            // A loaded save hands the paper in with the answers kept when it was first handed in.
            if (ReplayNext('x', out var kept))
            {
                var parts = kept.Split(',');
                for (int i = 0; i < answers.Length && i < parts.Length - 1; i++) answers[i] = int.Parse(parts[i]);
                grounded = parts[parts.Length - 1] == "1";
                yield break;
            }
            examOpen = true; Show(examRoot, true); DrawExam();
            while (!submitted) yield return null;
            Show(examRoot, false); examOpen = false;
            effects.PlayOneShot(page, .15f);
            Log('x', string.Join(",", answers) + "," + (grounded ? "1" : "0"));
        }

        protected override void Update()
        {
            base.Update();
            if (!examOpen || IsPaused) return;
            var k = Keyboard.current;
            if (groundingOpen)
            {
                groundingButton.SetEnabled(Time.time - groundingAt >= .75f);
                if (k != null && k.spaceKey.wasPressedThisFrame) Ground();
                return;
            }
            if (confirmOpen) { if (k != null && k.escapeKey.wasPressedThisFrame) CancelSubmit(); return; }
            if (!timeUp)
            {
                remaining = Mathf.Max(0, remaining - Time.deltaTime);
                if (remaining < 60 && !grounded) AddStress(Time.deltaTime * .3f);
                if (remaining <= 0) { timeUp = true; effects.PlayOneShot(bell, .12f); }
            }
            if (Time.time > tickAt) { effects.PlayOneShot(tick, .035f); tickAt = Time.time + 1; }
            DrawClock();
            if (k == null) return;
            if (k.digit1Key.wasPressedThisFrame) Answer(0);
            if (k.digit2Key.wasPressedThisFrame) Answer(1);
            if (k.digit3Key.wasPressedThisFrame) Answer(2);
            if (k.leftArrowKey.wasPressedThisFrame) Select((selected + 4) % 5);
            if (k.rightArrowKey.wasPressedThisFrame) Select((selected + 1) % 5);
            if (k.bKey.wasPressedThisFrame) BeginGrounding();
        }

        void Select(int index) { if (groundingOpen || confirmOpen) return; selected = index; effects.PlayOneShot(page, .06f); DrawExam(); }

        void Answer(int option)
        {
            if (groundingOpen || confirmOpen) return;
            answers[selected] = option; DrawExam();
            if (Answered >= 2 && !grounded) BeginGrounding();
        }

        void Flag() { if (groundingOpen || confirmOpen) return; flagged[selected] = !flagged[selected]; DrawExam(); }

        void BeginGrounding()
        {
            if (groundingOpen || confirmOpen) return;
            groundingOpen = true; groundingStep = 0; groundingAt = Time.time;
            examScreen.style.opacity = .25f; examScreen.SetEnabled(false);
            Show(groundingScreen, true); DrawGrounding();
        }

        void Ground()
        {
            if (!groundingOpen || Time.time - groundingAt < .75f) return;
            groundingStep++; groundingAt = Time.time;
            EaseStress(5);
            if (groundingStep < GroundingWords.Length) { DrawGrounding(); return; }
            grounded = true; groundingOpen = false;
            Show(groundingScreen, false); examScreen.style.opacity = 1; examScreen.SetEnabled(true); DrawExam();
        }

        void RequestSubmit()
        {
            if (groundingOpen) return;
            confirmOpen = true; examScreen.SetEnabled(false); Show(confirmScreen, true);
            confirmationText.text = Answered + " of 5 questions answered.\nYou may hand in your work as it is, or return to the paper.";
        }

        void CancelSubmit() { confirmOpen = false; Show(confirmScreen, false); examScreen.SetEnabled(true); }
        void Submit() { if (!confirmOpen) return; confirmOpen = false; Show(confirmScreen, false); submitted = true; }

        static void Show(UIE.VisualElement element, bool on) => element.EnableInClassList("hidden", !on);

        void DrawGrounding()
        {
            groundingCount.text = "5-4-3-2-1 GROUNDING  /  " + GroundingSense[groundingStep] + "  /  STEP " + (groundingStep + 1) + " OF " + GroundingWords.Length;
            groundingText.text = GroundingWords[groundingStep] + (groundingStep == GroundingWords.Length - 1 ? "\n\nLet your next breath come naturally.\nYou can return to one question at a time." : "");
            groundingButton.text = GroundingActions[groundingStep] + "  [Space]";
            groundingButton.SetEnabled(false);
        }

        void DrawClock()
        {
            int s = Mathf.CeilToInt(remaining);
            clock.text = timeUp ? "TIME'S UP  /  WRAP UP" : (s / 60).ToString("00") + ":" + (s % 60).ToString("00") + "  REMAINING";
            thought.text = timeUp ? "The bell has rung. Finish your thought, then hand in your paper." :
                grounded ? "I'm here, in this room. I can return to the next question." : "What if I can't finish?  ·  Press B for a 5-4-3-2-1 grounding break.";
        }

        void DrawExam()
        {
            DrawClock();
            question.text = Questions[selected];
            progress.text = "QUESTION " + (selected + 1).ToString("00") + "   /   " + Answered + " OF 5 ANSWERED";
            for (int i = 0; i < 5; i++)
            {
                questionButtons[i].EnableInClassList("selected", i == selected);
                questionButtons[i].text = (i + 1).ToString("00") + (answers[i] >= 0 ? "  DONE" : "  —") + (flagged[i] ? "  •" : "");
            }
            for (int i = 0; i < 3; i++)
            {
                answerButtons[i].EnableInClassList("selected", answers[selected] == i);
                answerButtons[i].text = (i + 1) + "   " + Options[selected][i] + (answers[selected] == i ? "  [selected]" : "");
            }
            flagButton.text = flagged[selected] ? "Remove review mark" : "Mark for review";
        }

        void BuildExam()
        {
            examRoot = Overlay(EpisodeUI.Load().examPaper, "examination").Q("exam-root");
            examScreen = examRoot.Q("exam-screen"); groundingScreen = examRoot.Q("grounding-screen"); confirmScreen = examRoot.Q("confirm-screen");
            clock = examRoot.Q<UIE.Label>("exam-clock"); thought = examRoot.Q<UIE.Label>("exam-thought");
            progress = examRoot.Q<UIE.Label>("exam-progress"); question = examRoot.Q<UIE.Label>("exam-question");
            questionButtons = new UIE.Button[5]; answerButtons = new UIE.Button[3];
            for (int i = 0; i < 5; i++) { int q = i; questionButtons[i] = examRoot.Q<UIE.Button>("exam-q-" + i); questionButtons[i].clicked += () => Select(q); }
            for (int i = 0; i < 3; i++) { int option = i; answerButtons[i] = examRoot.Q<UIE.Button>("exam-a-" + i); answerButtons[i].clicked += () => Answer(option); }
            flagButton = examRoot.Q<UIE.Button>("exam-flag"); flagButton.clicked += Flag;
            examRoot.Q<UIE.Button>("exam-ground").clicked += BeginGrounding;
            examRoot.Q<UIE.Button>("exam-submit").clicked += RequestSubmit;
            groundingCount = examRoot.Q<UIE.Label>("ground-count"); groundingText = examRoot.Q<UIE.Label>("ground-text");
            groundingButton = examRoot.Q<UIE.Button>("ground-button"); groundingButton.clicked += Ground;
            confirmationText = examRoot.Q<UIE.Label>("confirm-text");
            examRoot.Q<UIE.Button>("confirm-back").clicked += CancelSubmit;
            examRoot.Q<UIE.Button>("confirm-ok").clicked += Submit;
            // Space and Enter belong to the story and the grounding break; a focused button would also "click" on them.
            examRoot.Query<UIE.Button>().ForEach(b => b.focusable = false);
            Show(examRoot, false); Show(groundingScreen, false); Show(confirmScreen, false);
        }
    }
}
