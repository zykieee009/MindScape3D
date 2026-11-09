using System.Collections.Generic;
using System.IO;
using Mindscape.Minigames;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Chapter 1 - Episode 1 content: opening cutscene slides, and the family NPCs in the house.
// Both menu items can be re-run; they rebuild their own content and leave everything else alone.
public static class Episode1Builder
{
    const string CutscenePath = "Assets/Scenes/Cutscene - Episode 1.unity";
    const string EpisodePath = "Assets/Scenes/" + Mindscape.EpisodeOne.EpisodeOneStory.SceneName + ".unity";
    const string DummyModelPath = "Assets/Prefabs/NPC/DefaultNPCModel.prefab";
    const string TagPrefabPath = "Assets/Prefabs/NPC/NPCNameTag.prefab";
    const string FontPath = "Assets/Resources/MinigameTheme/ComicNeue-Bold SDF.asset";
    const string RegularFontPath = "Assets/Resources/MinigameTheme/ComicNeue-Regular SDF.asset";

    static readonly string[] SlideText =
    {
        "7:50 in the morning. The alarm rings, and the day begins the way days do. A day does not ask whether we are ready. It simply arrives.",
        "Joey opens his eyes. Between sleeping and waking there is a small pause. In that pause there is only the breath, moving in and out.",
        "A message appears on the screen: Important Presentation Today. It is written in plain words. What it means to Joey is something he can discover a little at a time.",
        "Many things sit in his mind at once: a presentation, lunch, a school bag, a word with Mom, the bus at eight. A crowded table can be cleared one dish at a time."
    };

    // ---------------------------------------------------------------- cutscene
    [MenuItem("Mindscape/Chapter 2 - Family Expectations/Episode 1/Build Cutscene Slides")]
    public static void BuildCutscene()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play mode first."); return; }
        var scene = EditorSceneManager.OpenScene(CutscenePath, OpenSceneMode.Single);
        var slidesGo = GameObject.Find("Slides");
        if (slidesGo == null) { Debug.LogError("No 'Slides' object in the cutscene scene."); return; }
        var root = slidesGo.transform;

        if (root.childCount == 2)
        {
            // Keep the last slide (it holds the Play Episode button) and clone slide 1 twice for the middle slides.
            for (int i = 0; i < 2; i++)
            {
                var clone = Object.Instantiate(root.GetChild(0).gameObject, root);
                clone.name = "Slide Clone";
                clone.transform.SetSiblingIndex(1 + i);
            }
        }
        if (root.childCount != 4) { Debug.LogError("Expected 2 or 4 slides under 'Slides', found " + root.childCount); return; }

