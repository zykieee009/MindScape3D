# Chapter 1, Episode 1 - The Paper on the Desk

Open `Assets/Scenes/Chapter 1 - Episode 1.unity` and enter Play mode, or launch Episode 1 from the main menu. The existing cutscene scene forwards to this episode's in-engine cinematics.

## Play

- Space / Enter or Continue: advance subtitles.
- Skip opening: jump into bedroom exploration.
- WASD and mouse: move and look using the existing player controller.
- E: examine the nearby object in front of Joey.
- J: read the three collected thoughts. Esc: close the journal or pause.
- Examine the paper, bag and notebook in any order. Repeats never add duplicate progress.
- Return to the paper after all three memories to play the ending.
- The ending saves a completion flag, then offers replay or the main menu.

This is a desktop keyboard/mouse implementation. The old prototype's mobile overlay is inactive in this scene. There is no timer, stress score, grade or failure screen. Full voice acting is not included; the scene uses subtitles, generated clock ticks and an alarm.

## Content

The furnished house and character are reused. The new desk, notebook, examination paper, school bag, alarm clock, morning fragments and examination tableau are authored Unity meshes. Cutscenes use camera movement, fades, character arrival, object close-ups and lighting changes. No generated illustration is required.

The old morning/presentation quest, NPC/minigame systems and slide objects are preserved inactive in their scenes. The standalone minigame lab and tutorial remain separate. No examination grade is revealed.

`EpisodeOneStory` owns the episode states: Opening -> Exploring <-> Memory -> Ending -> Complete. Journal and pause lock movement and interaction. A three-bit mask makes collection order-independent. Only completion is persisted under `Mindscape.FamilyExpectations.Episode1`; replay starts a fresh session. The menu's episode list indicates completed Episode 1 as Replay.

Episode 2 is implemented in `Assets/Scenes/Chapter 1 - Episode 2.unity`. The ending's Continue button is connected to it, and the scene is included in build settings.

## Authoring and verification

`Mindscape > Episode 1 > Build Family Expectations Gameplay` rebuilds the generated content in the existing episode and cutscene scenes. Save unrelated scene work before running it. It preserves the older scene objects inactive and replaces only its own named root. Custom edits to that root should be incorporated into `FamilyEpisodeOneBuilder` before rebuilding.

In a freshly started Episode 1 Play session, run `Mindscape > Episode 1 > Verify Gameplay (Play Mode)`. The integration check covers invalid/range-blocked actions, journal/pause control locking, out-of-order memory collection, repeat interactions, ending prerequisites, saved completion, replay, menu return and cutscene entry. It restores the previous completion flag and writes `Temp/EpisodeOneVerification.txt`.
