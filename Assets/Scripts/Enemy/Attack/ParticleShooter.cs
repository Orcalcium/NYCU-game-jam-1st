using UnityEngine;
using GameJam.Common;

/// <summary>
/// Controls a non-looping particle system that can be manually triggered to shoot projectiles.
/// Handles targeting, collision detection, and damage application.
/// Requires EnemyShooter2D for target information.
/// </summary>
[RequireComponent(typeof(EnemyShooter2D))]
public class ParticleShooter : MonoBehaviour
{
    [Header("Particle System")]
    [Tooltip("The particle system to shoot. Should have looping disabled and collision enabled.")]
    public ParticleSystem particleSystem;

    [Header("Shooting")]
    [Tooltip("Fire point where particles spawn from")]
    public Transform firePoint;
        
    [Tooltip("Damage dealt by each particle")]
    public int particleDamage = 1;

    [Header("Element")]
    [Tooltip("Element type of the projectiles")]
    public ElementType elementType = ElementType.Fire;

    [Header("Auto-Aim")]
    [Tooltip("If true, automatically aims at target when shooting")]
    public bool autoAim = true;

    private EnemyShooter2D enemyShooter;
    private Transform target;
    private Object ownerSource;

    void Awake()
    {
        // Get required EnemyShooter2D component
        enemyShooter = GetComponent<EnemyShooter2D>();
        
        // Auto-find particle system if not assigned
        if (particleSystem == null)
        {
            particleSystem = GetComponent<ParticleSystem>();
        }

        // Auto-find fire point if not assigned
        if (firePoint == null)
        {
            firePoint = transform;
        }

        SetupParticleSystem();
        ownerSource = this;
    }

    void Update()
    {
        // Get target from EnemyShooter2D
        if (enemyShooter != null)
        {
            target = enemyShooter.target;
        }
    }

    void SetupParticleSystem()
    {
        if (particleSystem == null)
        {
            Debug.LogWarning($"[ParticleShooter] No ParticleSystem assigned on {gameObject.name}");
            return;
        }

        // Configure particle system for manual shooting
        var main = particleSystem.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        // Ensure collision is enabled
        var collision = particleSystem.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.sendCollisionMessages = true;
    }

    /// <summary>
    /// Shoots the particle system towards the current target.
    /// </summary>
    public void Shoot()
    {
        if (target != null && autoAim)
        {
            ShootAt(target.position);
        }
        else
        {
            ShootInDirection(transform.right);
        }
    }

    /// <summary>
    /// Shoots the particle system at a specific world position.
    /// </summary>
    public void ShootAt(Vector3 targetPosition)
    {
        Vector3 startPos = firePoint != null ? firePoint.position : transform.position;
        Vector3 direction = (targetPosition - startPos).normalized;
        
        ShootInDirection(direction);
    }

    /// <summary>
    /// Shoots the particle system in a specific direction.
    /// </summary>
    public void ShootInDirection(Vector3 direction)
    {
        if (particleSystem == null)
        {
            Debug.LogWarning($"[ParticleShooter] Cannot shoot - no ParticleSystem on {gameObject.name}");
            return;
        }

        // Position the particle system at fire point
        Vector3 startPos = firePoint != null ? firePoint.position : transform.position;
        particleSystem.transform.position = startPos;

        // Aim the particle system
        if (direction.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            particleSystem.transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        // Play the particle system
        Debug.Log($"[ParticleShooter] Shooting particles from {startPos} towards direction {direction}");
        particleSystem.Play();
    }

    /// <summary>
    /// Stops the particle system.
    /// </summary>
    public void Stop()
    {
        if (particleSystem != null)
        {
            particleSystem.Stop();
        }
    }

    /// <summary>
    /// Sets the element type.
    /// </summary>
    public void SetElement(ElementType newElement)
    {
        elementType = newElement;
    }

    /// <summary>
    /// Sets the owner of the projectiles for damage attribution.
    /// </summary>
    public void SetOwner(Object owner)
    {
        ownerSource = owner;
    }

    void OnParticleCollision(GameObject other)
    {
        // Check if the collision is with a damageable object
        IElementDamageable damageable = other.GetComponent<IElementDamageable>();
        if (damageable != null)
        {
            if (damageable.CanBeHitBy(elementType))
            {
                damageable.TakeElementHit(elementType, particleDamage, ownerSource);
                Debug.Log($"[ParticleShooter] Particle hit {other.name} with {GameDefs.ElementToText(elementType)} for {particleDamage} damage");
            }
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
    }
#endif
}