        var groups = new CanvasGroup[4];
        var textFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RegularFontPath);
        var plate = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        for (int i = 0; i < 4; i++)
        {
            var slide = root.GetChild(i);
            slide.name = "Slide " + (i + 1);
            groups[i] = slide.GetComponent<CanvasGroup>();

            var image = slide.Find("Slide Image").GetComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Cutscenes/Episode 1/" + (i + 1) + ".png");
            image.preserveAspect = true; image.color = Color.white;

            // Readable caption plate behind the text.
            var textTr = slide.Find("Slide Text");
            var backing = slide.Find("Caption Backing");
            if (backing == null)
            {
                var b = new GameObject("Caption Backing", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                b.transform.SetParent(slide, false); backing = b.transform;
            }
            backing.SetSiblingIndex(textTr.GetSiblingIndex());
            var br = (RectTransform)backing; br.anchorMin = new Vector2(.06f, .11f); br.anchorMax = new Vector2(.94f, .33f); br.offsetMin = br.offsetMax = Vector2.zero;
            var bi = backing.GetComponent<Image>(); bi.sprite = plate; bi.type = Image.Type.Sliced; bi.color = new Color(.11f, .08f, .06f, .82f); bi.raycastTarget = false;

            var tr = (RectTransform)textTr; tr.anchorMin = new Vector2(.08f, .11f); tr.anchorMax = new Vector2(.92f, .33f); tr.offsetMin = tr.offsetMax = Vector2.zero;
            var tmp = textTr.GetComponent<TextMeshProUGUI>();
            if (textFont != null) tmp.font = textFont;
            tmp.fontSize = 36; tmp.enableAutoSizing = true; tmp.fontSizeMin = 22; tmp.fontSizeMax = 38;
            tmp.alignment = TextAlignmentOptions.Center; tmp.color = new Color32(240, 228, 204, 255); tmp.textWrappingMode = TextWrappingModes.Normal; tmp.raycastTarget = false;

            var ct = textTr.GetComponent<CutsceneText>();
            var so = new SerializedObject(ct);
            so.FindProperty("speakerName").stringValue = "Narrator";
            so.FindProperty("dialogue").stringValue = SlideText[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            tmp.text = SlideText[i];

            var next = slide.Find("Next Button");
            if (next != null)
            {
                var nb = next.GetComponent<CutsceneNextButton>();
                var nso = new SerializedObject(nb); nso.FindProperty("targetSlideIndex").intValue = i + 1; nso.ApplyModifiedPropertiesWithoutUndo();
                next.gameObject.SetActive(i < 3);
            }
            var play = slide.Find("Play Episode Button");
            if (play != null)
            {
                var pso = new SerializedObject(play.GetComponent<CutscenePlayEpisodeButton>());
                pso.FindProperty("sceneName").stringValue = Mindscape.EpisodeOne.EpisodeOneStory.SceneName; pso.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        var player = Object.FindAnyObjectByType<CutscenePlayer>();
        var pl = new SerializedObject(player);
        var arr = pl.FindProperty("slides"); arr.arraySize = 4;
        for (int i = 0; i < 4; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = groups[i];
        pl.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("Episode 1 cutscene rebuilt: 4 slides with images and narration.");
    }

    // ---------------------------------------------------------------- NPCs
    struct Choice { public string text; public int next; public bool ends; }
    static Choice C(string text, int next) { return new Choice { text = text, next = next }; }
    static Choice End(string text) { return new Choice { text = text, next = -1, ends = true }; }
    static NPCDialogueNode Node(string line, params Choice[] choices)
    {
        var n = new NPCDialogueNode { npcDialogue = line, playerChoices = new List<NPCDialogueChoice>() };
        foreach (var c in choices)
            n.playerChoices.Add(new NPCDialogueChoice { playerChoice = c.text, stressChange = 0f, nextDialogueIndex = c.next, endsDialogue = c.ends });
        return n;
    }

    internal static List<NPCDialogueNode> MomLines() { return new List<NPCDialogueNode>
    {
        Node("Good morning, Joey. There is breakfast on the table and lunch beside the door. A morning holds many small things, and each of them is only what it is.",
            C("What is today like?", 1), C("I have a presentation today.", 2)),
        Node("Today is a day like any other, made of hours. Some of what fills them is planned, and some of it is not. Days have always been this way.",
            C("I see.", 3)),
        Node("Yes, I know. A presentation is a few minutes of speaking placed inside a long day. The rest of the day keeps going, as days do.",
            C("That is one way to see it.", 3)),
        Node("Your father is in the living room, and your brother is somewhere in the house. Before you go, shall we look at a thought together, the way a judge looks at a case, one piece of evidence at a time?",
            End("Alright."))
    }; }

    static List<NPCDialogueNode> DadLines() { return new List<NPCDialogueNode>
    {
        Node("Joey. Come and sit beside me for a moment. There is one thing a person carries everywhere, and it is the breath.",
            C("Why the breath?", 1), C("I only have a moment.", 2)),
        Node("Because it is always here, in every kind of day. We do not have to find it or earn it. We only have to notice it.",
            C("How do I notice it?", 3)),
        Node("A moment is enough. A single breath takes only a few seconds, and it can be taken anywhere.",
            C("Alright.", 3)),
        Node("Then let us breathe together. In, and out, and we will see what we see.",
            End("Let's breathe."))
    }; }

    static List<NPCDialogueNode> TimLines() { return new List<NPCDialogueNode>
    {
        Node("Joey! When a door is open, is the room inside or outside?",
            C("Inside, I think.", 1), C("Maybe both.", 2)),
        Node("Then a door is neither. It is the place where one room turns into another.",
            C("That makes sense.", 3)),
        Node("Both. Then a door is where two places meet.",
            C("It might be.", 3)),
        Node("The bus comes down the street, goes to school, and comes back again. It keeps moving, and the road stays where it is.",
            End("Goodbye, Tim."))
    }; }

    [MenuItem("Mindscape/Chapter 2 - Family Expectations/Episode 1/Populate House NPCs")]
    public static void PopulateNPCs()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play mode first."); return; }
        var scene = EditorSceneManager.OpenScene(EpisodePath, OpenSceneMode.Single);
        var tagPrefab = EnsureTagPrefab();
        var dummy = AssetDatabase.LoadAssetAtPath<GameObject>(DummyModelPath);

        var manager = Object.FindAnyObjectByType<NPCInteractionManager>();
        if (manager == null) { Debug.LogError("No NPC System in the scene."); return; }
        foreach (var old in Object.FindObjectsByType<NPC>(FindObjectsInactive.Include)) Object.DestroyImmediate(old.gameObject);

        MakeNPC(manager.transform, tagPrefab, dummy, "Mom", new Vector3(-13.8f, 0f, 0.8f), 90f, 1f, MomLines());
        MakeNPC(manager.transform, tagPrefab, dummy, "Dad", new Vector3(-26.0f, 0f, -0.4f), 270f, 1f, DadLines());
        MakeNPC(manager.transform, tagPrefab, dummy, "Tim", new Vector3(-20.5f, 0f, -4.5f), 270f, 0.65f, TimLines());

        // Quest: all three family members are required, and Dad's talk opens the breathing exercise.
        var quest = Object.FindAnyObjectByType<Quest>();
        if (quest != null)
        {
            quest.requiredNPCNames = new List<string> { "Mom", "Dad", "Tim" };
            quest.episodeDurationMinutes = 5; quest.episodeDurationSeconds = 0;
            if (quest.requiredMinigames != null && quest.requiredMinigames.Count > 0)
            {
                quest.requiredMinigames[0].trigger = QuestMinigameTrigger.WhenTalkToNPC;
                quest.requiredMinigames[0].npcName = "Dad";
            }
            EditorUtility.SetDirty(quest);
        }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("Episode 1: Mom, Dad and Tim placed under 'NPC System'.");
    }

    static float FloorY(Vector3 p)
    {
        var origin = new Vector3(p.x, 6f, p.z);
        if (Physics.Raycast(origin, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore)) return hit.point.y;
        return 0.55f;
    }

    static void MakeNPC(Transform parent, GameObject tagPrefab, GameObject dummy, string npcName, Vector3 pos, float yaw, float scale, List<NPCDialogueNode> lines)
    {
        pos.y = FloorY(pos);
        var go = new GameObject("NPC - " + npcName, typeof(CapsuleCollider), typeof(NPC));
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        var col = go.GetComponent<CapsuleCollider>();
        col.height = 2f * scale; col.center = new Vector3(0f, scale, 0f); col.radius = 0.4f * Mathf.Max(0.75f, scale);

        var npc = go.GetComponent<NPC>();
        npc.npcName = npcName; npc.modelPrefab = dummy; npc.modelScale = scale; npc.modelOffset = Vector3.zero;
        npc.faceHeight = 1.65f * scale; npc.cameraDistance = scale < 1f ? 1.4f : 1.7f;
        npc.enableDialogueChaining = true; npc.dialogueNodes = lines;
        npc.RefreshModel();

        var tag = (GameObject)PrefabUtility.InstantiatePrefab(tagPrefab, go.transform);
        tag.name = "Name Tag";
        var nt = tag.GetComponent<NPCNameTag>(); nt.npc = npc; nt.height = 2f * scale + 0.4f; nt.controlHeight = true;
        tag.transform.localPosition = new Vector3(0f, nt.height, 0f);
        tag.GetComponentInChildren<TMP_Text>().text = npcName;
        EditorUtility.SetDirty(go);
    }

    static GameObject EnsureTagPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(TagPrefabPath);
        if (existing != null) return existing;
        Directory.CreateDirectory(Path.GetDirectoryName(TagPrefabPath));
        var root = new GameObject("Name Tag", typeof(RectTransform), typeof(Canvas), typeof(NPCNameTag));
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 5;
        var rt = (RectTransform)root.transform; rt.sizeDelta = new Vector2(300, 80); rt.localScale = Vector3.one * 0.006f;

        var plate = new GameObject("Plate", typeof(RectTransform), typeof(Image)); plate.transform.SetParent(root.transform, false);
        var pr = (RectTransform)plate.transform; pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = pr.offsetMax = Vector2.zero;
        var pi = plate.GetComponent<Image>(); pi.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"); pi.type = Image.Type.Sliced;
        pi.color = new Color(.11f, .08f, .06f, .86f); pi.raycastTarget = false;

        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); label.transform.SetParent(root.transform, false);
        var lr = (RectTransform)label.transform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = new Vector2(10, 4); lr.offsetMax = new Vector2(-10, -4);
        var tmp = label.GetComponent<TextMeshProUGUI>();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath); if (font != null) tmp.font = font;
        tmp.text = "NPC"; tmp.fontSize = 44; tmp.alignment = TextAlignmentOptions.Center; tmp.color = new Color32(240, 228, 204, 255);
        tmp.textWrappingMode = TextWrappingModes.NoWrap; tmp.raycastTarget = false;

        var nt = root.GetComponent<NPCNameTag>(); nt.label = tmp;
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, TagPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }
}

