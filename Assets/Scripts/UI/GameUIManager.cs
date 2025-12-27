using UnityEngine;

namespace GameJam.UI
{
    /// <summary>
    /// Minimal UI manager singleton used by gameplay code to update UI elements.
    /// Add concrete UI wiring here later (e.g., a Fire cooldown bar).
    /// </summary>
    public class GameUIManager : MonoBehaviour
    {
        public static GameUIManager Instance { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // Optionally keep across scenes
            // DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Update the fire cooldown UI. 'progress' should be between 0 and 1.
        /// Currently this is a stub; hook it up to UI elements as needed.
        /// </summary>
        public void UpdateFireCooldown(float progress)
        {
            // TODO: connect to actual UI (e.g., a UI Image fill amount)
            // For now, we simply ensure the method exists to avoid compile errors.
        }
    }
}
