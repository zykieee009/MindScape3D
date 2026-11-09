using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
// Editor only: Mindscape > NPCs > Build All 20 NPC Prefabs. Rebuilds Assets/Prefabs/NPCs from the NPC models;
// names, roles and greeting lines are in the NPCInfo list below. Run again after re-exporting the models.
public static class NPCPrefabBuilder
{
    const string ModelsFolder = "Assets/Characters/NPCs";
    const string PrefabsFolder = "Assets/Prefabs/NPCs";
    const string ModelPrefabsFolder = "Assets/Prefabs/NPCs/Models";
    const string TagPrefabPath = "Assets/Prefabs/NPC/NPCNameTag.prefab";
    const string ControllerPath = "Assets/BaldCharacter.controller";

    static NPCPrefabBuilder()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists($"{PrefabsFolder}/NPC_01_Maya.prefab"))
            {
                BuildAllPrefabs();
            }
        };
    }

    public struct NPCInfo
    {
        public string id;
        public string displayName;
        public string role;
        public string defaultGreeting;

        public NPCInfo(string id, string displayName, string role, string defaultGreeting)
        {
            this.id = id;
            this.displayName = displayName;
            this.role = role;
            this.defaultGreeting = defaultGreeting;
        }
    }

    public static readonly NPCInfo[] Roster = new NPCInfo[]
    {
        new NPCInfo("NPC_01_Maya", "Maya", "Playful Classmate", "Hi, Joey! Are you ready for class today?"),
        new NPCInfo("NPC_02_Nia", "Nia", "Cheerful Student", "Good morning! The weather feels so fresh today."),
        new NPCInfo("NPC_03_Leo", "Leo", "Sporty Student", "Hey! Going to run some laps after school?"),
        new NPCInfo("NPC_04_Hana", "Hana", "Diligent Classmate", "Don't forget we have our presentations coming up!"),
        new NPCInfo("NPC_05_Chloe", "Chloe", "Artistic Classmate", "I was sketching by the courtyard earlier. It's so quiet there."),
        new NPCInfo("NPC_06_Ethan", "Ethan", "Tech Club Student", "Hey, Joey! Did you check out the new computer lab setup?"),
        new NPCInfo("NPC_07_Malik", "Malik", "Outdoorsy Student", "Morning! Always nice to get some fresh air before the bell rings."),
        new NPCInfo("NPC_08_Zara", "Zara", "Fashion-forward Student", "Hi! Hope your day is going great so far."),
        new NPCInfo("NPC_09_Julian", "Julian", "Bookworm Classmate", "I just finished a wonderful book in the library."),
        new NPCInfo("NPC_10_Olivia", "Olivia", "Quiet Reader", "Hello, Joey. Take a gentle breath whenever things feel busy."),
        new NPCInfo("NPC_11_Amara", "Amara", "Sunshine Student", "Good morning, Joey! Keep your chin up today!"),
        new NPCInfo("NPC_12_Noah", "Noah", "Friendly Classmate", "Hey, Joey! Catch you at lunch!"),
        new NPCInfo("NPC_13_David", "David", "Class President", "Good morning, everyone. Let's make today productive and kind."),
        new NPCInfo("NPC_14_Mei", "Mei", "Sweet Musician", "I love the sound of the school bell in the morning."),
        new NPCInfo("NPC_15_Kai", "Kai", "Adventurous Explorer", "There are so many cool corners of the school grounds to explore!"),
        new NPCInfo("NPC_16_Tariq", "Tariq", "Athletic Sprinter", "Speed and patience—balance is everything!"),
        new NPCInfo("NPC_17_Penelope", "Penelope", "School Librarian", "Welcome, Joey. The library is always here when you need quiet focus."),
        new NPCInfo("NPC_18_Henderson", "Mr. Henderson", "School Teacher", "Good morning, Joey. Remember, one step at a time."),
        new NPCInfo("NPC_19_Chef_Rosa", "Chef Rosa", "Cafeteria Chef", "A warm lunch will be ready soon! Make sure to eat well today, sweetie."),
        new NPCInfo("NPC_20_Officer_Arthur", "Officer Arthur", "Community Driver", "Good morning, kiddo! Safe travels and have a great day at school.")
    };

    [MenuItem("Mindscape/NPCs/Build All 20 NPC Prefabs")]
    public static void BuildAllPrefabs()
    {
        Debug.Log("[NPCPrefabBuilder] Starting build of all 20 NPC prefabs...");

        if (!Directory.Exists(PrefabsFolder)) Directory.CreateDirectory(PrefabsFolder);
        if (!Directory.Exists(ModelPrefabsFolder)) Directory.CreateDirectory(ModelPrefabsFolder);
        AssetDatabase.Refresh();

        GameObject tagPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TagPrefabPath);
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);

        int count = 0;
        foreach (var info in Roster)
        {
            // Try GLB first, then FBX
            string modelAssetPath = $"{ModelsFolder}/{info.id}.glb";
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelAssetPath);
            if (modelAsset == null)
            {
                modelAssetPath = $"{ModelsFolder}/{info.id}.fbx";
                modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelAssetPath);
            }

            if (modelAsset == null)
            {
                Debug.LogWarning($"[NPCPrefabBuilder] Model asset not found at {ModelsFolder}/{info.id}.glb or .fbx");
                continue;
            }

            // 1. Create Standalone Model Prefab (Assets/Prefabs/NPCs/Models/NPC_XX_Model.prefab)
            string modelPrefabPath = $"{ModelPrefabsFolder}/{info.id}_Model.prefab";
            GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            modelInstance.name = $"{info.id}_Model";
            
            Animator animator = modelInstance.GetComponent<Animator>();
            if (animator == null) animator = modelInstance.AddComponent<Animator>();
            if (controller != null) animator.runtimeAnimatorController = controller;

            GameObject savedModelPrefab = PrefabUtility.SaveAsPrefabAsset(modelInstance, modelPrefabPath);
            Object.DestroyImmediate(modelInstance);

            // 2. Create Full Gameplay NPC Prefab (Assets/Prefabs/NPCs/NPC_XX.prefab)
            string npcPrefabPath = $"{PrefabsFolder}/{info.id}.prefab";
            GameObject npcRoot = new GameObject($"NPC - {info.displayName}", typeof(CapsuleCollider), typeof(NPC));
            
            // Configure Capsule Collider
            CapsuleCollider col = npcRoot.GetComponent<CapsuleCollider>();
            col.height = 1.8f;
            col.center = new Vector3(0f, 0.9f, 0f);
            col.radius = 0.35f;

            // Configure NPC script
            NPC npc = npcRoot.GetComponent<NPC>();
            npc.npcName = info.displayName;
            npc.modelPrefab = savedModelPrefab != null ? savedModelPrefab : modelAsset;
            npc.modelScale = 1f;
            npc.modelOffset = Vector3.zero;
            npc.interactionRadius = 3f;
            npc.turnDuration = 0.3f;
            npc.faceHeight = 1.65f;
            npc.cameraDistance = 1.7f;
            npc.cameraHeightOffset = 0.08f;
            npc.enableDialogueChaining = true;
            npc.dialogueNodes = new List<NPCDialogueNode>
            {
                new NPCDialogueNode
                {
                    npcDialogue = info.defaultGreeting,
                    playerChoices = new List<NPCDialogueChoice>
                    {
                        new NPCDialogueChoice { playerChoice = "Thank you!", endsDialogue = true, stressChange = -5f }
                    }
                }
            };

            // Spawn Name Tag child
            if (tagPrefab != null)
            {
                GameObject tag = (GameObject)PrefabUtility.InstantiatePrefab(tagPrefab, npcRoot.transform);
                tag.name = "Name Tag";
                tag.transform.localPosition = new Vector3(0f, 2.2f, 0f);
                NPCNameTag nt = tag.GetComponent<NPCNameTag>();
                if (nt != null)
                {
                    nt.npc = npc;
                    nt.height = 2.2f;
                    nt.controlHeight = true;
                }
                TMP_Text label = tag.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = info.displayName;
            }

            // Spawn 3D Model child under NPC
            GameObject childModel = (GameObject)PrefabUtility.InstantiatePrefab(savedModelPrefab != null ? savedModelPrefab : modelAsset, npcRoot.transform);
            childModel.name = "Model";
            childModel.transform.localPosition = Vector3.zero;
            childModel.transform.localRotation = Quaternion.identity;
            childModel.transform.localScale = Vector3.one;

            // Disable model colliders so the main NPC CapsuleCollider handles physics
            foreach (Collider c in childModel.GetComponentsInChildren<Collider>(true))
            {
                c.enabled = false;
            }

            PrefabUtility.SaveAsPrefabAsset(npcRoot, npcPrefabPath);
            Object.DestroyImmediate(npcRoot);

            count++;
            Debug.Log($"[NPCPrefabBuilder] Successfully built: {npcPrefabPath} and {modelPrefabPath}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[NPCPrefabBuilder] Complete! Generated {count} NPC prefabs in {PrefabsFolder}");
    }
}
