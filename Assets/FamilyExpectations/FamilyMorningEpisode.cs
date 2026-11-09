using System.Collections;
using UnityEngine;

namespace Mindscape.FamilyExpectations
{
    // Episode 2 · Before the Bell. Examination morning: turn off the alarm, find the student ID, the examination
    // materials and the school bag before the getting-ready clock runs down (running late is allowed), walk to the
    // bus stop, remember the night before while the traffic crawls, and catch the bus.
    public sealed class FamilyMorningEpisode : FamilyEpisode
    {
        public Transform alarmClock, frontDoor, busStop, boardingPoint, bus, studyView;
        public Transform[] items = new Transform[3];   // Student ID, Examination materials, School bag
        public GameObject carriedBag, routeMarkers, messyBed, fixedBed;
        public Light sun, deskLamp;
        public Vector3 outsideSpawn = new Vector3(-22.1f, .7f, 8.2f);
        public float outsideYaw;
        [Min(30)] public float preparationSeconds = 300;

        public override int Episode => 2;
        protected override float TimeLimit => 600;
        // Episodes 2-3 are the memory Episode 1's examination paper leads back to.
        protected override string FlashbackLabel => "Examination morning";

        static readonly string[] Names = { "Student ID", "Examination materials", "School bag" };
        static readonly string[] Places = { "beside the bed, by the alarm clock", "on the study desk", "in the living room, by the front door" };

        int packed;
        bool alarmOff, left, atStop, boarded, late;
        float getReady;
        AudioClip alarm;

        bool Packed(int i) => (packed & (1 << i)) != 0;
        bool AllPacked => packed == 7;

        protected override string RoamHint => !alarmOff ? "The alarm clock is beside the bed." :
            !AllPacked ? "Find your student ID, your examination materials, and your school bag." :
            !left ? "The front door is in the living room. Open it and step outside." : !atStop ? "Follow the blue markers to the bus stop." : "Board the bus at its open door.";

        protected override IEnumerator Play()
        {
            getReady = preparationSeconds;
            alarm = Tone("Morning alarm", 780, 1.4f, true);
            if (messyBed != null) messyBed.SetActive(true);
            if (fixedBed != null) fixedBed.SetActive(false);
            if (carriedBag != null) carriedBag.SetActive(false);
            if (routeMarkers != null) routeMarkers.SetActive(false);
            if (bus != null) bus.gameObject.SetActive(false);

            AddSpot(alarmClock, "Turn off the alarm", () => !alarmOff, SilenceAlarm);
            for (int i = 0; i < items.Length; i++)
            {
                int index = i;
                AddSpot(items[i], "Take your " + Names[i].ToLowerInvariant().Replace("id", "ID"), () => alarmOff && !Packed(index), () => Collect(index));
            }
            // The front door opens with its own [ E ]; stepping out onto the path is what leaves the house.
            AddArrival(Waypoint("Outside the front door", outsideSpawn), "Leave for school", () => AllPacked && !left, LeaveHouse);
            AddSpot(busStop, "Wait at the bus stop", () => left && !atStop, WaitAtStop, 4.5f);
            AddSpot(boardingPoint, "Board the bus", () => atStop && !boarded, Board, 4.5f);

            Shot(alarmClock.position + new Vector3(-2.8f, 2.2f, 2.4f), alarmClock.position);
            effects.clip = alarm; effects.loop = true; effects.volume = .14f; effects.Play();
            yield return Card(TitleCard, "Begin  [Space]");
            yield return DialogueSequence(
                Say("Examination morning", "An alarm cuts through the quiet."),
                Me("Wait... what time is it?\nOh no. I'm going to be late for the examination!"));
            AddStress(15);

            SetObjective("Turn off the alarm", Check(false, "Alarm clock beside the bed  ·  07:50"));
            yield return RoamUntil(() => alarmOff);
            yield return RoamUntil(() =>
            {
                SetObjective(AllPacked ? "Leave through the front door" : "Find your examination things", Preparation());
                return left;
            });
            SetObjective("Walk to the bus stop", Check(true, "Packed") + Check(false, "Follow the blue markers"));
            yield return RoamUntil(() => atStop);
            SetObjective("Board the bus", Check(true, "Bus stop") + Check(false, "The open bus door"));
            yield return RoamUntil(() => boarded);

            Complete("Before the bell",
                "You found your belongings and caught the bus.\nBecause of the delay, you reached school after the examination had begun." +
                (late ? "\n\nGetting ready took longer than planned, and you kept going anyway." : ""));
        }

        // The getting-ready clock runs only while you are searching. Running out of it is allowed: it only means
        // you are running late (the episode's own time limit is separate).
        protected override void Update()
        {
            base.Update();
            if (!alarmOff || left || late || !IsRoaming || IsPaused) return;
            getReady = Mathf.Max(0, getReady - Time.deltaTime);
            if (getReady <= 0) { late = true; AddStress(10); }
        }

