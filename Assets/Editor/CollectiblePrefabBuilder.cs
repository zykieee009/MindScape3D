using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
// Editor only: Mindscape > Collectibles > Build All 12 Collectible Prefabs. Rebuilds the collectible prefabs
// from their models (GLB first, then FBX).
public static class CollectiblePrefabBuilder
{
    const string ModelsFolder = "Assets/Models/Collectibles";
    const string PrefabsFolder = "Assets/Prefabs/Collectibles";

    public struct ItemDefinition
    {
        public string id;
        public string displayName;
        public string prefabFileName;
        public string modelFileName;
        public Vector3 colliderSize;
        public Vector3 colliderCenter;

        public ItemDefinition(string id, string displayName, string prefabFileName, string modelFileName, Vector3 colliderSize, Vector3 colliderCenter)
        {
            this.id = id;
            this.displayName = displayName;
            this.prefabFileName = prefabFileName;
            this.modelFileName = modelFileName;
            this.colliderSize = colliderSize;
            this.colliderCenter = colliderCenter;
        }
    }

    public static readonly ItemDefinition[] Collectibles = new ItemDefinition[]
    {
        new ItemDefinition("books", "Books", "Collectible_Books.prefab", "Collectible_Books", new Vector3(0.36f, 0.20f, 0.28f), new Vector3(0f, 0.10f, 0f)),
        new ItemDefinition("research_notes", "Research Notes", "Collectible_ResearchNotes.prefab", "Collectible_ResearchNotes", new Vector3(0.28f, 0.08f, 0.36f), new Vector3(0f, 0.04f, 0f)),
        new ItemDefinition("school_bag", "School Bag", "Collectible_SchoolBag.prefab", "Collectible_SchoolBag", new Vector3(0.36f, 0.45f, 0.28f), new Vector3(0f, 0.22f, 0f)),
        new ItemDefinition("examination_paper", "Examination Paper", "Collectible_ExaminationPaper.prefab", "Collectible_ExaminationPaper", new Vector3(0.32f, 0.08f, 0.42f), new Vector3(0f, 0.04f, 0f)),
        new ItemDefinition("notebook", "Notebook", "Collectible_Notebook.prefab", "Collectible_Notebook", new Vector3(0.26f, 0.08f, 0.32f), new Vector3(0f, 0.04f, 0f)),
        new ItemDefinition("student_id", "Student ID", "Collectible_StudentID.prefab", "Collectible_StudentID", new Vector3(0.22f, 0.30f, 0.18f), new Vector3(0f, 0.15f, 0f)),
        new ItemDefinition("breakfast_plate", "Breakfast Plate", "Collectible_BreakfastPlate.prefab", "Collectible_BreakfastPlate", new Vector3(0.28f, 0.06f, 0.28f), new Vector3(0f, 0.03f, 0f)),
        new ItemDefinition("laptop", "Laptop", "Collectible_Laptop.prefab", "Collectible_Laptop", new Vector3(0.34f, 0.24f, 0.28f), new Vector3(0f, 0.12f, 0.02f)),
        new ItemDefinition("phone", "Phone", "Collectible_Phone.prefab", "Collectible_Phone", new Vector3(0.14f, 0.05f, 0.24f), new Vector3(0f, 0.02f, 0f)),
        new ItemDefinition("old_certificate", "Old Certificate", "Collectible_OldCertificate.prefab", "Collectible_OldCertificate", new Vector3(0.36f, 0.28f, 0.20f), new Vector3(0f, 0.14f, 0f)),
        new ItemDefinition("drawing_sketchbook", "Drawing Sketchbook", "Collectible_DrawingSketchbook.prefab", "Collectible_DrawingSketchbook", new Vector3(0.34f, 0.06f, 0.26f), new Vector3(0f, 0.03f, 0f)),
        new ItemDefinition("old_notebook", "Old Notebook", "Collectible_OldNotebook.prefab", "Collectible_OldNotebook", new Vector3(0.28f, 0.07f, 0.30f), new Vector3(0f, 0.035f, 0f))
    };

    static CollectiblePrefabBuilder()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists($"{PrefabsFolder}/Collectible_OldNotebook.prefab"))
            {
                BuildAllPrefabs();
            }
        };
    }

    [MenuItem("Mindscape/Collectibles/Build All 12 Collectible Prefabs")]
    public static void BuildAllPrefabs()
    {
        if (!Directory.Exists(PrefabsFolder)) Directory.CreateDirectory(PrefabsFolder);

        int count = 0;
        foreach (var def in Collectibles)
        {
            // Try GLB first, then FBX
            string modelPath = $"{ModelsFolder}/{def.modelFileName}.glb";
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelAsset == null)
            {
                modelPath = $"{ModelsFolder}/{def.modelFileName}.fbx";
                modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            }

            if (modelAsset == null)
            {
                Debug.LogWarning($"[CollectiblePrefabBuilder] Model not found for {def.modelFileName}");
                continue;
            }

            string prefabPath = $"{PrefabsFolder}/{def.prefabFileName}";
            GameObject root = new GameObject(def.prefabFileName.Replace(".prefab", ""));

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = def.colliderSize;
            collider.center = def.colliderCenter;

            CollectibleItem item = root.AddComponent<CollectibleItem>();
            item.itemId = def.id;
            item.collectibleName = def.displayName;
            item.interactionRadius = 2.25f;
            item.modelPrefab = modelAsset;
            item.RefreshModel();

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            count++;
            Debug.Log($"[CollectiblePrefabBuilder] Saved prefab: {prefabPath}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[CollectiblePrefabBuilder] Complete! Generated {count} collectible prefabs in {PrefabsFolder}");
    }
}

