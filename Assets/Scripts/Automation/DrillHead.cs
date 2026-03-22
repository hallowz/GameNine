using UnityEngine;

namespace Voidborne.Automation
{
    /// <summary>
    /// Visual component attached to the AutoMiner prefab.
    /// When SetActive(true) is called:
    ///   • The drill bit rotates continuously.
    ///   • Rock-debris particle effects play.
    /// When SetActive(false):
    ///   • Rotation stops.
    ///   • Particles stop.
    /// </summary>
    public class DrillHead : MonoBehaviour
    {
        [Header("Drill Bit")]
        [Tooltip("The drill bit transform that rotates when active.")]
        [SerializeField] private Transform drillBit;

        [Tooltip("Rotation speed in degrees per second.")]
        [SerializeField] private float rotationSpeed = 360f;

        [Header("Particles")]
        [Tooltip("Particle system for rock debris when drilling.")]
        [SerializeField] private ParticleSystem rockDebrisParticles;

        [Header("Audio")]
        [SerializeField] private AudioSource drillAudio;
        [SerializeField] private AudioClip   drillLoopClip;

        // ── State ──────────────────────────────────────────────────────────

        private bool _active;

        // ── Unity lifecycle ────────────────────────────────────────────────

        private void Awake()
        {
            if (drillBit == null)
                drillBit = transform;

            // Start inactive.
            SetActive(false);
        }

        private void Update()
        {
            if (!_active) return;
            if (drillBit != null)
                drillBit.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
        }

        // ── Public API ─────────────────────────────────────────────────────

        /// <summary>Activate or deactivate the drill animation and particles.</summary>
        public void SetActive(bool active)
        {
            if (_active == active) return;
            _active = active;

            // Particles.
            if (rockDebrisParticles != null)
            {
                if (active) rockDebrisParticles.Play();
                else        rockDebrisParticles.Stop();
            }

            // Audio.
            if (drillAudio != null)
            {
                if (active)
                {
                    if (drillLoopClip != null && !drillAudio.isPlaying)
                    {
                        drillAudio.clip = drillLoopClip;
                        drillAudio.loop = true;
                        drillAudio.Play();
                    }
                }
                else
                {
                    drillAudio.Stop();
                }
            }
        }

        public bool IsActive => _active;
    }
}
