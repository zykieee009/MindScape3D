using UnityEngine;

namespace Mindscape.Vehicles
{
    /// <summary>
    /// Animates the School Bus:
    /// 1. Rolls wheels continuously based on translation speed (or simulated driving speed).
    /// 2. Applies subtle engine vibration and suspension rocking to the chassis.
    /// Works automatically in runtime coroutines (e.g. EpisodeTwoStory and HouseEpisode) and in editor preview.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class SchoolBusAnimator : MonoBehaviour
    {
        [Header("Wheel Configuration")]
        [Tooltip("Wheel radius in meters. Standard school bus wheel is ~0.70m.")]
        public float wheelRadius = 0.70f;
        
        [Tooltip("Local spin axis for wheels. Default is (0, 1, 0) for imported Blender axis.")]
        public Vector3 wheelSpinAxis = Vector3.up;

        [Tooltip("Transform of front-left wheel.")]
        public Transform wheelFL;
        [Tooltip("Transform of front-right wheel.")]
        public Transform wheelFR;
        [Tooltip("Transform of rear-left wheel.")]
        public Transform wheelRL;
        [Tooltip("Transform of rear-right wheel.")]
        public Transform wheelRR;

        [Header("Chassis & Vibration")]
        [Tooltip("Transform of bus body/chassis for engine vibration.")]
        public Transform busBody;

        [Tooltip("Frequency of idle engine vibration in Hz.")]
        public float idleVibrationFrequency = 22f;

        [Tooltip("Amplitude of idle engine vibration in meters (e.g. 0.002 = 2mm).")]
        public float idleVibrationAmplitude = 0.002f;

        [Tooltip("Speed multiplier for road rumble vibration.")]
        public float roadRumbleMultiplier = 0.0006f;

        [Tooltip("Suspension pitch/roll rocking intensity in degrees.")]
        public float suspensionRocking = 0.25f;

        [Header("Driving Simulation Mode")]
        [Tooltip("Force continuous driving animation even if bus transform is stationary.")]
        public bool simulateDriving = false;

        [Tooltip("Simulated driving speed in meters per second.")]
        public float simulatedSpeed = 6.0f;

        // Internal state
        private Vector3 lastPosition;
        private Vector3 initialBodyLocalPos;
        private Quaternion initialBodyLocalRot;
        private float currentSpeed;
        private float vibrationTimer;

        void Awake()
        {
            AutoAssignReferences();
        }

        void Start()
        {
            lastPosition = transform.position;
            if (busBody != null)
            {
                initialBodyLocalPos = busBody.localPosition;
                initialBodyLocalRot = busBody.localRotation;
            }
        }

        void OnEnable()
        {
            lastPosition = transform.position;
            AutoAssignReferences();
        }

        public void AutoAssignReferences()
        {
            if (busBody == null)
            {
                var body = transform.Find("Bus_Body");
                if (body != null) busBody = body;
            }

            if (wheelFL == null) wheelFL = FindWheel("Wheel_FL");
            if (wheelFR == null) wheelFR = FindWheel("Wheel_FR");
            if (wheelRL == null) wheelRL = FindWheel("Wheel_RL");
            if (wheelRR == null) wheelRR = FindWheel("Wheel_RR");

            if (busBody != null && initialBodyLocalPos == Vector3.zero && initialBodyLocalRot == Quaternion.identity)
            {
                initialBodyLocalPos = busBody.localPosition;
                initialBodyLocalRot = busBody.localRotation;
            }
        }

        private Transform FindWheel(string name)
        {
            var direct = transform.Find(name);
            if (direct != null) return direct;

            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                    return child;
            }
            return null;
        }

        void Update()
        {
            float dt = Application.isPlaying ? Time.deltaTime : 0.0333f;
            if (dt <= 0f) return;

            // 1. Calculate speed
            float deltaDistance = 0f;
            if (simulateDriving)
            {
                currentSpeed = simulatedSpeed;
                deltaDistance = currentSpeed * dt;
            }
            else
            {
                Vector3 deltaPos = transform.position - lastPosition;
                // Forward direction is +X in the bus's local space (transform.right)
                float forwardTravel = Vector3.Dot(deltaPos, transform.right);
                currentSpeed = Mathf.Abs(forwardTravel) / dt;
                deltaDistance = forwardTravel;
                lastPosition = transform.position;
            }

            // 2. Rotate Wheels
            if (Mathf.Abs(deltaDistance) > 0.00001f)
            {
                // Angle in degrees: distance / r * (180 / pi)
                // wheelRadius is at the model's own size; a scaled-up bus has proportionally bigger wheels.
                float degrees = (deltaDistance / Mathf.Max(0.1f, wheelRadius * transform.lossyScale.x)) * Mathf.Rad2Deg;

                RotateWheel(wheelFL, degrees);
                RotateWheel(wheelFR, degrees);
                RotateWheel(wheelRL, degrees);
                RotateWheel(wheelRR, degrees);
            }

            // 3. Chassis Vibration & Suspension Rocking
            if (busBody != null)
            {
                vibrationTimer += dt;

                // High-frequency engine idle vibration
                float vibFreq = idleVibrationFrequency + (currentSpeed * 2f);
                float vibAmp = idleVibrationAmplitude + (currentSpeed * roadRumbleMultiplier);
                float yOffset = Mathf.Sin(vibrationTimer * vibFreq * Mathf.PI * 2f) * vibAmp;
                float xOffset = Mathf.Cos(vibrationTimer * (vibFreq * 0.7f) * Mathf.PI * 2f) * (vibAmp * 0.4f);

                // Subtle road suspension sway (swaying left-to-right and pitch rocking)
                float swayTimer = vibrationTimer * (1.5f + currentSpeed * 0.1f);
                float rollAngle = Mathf.Sin(swayTimer) * suspensionRocking * (0.2f + Mathf.Clamp01(currentSpeed / 10f) * 0.8f);
                float pitchAngle = Mathf.Cos(swayTimer * 0.8f) * suspensionRocking * 0.4f * (0.2f + Mathf.Clamp01(currentSpeed / 10f) * 0.8f);

                busBody.localPosition = initialBodyLocalPos + new Vector3(xOffset, yOffset, 0f);
                busBody.localRotation = initialBodyLocalRot * Quaternion.Euler(pitchAngle, 0f, rollAngle);
            }
        }

        private void RotateWheel(Transform wheel, float degrees)
        {
            if (wheel == null) return;
            // Roll forward around the local wheel spin axis
            wheel.Rotate(wheelSpinAxis, degrees, Space.Self);
        }
    }
}

