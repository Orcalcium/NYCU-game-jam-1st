using UnityEngine;
using GameJam.Common;

/// <summary>
/// Uses a TimedDamageZone in a child GameObject to perform laser attacks.
/// Gets target information from EnemyShooter2D on parent or same GameObject.
/// Does not use object pooling - the zone is a persistent child.
/// Rotates children to face target, but does not move them.
/// </summary>
public class LaserAttacker : MonoBehaviour
{
    [Header("Zone Reference")]
    [Tooltip("TimedDamageZone in child GameObject (auto-found if not assigned)")]
    public TimedDamageZone damageZone;

    [Header("Attack Settings")]
    [Tooltip("Time between laser attacks")]
    public float attackInterval = 2f;

    [Tooltip("Auto-start attacking on enable")]
    public bool autoStart = true;

    [Header("Targeting")]
    [Tooltip("If true, laser rotates to face target. If false, laser is static and doesn't rotate.")]
    public bool trackTarget = true;

    [Tooltip("Transform to rotate (auto-set to this.transform if not assigned). Children will rotate around this point.")]
    public Transform rotatingTransform;

    private EnemyShooter2D enemyShooter;
    private float attackTimer;
    private bool isAttacking;
    private bool isZoneActive;

    void Awake()
    {
        // Try to get EnemyShooter2D from same GameObject first, then parent
        enemyShooter = GetComponent<EnemyShooter2D>();
        if (enemyShooter == null)
        {
            enemyShooter = GetComponentInParent<EnemyShooter2D>();
        }

        if (enemyShooter == null)
        {
            Debug.LogError($"[LaserAttacker] No EnemyShooter2D found on {gameObject.name} or parent");
            enabled = false;
            return;
        }

        // Auto-find TimedDamageZone in children if not assigned
        if (damageZone == null)
        {
            damageZone = GetComponentInChildren<TimedDamageZone>(true);
        }

        if (damageZone == null)
        {
            Debug.LogError($"[LaserAttacker] No TimedDamageZone found in children of {gameObject.name}");
            enabled = false;
            return;
        }

        // Set rotating transform to this transform if not assigned
        if (rotatingTransform == null)
        {
            rotatingTransform = transform;
        }

        // Ensure the zone starts inactive
        damageZone.gameObject.SetActive(false);
        isZoneActive = false;
    }

    void OnEnable()
    {
        if (autoStart)
        {
            StartAttacking();
        }
    }

    void OnDisable()
    {
        StopAttacking();
    }

    void Update()
    {
        if (!isAttacking) return;

        // Update rotation if tracking target
        if (trackTarget)
        {
            UpdateRotation();
        }

        attackTimer -= Time.deltaTime;

        if (attackTimer <= 0f)
        {
            FireLaser();
            attackTimer = attackInterval;
        }
    }

    void UpdateRotation()
    {
        if (rotatingTransform == null || enemyShooter == null || enemyShooter.target == null)
            return;

        Vector3 targetPosition = enemyShooter.target.position;
        Vector3 direction = targetPosition - rotatingTransform.position;

        if (direction.sqrMagnitude > 0.0001f)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            rotatingTransform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    /// <summary>
    /// Start attacking (firing laser at target).
    /// </summary>
    public void StartAttacking()
    {
        isAttacking = true;
        attackTimer = 0f; // Fire immediately on start
    }

    /// <summary>
    /// Stop attacking.
    /// </summary>
    public void StopAttacking()
    {
        isAttacking = false;
    }

    /// <summary>
    /// Fire a laser at the current zone position (children stay in place, only rotate).
    /// </summary>
    public void FireLaser()
    {
        if (enemyShooter == null)
        {
            Debug.LogWarning($"[LaserAttacker] No EnemyShooter2D reference on {gameObject.name}");
            return;
        }

        if (trackTarget && enemyShooter.target == null)
        {
            Debug.LogWarning($"[LaserAttacker] No target assigned to EnemyShooter2D on {gameObject.name}");
            return;
        }

        if (damageZone == null)
        {
            Debug.LogWarning($"[LaserAttacker] No damage zone assigned on {gameObject.name}");
            return;
        }

        // Don't fire if the zone is currently active
        if (isZoneActive)
        {
            Debug.Log($"[LaserAttacker] Zone still active on {gameObject.name}, skipping fire");
            return;
        }

        // Fire at the zone's current world position (don't move it)
        Vector3 zonePosition = damageZone.transform.position;
        FireLaserAt(zonePosition);
    }

    /// <summary>
    /// Fire a laser at a specific position.
    /// </summary>
    public void FireLaserAt(Vector3 position)
    {
        if (damageZone == null) return;

        isZoneActive = true;

        damageZone.Activate(
            position,
            enemyShooter.currentElement,
            enemyShooter,
            OnLaserComplete
        );

        Debug.Log($"[LaserAttacker] Fired laser at {position} with element {enemyShooter.currentElement}, trackTarget={trackTarget}");
    }

    private void OnLaserComplete(TimedDamageZone zone)
    {
        isZoneActive = false;
        Debug.Log($"[LaserAttacker] Laser complete on {gameObject.name}");
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Try to get enemy shooter if not cached
        if (enemyShooter == null)
        {
            enemyShooter = GetComponent<EnemyShooter2D>();
            if (enemyShooter == null)
            {
                enemyShooter = GetComponentInParent<EnemyShooter2D>();
            }
        }

        if (rotatingTransform == null)
        {
            rotatingTransform = transform;
        }

        if (trackTarget)
        {
            if (enemyShooter == null || enemyShooter.target == null) return;

            // Draw target position
            Gizmos.color = Color.cyan;
            Vector3 targetPos = enemyShooter.target.position;
            Gizmos.DrawWireSphere(targetPos, 0.3f);

            // Draw line from laser to target
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(rotatingTransform.position, targetPos);

            // Draw rotation direction
            Vector3 dir = targetPos - rotatingTransform.position;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(rotatingTransform.position, dir.normalized * 1.5f);
            }
        }
        else
        {
            // Draw static laser direction
            Gizmos.color = Color.red;
            Vector3 dir = rotatingTransform.right;
            Gizmos.DrawRay(rotatingTransform.position, dir * 2f);
        }

        // Draw zone if available
        if (damageZone != null)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            
            var col = damageZone.GetComponent<Collider2D>();
            if (col != null)
            {
                Vector3 zonePos = damageZone.transform.position;
                
                if (col is CircleCollider2D circleCol)
                {
                    Gizmos.DrawWireSphere(zonePos, circleCol.radius);
                }
                else if (col is BoxCollider2D boxCol)
                {
                    Gizmos.matrix = Matrix4x4.TRS(zonePos, damageZone.transform.rotation, Vector3.one);
                    Gizmos.DrawWireCube(boxCol.offset, boxCol.size);
                }
            }
        }
    }
#endif
}