// Wandering routes. Re-runnable: replaces the 'NPC Waypoints' object and each NPC's NPCWander.
public static class Episode1Wander
{
    struct Route { public string npc; public NPCWander.Pattern pattern; public float[] xz; }

    static readonly Route[] Routes =
    {
        // Mom circles the dining table.
        new Route { npc = "Mom", pattern = NPCWander.Pattern.Loop, xz = new[] { -13.8f, 0.8f,  -13.8f, 5.0f,  -19.6f, 5.0f,  -19.6f, -4.2f,  -13.8f, -4.2f } },
        // Dad strolls the east side of the living room.
        new Route { npc = "Dad", pattern = NPCWander.Pattern.PingPong, xz = new[] { -24.4f, -3.2f,  -24.4f, -0.4f,  -24.4f, 3.0f,  -28.6f, 3.0f } },
        // Tim walks the hallway between the front door and the bedrooms.
        new Route { npc = "Tim", pattern = NPCWander.Pattern.PingPong, xz = new[] { -21.5f, -5.0f,  -20.5f, -4.5f,  -21.5f, 2.5f } }
    };

    [MenuItem("Mindscape/Chapter 2 - Family Expectations/Episode 1/Add NPC Wandering")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play mode first."); return; }
        var scene = EditorSceneManager.GetActiveScene();
        var old = GameObject.Find("NPC Waypoints"); if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject("NPC Waypoints").transform;
        int problems = 0;

        foreach (var route in Routes)
        {
            NPC npc = null;
            foreach (var n in Object.FindObjectsByType<NPC>(FindObjectsInactive.Include)) if (n.npcName == route.npc) npc = n;
            if (npc == null) { Debug.LogWarning("No NPC named " + route.npc); continue; }

            var group = new GameObject(route.npc + " Waypoints").transform; group.SetParent(root, false);
            var points = new List<Transform>();
            for (int i = 0; i < route.xz.Length; i += 2)
            {
                var wp = new GameObject("WP " + (i / 2 + 1)).transform; wp.SetParent(group, false);
                float x = route.xz[i], z = route.xz[i + 1];
                float y = FloorHeight(x, z);
                wp.position = new Vector3(x, y, z); points.Add(wp);
            }

            var wander = npc.GetComponent<NPCWander>(); if (wander != null) Object.DestroyImmediate(wander);
            wander = npc.gameObject.AddComponent<NPCWander>();
            wander.waypoints = points; wander.pattern = route.pattern;

            // Check every straight segment (and the closing one for loops) for walls and furniture.
            for (int i = 0; i < points.Count; i++)
            {
                int j = (i + 1) % points.Count;
                if (j == 0 && route.pattern != NPCWander.Pattern.Loop) break;
                if (Blocked(points[i].position, points[j].position, npc)) { problems++; Debug.LogWarning(route.npc + ": blocked between WP " + (i + 1) + " and WP " + (j + 1)); }
            }
            EditorUtility.SetDirty(npc);
        }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("NPC wandering added. Blocked segments: " + problems);
    }

