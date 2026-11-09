using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mindscape.Vehicles
{
    /// <summary>
    /// Something city traffic (CityTrafficVehicle) waits behind instead of driving through: the story's school buses,
    /// which pull in at stops on the same road. Added to every story bus in a scene when it loads; while the bus is
    /// switched on it is in Active.
    /// </summary>
    public sealed class TrafficObstacle : MonoBehaviour
    {
        public static readonly List<TrafficObstacle> Active = new List<TrafficObstacle>();
        Renderer[] renderers;

        void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        void OnDisable() => Active.Remove(this);

        public Bounds Bounds
        {
            get
            {
                if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>(true);
                var b = new Bounds(transform.position, Vector3.zero);
                foreach (var r in renderers) if (r != null && r.enabled) b.Encapsulate(r.bounds);
                return b;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= Mark;
            SceneManager.sceneLoaded += Mark;
        }

        // "School bus", "Arrival bus": the story's buses (the traffic's own buses have CityTrafficVehicle).
        static bool IsBus(Transform t) => t.name.EndsWith(" bus", System.StringComparison.OrdinalIgnoreCase) || t.name.StartsWith("School bus", System.StringComparison.Ordinal);

        static void Mark(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (IsBus(t) && t.GetComponent<TrafficObstacle>() == null && t.GetComponentInParent<CityTrafficVehicle>(true) == null
                        && (t.parent == null || !IsBus(t.parent)))
                        t.gameObject.AddComponent<TrafficObstacle>();
        }
    }
}
