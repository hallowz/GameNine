using UnityEngine;
using UnityEngine.InputSystem;
using Voidborne.Diagnostics;
using Voidborne.World.Chunks;

namespace Voidborne.Player
{
    /// <summary>
    /// Handles terrain deformation input on the player.
    /// Left click (Attack action) removes terrain (digs).
    /// Right click (AltFire action) adds terrain (builds).
    /// Raycasts from the camera to find the deformation point.
    /// </summary>
    public class PlayerTerrainInteraction : MonoBehaviour
    {
        [Header("Global Toggle")]
        [Tooltip("When false, all terrain editing input is ignored. Disable while guns are equipped.")]
        [SerializeField] private bool terrainEditingEnabled = false;

        [Header("Deformation Settings")]
        [Tooltip("Radius of the terrain deformation sphere in world units.")]
        [SerializeField] private float deformRadius = 2.5f;

        [Tooltip("Intensity of terrain deformation per click. Higher = more material added/removed.")]
        [SerializeField] private float deformIntensity = 1.0f;

        [Header("Raycast Settings")]
        [Tooltip("Maximum distance for terrain interaction raycast.")]
        [SerializeField] private float maxRaycastDistance = 50f;

        [Header("Continuous Editing")]
        [Tooltip("Seconds between deformation pulses while holding the button. Lower = faster editing.")]
        [SerializeField] private float editCooldown = 0.05f;

        [Header("Visual Feedback")]
        [Tooltip("Show a wireframe sphere gizmo at the deformation point in the Scene view.")]
        [SerializeField] private bool showGizmo = true;

        /// <summary>
        /// When true, left-click (dig/attack) input is handled by another component
        /// (e.g., PlayerMining) and this script will skip its own left-click path.
        /// Set to true in PlayerMining.Awake().
        /// </summary>
        public bool OverrideLeftClick { get; set; } = false;

        private InputSystem_Actions inputActions;
        private InputAction attackAction;
        private InputAction altFireAction;
        private Camera playerCamera;

        // Last hit point for gizmo visualization
        private Vector3 lastHitPoint;
        private bool hasHitPoint;

        private float lastEditTime = float.MinValue;

        private void Awake()
        {
            // Find the camera — look for a child camera first, fall back to Camera.main
            playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }
        }

        private void OnEnable()
        {
            inputActions = new InputSystem_Actions();
            inputActions.Player.Enable();

            // Attack = left click (dig)
            attackAction = inputActions.Player.Attack;
            attackAction.performed += OnAttack;

            // AltFire = right click (build) — use FindAction on the asset since the
            // auto-generated C# wrapper may not have the AltFire property yet
            altFireAction = inputActions.asset.FindAction("Player/AltFire");
            if (altFireAction != null)
            {
                altFireAction.performed += OnAltFire;
            }
            else
            {
                Debug.LogWarning("[PlayerTerrainInteraction] AltFire action not found in Player action map. " +
                    "Right-click building will not work. Re-import InputSystem_Actions.inputactions in Unity.");
            }
        }

        private void OnDisable()
        {
            if (inputActions != null)
            {
                if (attackAction != null)
                {
                    attackAction.performed -= OnAttack;
                }

                if (altFireAction != null)
                {
                    altFireAction.performed -= OnAltFire;
                }

                inputActions.Player.Disable();
                inputActions.Dispose();
                inputActions = null;
            }
        }

        private static readonly RuntimeProfiler.Token s_prof = RuntimeProfiler.Register("TerrainInteract.Update");

        private void Update()
        {
            RuntimeProfiler.Begin(s_prof);
            if (!terrainEditingEnabled) { RuntimeProfiler.End(s_prof); return; }

            // Update gizmo target — raycast every frame to show preview
            if (showGizmo && playerCamera != null)
            {
                Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
                if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance))
                {
                    lastHitPoint = hit.point;
                    hasHitPoint = true;
                }
                else
                {
                    hasHitPoint = false;
                }
            }

            // Hold-to-dig/build: fire continuously while button is held, throttled by editCooldown.
            if (Time.time - lastEditTime < editCooldown) return;

            // Left-click dig is suppressed when PlayerMining is handling it.
            bool digging  = !OverrideLeftClick && attackAction  != null && attackAction.IsPressed();
            bool building = altFireAction != null && altFireAction.IsPressed();

            if (digging || building)
            {
                PerformDeformation(digging ? -deformIntensity : deformIntensity);
                lastEditTime = Time.time;
            }
            RuntimeProfiler.End(s_prof);
        }

        /// <summary>
        /// Left click: Dig terrain (remove material).
        /// Suppressed when terrainEditingEnabled is false or OverrideLeftClick is true.
        /// </summary>
        private void OnAttack(InputAction.CallbackContext ctx)
        {
            if (!terrainEditingEnabled) return;
            if (OverrideLeftClick) return;
            PerformDeformation(-deformIntensity);
            lastEditTime = Time.time;
        }

        /// <summary>
        /// Right click: Build terrain (add material).
        /// </summary>
        private void OnAltFire(InputAction.CallbackContext ctx)
        {
            if (!terrainEditingEnabled) return;
            PerformDeformation(deformIntensity);
            lastEditTime = Time.time;
        }

        /// <summary>
        /// Performs a raycast from the camera and deforms terrain at the hit point.
        /// </summary>
        private void PerformDeformation(float intensity)
        {
            if (playerCamera == null)
                return;

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance))
            {
                // Offset the deformation center slightly along the ray direction
                // for digging (into terrain) vs building (away from terrain)
                Vector3 deformCenter;
                if (intensity < 0f)
                {
                    // Dig: push the center slightly into the terrain
                    deformCenter = hit.point + ray.direction * 0.1f;
                }
                else
                {
                    // Build: center right at the surface
                    deformCenter = hit.point - ray.direction * 0.1f;
                }

                TerrainDeformer.DeformSphere(deformCenter, deformRadius, intensity);
            }
        }


        private void OnDrawGizmos()
        {
            if (terrainEditingEnabled && showGizmo && hasHitPoint)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
                Gizmos.DrawWireSphere(lastHitPoint, deformRadius);
            }
        }
    }
}
