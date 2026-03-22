using UnityEngine;

namespace Voidborne.World.Decoration
{
    /// <summary>
    /// W2.4 — Global wind parameters consumed by GrassBladeShader.
    /// Assign a single instance to GrassRenderer. No per-blade wind state.
    /// </summary>
    [CreateAssetMenu(fileName = "GrassWindSettings", menuName = "Voidborne/Grass Wind Settings")]
    public class GrassWindSettings : ScriptableObject
    {
        [Header("Wind")]
        [Tooltip("Primary wind direction (XZ). Normalised automatically.")]
        public Vector2 windDirection = new Vector2(1f, 0.3f);

        [Range(0f, 5f)]
        [Tooltip("Wind oscillation speed multiplier.")]
        public float windSpeed = 1.2f;

        [Range(0f, 1f)]
        [Tooltip("Spatial frequency of wind variation (higher = more turbulent).")]
        public float windFrequency = 0.08f;

        [Range(0f, 0.6f)]
        [Tooltip("Maximum displacement amplitude at blade tip.")]
        public float windAmplitude = 0.18f;
    }
}
