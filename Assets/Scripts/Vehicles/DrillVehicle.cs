using UnityEngine;
using UnityEngine.InputSystem;
using Voidborne.World.Chunks;

namespace Voidborne.Vehicles
{
    /// <summary>
    /// Drill Frame vehicle. Holding F activates the front-mounted drill, boring through
    /// soft terrain using TerrainDeformer.DeformSphere(). If a TerrainLampArray utility
    /// is installed, a point light illuminates the area while driving.
    /// Low chassis allows navigation through tunnels that larger vehicles cannot enter.
    /// </summary>
    public class DrillVehicle : WheeledVehicle
    {
        [Header("Drill")]
        [SerializeField] private float drillReach          = 2.5f;  // metres in front of pivot
        [SerializeField] private float drillRadius         = 2.0f;  // deformation sphere radius
        [SerializeField] private float drillIntensity      = 1.2f;  // removal intensity per pulse
        [SerializeField] private float drillDensityThreshold = 0.1f; // density < this = solid (drillable)
        [SerializeField] private float drillPulseRate      = 0.15f; // seconds between pulses while held
        [SerializeField] private GameObject drillHeadVisual;         // optional spinning mesh at drill tip

        private Light _lampLight;
        private float _drillCooldown;

        protected override void Awake()
        {
            base.Awake();
            SetupLampArray();
        }

        private void SetupLampArray()
        {
            var utility = Assembly.GetFirstInstalledComponent<UtilityComponent>(AttachmentType.Utility);
            if (utility == null || utility.utilityType != UtilityType.TerrainLampArray) return;

            var lampGo = new GameObject("TerrainLampArray_Light");
            lampGo.transform.SetParent(transform);
            lampGo.transform.localPosition = Vector3.up * 0.5f;

            _lampLight           = lampGo.AddComponent<Light>();
            _lampLight.type      = LightType.Point;
            _lampLight.range     = utility.lampRadius;
            _lampLight.intensity = 3f;
            _lampLight.color     = new Color(1f, 0.95f, 0.8f);
            _lampLight.enabled   = false; // enabled only while being driven
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();

            // Lamp follows driving state
            if (_lampLight != null)
                _lampLight.enabled = IsBeingDriven;

            if (!IsBeingDriven) return;

            _drillCooldown -= Time.fixedDeltaTime;

            bool fHeld = Keyboard.current != null && Keyboard.current.fKey.isPressed;

            if (fHeld && _drillCooldown <= 0f)
            {
                TryDrill();
                _drillCooldown = drillPulseRate;
            }

            // Spin drill head: fast while drilling, slow spin when idle
            if (drillHeadVisual != null)
            {
                float spinSpeed = fHeld ? 720f : 90f;
                drillHeadVisual.transform.Rotate(Vector3.forward, spinSpeed * Time.fixedDeltaTime);
            }
        }

        private void TryDrill()
        {
            Vector3 drillCenter = transform.position + transform.forward * drillReach;
            if (!IsSolidAt(drillCenter)) return;
            TerrainDeformer.DeformSphere(drillCenter, drillRadius, -drillIntensity);
        }

        private bool IsSolidAt(Vector3 worldPos)
        {
            if (ChunkManager.Instance == null) return false;

            Vector3Int chunkPos = ChunkCoordUtility.WorldToChunkPos(worldPos);
            ChunkData  chunk    = ChunkManager.Instance.GetChunk(chunkPos);
            if (chunk == null) return false;

            int lx = Mathf.Clamp(Mathf.FloorToInt(worldPos.x - chunkPos.x * ChunkData.SIZE), 0, ChunkData.SIZE - 1);
            int ly = Mathf.Clamp(Mathf.FloorToInt(worldPos.y - chunkPos.y * ChunkData.SIZE), 0, ChunkData.SIZE - 1);
            int lz = Mathf.Clamp(Mathf.FloorToInt(worldPos.z - chunkPos.z * ChunkData.SIZE), 0, ChunkData.SIZE - 1);

            return chunk.GetDensity(lx, ly, lz) < drillDensityThreshold;
        }
    }
}
