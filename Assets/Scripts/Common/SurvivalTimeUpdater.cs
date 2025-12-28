// File: Common/SurvivalTimeUpdater.cs
using UnityEngine;
using GameJam.Common;

/// <summary>
/// MonoBehaviour that updates the survival time in the SurvivalTimeTracker singleton.
/// Attach this to a GameObject in your scene to start tracking time.
/// </summary>
public class SurvivalTimeUpdater : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("If true, time starts tracking immediately on Start()")]
    public bool startOnAwake = true;

    [Tooltip("If true, resets the survival time to 0 on Start()")]
    public bool resetOnStart = true;

    private bool isTracking = false;

    void Start()
    {
        if (resetOnStart)
        {
            SurvivalTimeTracker.Instance.Reset();
        }

        if (startOnAwake)
        {
            StartTracking();
        }
    }

    void Update()
    {
        if (isTracking)
        {
            float currentTime = SurvivalTimeTracker.Instance.SurvivalTime;
            SurvivalTimeTracker.Instance.SetTime(currentTime + Time.deltaTime);
        }
    }

    /// <summary>
    /// Start tracking survival time.
    /// </summary>
    public void StartTracking()
    {
        isTracking = true;
    }

    /// <summary>
    /// Stop tracking survival time.
    /// </summary>
    public void StopTracking()
    {
        isTracking = false;
    }

    /// <summary>
    /// Reset the survival time to zero and optionally restart tracking.
    /// </summary>
    public void ResetTime(bool startTrackingAgain = false)
    {
        SurvivalTimeTracker.Instance.Reset();
        isTracking = startTrackingAgain;
    }
}
