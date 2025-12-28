using UnityEngine;

/// <summary>
/// Example component that uses ParticleShooter to shoot at regular intervals.
/// Attach this alongside a ParticleShooter component.
/// </summary>
[RequireComponent(typeof(ParticleShooter))]
public class TimeShoot : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Time between shots")]
    public float shootInterval = 1f;
    
    [Tooltip("Start shooting automatically")]
    public bool autoStart = true;

    private ParticleShooter shooter;
    [SerializeField]
    private float shootTimer;
    [SerializeField]
    private bool isShooting;

    void Awake()
    {
        shooter = GetComponent<ParticleShooter>();
        
        if (autoStart)
        {
            StartShooting();
        }
    }

    void Update()
    {
        if (!isShooting) return;

        shootTimer -= Time.deltaTime;
        
        if (shootTimer <= 0f)
        {
            shooter.Shoot();
            shootTimer = shootInterval;
        }
    }

    /// <summary>
    /// Start shooting at regular intervals.
    /// </summary>
    public void StartShooting()
    {
        isShooting = true;
        shootTimer = 0f; // Shoot immediately on start
    }

    /// <summary>
    /// Stop shooting.
    /// </summary>
    public void StopShooting()
    {
        isShooting = false;
    }

    /// <summary>
    /// Shoot once immediately.
    /// </summary>
    public void ShootOnce()
    {
        if (shooter != null)
        {
            shooter.Shoot();
        }
    }
}
