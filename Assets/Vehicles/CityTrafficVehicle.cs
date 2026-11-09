using UnityEngine;

namespace Mindscape.Vehicles
{
    /// <summary>
    /// A vehicle that drives round a closed loop of road points forever. The model's own clips (wheel rolls, body vibration)
    /// loop alongside: they are sampled every frame, the wheel clips at the speed that matches the distance driven so
    /// the tyres do not slip, the body clip in real time.
    /// </summary>
    [DisallowMultipleComponent]
    public class CityTrafficVehicle : MonoBehaviour
    {
        [Tooltip("The loop to drive, in world space (closed: the last point joins the first).")]
        public Vector3[] path;
        [Tooltip("Driving speed in world units per second.")]
        public float speed = 18f;
        [Tooltip("Where on the loop (world units from the first point) the vehicle currently is.")]
        public float distance;
        [Tooltip("The model's front, in its own space. The vehicle models import facing -X.")]
        public Vector3 modelForward = Vector3.left;
        [Tooltip("The model's looping clips: one wheel roll per wheel plus the body vibration.")]
        public AnimationClip[] clips;
        [Tooltip("Speed in model units per second at which the wheel-roll clips turn the tyres without slipping (2 turns per loop).")]
        public float modelRollSpeed = 2.5f;

        float[] cumulative;
        float length, wheelClock, bodyClock;
        GameObject animationRoot;
        Quaternion fix;

        void Awake() => Prepare();

        void Prepare()
        {
            if (path == null || path.Length < 2) return;
            cumulative = new float[path.Length + 1];
            for (int i = 0; i < path.Length; i++)
                cumulative[i + 1] = cumulative[i] + Vector3.Distance(path[i], path[(i + 1) % path.Length]);
            length = cumulative[path.Length];
            var animator = GetComponentInChildren<Animator>(true);
            animationRoot = animator != null ? animator.gameObject : gameObject;
            fix = Quaternion.Inverse(Quaternion.LookRotation(modelForward));
        }

        /// <summary>Puts the vehicle at the given distance along the loop, facing the way it drives.</summary>
        public void PlaceAt(float at)
        {
            if (cumulative == null) Prepare();
            if (cumulative == null || length <= 0f) return;
            distance = Mathf.Repeat(at, length);
            int lo = 0, hi = path.Length;
            while (hi - lo > 1) { int mid = (lo + hi) / 2; if (cumulative[mid] <= distance) lo = mid; else hi = mid; }
            float segment = cumulative[lo + 1] - cumulative[lo];
            Vector3 a = path[lo], b = path[(lo + 1) % path.Length];
            float t = segment > 1e-4f ? (distance - cumulative[lo]) / segment : 0f;
            Vector3 heading = b - a; heading.y = 0;
            transform.position = Vector3.Lerp(a, b, t);
            if (heading.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(heading.normalized) * fix;
        }

        void Update()
        {
            if (cumulative == null) Prepare();
            if (cumulative == null || length <= 0f) return;
            float dt = Time.deltaTime;
            if (Blocked()) return;   // waits behind a story bus (wheels and body stop with it)
            PlaceAt(distance + speed * dt);
            wheelClock += dt * speed / Mathf.Max(0.01f, transform.lossyScale.x) / Mathf.Max(0.01f, modelRollSpeed);
            bodyClock += dt;
        }

        // A story bus (TrafficObstacle) a few metres ahead in this lane stops the car until it has gone. A car that is
        // already overlapping one when it appears drives on out of it.
        bool Blocked()
        {
            if (TrafficObstacle.Active.Count == 0) return false;
            Vector3 heading = transform.rotation * Quaternion.Inverse(fix) * Vector3.forward; heading.y = 0;
            if (heading.sqrMagnitude < 1e-6f) return false;
            heading.Normalize();
            foreach (var obstacle in TrafficObstacle.Active)
            {
                var b = obstacle.Bounds; b.Expand(new Vector3(1.2f, 100f, 1.2f));
                if (b.Contains(transform.position)) continue;
                for (float ahead = 2.5f; ahead <= 8f; ahead += 1.5f)
                    if (b.Contains(transform.position + heading * ahead)) return true;
            }
            return false;
        }

        void LateUpdate()
        {
            if (clips == null || animationRoot == null) return;
            foreach (var clip in clips)
            {
                if (clip == null || clip.length <= 0f) continue;
                bool body = clip.name.IndexOf("Vib", System.StringComparison.OrdinalIgnoreCase) >= 0;
                clip.SampleAnimation(animationRoot, Mathf.Repeat(body ? bodyClock : wheelClock, clip.length));
            }
        }
    }
}
