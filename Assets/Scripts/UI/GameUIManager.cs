using UnityEngine;
using GameJam.Common;

namespace GameJam.UI
{
    /// <summary>
    /// Minimal UI manager singleton used by gameplay code to update UI elements.
    /// Add concrete UI wiring here later (e.g., a Fire cooldown bar).
    /// </summary>
    public class GameUIManager : MonoBehaviour
    {
        public static GameUIManager Instance { get; private set; }

        [Header("Player HP UI")]
        public GameObject[] hpPoints; // Assign 3 objects in the inspector
        public int maxHp = 3;

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

        /// <summary>
        /// Update the HP UI objects (show/hide based on current HP).
        /// </summary>
        /// <summary>
        /// Update the HP UI objects (show/hide based on current HP). If HP was lost, also change color of the lost HP object.
        /// </summary>
        public void UpdateHp(int hp, ElementType? lastDamageElement = null)
        {
            Debug.Log($"[UI] UpdateHp called: hp={hp} lastDamageElement={(lastDamageElement.HasValue ? lastDamageElement.Value.ToString() : "none")}");
            if (hpPoints == null || hpPoints.Length == 0) return;

            if (hp > maxHp || hp < 0)
            {
                Debug.LogWarning($"[UI] UpdateHp: received hp {hp} outside expected range 0..{maxHp}. Clamping for display.");
            }
            int hpClamped = Mathf.Clamp(hp, 0, maxHp);
            for (int i = 0; i < hpPoints.Length; i++)
            {
                if (hpPoints[i] != null)
                {
                    bool shouldShow = i < hpClamped;
                    // If this HP point was just lost, update its color
                    if (!shouldShow && lastDamageElement.HasValue)
                    {
                        var img = hpPoints[i].GetComponent<UnityEngine.UI.Image>();
                        Debug.Log($"[UI] HP index {i} lost; img={(img == null ? "null" : "found")}");
                        if (img != null)
                        {
                            Color elementColor = GameDefs.ElementToColor(lastDamageElement.Value);
                            Color newColor = new Color(
                                Mathf.Max(0, 1f - elementColor.r),
                                Mathf.Max(0, 1f - elementColor.g),
                                Mathf.Max(0, 1f - elementColor.b),
                                1f
                            );
                            Debug.Log($"[UI] Setting color of HP index {i} to {newColor}");
                            img.color = newColor;
                        }
                    }
                    hpPoints[i].SetActive(shouldShow);
                }
            }
        }
                // DeductHpColor is now handled in UpdateHp

        /// <summary>
        /// Reset all HP objects to white (full health).
        /// </summary>
        public void ResetHpColors()
        {
            if (hpPoints == null) return;
            foreach (var obj in hpPoints)
            {
                if (obj == null) continue;
                var img = obj.GetComponent<UnityEngine.UI.Image>();
                if (img != null) img.color = Color.white;
            }
        }
    }
}
