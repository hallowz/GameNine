using System.Collections;
using UnityEngine;
using Voidborne.Combat;
using Voidborne.Enemies;

/// <summary>
/// Implements the six Cortex Device active abilities. Lives on the Player root.
///
/// Hold X (no menu open) — opens radial ability wheel; time slows significantly.
///   Move mouse to aim the direction line toward an ability node.
///   Release X — confirms selection and restores time.
/// Q — fire the currently selected ability.
///
/// Module abilities:
///   0 — The Pulse       (8s CD)  : directional shockwave staggers enemies, shatters terrain.
///   1 — The Shard Index (15s CD) : 6s material overlay, ores/weak-points visible.
///   2 — The Interval    (45s CD) : 4s world slow (30%), player moves at full speed.
///   3 — The Conductor   (charge) : builds charge over 30s; release: electrical bolt arc × 3.
///   4 — The Threshold   (60s CD) : 6s phase-out, pass through thin terrain, silent melee kill.
///   5 — The Fold        (power)  : opens portal to selected pocket dimension.
/// </summary>
[RequireComponent(typeof(CortexDevice))]
public class CortexAbilities : MonoBehaviour
{
    // ---------------------------------------------------------------
    //  Constants
    // ---------------------------------------------------------------

    /// <summary>Cooldown durations in seconds. −1 = not cooldown-based (charge or power cost).</summary>
    private static readonly float[] CooldownDurations = { 8f, 15f, 45f, -1f, 60f, -1f };

    private const float ConductorChargeTime    = 30f;   // seconds to full charge
    private const float ConductorArcDamage     = 60f;
    private const float ConductorDisableDuration = 6f;
    private const int   ConductorArcTargets    = 3;
    private const float ConductorArcRange      = 15f;

    private const float IntervalTimeScale      = 0.3f;
    private const float IntervalDuration       = 4f;    // real seconds

    private const float ThresholdDuration      = 6f;
    private const float ThresholdSilentWindow  = 2f;

    private const float PulseRange             = 8f;
    private const float PulseHalfAngle         = 45f;   // degrees, cone half-angle
    private const float PulseDamage            = 25f;
    private const float PulseStaggerDuration   = 2f;

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

    private CortexDevice                 _device;
    private CortexDevice_PocketDimension _pocket;
    private UnityEngine.CharacterController _charController;
    private CortexWheelUI                _wheel;

    // Cooldown timers indexed by module (0–5). Counts down to 0.
    private readonly float[] _cooldownTimers = new float[CortexDevice.ModuleCount];

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
        _device         = GetComponent<CortexDevice>();
        _pocket         = GetComponent<CortexDevice_PocketDimension>();
        _charController = GetComponent<UnityEngine.CharacterController>();

        // Conductor starts from zero.
        _conductorCharge = 0f;

