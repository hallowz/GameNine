using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A dropped item in the world.
/// - Falls to the ground with physics
/// - Bobs and spins once grounded
/// - Merges with nearby WorldItems of the same type
/// - Magnetises toward the player within magnetRange, picks up within pickupRange
/// - Plays a procedural pop sound on pickup
///
/// Driven by WorldItemManager (no per-item Update).
/// </summary>
public class WorldItem : MonoBehaviour
{
    public ItemStack itemStack;

    /// <summary>
    /// When this WorldItem is a dropped BackpackItem, stores the BackpackInstance ID
    /// so the contents can be restored when picked up.
    /// </summary>
    public string backpackInstanceId;

    /// <summary>
    /// When this WorldItem is a detached vehicle part, stores its condition.
    /// </summary>
    public Voidborne.Vehicles.VehiclePartCondition partCondition;

    [SerializeField] private float bobHeight        = 0.15f;
    [SerializeField] private float bobSpeed         = 2.5f;
    [SerializeField] private float rotateSpeed      = 60f;
    [SerializeField] private float magnetRange      = 4f;    // start attracting
    [SerializeField] private float pickupRange      = 0.5f;  // auto-pickup
    [SerializeField] private float magnetSpeed      = 9f;
    [SerializeField] private float mergeRadius      = 1.5f;

    private enum Phase { Falling, Grounded }
    private Phase _phase = Phase.Falling;

    private float     _groundY;
    private float     _bobPhase;
    private float     _spawnTime;
    private Rigidbody _rb;

    private bool _isVehiclePart;

    private const float SpawnImmunity = 0.4f;   // don't pick up immediately after spawn

    // Shared across all instances
    private static AudioClip _pickupClip;
    private static readonly Collider[] _mergeBuffer = new Collider[16];

    // Material cache — prevents leaked Material instances (was creating new Material per primitive)
    private static readonly Dictionary<Color, Material> _materialCache = new();
    private static Shader _cachedShader;

    // -----------------------------------------------------------------------
    //  Public API for WorldItemManager
    // -----------------------------------------------------------------------

    public float SpawnTime => _spawnTime;
    public bool  IsGrounded => _phase == Phase.Grounded;

    // -----------------------------------------------------------------------
    //  Unity lifecycle
    // -----------------------------------------------------------------------

    private void Start()
    {
        _spawnTime = Time.time;
        _bobPhase  = Random.value * Mathf.PI * 2f; // random phase → items don't bob in sync

        BuildModel();

        // Physics body — gravity on, NOT kinematic so it falls
        _rb                  = gameObject.AddComponent<Rigidbody>();
        _rb.mass             = 0.15f;
        _rb.linearDamping    = 0.4f;
        _rb.angularDamping   = 10f;
        _rb.useGravity       = true;
        _rb.isKinematic      = false;
        _rb.constraints      = RigidbodyConstraints.FreezeRotation;

        // Small sphere collider so item rests on terrain
        SphereCollider sc = gameObject.AddComponent<SphereCollider>();
        sc.radius    = 0.18f;
        sc.isTrigger = false;

        if (_pickupClip == null) _pickupClip = BuildPickupClip();

        // Register with central manager
        WorldItemManager.EnsureExists();
        WorldItemManager.Instance.Register(this);
    }

    private void OnDestroy()
    {
        if (WorldItemManager.Instance != null)
            WorldItemManager.Instance.Unregister(this);
    }

    /// <summary>Initialize this WorldItem as a detached vehicle part (called by VehicleBodyPartSlot).</summary>
    public void InitAsVehiclePart(Voidborne.Vehicles.VehiclePartItem part, Voidborne.Vehicles.VehiclePartCondition condition)
    {
        itemStack = new ItemStack(part, 1);
        partCondition = condition;
        _isVehiclePart = true;
    }

    // -----------------------------------------------------------------------
    //  Tick — called by WorldItemManager instead of Update
    // -----------------------------------------------------------------------

