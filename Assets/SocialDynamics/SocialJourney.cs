using System.Collections;
using Mindscape.AcademicPressure;
using UnityEngine;

namespace Mindscape.SocialDynamics
{
    // Episode 1 ends by getting ready and going: change in the bathroom, out of the front door, down to the bus stop,
    // and the bus to the reunion. Episode 5's talk with Justin happens out on the hall's terrace, past the window.
    public sealed partial class SocialDynamicsEpisode
    {
        // ================================================================ Episode 1 · getting ready, the bus

        IEnumerator GetReadyAndGo()
        {
            bool dressed = false, outside = false, atStop = false;
            var bath = AddBathroom("Get changed for the reunion", () => !dressed, Outfit.GoingOut,
                "This shirt still fits. Okay. I can do this.", () => dressed = true);
            if (bath != null)
            {
                yield return Think("I should get changed before I go.");
                SetObjective("Getting ready", Check(false, "Change in the bathroom"));
                yield return RoamUntil(() => dressed);
            }
            else PlayerOutfit.Wear(player, Outfit.GoingOut);

            // Out of the front door (it opens with its own [ E ]).
            HomeWelcome.FrontDoor(spawn.position, out var door, out var inward);
            Vector3 step = Ground(door - inward * 2.6f, door.y) + Vector3.up * .1f;
            AddArrival(Waypoint("Outside the front door", step), "Head out", () => !outside, () => { outside = true; return Think("The reunion is across town. The bus will get me there."); }, 2f);
            SetObjective("Head out", Check(true, "Changed") + Check(false, "Out the front door"));
            yield return RoamUntil(() => outside);

            var ride = BusRide.Find();
            if (ride == null) { yield return Card("On the way to the reunion", "Continue  [Space]"); yield break; }
            Vector3 kerb = Ground(ride.kerb, step.y);
            AddArrival(Waypoint("At the bus stop", kerb + Vector3.up * .1f), "Walk to the bus stop", () => !atStop, () => { atStop = true; return Nothing(); }, 3f);
            SetObjective("To the bus stop", Check(true, "Out the front door") + Check(false, "The bus stop down the street"));
            yield return RoamUntil(() => atStop);

            // The bus pulls in at the shelter.
            SetLocked(true);
            Vector3 toKerb = Flat(kerb - ride.stop).normalized;
            Teleport(kerb + Vector3.up * .1f, Quaternion.LookRotation(-toKerb).eulerAngles.y);
            ride.Place(-70);
            Shot(kerb + toKerb * 6 - ride.heading * 7 + Vector3.up * 4, ride.stop + Vector3.up * 2);
            ShotFocus.Clear();
            yield return ride.Drive(-70, 0, 6f, true);
            yield return Think("Here it is. No turning back now.");
            // On board; the camera follows the bus down the road.
            yield return Fade(0, 1, .4f);
            player.gameObject.SetActive(false);
            yield return Fade(1, 0, .4f);
            bool gone = false;
            StartCoroutine(ride.Drive(0, 140, 9f, false, () =>
                Shot(ride.bus.position + toKerb * 19 + Vector3.up * 8.5f - ride.heading * 14, ride.bus.position + Vector3.up * 2f)));
            StartCoroutine(After(9f, () => gone = true));
            yield return Think("Everyone will be there. I hope I still remember how to talk to them.");
            while (!gone) yield return null;
            yield return Card("On the way to the reunion", "Continue  [Space]");
            player.gameObject.SetActive(true);
        }

        static IEnumerator After(float seconds, System.Action then) { yield return new WaitForSeconds(seconds); then(); }

        // ================================================================ Episode 5 · the terrace

