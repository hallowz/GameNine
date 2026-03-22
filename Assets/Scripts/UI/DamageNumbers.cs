using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Voidborne.Diagnostics;

namespace Voidborne.UI
{
    /// <summary>
    /// Singleton that pools floating damage-number labels.
    /// Numbers spawn at the world-space hit point, float upward, face the camera,
    /// and fade out over <see cref="lifetime"/> seconds.
    ///
    /// Uses a batched Update loop instead of per-number coroutines to avoid
    /// coroutine overhead under heavy combat (20+ enemies).
    ///
    /// EnemyEntity creates this automatically on first enemy Start if it is not already
    /// present in the scene.
    /// </summary>
    public class DamageNumbers : MonoBehaviour
    {
        public static DamageNumbers Instance { get; private set; }

        [SerializeField] private int   poolSize   = 30;
        [SerializeField] private float floatSpeed = 1.8f;
        [SerializeField] private float lifetime   = 0.75f;
        [SerializeField] private float fontSize   = 5f;

        private static readonly RuntimeProfiler.Token s_prof = RuntimeProfiler.Register("DamageNumbers.Update");

        private readonly Queue<DamageNumberItem> _pool = new();
        private readonly List<ActiveDamageNumber> _active = new();
        private Transform _cameraTransform;

        // -----------------------------------------------------------------------
        // Unity Messages
        // -----------------------------------------------------------------------

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            for (int i = 0; i < poolSize; i++)
                _pool.Enqueue(CreateItem());
        }

        private void Start()
        {
            Camera cam = Camera.main;
            if (cam != null) _cameraTransform = cam.transform;
        }

        private void Update()
        {
            RuntimeProfiler.Begin(s_prof);
            if (_active.Count == 0) { RuntimeProfiler.End(s_prof); return; }

            float dt = Time.deltaTime;
            Quaternion camRot = _cameraTransform != null ? _cameraTransform.rotation : Quaternion.identity;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                ActiveDamageNumber entry = _active[i];
                entry.elapsed += dt;
                float t = entry.elapsed / lifetime;

                if (t >= 1f)
                {
                    // Return to pool
                    entry.item.GO.SetActive(false);
                    _pool.Enqueue(entry.item);
                    // Swap-with-last removal
                    int last = _active.Count - 1;
                    if (i < last) _active[i] = _active[last];
                    _active.RemoveAt(last);
                    continue;
                }

                // Float upward
                entry.item.GO.transform.position = entry.origin + Vector3.up * (floatSpeed * entry.elapsed);

                // Always face camera
                if (_cameraTransform != null)
                    entry.item.GO.transform.rotation = camRot;

                // Fade: hold full alpha for the first 40%, then fade to 0
                float alpha = t < 0.4f ? 1f : 1f - ((t - 0.4f) / 0.6f);
                Color c = entry.baseColor;
                c.a = alpha;
                entry.item.TMP.color = c;

                _active[i] = entry; // write back struct changes
            }
            RuntimeProfiler.End(s_prof);
        }

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Spawns a floating damage number at <paramref name="worldPos"/>.
        /// </summary>
        public void Spawn(float damage, Vector3 worldPos)
        {
            DamageNumberItem item = _pool.Count > 0 ? _pool.Dequeue() : CreateItem();
            item.GO.SetActive(true);
            item.GO.transform.position = worldPos;

            Color baseColor = damage >= 50f ? new Color(1f, 0.55f, 0f) : Color.white;
            item.TMP.color = baseColor;
            item.TMP.text  = Mathf.RoundToInt(damage).ToString();

            _active.Add(new ActiveDamageNumber
            {
                item      = item,
                origin    = worldPos,
                baseColor = baseColor,
                elapsed   = 0f
            });
        }

        // -----------------------------------------------------------------------
        // Pool helpers
        // -----------------------------------------------------------------------

        private DamageNumberItem CreateItem()
        {
            GameObject go = new GameObject("DmgNum");
            go.transform.SetParent(transform, false);

            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            tmp.fontSize        = fontSize;
            tmp.alignment       = TextAlignmentOptions.Center;
            tmp.fontStyle       = FontStyles.Bold;
            tmp.color           = Color.white;
            tmp.outlineWidth    = 0.2f;
            tmp.outlineColor    = new Color32(0, 0, 0, 200);
            tmp.enableWordWrapping = false;

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            go.SetActive(false);
            return new DamageNumberItem(go, tmp);
        }
    }

    internal struct ActiveDamageNumber
    {
        public DamageNumberItem item;
        public Vector3 origin;
        public Color baseColor;
        public float elapsed;
    }

    internal sealed class DamageNumberItem
    {
        public readonly GameObject  GO;
        public readonly TextMeshPro TMP;
        public DamageNumberItem(GameObject go, TextMeshPro tmp) { GO = go; TMP = tmp; }
    }
}
