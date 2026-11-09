using UnityEngine;

namespace Mindscape
{
    /// <summary>
    /// Controls switching between Fixed (neatly made) and Messy (unmade) visual modes of the bed.
    /// Supports runtime inspection, cutscenes, chores, and editor preview.
    /// </summary>
    [ExecuteAlways]
    public class BedModeController : MonoBehaviour
    {
        public enum BedMode
        {
            Fixed,
            Messy
        }

        [Tooltip("The GameObject representing the neatly made bedding (duvet and centered pillow).")]
        [SerializeField] private GameObject fixedBed;

        [Tooltip("The GameObject representing the unmade rumpled bedding (rumpled duvet and askew pillow).")]
        [SerializeField] private GameObject messyBed;

        [Tooltip("Active visual mode.")]
        [SerializeField] private BedMode currentMode = BedMode.Fixed;

        public BedMode CurrentMode => currentMode;
        public GameObject FixedBed => fixedBed;
        public GameObject MessyBed => messyBed;

        private void Awake()
        {
            ApplyMode(currentMode);
        }

        public void SetMode(BedMode mode)
        {
            currentMode = mode;
            ApplyMode(mode);
        }

        public void SetFixed() => SetMode(BedMode.Fixed);
        public void SetMessy() => SetMode(BedMode.Messy);

        private void ApplyMode(BedMode mode)
        {
            if (fixedBed != null) fixedBed.SetActive(mode == BedMode.Fixed);
            if (messyBed != null) messyBed.SetActive(mode == BedMode.Messy);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplyMode(currentMode);
        }
#endif
    }
}