        static readonly string[] TerraceLines = { "Justin|Hey. I was wondering where you went.", "Me|Sorry. I just needed some time alone.", "Justin|You don't have to apologize. I just wanted to check if you're okay.", "Me|Honestly, I'm not sure.", "Me|Everyone has so much to talk about. Their jobs, their plans, the things they've achieved.", "Me|I'm happy for them. I really am. But when I hear all those stories, I start thinking about my own life.", "Justin|What do you think about?", "Me|I think about the things I should have done by now. The things I could have continued. The opportunities I might have missed.", "Me|And then I start wondering if everyone else is going to notice that I'm not where I thought I'd be.", "Justin|Can I tell you something?", "Me|Sure.", "Justin|I used to feel that way, too.", "Justin|There was a time when I thought everyone around me was doing better. I'd go home wondering why I wasn't doing the same things.", "Me|You did?", "Justin|Yeah. I just didn't talk about it much.", "Justin|Sometimes I thought I had to prove I was doing well. I was afraid people would think I was wasting my time.", "Justin|I realized I was spending so much time trying to look successful that I wasn't enjoying the life I had.", "Me|So what did you do?", "Justin|I started paying attention to what mattered to me, instead of asking whether I was doing better than someone else.", "Justin|I'm still figuring things out. I don't have everything planned, either.", "Me|But you seem like you know what you're doing.", "Justin|That's what it might look like from the outside. But I still have days when I feel lost.", "Justin|You don't need to prove yourself to others. You just need to be yourself and accept who you are.", "Me|I don't know if I can do that so easily.", "Justin|You don't have to figure it out tonight.", "Justin|You don't have to become someone impressive just to deserve being here. We invited you because you're our friend. Not because we expected a list of achievements.", "Me|I guess I forgot that.", "Justin|Then let me remind you. You still matter, even when you don't feel proud of yourself.", "Me|Thank you for coming to check on me.", "Justin|Of course. That's what friends are for.", "Justin|Come on. You don't have to tell everyone your whole life story. Just come sit with us for a while." };

        // Justin finds you by the window and the two of you step out onto the terrace: the talk happens at the railing
        // over the evening city, the camera on whoever is speaking. Then back inside.
        IEnumerator TerraceTalk()
        {
            var justin = transform.Find("Justin");
            Vector3 window = points[4].position;
            Vector3 justinWas = justin != null ? justin.position : window; Quaternion justinTurn = justin != null ? justin.rotation : Quaternion.identity;
            Vector3 playerWas = player.position; float playerYaw = player.eulerAngles.y;

            // Justin walks up to the window first.
            if (justin != null)
            {
                ShotOver(points[4], 3.4f, 1.8f);
                yield return HomeWelcome.StepToward(justin, window + Flat(player.position - window).normalized * .2f + Vector3.Cross(Vector3.up, Flat(player.position - window).normalized) * 1.4f, 1.4f);
                yield return HomeWelcome.TurnTo(justin, player.position);
            }
            yield return Lines("Justin|Hey. It's a little loud in there. Want some air?", "Me|Yeah. That would be nice.");

            yield return Fade(0, 1, .7f);
            var terrace = SocialStaging.Terrace(transform, window);
            Vector3 outward = terrace.outward, along = Vector3.Cross(Vector3.up, outward);
            Vector3 me = terrace.rail - outward * .95f - along * .9f, him = terrace.rail - outward * .95f + along * .95f;
            Teleport(me, Quaternion.LookRotation(outward).eulerAngles.y);
            if (justin != null) HomeWelcome.Place(justin, him, him + outward * 4);
            float h = PlayerHeight();
            // From behind the two of them, the evening city ahead.
            Vector3 mid = (me + him) * .5f;
            Vector3 look = mid + Vector3.up * h * .55f;
            Shot(HomeWelcome.ClearView(look, mid - outward * 3.9f + Vector3.up * h * 1.1f + along * 1.2f), mid + outward * 6 + Vector3.up * h * .55f);
            ShotFocus.Clear();
            yield return Fade(1, 0, 1f);
            var drift = StartCoroutine(Glide(HomeWelcome.ClearView(look, mid - outward * 3.2f + Vector3.up * h * .85f - along * .8f), mid + outward * 6 + Vector3.up * h * .5f, 6f));
            yield return Think("The city lights go on for blocks. It's quieter out here.");
            StopCoroutine(drift);   // the conversation's own shots take over
            // They turn toward each other to talk.
            StartCoroutine(Turn(player, Quaternion.LookRotation(Flat(him - me) + outward * .8f), .6f));
            if (justin != null) yield return HomeWelcome.TurnTo(justin, me + outward * .6f, .6f);
            yield return Lines(TerraceLines);

            // Back inside, by the window.
            yield return Fade(0, 1, .7f);
            Destroy(terrace.gameObject);
            Teleport(playerWas, playerYaw);
            if (justin != null) { justin.SetPositionAndRotation(justinWas, justinTurn); HomeWelcome.Play(justin, "Idle"); }
            ShotOver(points[4]);
            yield return Fade(1, 0, .7f);
        }
    }
}
