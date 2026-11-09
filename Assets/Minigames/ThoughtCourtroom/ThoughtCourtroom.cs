using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Mindscape.Minigames
{
    [Serializable]
    public class ThoughtCase
    {
        [TextArea(1, 3)] public string thought = "Everyone is ahead of me, so I'm not good enough.";
        public string[] evidenceFor = { "I haven't finished my project.", "Other students seem prepared." };
        public string[] evidenceAgainst = { "I don't know how much they actually completed.", "I have completed parts of my project.", "Being behind does not mean I will fail." };
        public string[] balancedThoughts = { "Other students may be ahead, but I can still work on my project one step at a time." };
    }

    public sealed class ThoughtCourtroomResult
    {
        public readonly List<string> evidenceFor = new List<string>();
        public readonly List<string> evidenceAgainst = new List<string>();
        public string balancedThought = "";
        public bool wroteOwn;
    }

    // CBT thought record presented as a courtroom: the thought, evidence for, evidence against, a balanced
    // thought (choose one or write your own), then "Thought examined". Every answer is accepted; there are
    // no wrong picks, strikes or scores. The screen is built at runtime in the shared minigame theme.
    [AddComponentMenu("Mindscape/Minigames/Thought Courtroom")]
    public sealed class ThoughtCourtroom : MonoBehaviour
    {
        public enum Step { Closed, Thought, EvidenceFor, EvidenceAgainst, Balanced, Examined }

        public Step CurrentStep { get; private set; }
        public bool IsOpen => CurrentStep != Step.Closed;
        public ThoughtCourtroomResult Result { get; private set; }

        static readonly Color Ink = new Color32(48, 45, 40, 255), Cream = new Color32(240, 228, 204, 255), Parchment = new Color32(227, 212, 178, 255),
            Brass = new Color32(209, 168, 79, 255), Brown = new Color32(118, 92, 58, 255), Paper = new Color32(246, 238, 220, 255), Muted = new Color32(120, 104, 80, 255);

        ThoughtCase data;
        Action<ThoughtCourtroomResult> finished;
        GameObject canvasObject;
        RectTransform card, options;
        TMP_Text stepLabel, heading, lead, body, hint;
        Button primary;
        TMP_InputField ownThought;
        readonly List<Button> optionButtons = new List<Button>();
        readonly List<bool> selected = new List<bool>();
        int balancedPick = -1;

        public void Open(ThoughtCase thoughtCase, Action<ThoughtCourtroomResult> onFinished)
        {
            if (IsOpen) return;
            data = thoughtCase ?? new ThoughtCase(); finished = onFinished; Result = new ThoughtCourtroomResult();
            if (canvasObject == null) Build();
            canvasObject.SetActive(true);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            Show(Step.Thought);
        }

        void Update()
        {
            if (!IsOpen || ownThought != null && ownThought.isFocused) return;
            var k = Keyboard.current;
            if (k == null) return;
            if ((k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame) && primary.interactable) primary.onClick.Invoke();
            for (int i = 0; i < optionButtons.Count && i < 9; i++)
                if (k[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) optionButtons[i].onClick.Invoke();
        }

        void Show(Step step)
        {
            CurrentStep = step;
            ClearOptions();
            ownThought.gameObject.SetActive(false);
            body.text = ""; hint.text = "";
            switch (step)
            {
                case Step.Thought:
                    stepLabel.text = "THE THOUGHT ON TRIAL";
                    heading.text = "THOUGHT COURTROOM";
                    lead.text = "A thought has been brought to court. Let's look at it carefully, the way a judge would.";
                    body.text = "Thought:\n<size=120%>\"" + data.thought + "\"</size>";
                    SetPrimary("Examine this thought", true);
                    break;
                case Step.EvidenceFor:
                    stepLabel.text = "STEP 1 OF 3  ·  EVIDENCE FOR";
                    heading.text = "Evidence FOR";
                    lead.text = "What seems to support the thought? Choose any that apply.";
                    Toggles(data.evidenceFor);
                    SetPrimary("Next: evidence against", false);
                    break;
                case Step.EvidenceAgainst:
                    stepLabel.text = "STEP 2 OF 3  ·  EVIDENCE AGAINST";
                    heading.text = "Evidence AGAINST";
                    lead.text = "What doesn't fit the thought? Choose any that apply.";
                    Toggles(data.evidenceAgainst);
                    SetPrimary("Next: balanced thought", false);
                    break;
                case Step.Balanced:
                    stepLabel.text = "STEP 3 OF 3  ·  BALANCED THOUGHT";
                    heading.text = "Balanced Thought";
                    lead.text = "Choose a more balanced thought, or write one in your own words.";
                    balancedPick = -1;
                    for (int i = 0; i < data.balancedThoughts.Length; i++) { int id = i; AddOption("\"" + data.balancedThoughts[i] + "\"", () => PickBalanced(id)); }
                    AddOption("Write my own...", () => PickBalanced(data.balancedThoughts.Length));
                    LayoutOptions(.32f);
                    SetPrimary("Examine the thought", false);
                    break;
                case Step.Examined:
                    stepLabel.text = "VERDICT";
                    heading.text = "Thought Examined";
                    lead.text = "The thought was heard, and so was the evidence.";
                    body.text =
                        "<color=#765C3A>ORIGINAL THOUGHT</color>\n<s>\"" + data.thought + "\"</s>\n\n" +
                        "<color=#765C3A>EVIDENCE FOR</color>\n" + Lines(Result.evidenceFor) +
                        "<color=#765C3A>EVIDENCE AGAINST</color>\n" + Lines(Result.evidenceAgainst) +
                        "<color=#765C3A>BALANCED THOUGHT</color>\n\"" + Result.balancedThought + "\"";
                    SetPrimary("Leave the courtroom", true);
                    break;
            }
        }

        void OnPrimary()
        {
            switch (CurrentStep)
            {
                case Step.Thought: Show(Step.EvidenceFor); break;
                case Step.EvidenceFor: Collect(data.evidenceFor, Result.evidenceFor); Show(Step.EvidenceAgainst); break;
                case Step.EvidenceAgainst: Collect(data.evidenceAgainst, Result.evidenceAgainst); Show(Step.Balanced); break;
                case Step.Balanced:
                    Result.wroteOwn = balancedPick >= data.balancedThoughts.Length;
                    Result.balancedThought = Result.wroteOwn ? ownThought.text.Trim() : data.balancedThoughts[balancedPick];
                    Show(Step.Examined); break;
                case Step.Examined: Close(); break;
            }
        }

        void Close()
        {
            CurrentStep = Step.Closed;
            canvasObject.SetActive(false);
            var callback = finished; finished = null;
            callback?.Invoke(Result);
        }

        // ---------------------------------------------------------------- options

        void Toggles(string[] items)
        {
            for (int i = 0; i < items.Length; i++) { int id = i; AddOption(items[i], () => ToggleOption(id)); }
            LayoutOptions(.12f);
            hint.text = "Select at least one. Tap again to unselect.";
        }

        void ToggleOption(int index)
        {
            selected[index] = !selected[index];
            Tint(optionButtons[index], selected[index]);
            primary.interactable = selected.Contains(true);
        }

        void PickBalanced(int index)
        {
            balancedPick = index;
            for (int i = 0; i < optionButtons.Count; i++) Tint(optionButtons[i], i == index);
            bool own = index >= data.balancedThoughts.Length;
            ownThought.gameObject.SetActive(own);
            if (own) { ownThought.ActivateInputField(); hint.text = "Write it the way you would say it to yourself."; }
            else hint.text = "";
            RefreshBalanced();
        }

        void RefreshBalanced()
        {
            if (CurrentStep != Step.Balanced) return;
            primary.interactable = balancedPick >= 0 && (balancedPick < data.balancedThoughts.Length || ownThought.text.Trim().Length > 0);
        }

        void Collect(string[] items, List<string> into)
        {
            into.Clear();
            for (int i = 0; i < items.Length; i++) if (selected[i]) into.Add(items[i]);
        }

        static string Lines(List<string> items)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var s in items) sb.Append("•  ").Append(s).Append('\n');
            return sb.Append('\n').ToString();
        }

        void AddOption(string words, UnityEngine.Events.UnityAction action)
        {
            var r = Rect("Option", options, Vector2.zero, Vector2.one);
            var image = r.gameObject.AddComponent<Image>(); image.color = Paper;
            var outline = r.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(Brown.r, Brown.g, Brown.b, .5f); outline.effectDistance = new Vector2(2, -2);
            var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = image; b.onClick.AddListener(action);
            var t = Text(r, (optionButtons.Count + 1) + "   " + words, new Vector2(.03f, .08f), new Vector2(.97f, .92f), 25, false, Ink);
            t.alignment = TextAlignmentOptions.MidlineLeft;
            optionButtons.Add(b); selected.Add(false);
        }

        void LayoutOptions(float bottom)
        {
            float top = .64f, gap = .02f, h = (top - bottom - gap * (optionButtons.Count - 1)) / Mathf.Max(1, optionButtons.Count);
            h = Mathf.Min(h, .13f);
            for (int i = 0; i < optionButtons.Count; i++)
            {
                var r = (RectTransform)optionButtons[i].transform; float y = top - i * (h + gap);
                r.anchorMin = new Vector2(0, y - h); r.anchorMax = new Vector2(1, y); r.offsetMin = r.offsetMax = Vector2.zero;
            }
        }

        void ClearOptions()
        {
            foreach (var b in optionButtons) Destroy(b.gameObject);
            optionButtons.Clear(); selected.Clear();
        }

        static void Tint(Button b, bool on)
        {
            ((Image)b.targetGraphic).color = on ? Brass : Paper;
        }

        void SetPrimary(string words, bool enabled)
        {
            primary.GetComponentInChildren<TMP_Text>().text = words; primary.interactable = enabled;
        }

        // ---------------------------------------------------------------- screen

        void Build()
        {
            MinigameUI.EnsureInput();
            canvasObject = new GameObject("Thought Courtroom Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 250;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var root = canvasObject.transform;

            var art = Rect("Backdrop art", root, Vector2.zero, Vector2.one).gameObject.AddComponent<RawImage>();
            art.texture = Resources.Load<Texture2D>("MenuArt/Background"); art.raycastTarget = false;
            Rect("Warm shade", root, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>().color = new Color(Ink.r, Ink.g, Ink.b, .93f);

            Text(root, "MINDSCAPE  /  THOUGHT COURTROOM", new Vector2(.2f, .91f), new Vector2(.8f, .96f), 22, false, Brass).alignment = TextAlignmentOptions.Center;
            // The bench: a brass rail above the parchment record.
            Rect("Bench rail", root, new Vector2(.14f, .885f), new Vector2(.86f, .893f)).gameObject.AddComponent<Image>().color = Brass;

            card = Rect("Case record", root, new Vector2(.14f, .1f), new Vector2(.86f, .87f));
            card.gameObject.AddComponent<Image>().color = Parchment;
            var edge = card.gameObject.AddComponent<Outline>(); edge.effectColor = Brown; edge.effectDistance = new Vector2(3, -3);
            stepLabel = Text(card, "", new Vector2(.05f, .91f), new Vector2(.95f, .97f), 20, false, Brown);
            heading = Text(card, "", new Vector2(.05f, .80f), new Vector2(.95f, .91f), 52, true, Ink); heading.fontStyle = FontStyles.UpperCase;
            lead = Text(card, "", new Vector2(.05f, .69f), new Vector2(.95f, .79f), 26, false, Ink);
            body = Text(card, "", new Vector2(.05f, .16f), new Vector2(.95f, .68f), 30, true, Ink); body.alignment = TextAlignmentOptions.TopLeft;
            options = Rect("Options", card, new Vector2(.05f, 0), new Vector2(.95f, 1));
            ownThought = OwnThoughtField(card);
            hint = Text(card, "", new Vector2(.05f, .04f), new Vector2(.6f, .1f), 19, false, Muted);

            var pr = Rect("Continue", card, new Vector2(.62f, .03f), new Vector2(.95f, .11f));
            var pi = pr.gameObject.AddComponent<Image>(); pi.color = Brown;
            primary = pr.gameObject.AddComponent<Button>(); primary.targetGraphic = pi; primary.onClick.AddListener(OnPrimary);
            var colors = primary.colors; colors.disabledColor = new Color(.62f, .6f, .56f, .7f); colors.highlightedColor = new Color(1.18f, 1.12f, 1.04f); primary.colors = colors;
            Text(pr, "", new Vector2(.04f, .1f), new Vector2(.96f, .9f), 24, false, Cream).alignment = TextAlignmentOptions.Center;
            canvasObject.SetActive(false);
        }

        TMP_InputField OwnThoughtField(Transform parent)
        {
            var box = Rect("Own balanced thought", parent, new Vector2(.05f, .13f), new Vector2(.95f, .30f));
            box.gameObject.SetActive(false);
            box.gameObject.AddComponent<Image>().color = Paper;
            var field = box.gameObject.AddComponent<TMP_InputField>();
            var area = Rect("Text Area", box, Vector2.zero, Vector2.one); area.offsetMin = new Vector2(16, 10); area.offsetMax = new Vector2(-16, -10);
            area.gameObject.AddComponent<RectMask2D>();
            var placeholder = Text(area, "Other people may be ahead, and I can...", Vector2.zero, Vector2.one, 24, false, new Color(Ink.r, Ink.g, Ink.b, .45f));
            placeholder.fontStyle = FontStyles.Italic; placeholder.enableAutoSizing = false; placeholder.alignment = TextAlignmentOptions.TopLeft;
            var text = Text(area, "", Vector2.zero, Vector2.one, 24, false, Ink); text.enableAutoSizing = false; text.alignment = TextAlignmentOptions.TopLeft;
            field.textViewport = area; field.textComponent = text; field.placeholder = placeholder; field.targetGraphic = box.GetComponent<Image>();
            field.lineType = TMP_InputField.LineType.MultiLineNewline; field.characterLimit = 240; field.pointSize = 24;
            field.onValueChanged.AddListener(_ => RefreshBalanced());
            return field;
        }

        static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false); r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero; return r;
        }

        static TMP_Text Text(Transform parent, string words, Vector2 min, Vector2 max, float size, bool serif, Color color)
        {
            var t = Rect("Text", parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
            t.font = serif ? MinigameUI.SerifFont() : MinigameUI.Font(false); t.text = words; t.fontSize = size; t.color = color;
            t.enableAutoSizing = true; t.fontSizeMin = size * .65f; t.fontSizeMax = size; t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }
    }
}
