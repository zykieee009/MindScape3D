using System.IO;
using UnityEditor;
using UnityEngine;
using Mindscape.Vehicles;

[InitializeOnLoad]
public static class SchoolBusPrefabBuilder
{
    const string ModelPath = "Assets/Models/Vehicles/School_Bus.fbx";
    const string SchoolBusPrefabPath = "Assets/Prefabs/SchoolBus.prefab";
    const string CommuterBusPrefabPath = "Assets/EpisodeTwo/Prefabs/Commuter bus.prefab";

    static SchoolBusPrefabBuilder()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(SchoolBusPrefabPath))
            {
                BuildPrefabs();
            }
        };
    }

    [MenuItem("Mindscape/Vehicles/Build School Bus Prefabs")]
    public static void BuildPrefabs()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null)
        {
            Debug.LogError($"[SchoolBusPrefabBuilder] Model not found at {ModelPath}");
            return;
        }

        // 1. Build Assets/Prefabs/SchoolBus.prefab
        string prefabsDir = Path.GetDirectoryName(SchoolBusPrefabPath);
        if (!Directory.Exists(prefabsDir)) Directory.CreateDirectory(prefabsDir);

        GameObject busInstance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        busInstance.name = "SchoolBus";

        ConfigureBusComponents(busInstance);

        PrefabUtility.SaveAsPrefabAsset(busInstance, SchoolBusPrefabPath);
        Debug.Log($"[SchoolBusPrefabBuilder] Created: {SchoolBusPrefabPath}");

        // 2. Update Assets/EpisodeTwo/Prefabs/Commuter bus.prefab
        string commuterDir = Path.GetDirectoryName(CommuterBusPrefabPath);
        if (!Directory.Exists(commuterDir)) Directory.CreateDirectory(commuterDir);

        GameObject commuterInstance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        commuterInstance.name = "School route commuter bus";

        ConfigureBusComponents(commuterInstance);

        PrefabUtility.SaveAsPrefabAsset(commuterInstance, CommuterBusPrefabPath);
        Debug.Log($"[SchoolBusPrefabBuilder] Updated: {CommuterBusPrefabPath}");

        Object.DestroyImmediate(busInstance);
        Object.DestroyImmediate(commuterInstance);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void ConfigureBusComponents(GameObject busObj)
    {
        // Add SchoolBusAnimator
        var animator = busObj.GetComponent<SchoolBusAnimator>();
        if (animator == null)
        {
            animator = busObj.AddComponent<SchoolBusAnimator>();
        }

        animator.wheelRadius = 0.70f;
        animator.wheelSpinAxis = Vector3.up;
        animator.idleVibrationFrequency = 22f;
        animator.idleVibrationAmplitude = 0.002f;
        animator.roadRumbleMultiplier = 0.0006f;
        animator.suspensionRocking = 0.25f;

        animator.busBody = busObj.transform.Find("Bus_Body");
        animator.wheelFL = busObj.transform.Find("Wheel_FL");
        animator.wheelFR = busObj.transform.Find("Wheel_FR");
        animator.wheelRL = busObj.transform.Find("Wheel_RL");
        animator.wheelRR = busObj.transform.Find("Wheel_RR");

        // The axle runs across the bus (its local Z). The FBX import changes the wheels' axes from Blender's, so
        // a fixed "up" made the wheels spin flat like turntables; express the axle in the wheel's own space instead.
        // SchoolBusAnimator turns by (distance along the bus's +X) / radius; the model's front is -X, so driving
        // forward is negative travel, and the axle sign below makes that roll the wheels forward.
        if (animator.wheelFL != null)
        {
            Vector3 axle = animator.wheelFL.InverseTransformDirection(-busObj.transform.forward);
            animator.wheelSpinAxis = new Vector3(Mathf.Round(axle.x), Mathf.Round(axle.y), Mathf.Round(axle.z));
        }

        // Add BoxCollider for vehicle volume
        var col = busObj.GetComponent<BoxCollider>();
        if (col == null)
        {
            col = busObj.AddComponent<BoxCollider>();
        }
        col.size = new Vector3(11.0f, 3.4f, 3.3f);
        col.center = new Vector3(0.65f, 1.85f, 0f);
    }
}

