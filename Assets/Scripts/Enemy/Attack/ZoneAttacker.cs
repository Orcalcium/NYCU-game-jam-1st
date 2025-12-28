using UnityEngine;
using UnityEngine.Pool;
using GameJam.Common;

/// <summary>
/// Spawns timed damage zones on top of the target position repeatedly.
/// Requires EnemyShooter2D to get target information.
/// </summary>
[RequireComponent(typeof(EnemyShooter2D))]
public class ZoneAttacker : MonoBehaviour
{
    [Header("Zone Prefab")]
    [Tooltip("Prefab of the TimedDamageZone to spawn")]
    public TimedDamageZone zonePrefab;

    [Header("Pool Settings")]
    [Tooltip("Initial pool capacity")]
    public int poolCapacity = 5;

    [Tooltip("Maximum pool size")]
    public int poolMaxSize = 10;

    [Header("Attack Settings")]
    [Tooltip("Time between zone spawns")]
    public float attackInterval = 2f;

    [Tooltip("Offset from target position (if needed)")]
    public Vector3 spawnOffset = Vector3.zero;

    [Tooltip("Random spawn radius around the player (0 = spawn directly on player)")]
    public float spawnRadius = 3f;

    [Tooltip("Auto-start attacking on enable")]
    public bool autoStart = true;

    private EnemyShooter2D enemyShooter;
    private ObjectPool<TimedDamageZone> zonePool;
    private float attackTimer;
    private bool isAttacking;

    void Awake()
    {
        enemyShooter = GetComponent<EnemyShooter2D>();

        if (enemyShooter == null)
        {
            Debug.LogError($"[ZoneAttacker] No EnemyShooter2D found on {gameObject.name}");
            enabled = false;
            return;
        }

        if (zonePrefab == null)
        {
            Debug.LogError($"[ZoneAttacker] No zone prefab assigned on {gameObject.name}");
            enabled = false;
            return;
        }

        InitializePool();
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

    void InitializePool()
    {
        zonePool = new ObjectPool<TimedDamageZone>(
            createFunc: CreateZone,
            actionOnGet: OnGetZone,
            actionOnRelease: OnReleaseZone,
            actionOnDestroy: OnDestroyZone,
            collectionCheck: true,
            defaultCapacity: poolCapacity,
            maxSize: poolMaxSize
        );
    }

    private TimedDamageZone CreateZone()
    {
        TimedDamageZone zone = Instantiate(zonePrefab);
        zone.gameObject.SetActive(false);
        return zone;
    }

    private void OnGetZone(TimedDamageZone zone)
    {
        zone.gameObject.SetActive(true);
    }

    private void OnReleaseZone(TimedDamageZone zone)
    {
        zone.gameObject.SetActive(false);
    }

    private void OnDestroyZone(TimedDamageZone zone)
    {
        if (zone != null)
        {
            Destroy(zone.gameObject);
        }
    }

    void Update()
    {
        if (!isAttacking) return;

        attackTimer -= Time.deltaTime;

        if (attackTimer <= 0f)
        {
            SpawnZone();
            attackTimer = attackInterval;
        }
    }

    /// <summary>
    /// Start attacking (spawning zones on target).
    /// </summary>
    public void StartAttacking()
    {
        isAttacking = true;
        attackTimer = 0f; // Spawn immediately on start
    }

    /// <summary>
    /// Stop attacking.
    /// </summary>
    public void StopAttacking()
    {
        isAttacking = false;
    }

    /// <summary>
    /// Returns whether this attacker is currently attacking.
    /// Used by EnemyShooter2D to adapt movement (e.g., Rampage charging behavior).
    /// </summary>
    public bool IsAttacking()
    {
        return isAttacking;
    }

    /// <summary>
    /// Manually spawn a zone at the target position.
    /// </summary>
    public void SpawnZone()
    {
        if (enemyShooter.target == null)
        {
            Debug.LogWarning($"[ZoneAttacker] No target assigned to EnemyShooter2D on {gameObject.name}");
            return;
        }

        Vector3 targetPosition = enemyShooter.target.position + spawnOffset;
        
        // Apply random circular offset using radius and angle
        if (spawnRadius > 0f)
        {
            float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float randomDistance = Random.Range(0f, spawnRadius);
            
            float offsetX = Mathf.Cos(randomAngle) * randomDistance;
            float offsetY = Mathf.Sin(randomAngle) * randomDistance;
            
            targetPosition += new Vector3(offsetX, offsetY, 0f);
        }
        
        SpawnZoneAt(targetPosition);
    }

    /// <summary>
    /// Spawn a zone at a specific position.
    /// </summary>
    public void SpawnZoneAt(Vector3 position)
    {
        TimedDamageZone zone = zonePool.Get();

        if (zone != null)
        {
            zone.Activate(
                position,
                enemyShooter.currentElement,
                enemyShooter,
                OnZoneComplete
            );
        }
    }

    private void OnZoneComplete(TimedDamageZone zone)
    {
        zonePool.Release(zone);
    }

    void OnDestroy()
    {
        // Clean up pool
        zonePool?.Clear();
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (enemyShooter == null || enemyShooter.target == null) return;

        Vector3 spawnPos = enemyShooter.target.position + spawnOffset;
        
        // Draw spawn radius circle
        if (spawnRadius > 0f)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            DrawCircle(spawnPos, spawnRadius, 32);
        }
        
        // Draw center point
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(spawnPos, 0.5f);

        // Draw line from enemy to spawn position
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, spawnPos);
    }

    private void DrawCircle(Vector3 center, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0f, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
#endif
}
