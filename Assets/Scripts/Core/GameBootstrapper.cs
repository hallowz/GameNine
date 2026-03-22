using UnityEngine;
using UnityEngine.SceneManagement;
using Voidborne.Player;

namespace Voidborne.Core
{
    public class GameBootstrapper : MonoBehaviour
    {
        private void Awake()
        {
            // Ensure only one bootstrapper exists
            DontDestroyOnLoad(gameObject);

            Initialize();
        }

        private void Initialize()
        {
            // Initialize core systems
            Debug.Log("[Voidborne] Game bootstrapper initialized.");

            // Verify player systems
            if (PlayerManager.Instance != null)
            {
                Debug.Log("[Voidborne] PlayerManager found.");
            }
            else
            {
                Debug.Log("[Voidborne] PlayerManager not yet initialized — it will self-register on Awake.");
            }

            // TODO: Initialize additional singletons as they are created in later chunks
        }
    }
}
