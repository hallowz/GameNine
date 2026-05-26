using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Voidborne.Combat;
using Voidborne.Enemies;

/// <summary>
/// Implements the six Index Device active abilities. Lives on the Player root.
///
/// Hold X (no menu open) — opens radial ability wheel; time slows significantly.
///   Move mouse to aim the direction line toward an ability node.
///   Release X — confirms selection and restores time.
/// Q — fire the currently selected ability.
///
/// Module abilities:
///   0 — The Tether      (3s CD)  : grappling hook — pull player to terrain, tow vehicles.
///   1 — The Shard Index (15s CD) : 6s material overlay, ores/weak-points visible.
///   2 — The Interval    (45s CD) : 4s world slow (30%), player moves at full speed.
///   3 — The Conductor   (charge) : builds charge over 30s; release: electrical bolt arc × 3.
///   4 — The Threshold   (60s CD) : 6s phase-out, pass through thin terrain, silent melee kill.
///   5 — The Fold        (power)  : opens portal to selected pocket dimension.
/// </summary>
[RequireComponent(typeof(IndexDevice))]
public class IndexAbilities : MonoBehaviour
{
    // ---------------------------------------------------------------
    //  Constants
    // ---------------------------------------------------------------

    /// <summary>Cooldown durations in seconds. −1 = not cooldown-based (charge or power cost).</summary>
    private static readonly float[] CooldownDurations = { 3f, 15f, 45f, -1f, 60f, -1f };

    private const float ConductorChargeTime    = 30f;   // seconds to full charge
    private const float ConductorArcDamage     = 60f;
    private const float ConductorDisableDuration = 6f;
    private const int   ConductorArcTargets    = 3;
    private const float ConductorArcRange      = 15f;

    private const float IntervalTimeScale      = 0.3f;
    private const float IntervalDuration       = 4f;    // real seconds

    private const float ThresholdDuration      = 6f;
    private const float ThresholdSilentWindow  = 2f;

    // Module 0 — The Tether (grappling hook)
    private const float TetherRange            = 40f;   // max raycast distance
    private const float TetherPullSpeed        = 18f;   // player pull speed toward anchor
    private const float TetherVehicleForce     = 800f;  // force applied to tow vehicles
    private const float TetherMinDetachDist    = 1.5f;  // auto-detach when this close to anchor

    private const float ShardDuration          = 6f;

    // ---------------------------------------------------------------
    //  Inspector
    // ---------------------------------------------------------------

    [Header("Layers")]
    [SerializeField] private LayerMask enemyLayerMask   = 1 << 8;
    [SerializeField] private LayerMask terrainLayerMask = 1 << 6;

    [Header("Wrist HUD Transform")]
    [Tooltip("Optional: Assign a world-space canvas on the player's wrist for ability readout.")]
    [SerializeField] private Transform wristHUDParent;

    // ---------------------------------------------------------------
    //  Runtime state
    // ---------------------------------------------------------------

    private IndexDevice                 _device;
    private IndexDevice_PocketDimension _pocket;
    private UnityEngine.CharacterController _charController;
    private IndexWheelUI                _wheel;

    // Cached collider buffer for Physics.OverlapSphereNonAlloc (avoids GC allocs)
    private readonly Collider[] _overlapBuffer = new Collider[32];

    // Cooldown timers indexed by module (0–5). Counts down to 0.
    private readonly float[] _cooldownTimers = new float[IndexDevice.ModuleCount];

    // Module 3 — Conductor
    private float _conductorCharge;         // 0 → ConductorChargeTime
    private bool  _conductorFullyCharged;

    // Module 2 — Interval
    private bool  _intervalActive;
    private float _savedWalkSpeed;
    private float _intervalRealTimer;

    // Module 4 — Threshold
    private bool  _thresholdActive;
    private float _thresholdRealTimer;
    private float _thresholdExitTime = -99f;

