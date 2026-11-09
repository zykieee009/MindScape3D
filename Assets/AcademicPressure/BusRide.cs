using System.Linq;
using Mindscape.Vehicles;
using UnityEngine;

namespace Mindscape.AcademicPressure
{
    // A school bus from the city traffic, borrowed to bring the player home: it is taken off its loop and driven along the
    // traffic lane that passes the bus shelter (the same road the city's cars use), so it stops at the kerb by the shelter.
    public sealed class BusRide
    {
        public Transform bus;
        public Vector3 stop;        // on the lane, beside the shelter
        public Vector3 kerb;        // where the passenger steps off, on the pavement
        public Vector3 heading;     // the way the bus drives
        Vector3[] lane; float[] along; float length, stopAt;
        Quaternion fix;

        public static BusRide Find()
        {
            var shelter = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).FirstOrDefault(r => r.name == "Bus stop | shelter" && r.gameObject.activeInHierarchy);
            var traffic = Object.FindObjectsByType<CityTrafficVehicle>(FindObjectsSortMode.None);
            if (shelter == null || traffic.Length == 0) return null;
            Vector3 at = shelter.bounds.center;
            // The lane that passes closest to the shelter (the near side of the road).
            CityTrafficVehicle best = null; float bestDistance = float.MaxValue; Vector3 bestPoint = default;
            foreach (var v in traffic)
            {
                if (v.path == null || v.path.Length < 2) continue;
                for (int i = 0; i < v.path.Length; i++)
                {
                    Vector3 p = Closest(v.path[i], v.path[(i + 1) % v.path.Length], at);
                    float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(at.x, at.z));
                    if (d < bestDistance) { bestDistance = d; best = v; bestPoint = p; }
                }
            }
            if (best == null || bestDistance > 30) return null;
            var bus = traffic.FirstOrDefault(v => v.name.Contains("SchoolBus")) ?? best;
            var ride = new BusRide { bus = bus.transform, lane = best.path.ToArray(), fix = Quaternion.Inverse(Quaternion.LookRotation(bus.modelForward)) };
            bus.enabled = false;   // off its loop for the ride
            ride.Measure();
            ride.stopAt = ride.DistanceOf(bestPoint);
            ride.stop = ride.PointAt(ride.stopAt, out ride.heading);
            ride.stop.y = bus.transform.position.y;
            Vector3 toShelter = at - ride.stop; toShelter.y = 0;
            ride.kerb = Vector3.Lerp(new Vector3(ride.stop.x, at.y, ride.stop.z), new Vector3(at.x, at.y, at.z), .55f);
            return ride;
        }

        static Vector3 Closest(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a; float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
            return a + ab * t;
        }

        void Measure()
        {
            along = new float[lane.Length + 1];
            for (int i = 0; i < lane.Length; i++) along[i + 1] = along[i] + Vector3.Distance(lane[i], lane[(i + 1) % lane.Length]);
            length = along[lane.Length];
        }

        float DistanceOf(Vector3 point)
        {
            float best = float.MaxValue, at = 0;
            for (int i = 0; i < lane.Length; i++)
            {
                Vector3 p = Closest(lane[i], lane[(i + 1) % lane.Length], point);
                float d = (p - point).sqrMagnitude;
                if (d < best) { best = d; at = along[i] + Vector3.Distance(lane[i], p); }
            }
            return at;
        }

        Vector3 PointAt(float distance, out Vector3 dir)
        {
            distance = Mathf.Repeat(distance, length);
            int i = 0; while (i < lane.Length - 1 && along[i + 1] < distance) i++;
            Vector3 a = lane[i], b = lane[(i + 1) % lane.Length];
            dir = b - a; dir.y = 0; dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
            float seg = along[i + 1] - along[i];
            return Vector3.Lerp(a, b, seg > 1e-4f ? (distance - along[i]) / seg : 0);
        }

        /// <summary>Drives the bus along the lane from 'from' to 'to' (offsets from the stop): slowing into the stop when
        /// 'arriving', speeding away otherwise. 'eachFrame' runs after every move (e.g. to keep a camera on the bus).</summary>
        public System.Collections.IEnumerator Drive(float from, float to, float seconds, bool arriving, System.Action eachFrame = null)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                Place(Mathf.Lerp(from, to, arriving ? Mathf.Sin(k * Mathf.PI * .5f) : k * k));
                eachFrame?.Invoke();
                yield return null;
            }
            Place(to);
            eachFrame?.Invoke();
        }

        /// <summary>Puts the bus 'offset' units along the lane from the stop (negative: before it).</summary>
        public void Place(float offset)
        {
            var p = PointAt(stopAt + offset, out var dir);
            p.y = stop.y;
            bus.SetPositionAndRotation(p, Quaternion.LookRotation(dir) * fix);
            bus.gameObject.SetActive(true);
        }
    }
}