        // Build radial wheel (creates UI lazily on first frame).
        _wheel = gameObject.AddComponent<CortexWheelUI>();
        _wheel.Initialize(_device, this);
        _wheel.OnModuleSelected += OnWheelModuleSelected;
    }

    // ---------------------------------------------------------------
    //  Update — input polling
    // ---------------------------------------------------------------

    private void Update()
    {
        TickCooldowns();
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
        bool anyUIOpen = UIManager.Instance != null && UIManager.Instance.IsAnyUIOpen;

        // Tab held — open radial ability wheel (blocks if any other UI is open).
        if (Input.GetKeyDown(KeyCode.X) && !anyUIOpen && _device.InstalledCount > 0)
            _wheel.Open();

        // Tick wheel every frame while open.
        if (_wheel.IsOpen)
            _wheel.Tick();

        // X released — confirm selection and close wheel.
        if (Input.GetKeyUp(KeyCode.X) && _wheel.IsOpen)
        {
            int selected = _wheel.Close();
            if (selected >= 0 && _device.IsModuleInstalled(selected))
                _device.SetActiveModule(selected);
        }

        // Q — fire the active module's ability (wheel must be closed).
        if (Input.GetKeyDown(KeyCode.Q) && !_wheel.IsOpen)
        {
            int idx = _device.ActiveModuleIndex;
            if (idx >= 0)
                TriggerAbility(idx);
        }
    }

    private void OnWheelModuleSelected(int moduleIndex)
    {
        // Optional: play a selection sound here.
    }

    // ---------------------------------------------------------------
    //  Public ability trigger (also callable from CortexDevice / cinematics)
    // ---------------------------------------------------------------

    public void TriggerAbility(int moduleIndex)
    {
        if (!_device.IsModuleInstalled(moduleIndex)) return;

        switch (moduleIndex)
        {
            case 0: ActivatePulse();      break;
            case 1: ActivateShardIndex(); break;
            case 2: ActivateInterval();   break;
            case 3: ActivateConductor();  break;
            case 4: ActivateThreshold();  break;
            case 5: ActivateFold();       break;
        }
    }

    // ---------------------------------------------------------------
    //  Module 0 — The Pulse
    // ---------------------------------------------------------------

    private void ActivatePulse()
    {
        if (_cooldownTimers[0] > 0f) return;

        Vector3 origin    = transform.position + Vector3.up;
        Vector3 direction = transform.forward;

        Collider[] hits = Physics.OverlapSphere(origin, PulseRange, enemyLayerMask);
        foreach (Collider col in hits)
        {
            Vector3 toEnemy = (col.transform.position - origin).normalized;
            float   angle   = Vector3.Angle(direction, toEnemy);
            if (angle > PulseHalfAngle) continue;

            if (col.TryGetComponent(out EnemyEntity enemy))
            {
                enemy.TakeDamage(new DamageInfo { Amount = PulseDamage, Type = DamageType.Generic });
                enemy.Stagger(PulseStaggerDuration);
            }
        }

        // TODO (art): spawn directional shockwave VFX along transform.forward.

        _cooldownTimers[0] = CooldownDurations[0];
        Debug.Log("[CortexAbilities] The Pulse fired.");
    }

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
        Debug.Log("[CortexAbilities] The Shard Index active.");
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
        Debug.Log("[CortexAbilities] The Interval active.");
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
            Debug.Log("[CortexAbilities] Conductor fully charged.");
        }
    }

    private void ActivateConductor()
    {
        if (!_conductorFullyCharged) return;

        // Find up to 3 nearby enemies and arc to them.
        Collider[] hits = Physics.OverlapSphere(transform.position, ConductorArcRange, enemyLayerMask);
        int        arcs = 0;

        foreach (Collider col in hits)
        {
            if (arcs >= ConductorArcTargets) break;
            if (col.TryGetComponent(out EnemyEntity enemy))
            {
                enemy.TakeDamage(new DamageInfo { Amount = ConductorArcDamage, Type = DamageType.Generic });
                enemy.DisableMechanical(ConductorDisableDuration);
                arcs++;
                // TODO (art): draw arc VFX from player to enemy.
            }
        }

        _conductorCharge       = 0f;
        _conductorFullyCharged = false;
        Debug.Log($"[CortexAbilities] Conductor discharged — hit {arcs} targets.");
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
        Debug.Log("[CortexAbilities] Conductor discharged on melee contact.");
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
        Debug.Log("[CortexAbilities] The Threshold active.");
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

        Debug.Log("[CortexAbilities] The Threshold ended.");
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
            Debug.LogWarning("[CortexAbilities] CortexDevice_PocketDimension not found.");
            return;
        }
        _pocket.TryOpenPortal();
    }

    // ---------------------------------------------------------------
    //  Cooldown ticks
    // ---------------------------------------------------------------

    private void TickCooldowns()
    {
        for (int i = 0; i < CortexDevice.ModuleCount; i++)
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
        if (moduleIndex < 0 || moduleIndex >= CortexDevice.ModuleCount) return 0f;
        float dur = CooldownDurations[moduleIndex];
        if (dur <= 0f) return 0f;
        return Mathf.Clamp01(_cooldownTimers[moduleIndex] / dur);
    }

    public bool IsOnCooldown(int moduleIndex)
    {
        if (moduleIndex < 0 || moduleIndex >= CortexDevice.ModuleCount) return false;
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