    // Module 0 — Tether (grappling hook)
    private enum TetherState { Idle, Attached, Detaching }
    private TetherState _tetherState = TetherState.Idle;
    private Vector3     _tetherAnchor;
    private Rigidbody   _tetherVehicleRb;   // non-null when attached to a vehicle
    private LineRenderer _tetherLine;

    // Module 1 — Shard Index
    private bool  _shardActive;
    private float _shardTimer;

    // ---------------------------------------------------------------
    //  Static read-only flags — polled by other systems
    // ---------------------------------------------------------------

    /// <summary>True while The Shard Index overlay is active. Terrain and enemy shaders poll this.</summary>
    public static bool ShardIndexActive { get; private set; }

    /// <summary>True while The Threshold phase-out is active. Melee checks this for silent kill window.</summary>
    public static bool ThresholdActive { get; private set; }

    /// <summary>
    /// Timestamp (Time.time) when The Threshold ended.
    /// Melee checks if Time.time − ThresholdExitTime ≤ 2s for the silent-kill window.
    /// </summary>
    public static float ThresholdExitTime { get; private set; } = -99f;

    // ---------------------------------------------------------------
    //  Lifecycle
    // ---------------------------------------------------------------

    private void Awake()
    {
        _device         = GetComponent<IndexDevice>();
        _pocket         = GetComponent<IndexDevice_PocketDimension>();
        _charController = GetComponent<UnityEngine.CharacterController>();

        // Conductor starts from zero.
        _conductorCharge = 0f;

        // Build radial wheel (creates UI lazily on first frame).
        _wheel = gameObject.AddComponent<IndexWheelUI>();
        _wheel.Initialize(_device, this);
        _wheel.OnModuleSelected += OnWheelModuleSelected;
    }

    // ---------------------------------------------------------------
    //  Update — input polling
    // ---------------------------------------------------------------

    private void Update()
    {
        TickCooldowns();
        TickTether();
        TickConductorCharge();
        TickInterval();
        TickThreshold();
        TickShard();

        HandleInput();
    }

    // ---------------------------------------------------------------
    //  Input
    // ---------------------------------------------------------------

    private void HandleInput()
    {
        if (Keyboard.current == null) return;

        bool anyUIOpen = UIManager.Instance != null && UIManager.Instance.IsAnyUIOpen;

        // X held — open radial ability wheel (blocks if any other UI is open).
        if (Keyboard.current.xKey.wasPressedThisFrame && !anyUIOpen && _device.InstalledCount > 0)
            _wheel.Open();

        // Tick wheel every frame while open.
        if (_wheel.IsOpen)
            _wheel.Tick();

        // X released — confirm selection and close wheel.
        if (Keyboard.current.xKey.wasReleasedThisFrame && _wheel.IsOpen)
        {
            int selected = _wheel.Close();
            if (selected >= 0 && _device.IsModuleInstalled(selected))
                _device.SetActiveModule(selected);
        }

        // Q — fire the active module's ability (wheel must be closed).
        if (Keyboard.current.qKey.wasPressedThisFrame && !_wheel.IsOpen)
        {
            int idx = _device.ActiveModuleIndex;
            if (idx >= 0)
            {
                // Tether toggle: Q again while attached detaches.
                if (idx == 0 && _tetherState == TetherState.Attached)
                    DetachTether();
                else
                    TriggerAbility(idx);
            }
        }

        // Jump also detaches the tether.
        if (_tetherState == TetherState.Attached && Keyboard.current.spaceKey.wasPressedThisFrame)
            DetachTether();
    }

    private void OnWheelModuleSelected(int moduleIndex)
    {
        // Optional: play a selection sound here.
    }

    // ---------------------------------------------------------------
    //  Public ability trigger (also callable from IndexDevice / cinematics)
    // ---------------------------------------------------------------

