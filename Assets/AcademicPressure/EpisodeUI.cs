using UnityEngine;
using UnityEngine.UIElements;

namespace Mindscape.AcademicPressure
{
    // Points the story episodes (and the NPC conversation panel) at their UI Toolkit files, which live in
    // Assets/UI Toolkit. Both are built at runtime with nothing to drag a file onto, so they load this one asset
    // from Resources/EpisodeUI.
    [CreateAssetMenu(menuName = "Mindscape/Episode UI", fileName = "EpisodeUI")]
    public class EpisodeUI : ScriptableObject
    {
        public PanelSettings panelSettings;
        public VisualTreeAsset objective;
        public VisualTreeAsset status;
        public VisualTreeAsset dialogue;
        public VisualTreeAsset choice;
        public VisualTreeAsset storyCard;
        public VisualTreeAsset complete;
        public VisualTreeAsset pauseMenu;
        public VisualTreeAsset npcDialogue; // used by NPCInteractionManager in every scene with talkable NPCs
        public VisualTreeAsset interactPrompt; // [ E ] interact (episodes) and [ E ] talk (NPCInteractionManager); one shows at a time (InteractFocus)
        public VisualTreeAsset flashback; // frame + "FLASHBACK" badge for flashback episodes and memory moments
        public VisualTreeAsset phoneChat; // a text conversation on a phone (OpenPhone / PhoneMessage)
        public VisualTreeAsset photograph; // a photograph held up close (ShowPhoto)
        public VisualTreeAsset textEntry; // typed answers: the player's name, the reflection journal (Ask)
        public VisualTreeAsset screenFade; // the full-screen black the story fades through (AcademicEpisode.Fade)
        public VisualTreeAsset examPaper; // Chapter 2 · Episode 3: the paper, the grounding break, the hand-in prompt
        public VisualTreeAsset thoughtJournal; // Chapter 2 · Episode 1: the thought journal and its button
        public VisualTreeAsset laptopTasks; // Chapter 1 · Episode 4: the laptop's task list
        public VisualTreeAsset breathing; // the guided breathing exercise (BreathingMinigame)
        public VisualTreeAsset worryCourtroom; // the Worry Courtroom minigame (WorryCourtroomMinigame)
        public VisualTreeAsset crosshair; // the dot in the middle of the view (PlayerMovement)

        public static EpisodeUI Load() => Resources.Load<EpisodeUI>("EpisodeUI");

        // A UI Toolkit document made from one of the files above, for code that is not an episode (minigames, doors,
        // the player). Starts with picking switched off on the root so clicks pass through to whatever is behind it;
        // the screens inside pick for themselves. Returns null when the asset or the file is missing.
        public static UIDocument Spawn(string name, VisualTreeAsset tree, int sortingOrder, Transform parent = null)
        {
            var ui = Load();
            if (ui == null || ui.panelSettings == null || tree == null) return null;
            var go = new GameObject(name);
            go.SetActive(false); // set everything up before UIDocument runs OnEnable
            if (parent != null) go.transform.SetParent(parent, false);
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = ui.panelSettings;
            document.visualTreeAsset = tree;
            document.sortingOrder = sortingOrder;
            go.SetActive(true);
            document.rootVisualElement.pickingMode = PickingMode.Ignore;
            return document;
        }
    }
}