    // Highest solid surface at x,z, ignoring NPCs and the player so a waypoint never lands on someone's head.
    static float FloorHeight(float x, float z)
    {
        float best = float.NegativeInfinity;
        foreach (var h in Physics.RaycastAll(new Vector3(x, 6f, z), Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.GetComponentInParent<NPC>() != null || h.collider.GetComponentInParent<PlayerControls>() != null) continue;
            if (h.collider.GetComponentInParent<CollectibleItem>() != null) continue;
            if (h.point.y > best) best = h.point.y;
        }
        return float.IsNegativeInfinity(best) ? 0.55f : best;
    }

    static bool Blocked(Vector3 a, Vector3 b, NPC self)
    {
        Vector3 dir = b - a; float dist = dir.magnitude; if (dist < 0.01f) return false;
        dir /= dist;
        var hits = Physics.CapsuleCastAll(a + Vector3.up * 0.45f, a + Vector3.up * 1.5f, 0.35f, dir, dist, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.collider.GetComponentInParent<NPC>() != null) continue;
            if (h.collider.GetComponentInParent<PlayerControls>() != null) continue;
            Debug.Log("   hit " + h.collider.name + " at " + h.point);
            return true;
        }
        return false;
    }
}


// Episode 1 minigames: the Worry Courtroom (opened after Mom's talk) and the themed breathing screen (after Dad's talk).
public static class Episode1Minigames
{
    const string EpisodePath = "Assets/Scenes/" + Mindscape.EpisodeOne.EpisodeOneStory.SceneName + ".unity";
    const string CasePath = "Assets/Minigames/WorryCourtroom/Cases/Chapter1_Presentation.asset";

