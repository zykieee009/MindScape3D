# Chapter 1 · Episode 2 — Before the Bell

Open `Assets/Scenes/Chapter 1 - Episode 2.unity` and press Play, or select Episode 2 from the main menu. Episode 1's Continue button also leads here.

## Playable sequence

1. Morning alarm cinematic. Space/Continue advances the dialogue; Skip opening goes directly to control.
2. Approach the bedside clock and press E. The five-minute preparation timer begins.
3. Collect the student ID beside the bed, examination materials on the desk, and school bag in the living room, in any order. H gives a hint for the next missing item.
4. Bring all three to the front door. E starts the departure transition.
5. Walk through the yard and follow the gold sidewalk markers to the neighborhood bus stop. P opens the phone.
6. E at the stop, then E again to check the time: traffic dialogue, a late-night studying flashback, and the bus arrival cinematic.
7. Approach the marked boarding position and press E. Departure leads to the saved completion screen and Episode 3 teaser.

WASD moves; mouse looks; Shift runs; E interacts; H hints; P checks/puts away the phone; Escape pauses; Space advances dialogue. Timer expiry changes the objective to “Running late” but never resets progress. Pause freezes the clock. The phone locks movement. Interactions require proximity and the appropriate story phase.

## Resources

The existing house, Joey character, furniture and animation controller are reused. The builder creates original low-poly neighborhood geometry, a bus shelter, commuter bus, student ID, examination kit, bag, phone, route markers and material palette. Reusable objects are saved in `Assets/EpisodeTwo/Prefabs`. Alarm, clock ticks, footsteps, pickup and engine sounds are synthesized at runtime; no downloaded assets or paid dependencies are needed. Cutscenes use the 3D scene, camera choreography, fades and subtitles (not recorded voice acting).

`Assets/Resources/EpisodeTwo/BusStopPreview.png` is an in-engine scene capture used by the menu and loading screen.

## Authoring and checks

- `FamilyEpisodeTwoBuilder.Build()` / Mindscape → Episode 2 → Build Before the Bell regenerates the authored Episode 2 scene from Episode 1. Save current scene edits first. Regeneration replaces Episode 2 scene authoring; preserve custom changes before rebuilding.
- `FamilyEpisodeTwoVerification.Run()` in Play mode runs the functional flow and real CharacterController yard/sidewalk traversal. Results are written to `Temp/EpisodeTwoVerification.txt`. It restores the save values present when the test begins.
- Completion uses `Mindscape.FamilyExpectations.Episode2`; the existing menu save convention uses episode ordinal 3 for this story episode. Continue restarts the episode, not a mid-scene checkpoint.
- The completion screen continues to Episode 3, “Running Out of Time,” for the late examination arrival.

Desktop keyboard/mouse prototype. Dedicated touch controls, recorded performances and production sound are not included.
