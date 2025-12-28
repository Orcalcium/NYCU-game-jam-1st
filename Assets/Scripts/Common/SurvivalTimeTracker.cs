// File: Common/SurvivalTimeTracker.cs
using UnityEngine;

namespace GameJam.Common
{
    /// <summary>
    /// Singleton class (non-MonoBehaviour) that tracks the player's survival time.
    /// Access via SurvivalTimeTracker.Instance.
    /// </summary>
    public class SurvivalTimeTracker
    {
        private static SurvivalTimeTracker instance;
        
        public static SurvivalTimeTracker Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new SurvivalTimeTracker();
                }
                return instance;
            }
        }

        private float survivalTime;

        /// <summary>
        /// Gets the current survival time in seconds.
        /// </summary>
        public float SurvivalTime => survivalTime;

        /// <summary>
        /// Sets the survival time in seconds.
        /// </summary>
        public void SetTime(float time)
        {
            survivalTime = time;
        }

        /// <summary>
        /// Resets the survival time to zero.
        /// </summary>
        public void Reset()
        {
            survivalTime = 0f;
        }

        /// <summary>
        /// Gets the survival time formatted as MM:SS.
        /// </summary>
        public string GetFormattedTime()
        {
            int minutes = Mathf.FloorToInt(survivalTime / 60f);
            int seconds = Mathf.FloorToInt(survivalTime % 60f);
            return string.Format("{0:00}:{1:00}", minutes, seconds);
        }

        /// <summary>
        /// Gets the survival time formatted as MM:SS.MS (with milliseconds).
        /// </summary>
        public string GetFormattedTimeWithMilliseconds()
        {
            int minutes = Mathf.FloorToInt(survivalTime / 60f);
            int seconds = Mathf.FloorToInt(survivalTime % 60f);
            int milliseconds = Mathf.FloorToInt((survivalTime * 100f) % 100f);
            return string.Format("{0:00}:{1:00}.{2:00}", minutes, seconds, milliseconds);
        }

        private SurvivalTimeTracker()
        {
            survivalTime = 0f;
        }
    }
}
