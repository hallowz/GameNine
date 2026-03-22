using UnityEngine;

namespace Voidborne.Player
{
    /// <summary>
    /// Singleton that holds references to the player's core components.
    /// Other systems (e.g., ChunkLoader) can find the player via PlayerManager.Instance.
    /// </summary>
    public class PlayerManager : MonoBehaviour
    {
        public static PlayerManager Instance { get; private set; }

        [Header("Component References")]
        [SerializeField] private FirstPersonController controller;
        [SerializeField] private FirstPersonCamera cameraController;

        [Header("Player Stats (Placeholder)")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth = 100f;

        public FirstPersonController Controller => controller;
        public FirstPersonCamera CameraController => cameraController;
        public Transform PlayerTransform => transform;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;

        /// <summary>
        /// Stamina is managed by FirstPersonController. Exposed here for convenience.
        /// </summary>
        public float CurrentStamina => controller != null ? controller.CurrentStamina : 0f;
        public float MaxStamina => controller != null ? controller.MaxStamina : 100f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[PlayerManager] Duplicate PlayerManager found — destroying this one.");
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Auto-find components if not assigned
            if (controller == null)
                controller = GetComponent<FirstPersonController>();
            if (cameraController == null)
                cameraController = GetComponentInChildren<FirstPersonCamera>();

            if (controller == null)
                Debug.LogError("[PlayerManager] FirstPersonController not found on player.");
            if (cameraController == null)
                Debug.LogError("[PlayerManager] FirstPersonCamera not found in children.");
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Apply damage to the player. Placeholder implementation.
        /// </summary>
        public void TakeDamage(float amount)
        {
            currentHealth -= amount;
            currentHealth = Mathf.Max(currentHealth, 0f);

            if (currentHealth <= 0f)
            {
                Debug.Log("[PlayerManager] Player has died.");
                // TODO: Death handling in a later chunk
            }
        }

        /// <summary>
        /// Heal the player. Placeholder implementation.
        /// </summary>
        public void Heal(float amount)
        {
            currentHealth += amount;
            currentHealth = Mathf.Min(currentHealth, maxHealth);
        }
    }
}
