// File: UI/SurvivalTimeDisplay.cs
using UnityEngine;
using TMPro;
using GameJam.Common;

namespace GameJam.UI
{
    /// <summary>
    /// MonoBehaviour that displays the survival time from SurvivalTimeTracker singleton
    /// in a TextMeshProUGUI component.
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class SurvivalTimeDisplay : MonoBehaviour
    {
        [Header("Display Settings")]
        [Tooltip("The TextMeshProUGUI component to display the time in")]
        public TextMeshProUGUI timeText;

        [Tooltip("If true, displays milliseconds (MM:SS.MS format). If false, displays MM:SS format.")]
        public bool showMilliseconds = false;

        [Tooltip("Optional prefix text (e.g., 'Time: ')")]
        public string prefix = "";

        [Tooltip("Optional suffix text")]
        public string suffix = "";

        void Awake()
        {
            if (timeText == null)
            {
                timeText = GetComponent<TextMeshProUGUI>();
            }

            if (timeText == null)
            {
                Debug.LogError("[SurvivalTimeDisplay] No TextMeshProUGUI component found!");
                enabled = false;
            }
        }

        void Update()
        {
            if (timeText != null)
            {
                string formattedTime = showMilliseconds 
                    ? SurvivalTimeTracker.Instance.GetFormattedTimeWithMilliseconds()
                    : SurvivalTimeTracker.Instance.GetFormattedTime();

                timeText.text = prefix + formattedTime + suffix;
            }
        }

        /// <summary>
        /// Manually update the display with the current time.
        /// </summary>
        public void UpdateDisplay()
        {
            if (timeText != null)
            {
                string formattedTime = showMilliseconds 
                    ? SurvivalTimeTracker.Instance.GetFormattedTimeWithMilliseconds()
                    : SurvivalTimeTracker.Instance.GetFormattedTime();

                timeText.text = prefix + formattedTime + suffix;
            }
        }
    }
}