    static EvidenceCard Fact(string t) { return new EvidenceCard { text = t, isFact = true }; }
    static EvidenceCard Guess(string t, ThinkingTrap trap) { return new EvidenceCard { text = t, isFact = false, trap = trap }; }
    static AnswerOption Good(string t) { return new AnswerOption { text = t, isGood = true }; }
    static AnswerOption Bad(string t, string why) { return new AnswerOption { text = t, isGood = false, whyNot = why }; }

    static WorryCourtroomCase EnsureCase()
    {
        var existing = AssetDatabase.LoadAssetAtPath<WorryCourtroomCase>(CasePath);
        if (existing != null) return existing;
        Directory.CreateDirectory(Path.GetDirectoryName(CasePath));
        var c = ScriptableObject.CreateInstance<WorryCourtroomCase>();
        c.chapterLabel = "Chapter 1"; c.caseName = "The presentation";
        c.worry = "I'll mess up my presentation and everyone will laugh.";
        c.evidence = new List<EvidenceCard>
        {
            Fact("I stumbled over a word during practice."),
            Guess("Everyone will laugh at me.", ThinkingTrap.FortuneTelling),
            Fact("My voice shook a little during rehearsal."),
            Guess("One mistake will ruin everything.", ThinkingTrap.Catastrophizing),
            Guess("I feel like I'm going to freeze, so I will.", ThinkingTrap.EmotionalReasoning),
            Guess("I'm terrible at speaking.", ThinkingTrap.Labeling)
        };
        c.defence = new List<AnswerOption>
        {
            Good("I practiced twice, and the second run went better."),
            Good("Classmates have made small mistakes before and moved on."),
            Good("I can bring note cards to help me."),
            Good("Slow breathing before I start helps me stay calm."),
            Bad("I'll tell the teacher I'm sick and skip it.", "That avoids the fear and doesn't test the worry."),
            Bad("Nobody will really be listening anyway.", "That is a guess, and it doesn't help you feel ready.")
        };
        c.friend = new List<AnswerOption>
        {
            Good("Feeling nervous is normal. Practice once more and take it one slide at a time."),
            Bad("Don't bother. You'll embarrass yourself.", "A good friend wouldn't predict the worst for you."),
            Bad("Just don't be nervous.", "Nobody can switch nerves off. Normal and okay is kinder.")
        };
        c.verdict = new List<AnswerOption>
        {
            Good("I might feel nervous and still do a good enough job. Small mistakes are okay."),
            Bad("It has to be perfect or I fail.", "All-or-nothing thinking. Good enough is the goal."),
            Bad("I should avoid the presentation entirely.", "Avoiding keeps the worry alive. It doesn't answer it."),
            Bad("I'm not nervous at all, so I don't need to prepare.", "Pretending skips the real feeling and the real preparation.")
        };
        c.plan = new List<AnswerOption>
        {
            Good("Practice in front of a mirror or a friend tonight."),
            Good("Write my key points on note cards."),
            Bad("Memorize every word perfectly.", "Perfect is a trap. Aim for good enough."),
            Bad("Hope it goes fine.", "Hope isn't an action. Pick something you can do.")
        };
        AssetDatabase.CreateAsset(c, CasePath); AssetDatabase.SaveAssets();
        return c;
    }

