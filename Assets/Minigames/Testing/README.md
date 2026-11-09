# Minigame Test Lab

Open `Assets/Scenes/MinigameTestLab.unity`, press Play, and select any activity.
Use Replay to reset it and Close to return to the launcher. Completion badges last
for the current Play session. Click/tap input works with the project's Input System.

Each of the ten children under **Minigame Test Lab** is a separate prefab instance
with its own minigame MonoBehaviour. Reuse prefabs from `Assets/Prefabs/Minigames`
in other scenes, or add components under **Mindscape / Minigames**. Call `Open()`
from a UnityEvent or script. `Replay()`, `Close()`, `IsOpen`, `IsComplete`,
`onCompleted`, and `onClosed` are shared by every `MinigamePopup`.
The components generate their own uGUI popup on demand and ensure an EventSystem
exists. Only one lab popup is open at once. Closing or disabling cleans up UI and
coroutines and restores the previous cursor state. Integrators should connect
player movement / quest progression explicitly through their own interaction code.
The existing quest-linked `BreathingMinigame` is unchanged.

Breathing uses a click once per inhale/hold/exhale phase (two cycles by default).
Missed phases require Replay. Focus alternates study notes and distractions;
clicking distractions subtracts a point. Memory matches four semantic pairs.
Grounding is a schematic illustrated room with five sense rounds. Schedule uses
select-card / select-slot placement, with five slots and no duplicate activities.
Emotion choices use contextual examples rather than judgments about real people.
Stress numbers are game values, not a clinical measure.

Editor menus:
- **Mindscape > Minigame Lab > Create or Open Test Scene** creates the scene and
  prefabs if missing. Existing prefabs and scenes are not overwritten.
- **Mindscape > Minigame Lab > Verify All Games (Play Mode)** runs interaction,
  completion-event, reset, close, and popup-exclusivity checks. Results are written
  to `Temp/MinigameLabVerification.txt` and the Unity Console.

The lab does not alter build settings or existing story scenes.
