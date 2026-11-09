# Chapter 1 · Episode 3 — Running Out of Time

Open `Assets/Scenes/Chapter 1 - Episode 3.unity`, select Episode 3 in the main menu, or continue from Episode 2.

## Play

1. Advance or skip the school-arrival opening.
2. Follow the corridor to Room 03. Press E near the doorway to speak to the teacher.
3. Enter the classroom and find the gold marker over the empty chair. Press E to sit.
4. Answer five short general-studies questions in any order. Choose with the mouse or keys 1–3; use the question buttons or arrow keys to move between questions. Answers can be revised and questions marked for review.
5. After two questions, a three-step grounding moment interrupts Joey's anxious thoughts. Read each prompt and click or press Space. B also opens grounding during the examination.
6. Hand in the paper, confirm submission, and advance the closing conversation. Incomplete papers are accepted. No grade is revealed.

WASD moves, mouse looks, E interacts, Space/Enter advances dialogue, Esc pauses. The three-minute clock pauses during grounding, submission confirmation, and the pause menu. Expiry rings the bell and invites the player to wrap up without resetting progress or forcing submission. This is a narrative prototype, not an academic scoring system. There is no hard failure.

## Assets and authoring

The builder reuses the furnished classroom GLB, Episode 2 player, family character model for the teacher, commuter bus, school bag and examination kit. It authors a covered corridor, seated low-poly classmates, school signs, clock and material palette. The examination and grounding interfaces are native uGUI/TMP. Clock, bell and paper sounds are synthesized. Dialogue uses subtitles; recorded voice acting and dedicated touch movement are not included.

`Mindscape > Episode 3 > Build Running Out of Time` regenerates the Episode 3 scene and preview. Save open scene edits first. Preserve edits to this generated scene in `FamilyEpisodeThreeBuilder` before rebuilding. Other story scenes are read, not regenerated. The builder adds Episode 3 to global and custom build-profile scene lists.

`Mindscape > Episode 3 > Verify Gameplay (Play Mode)` exercises the actual corridor route, interaction guards, answers, pause/grounding clock behavior, early submission, completion, replay, menu and Episode 2 continuation. Results are saved to `Temp/EpisodeThreeVerification.txt`; prior save values are restored.

Completion: `Mindscape.FamilyExpectations.Episode3`. Menu save ordinal: 4 (tutorial = 1). Continue restarts the episode. The menu and loading image use `Resources/EpisodeThree/ClassroomPreview.png`, captured from the authored scene. Episode 4 remains unavailable.