    public void Tick(float dt, Transform playerTransform)
    {
        switch (_phase)
        {
            case Phase.Falling:
                TickFalling();
                break;
            case Phase.Grounded:
                TickGrounded(dt, playerTransform);
                break;
        }
    }

    private void TickFalling()
    {
        // Wait a moment then watch for the item to slow down (landed)
        if (Time.time - _spawnTime < 0.25f) return;
        if (_rb == null || _rb.linearVelocity.magnitude > 0.4f) return;

        // Settled — switch to grounded behaviour
        _phase    = Phase.Grounded;
        _groundY  = transform.position.y;

        _rb.isKinematic = true;
        _rb.useGravity  = false;
    }

    private void TickGrounded(float dt, Transform playerTransform)
    {
        if (playerTransform == null) return;

        float distSq = (transform.position - playerTransform.position).sqrMagnitude;

        if (distSq <= magnetRange * magnetRange)
        {
            // Accelerate as the item gets closer
            float dist = Mathf.Sqrt(distSq);
            float t = 1f - Mathf.Clamp01((dist - pickupRange) / (magnetRange - pickupRange));
            float speed = Mathf.Lerp(magnetSpeed * 0.4f, magnetSpeed, t);

            transform.position = Vector3.MoveTowards(
                transform.position,
                playerTransform.position,
                speed * dt);

            if (distSq <= pickupRange * pickupRange && Time.time - _spawnTime > SpawnImmunity)
                TryPickup(playerTransform);
        }
        else
        {
            // Bob in place
            _bobPhase += bobSpeed * dt;
            Vector3 pos = transform.position;
            pos.y = _groundY + 0.22f + Mathf.Sin(_bobPhase) * bobHeight * 0.5f;
            transform.position = pos;

            transform.Rotate(Vector3.up, rotateSpeed * dt, Space.World);
        }
    }

    private void TryPickup(Transform playerTransform)
    {
        PlayerInventory inv = playerTransform.GetComponent<PlayerInventory>();
        if (inv == null) return;
        if (!inv.AddItem(itemStack)) return;  // inventory full

        // Restore backpack instance contents if this was a dropped backpack
        if (!string.IsNullOrEmpty(backpackInstanceId) && itemStack.item is BackpackItem)
        {
            BackpackInstance instance = BackpackInstance.Get(backpackInstanceId);
            if (instance != null)
            {
                // Find which hotbar slot the item just landed in and restore the instance
                for (int i = 0; i < inv.Hotbar.SlotCount; i++)
                {
                    ItemStack slot = inv.Hotbar.GetSlot(i);
                    if (!slot.IsEmpty && slot.item == itemStack.item)
                    {
                        inv.RestoreHotbarBackpackInstance(i, instance);
                        break;
                    }
                }
            }
        }

        PlayPickup();
        Destroy(gameObject);
    }

    // -----------------------------------------------------------------------
    //  Merging nearby stacks of the same item (called by WorldItemManager)
    // -----------------------------------------------------------------------