    public void TriggerAbility(int moduleIndex)
    {
        if (!_device.IsModuleInstalled(moduleIndex)) return;

        switch (moduleIndex)
        {
            case 0: ActivateTether();     break;
            case 1: ActivateShardIndex(); break;
            case 2: ActivateInterval();   break;
            case 3: ActivateConductor();  break;
            case 4: ActivateThreshold();  break;
            case 5: ActivateFold();       break;
        }
    }

    // ---------------------------------------------------------------
    //  Module 0 — The Tether (grappling hook)
    // ---------------------------------------------------------------

    private void ActivateTether()
    {
        if (_cooldownTimers[0] > 0f) return;
        if (_tetherState != TetherState.Idle) return;

        // Raycast from camera forward.
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, TetherRange))
        {
            Debug.Log("[IndexAbilities] Tether: no target in range.");
            return;
        }

        _tetherAnchor = hit.point;
        _tetherVehicleRb = null;

        // Check if we hit a vehicle (has Rigidbody on self or parent).
        Rigidbody vehicleRb = hit.collider.GetComponentInParent<Rigidbody>();
        if (vehicleRb != null)
        {
            _tetherVehicleRb = vehicleRb;
            Debug.Log("[IndexAbilities] Tether attached to vehicle.");
        }
        else
        {
            Debug.Log($"[IndexAbilities] Tether attached to terrain at {hit.point}");
        }

        _tetherState = TetherState.Attached;

        // Create or enable line renderer for cable visual.
        EnsureTetherLine();
        _tetherLine.enabled = true;
    }

    private void TickTether()
    {
        if (_tetherState != TetherState.Attached) return;

        // Update line renderer.
        if (_tetherLine != null)
        {
            Vector3 handPos = transform.position + transform.up * 0.8f + transform.right * -0.2f;
            _tetherLine.SetPosition(0, handPos);

            if (_tetherVehicleRb != null)
                _tetherAnchor = _tetherVehicleRb.worldCenterOfMass;

            _tetherLine.SetPosition(1, _tetherAnchor);
        }

        if (_tetherVehicleRb != null)
        {
            // Vehicle tow: pull vehicle toward player.
            Vector3 pullDir = (transform.position - _tetherVehicleRb.worldCenterOfMass).normalized;
            _tetherVehicleRb.AddForce(pullDir * TetherVehicleForce * Time.deltaTime, ForceMode.Force);
        }
        else
        {
            // Terrain pull: move player toward anchor.
            Vector3 toAnchor = _tetherAnchor - transform.position;
            float dist = toAnchor.magnitude;

            if (dist < TetherMinDetachDist)
            {
                DetachTether();
                return;
            }

            Vector3 pullVelocity = toAnchor.normalized * TetherPullSpeed;

            // Apply via CharacterController.
            if (_charController != null)
                _charController.Move(pullVelocity * Time.deltaTime);
        }
    }

    private void DetachTether()
    {
        if (_tetherState == TetherState.Idle) return;

        _tetherState = TetherState.Idle;
        _tetherVehicleRb = null;

        if (_tetherLine != null)
            _tetherLine.enabled = false;

        _cooldownTimers[0] = CooldownDurations[0];
        Debug.Log("[IndexAbilities] Tether detached.");
    }

    private void EnsureTetherLine()
    {
        if (_tetherLine != null) return;

        _tetherLine = gameObject.AddComponent<LineRenderer>();
        _tetherLine.positionCount = 2;
        _tetherLine.startWidth = 0.02f;
        _tetherLine.endWidth = 0.015f;
        _tetherLine.material = new Material(Shader.Find("Sprites/Default"));
        _tetherLine.startColor = new Color(0.3f, 0.75f, 0.85f, 0.9f);
        _tetherLine.endColor = new Color(0.2f, 0.5f, 0.6f, 0.7f);
        _tetherLine.enabled = false;
    }

    /// <summary>True while the Tether is attached to something.</summary>
    public bool IsTetherActive => _tetherState == TetherState.Attached;

    // ---------------------------------------------------------------
    //  Module 1 — The Shard Index
    // ---------------------------------------------------------------

    private void ActivateShardIndex()
    {
        if (_cooldownTimers[1] > 0f) return;

        _shardActive   = true;
        ShardIndexActive = true;
        _shardTimer    = ShardDuration;

        _cooldownTimers[1] = CooldownDurations[1];
        Debug.Log("[IndexAbilities] The Shard Index active.");
    }

    private void TickShard()
    {
        if (!_shardActive) return;
        _shardTimer -= Time.deltaTime;
        if (_shardTimer <= 0f)
        {
            _shardActive     = false;
            ShardIndexActive = false;
        }
    }

    // ---------------------------------------------------------------
    //  Module 2 — The Interval
    // ---------------------------------------------------------------

    private void ActivateInterval()
    {
        if (_cooldownTimers[2] > 0f) return;
        if (_intervalActive) return;

        _intervalActive    = true;
        _intervalRealTimer = 0f;
        Time.timeScale     = IntervalTimeScale;
        Time.fixedDeltaTime = 0.02f * IntervalTimeScale;

        // Boost player speed so they move at full speed despite the slow time scale.
        var fpc = GetComponent<Voidborne.Player.FirstPersonController>();
        if (fpc != null)
        {
            _savedWalkSpeed         = fpc.WalkSpeed;
            fpc.ExternalSpeedMult   = 1f / IntervalTimeScale;
        }

        _cooldownTimers[2] = CooldownDurations[2];
        Debug.Log("[IndexAbilities] The Interval active.");
    }

    private void TickInterval()
    {
        if (!_intervalActive) return;

        _intervalRealTimer += Time.unscaledDeltaTime;
        if (_intervalRealTimer >= IntervalDuration)
            EndInterval();
    }

    private void EndInterval()
    {
        _intervalActive     = false;
        Time.timeScale      = 1f;
        Time.fixedDeltaTime = 0.02f;

        var fpc = GetComponent<Voidborne.Player.FirstPersonController>();
        if (fpc != null)
            fpc.ExternalSpeedMult = 1f;
    }

    /// <summary>Taking damage while Interval is active cancels it immediately.</summary>
    public void OnPlayerTookDamage()
    {
        if (_intervalActive) EndInterval();
    }

    // ---------------------------------------------------------------
    //  Module 3 — The Conductor
    // ---------------------------------------------------------------

    private void TickConductorCharge()
    {
        if (!_device.IsModuleInstalled(3)) return;
        if (_conductorFullyCharged) return;

        _conductorCharge += Time.deltaTime;
        if (_conductorCharge >= ConductorChargeTime)
        {
            _conductorCharge      = ConductorChargeTime;
            _conductorFullyCharged = true;
            Debug.Log("[IndexAbilities] Conductor fully charged.");
        }
    }

    private void ActivateConductor()
    {
        if (!_conductorFullyCharged) return;

        // Find up to 3 nearby enemies and arc to them.
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, ConductorArcRange, _overlapBuffer, enemyLayerMask);
        int arcs = 0;

        for (int i = 0; i < hitCount; i++)
        {
            if (arcs >= ConductorArcTargets) break;
            if (_overlapBuffer[i].TryGetComponent(out EnemyEntity enemy))
            {
                enemy.TakeDamage(new DamageInfo { Amount = ConductorArcDamage, Type = DamageType.Generic });
                enemy.DisableMechanical(ConductorDisableDuration);
                arcs++;
                // TODO (art): draw arc VFX from player to enemy.
            }
        }

        _conductorCharge       = 0f;
        _conductorFullyCharged = false;
        Debug.Log($"[IndexAbilities] Conductor discharged — hit {arcs} targets.");
    }

    /// <summary>Called by melee system when the player strikes with a fully-charged Conductor.</summary>
    public bool TryMeleeDischarge(EnemyEntity target)
    {
        if (!_conductorFullyCharged) return false;
        if (!_device.IsModuleInstalled(3)) return false;

        target.TakeDamage(new DamageInfo { Amount = ConductorArcDamage, Type = DamageType.Generic });
        target.DisableMechanical(ConductorDisableDuration);

        _conductorCharge       = 0f;
        _conductorFullyCharged = false;
        Debug.Log("[IndexAbilities] Conductor discharged on melee contact.");
        return true;
    }

    /// <summary>Returns charge ratio 0–1 for the HUD charge bar.</summary>
    public float GetConductorChargeRatio() =>
        _conductorCharge / ConductorChargeTime;

    // ---------------------------------------------------------------
    //  Module 4 — The Threshold
    // ---------------------------------------------------------------

    private void ActivateThreshold()
    {
        if (_cooldownTimers[4] > 0f) return;
        if (_thresholdActive) return;

        _thresholdActive    = true;
        ThresholdActive     = true;
        _thresholdRealTimer = 0f;

        // Disable player collision with terrain so they can pass through ≤2-block-thick surfaces.
        Physics.IgnoreLayerCollision(gameObject.layer, LayerMaskToLayer(terrainLayerMask), true);

        // TODO (art/audio): play phase-out VFX/SFX, reduce player visibility.

        _cooldownTimers[4] = CooldownDurations[4];
        Debug.Log("[IndexAbilities] The Threshold active.");
    }

    private void TickThreshold()
    {
        if (!_thresholdActive) return;

        _thresholdRealTimer += Time.unscaledDeltaTime;
        if (_thresholdRealTimer >= ThresholdDuration)
            EndThreshold();
    }

    private void EndThreshold()
    {
        _thresholdActive   = false;
        ThresholdActive    = false;
        ThresholdExitTime  = Time.time;

        Physics.IgnoreLayerCollision(gameObject.layer, LayerMaskToLayer(terrainLayerMask), false);

        Debug.Log("[IndexAbilities] The Threshold ended.");
    }

    /// <summary>
    /// Returns true if a melee kill should be completely silent (no nearby enemy alert).
    /// Condition: within 2s of The Threshold ending.
    /// </summary>
    public static bool IsSilentKillWindow() =>
        Time.time - ThresholdExitTime <= ThresholdSilentWindow;

    // ---------------------------------------------------------------
    //  Module 5 — The Fold
    // ---------------------------------------------------------------

    private void ActivateFold()
    {
        if (_pocket == null)
        {
            Debug.LogWarning("[IndexAbilities] IndexDevice_PocketDimension not found.");
            return;
        }
        _pocket.TryOpenPortal();
    }

    // ---------------------------------------------------------------
    //  Cooldown ticks
    // ---------------------------------------------------------------

    private void TickCooldowns()
    {
        for (int i = 0; i < IndexDevice.ModuleCount; i++)
        {
            if (CooldownDurations[i] > 0f && _cooldownTimers[i] > 0f)
                _cooldownTimers[i] -= Time.deltaTime;
        }
    }

    // ---------------------------------------------------------------
    //  HUD queries
    // ---------------------------------------------------------------

    /// <summary>Returns remaining cooldown ratio (0 = ready, 1 = just fired) for HUD.</summary>
    public float GetCooldownRatio(int moduleIndex)
    {
        if (moduleIndex < 0 || moduleIndex >= IndexDevice.ModuleCount) return 0f;
        float dur = CooldownDurations[moduleIndex];
        if (dur <= 0f) return 0f;
        return Mathf.Clamp01(_cooldownTimers[moduleIndex] / dur);
    }

    public bool IsOnCooldown(int moduleIndex)
    {
        if (moduleIndex < 0 || moduleIndex >= IndexDevice.ModuleCount) return false;
        return _cooldownTimers[moduleIndex] > 0f;
    }

    // ---------------------------------------------------------------
    //  Helpers
    // ---------------------------------------------------------------

    private static int LayerMaskToLayer(LayerMask mask)
    {
        int val = mask.value;
        for (int i = 0; i < 32; i++)
            if ((val & (1 << i)) != 0) return i;
        return 0;
    }
}