    [MenuItem("Mindscape/Chapter 2 - Family Expectations/Episode 1/Add Worry Courtroom and Breathing Screen")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play mode first."); return; }
        var scene = EditorSceneManager.OpenScene(EpisodePath, OpenSceneMode.Single);
        var courtCase = EnsureCase();

        // Courtroom prefab instance, closed until the quest opens it.
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Minigames/WorryCourtroom.prefab");
        if (prefab == null) { Debug.LogError("Worry Courtroom prefab missing (Assets/Prefabs/Minigames/WorryCourtroom.prefab)."); return; }
        foreach (var old in Object.FindObjectsByType<WorryCourtroomMinigame>(FindObjectsInactive.Include)) Object.DestroyImmediate(old.gameObject);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "Worry Courtroom";
        var court = instance.GetComponent<WorryCourtroomMinigame>();
        var so = new SerializedObject(court);
        so.FindProperty("startOpen").boolValue = false;
        var cases = so.FindProperty("cases"); cases.arraySize = 1; cases.GetArrayElementAtIndex(0).objectReferenceValue = courtCase;
        so.ApplyModifiedProperties();

        // Breathing exercise: its screen is a UI Toolkit file that the component loads at runtime.
        var breathing = Object.FindAnyObjectByType<BreathingMinigame>();
        if (breathing != null) EditorUtility.SetDirty(breathing);

        // Quest: Mom's talk opens the courtroom (Dad's talk already opens breathing).
        var quest = Object.FindAnyObjectByType<Quest>();
        if (quest != null)
        {
            quest.requiredMinigames.RemoveAll(r => r == null || r.minigame == null || r.minigame is WorryCourtroomMinigame);
            quest.requiredMinigames.Add(new QuestMinigameRequirement { minigame = court, trigger = QuestMinigameTrigger.WhenTalkToNPC, npcName = "Mom" });
            EditorUtility.SetDirty(quest);
            // The breathing requirement was removed above only if its reference was empty; make sure it is still present.
            if (breathing != null && !quest.requiredMinigames.Exists(r => r.minigame == breathing))
                quest.requiredMinigames.Insert(0, new QuestMinigameRequirement { minigame = breathing, trigger = QuestMinigameTrigger.WhenTalkToNPC, npcName = "Dad" });
        }

        // Mom's closing line now leads into the courtroom.
        foreach (var n in Object.FindObjectsByType<NPC>(FindObjectsInactive.Include))
            if (n.npcName == "Mom") { n.dialogueNodes = Episode1Builder.MomLines(); EditorUtility.SetDirty(n); }

        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("Episode 1: Worry Courtroom (after Mom) and the themed Breathing Exercise (after Dad) are set up.");
    }
}