    public void TryMergeNearby()
    {
        if (itemStack.IsEmpty || itemStack.item == null) return;
        // Vehicle parts never merge — each has unique condition
        if (_isVehiclePart) return;
        int maxStack = itemStack.item.maxStackSize;
        if (itemStack.quantity >= maxStack) return;

        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, mergeRadius, _mergeBuffer);
        for (int i = 0; i < hitCount; i++)
        {
            var hit = _mergeBuffer[i];
            if (hit.gameObject == gameObject) continue;

            WorldItem other = hit.GetComponent<WorldItem>();
            if (other == null || other.itemStack.IsEmpty) continue;
            if (other.itemStack.item != itemStack.item) continue;

            int space = maxStack - itemStack.quantity;
            int take  = Mathf.Min(space, other.itemStack.quantity);
            if (take <= 0) continue;

            itemStack       = new ItemStack(itemStack.item, itemStack.quantity + take);
            int leftover    = other.itemStack.quantity - take;

            if (leftover <= 0)
                Destroy(other.gameObject);
            else
                other.itemStack = new ItemStack(other.itemStack.item, leftover);

            if (itemStack.quantity >= maxStack) break;
        }
    }

    // -----------------------------------------------------------------------
    //  Model building — distinct shapes per item ID
    // -----------------------------------------------------------------------

    private void BuildModel()
    {
        if (itemStack.item == null)
        {
            AddPrim(PrimitiveType.Cube, Color.white, Vector3.one * 0.32f);
            return;
        }

        switch (itemStack.item.itemId)
        {
            case "wood":
                // Brown cylinder lying on its side (log)
                var log = AddPrim(PrimitiveType.Cylinder, new Color(0.55f, 0.34f, 0.14f), Vector3.one);
                log.transform.localScale    = new Vector3(0.18f, 0.26f, 0.18f);
                log.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                break;

            case "stone":
                AddPrim(PrimitiveType.Sphere, new Color(0.55f, 0.55f, 0.55f), Vector3.one * 0.32f);
                break;

            case "iron_ore":
                AddPrim(PrimitiveType.Sphere, new Color(0.40f, 0.36f, 0.36f), Vector3.one * 0.30f);
                // Small orange vein dot on top
                var iov = AddPrim(PrimitiveType.Sphere, new Color(0.80f, 0.38f, 0.15f), Vector3.one);
                iov.transform.localScale    = Vector3.one * 0.12f;
                iov.transform.localPosition = new Vector3(0.07f, 0.1f, 0f);
                break;

            case "copper_ore":
                AddPrim(PrimitiveType.Sphere, new Color(0.42f, 0.34f, 0.28f), Vector3.one * 0.30f);
                var cov = AddPrim(PrimitiveType.Sphere, new Color(0.22f, 0.72f, 0.55f), Vector3.one);
                cov.transform.localScale    = Vector3.one * 0.12f;
                cov.transform.localPosition = new Vector3(-0.07f, 0.1f, 0f);
                break;

            case "coal":
                AddPrim(PrimitiveType.Sphere, new Color(0.13f, 0.13f, 0.15f), Vector3.one * 0.28f);
                break;

            case "iron_ingot":
                AddPrim(PrimitiveType.Cube, new Color(0.68f, 0.68f, 0.72f), new Vector3(0.42f, 0.11f, 0.20f));
                break;

            case "copper_ingot":
                AddPrim(PrimitiveType.Cube, new Color(0.80f, 0.44f, 0.14f), new Vector3(0.42f, 0.11f, 0.20f));
                break;

            case "stick":
                var stick = AddPrim(PrimitiveType.Cylinder, new Color(0.72f, 0.54f, 0.30f), Vector3.one);
                stick.transform.localScale = new Vector3(0.06f, 0.22f, 0.06f);
                break;

            case "wood_planks":
                AddPrim(PrimitiveType.Cube, new Color(0.76f, 0.60f, 0.38f), new Vector3(0.40f, 0.08f, 0.40f));
                break;

            case "torch":
                var tHandle = AddPrim(PrimitiveType.Cylinder, new Color(0.52f, 0.35f, 0.18f), Vector3.one);
                tHandle.transform.localScale    = new Vector3(0.07f, 0.16f, 0.07f);
                tHandle.transform.localPosition = new Vector3(0f, -0.04f, 0f);
                var tFlame = AddPrim(PrimitiveType.Sphere, new Color(1f, 0.58f, 0.05f), Vector3.one);
                tFlame.transform.localScale    = Vector3.one * 0.13f;
                tFlame.transform.localPosition = new Vector3(0f, 0.20f, 0f);
                break;

            case "wood_pickaxe":
                BuildPickaxeModel(new Color(0.72f, 0.56f, 0.32f));
                break;

            case "stone_pickaxe":
                BuildPickaxeModel(new Color(0.55f, 0.55f, 0.55f));
                break;

            case "iron_pickaxe":
                BuildPickaxeModel(new Color(0.65f, 0.65f, 0.70f));
                break;

            default:
                AddPrim(PrimitiveType.Cube, GetTypeColor(itemStack.item.itemType), Vector3.one * 0.32f);
                break;
        }
    }

    private void BuildPickaxeModel(Color headColor)
    {
        var handle = AddPrim(PrimitiveType.Cylinder, new Color(0.62f, 0.44f, 0.22f), Vector3.one);
        handle.transform.localScale    = new Vector3(0.06f, 0.18f, 0.06f);
        handle.transform.localPosition = new Vector3(0f, -0.04f, 0f);

        var head = AddPrim(PrimitiveType.Cube, headColor, Vector3.one);
        head.transform.localScale    = new Vector3(0.36f, 0.08f, 0.09f);
        head.transform.localPosition = new Vector3(0f, 0.16f, 0f);
    }

    /// <summary>Creates a primitive child, strips its collider, applies cached material.</summary>
    private GameObject AddPrim(PrimitiveType type, Color color, Vector3 scale)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale    = scale;

        Collider c = go.GetComponent<Collider>();
        if (c != null) Destroy(c);

        Renderer rend = go.GetComponent<Renderer>();
        if (rend != null)
            rend.sharedMaterial = GetCachedMaterial(color);

        return go;
    }

    /// <summary>
    /// Returns a shared Material for the given color, creating one if needed.
    /// This replaces the old code that did `new Material(...)` per primitive,
    /// which leaked material instances that were never cleaned up.
    /// </summary>
    private static Material GetCachedMaterial(Color color)
    {
        if (_materialCache.TryGetValue(color, out Material mat))
            return mat;

        if (_cachedShader == null)
        {
            _cachedShader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");
        }

        mat = new Material(_cachedShader);
        mat.color = color;
        _materialCache[color] = mat;
        return mat;
    }

    private static Color GetTypeColor(ItemType type)
    {
        switch (type)
        {
            case ItemType.Resource:   return new Color(0.55f, 0.80f, 0.55f);
            case ItemType.Tool:       return new Color(0.70f, 0.70f, 0.40f);
            case ItemType.Weapon:     return new Color(0.90f, 0.30f, 0.30f);
            case ItemType.Armor:      return new Color(0.40f, 0.40f, 0.90f);
            case ItemType.Consumable: return new Color(0.80f, 0.50f, 0.90f);
            case ItemType.Block:      return new Color(0.70f, 0.60f, 0.50f);
            default:                  return Color.white;
        }
    }

    // -----------------------------------------------------------------------
    //  Audio
    // -----------------------------------------------------------------------

    private void PlayPickup()
    {
        if (_pickupClip == null) return;

        GameObject go    = new GameObject("PickupSfx");
        AudioSource src  = go.AddComponent<AudioSource>();
        src.clip         = _pickupClip;
        src.volume       = 0.55f;
        src.pitch        = Random.Range(0.92f, 1.12f);
        src.spatialBlend = 0f; // 2D
        src.Play();
        Destroy(go, _pickupClip.length + 0.15f);
    }

    private static AudioClip BuildPickupClip()
    {
        // Short "pop" — decaying sine wave with a slight frequency sweep
        const int   sampleRate = 44100;
        const float duration   = 0.09f;
        int         samples    = Mathf.RoundToInt(sampleRate * duration);

        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t        = (float)i / sampleRate;
            float envelope = Mathf.Pow(1f - t / duration, 3f);       // fast cubic decay
            float freq     = 900f + 300f * (1f - t / duration);      // sweep 1200→900 Hz
            data[i]        = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * 0.45f;
        }

        AudioClip clip = AudioClip.Create("PickupPop", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
