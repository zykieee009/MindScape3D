# Chapter 3 - Social Dynamics

Eight episodes adapt the eight scenes in `Chapter 3 - Social Dynamics.pdf`:

1. The Invitation: examine the phone, photograph, and desk in any order; choose a reply to Justin.
2. Arriving at the Reunion: meet Justin and join the old classmates.
3. Everyone Seems to Be Moving Forward: listen to both conversations in either order; choose how much to share.
4. The Corner of the Room: find space near the window and check the phone.
5. A Friend Checks In: Justin shares his own uncertainty and offers support.
6. Choosing to Rejoin the Group: rejoin friends, save an appreciation to the reflection journal, and join the photograph.
7. The Walk Home: follow streetlights, revisit the photo, greet family, and return to the bedroom.
8. The Mirror: approach the live mirror and finish the self-acceptance sequence.

The main menu exposes all eight episodes, Continue restarts the latest episode, and the journal includes **One Thing About Me**. Chapter 2 Episode 5 can continue into Chapter 3. Controls use the existing movement/look bindings, E to interact, Space to advance, number keys or buttons for choices, and Esc to pause.

`Mindscape/Chapter 3 - Social Dynamics/Build All Eight Episodes` generates scenes, preview textures, and build-profile entries. Save the currently open scene first. Rebuilding replaces generated Chapter 3 scenes; move custom scene edits elsewhere before running it again. Earlier chapter source scenes are opened as templates and never saved by this builder.

`Mindscape/Chapter 3 - Social Dynamics/Verify All Episodes (Play Mode)` exercises all eight story coroutines, alternate exploration orders, both invitation/check-in choices, all three appreciation choices, interaction proximity, pause, journal persistence, and completion. It temporarily suppresses database writes and restores the touched PlayerPrefs. Results go to `Temp/SocialDynamicsVerification.txt`. This checks story progression; it is not a full manual navigation or exported WebGL/mobile test.

The reunion and walk-home environments are prototype staging. Some PDF direction is conveyed through dialogue and camera cuts rather than bespoke character animation. The mirror uses a 256 x 512 live camera view with the mirror surface excluded to avoid recursion.