        string Preparation()
        {
            int s = Mathf.CeilToInt(getReady);
            string clock = late ? "<color=#E89678>Running late  ·  keep going</color>\n" : "<color=#DBB36F>" + (s / 60).ToString("00") + ":" + (s % 60).ToString("00") + "  to get ready</color>\n";
            string list = clock;
            for (int i = 0; i < 3; i++) list += Check(Packed(i), Names[i] + (Packed(i) ? "" : "  ·  " + Places[i]));
            if (AllPacked) list += Check(false, "Front door, through the living room");
            return list;
        }

        IEnumerator SilenceAlarm()
        {
            effects.Stop(); effects.loop = false;
            ShotOver(alarmClock, 1.6f, 1.2f);
            yield return Think("My ID, my examination materials, and my bag. Where did I put everything?");
            alarmOff = true;
        }

        IEnumerator Collect(int i)
        {
            // The close-up shows the item while the thought is on screen; it is taken (hidden) once the player continues.
            ShotOver(items[i], 1.8f, 1.3f);
            effects.PlayOneShot(chime, .12f);
            packed |= 1 << i;
            int count = (packed & 1) + ((packed >> 1) & 1) + ((packed >> 2) & 1);
            yield return Think(AllPacked ? "Everything is packed. I need to leave now." : Names[i] + ". " + count + " of 3 ready.");
            items[i].gameObject.SetActive(false);
            if (i == 2 && carriedBag != null) carriedBag.SetActive(true);
            if (AllPacked) EaseStress(5);
        }

        IEnumerator LeaveHouse()
        {
            if (routeMarkers != null) routeMarkers.SetActive(true);
            yield return Me(late ? "I'm later than I planned. But I have everything. I need to keep going." : "Okay, I have everything. I just need to get to school!");
            left = true;
        }

        IEnumerator WaitAtStop()
        {
            if (routeMarkers != null) routeMarkers.SetActive(false);
            Shot(busStop.position + new Vector3(-6, 3.5f, -6), busStop.position + Vector3.up);
            yield return DialogueSequence(
                Think("No bus yet. The traffic is barely moving.", () =>
                {
                    OpenPhoneLock("8:04", "");
                    PhoneNotification("Calendar", "Examination", "The examination started at 08:00.");
                }),
                BeforeDialogue(new WaitForSeconds(.6f),
                    Me("Please, not today. I have an examination.\nThe traffic isn't moving. I can't make the bus arrive sooner.")));
            ClosePhone();
            AddStress(15);
            yield return Think("I can't make the bus arrive sooner. I can give myself one moment to breathe while I wait.");
            bool busBreathCompleted = false;
            yield return Breathe(done => busBreathCompleted = done);
            Record("family.bus_stop_breathing", busBreathCompleted ? "completed" : "closed_early");
            yield return Think("The traffic is still slow. I can meet the next moment when it comes.");

            // The night before.
            float sunWas = sun != null ? sun.intensity : 1, lampWas = deskLamp != null ? deskLamp.intensity : 1;
            yield return Fade(0, 1, .5f);
            if (sun != null) sun.intensity = .08f;
            if (deskLamp != null) deskLamp.intensity = 4;
            player.gameObject.SetActive(false);
            Shot(studyView.position, studyView.position + studyView.forward * 3);
            ShowFlashback("The night before");
            yield return Fade(1, 0, .5f);
            yield return DialogueSequence(
                Say("The night before", "One more page. One more lesson.\nI thought studying longer would make me feel prepared."),
                Say(PlayerName + "  /  remembering", "I should have slept earlier. I didn't realize how tired I would be this morning."));
            yield return Fade(0, 1, .5f);
            EndFlashback();
            if (sun != null) sun.intensity = sunWas;
            if (deskLamp != null) deskLamp.intensity = lampWas;
            player.gameObject.SetActive(true);

            // The bus pulls in.
            if (bus != null)
            {
                Vector3 stopAt = bus.position, from = stopAt + Vector3.left * 30;
                bus.position = from; bus.gameObject.SetActive(true);
                Shot(busStop.position + new Vector3(-5, 4, -4), stopAt + Vector3.up * 1.5f);
                yield return Fade(1, 0, .5f);
                for (float t = 0; t < 3; t += Time.deltaTime) { bus.position = Vector3.Lerp(from, stopAt, Mathf.SmoothStep(0, 1, t / 3)); yield return null; }
                bus.position = stopAt;
            }
            else yield return Fade(1, 0, .5f);
            yield return Me("It's here. I can still make it. I just need to get there.");
            EaseStress(8);
            atStop = true;
        }

        IEnumerator Board()
        {
            yield return Fade(0, 1, .4f);
            player.gameObject.SetActive(false);
            Vector3 at = bus != null ? bus.position : busStop.position;
            Shot(at + new Vector3(4, 6, -14), at + Vector3.up * 1.5f);
            yield return Fade(1, 0, .4f);
            yield return Think("The examination has already started.\nWhen I arrive, I'll have to begin with the time I have left.");
            if (bus != null)
                for (float t = 0; t < 4; t += Time.deltaTime) { bus.position += Vector3.right * Time.deltaTime * (2 + t * 3); yield return null; }
            yield return Fade(0, 1, 1);
            boarded = true;
        }
    }
}
