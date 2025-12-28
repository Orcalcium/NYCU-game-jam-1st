// File: Combat/EnemyPoolManager.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Pool;
using GameJam.Common;

public class EnemyPoolManager : MonoBehaviour
{
    public static EnemyPoolManager Instance { get; private set; }

    [Header("Pool")]
    [Tooltip("List of enemy prefabs to spawn. Each has equal spawn probability.")]
    public List<EnemyShooter2D> enemyPrefabs = new List<EnemyShooter2D>();
    
    [Tooltip("Initial pool capacity per enemy type")]
    public int poolCapacityPerType = 5;
    
    [Tooltip("Maximum pool size per enemy type")]
    public int poolMaxSizePerType = 20;
    
    public int maxAliveEnemies = 10;

    [Header("Spawn")]
    public Transform[] spawnPoints;
    public Transform player;
    public float spawnInterval = 2.5f;
    [Tooltip("Multiplier applied to enemy moveSpeed when spawned (1 = use prefab default)")]
    public float spawnMoveSpeedMultiplier = 1.0f;

    [Header("Spawn Anti-Overlap")]
    public LayerMask enemyLayer;
    public float spawnClearRadius = 0.7f;
    public int spawnTryCount = 20;
    public float spawnJitterRadius = 0.6f;

    [Header("Monster VFX")]
    public float emissionIntervalMin = 3f;
    public float emissionIntervalMax = 5f;

    struct ParticleCollisionDefault
    {
        public bool enabled;
        public LayerMask collidesWith;
    }

    // Pool per enemy type
    private Dictionary<EnemyShooter2D, ObjectPool<EnemyShooter2D>> enemyPools = new Dictionary<EnemyShooter2D, ObjectPool<EnemyShooter2D>>();
    
    // Track all active enemies across all pools
    private List<EnemyShooter2D> allActiveEnemies = new List<EnemyShooter2D>();
    
    private Dictionary<ParticleSystem, ParticleCollisionDefault> particleCollisionDefaults = new Dictionary<ParticleSystem, ParticleCollisionDefault>();
    
    private float spawnTimer;

    void Awake()
    {
        Instance = this;

        // Validate enemy prefabs list
        if (enemyPrefabs == null || enemyPrefabs.Count == 0)
        {
            Debug.LogError("[EnemyPoolManager] No enemy prefabs assigned!");
            enabled = false;
            return;
        }

        // Initialize a pool for each enemy type
        foreach (var prefab in enemyPrefabs)
        {
            if (prefab == null)
            {
                Debug.LogWarning("[EnemyPoolManager] Null prefab found in enemy prefabs list, skipping.");
                continue;
            }

            var pool = new ObjectPool<EnemyShooter2D>(
                createFunc: () => CreateEnemy(prefab),
                actionOnGet: OnGetEnemy,
                actionOnRelease: OnReleaseEnemy,
                actionOnDestroy: OnDestroyEnemy,
                collectionCheck: true,
                defaultCapacity: poolCapacityPerType,
                maxSize: poolMaxSizePerType
            );

            enemyPools[prefab] = pool;
        }
    }

    void Update()
    {
        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            TrySpawn();
            spawnTimer = spawnInterval;
        }
    }

    private EnemyShooter2D CreateEnemy(EnemyShooter2D prefab)
    {
        EnemyShooter2D e = Instantiate(prefab, transform);
        e.gameObject.SetActive(false);
        CacheParticleCollisionDefaults(e);
        return e;
    }

    private void OnGetEnemy(EnemyShooter2D enemy)
    {
        enemy.gameObject.SetActive(true);
        if (!allActiveEnemies.Contains(enemy))
        {
            allActiveEnemies.Add(enemy);
        }
    }

    private void OnReleaseEnemy(EnemyShooter2D enemy)
    {
        enemy.gameObject.SetActive(false);
        allActiveEnemies.Remove(enemy);
    }

    private void OnDestroyEnemy(EnemyShooter2D enemy)
    {
        if (enemy != null)
        {
            allActiveEnemies.Remove(enemy);
            Destroy(enemy.gameObject);
        }
    }

    void TrySpawn()
    {
        if (GetAliveCount() >= maxAliveEnemies) return;

        // Pick a random enemy type with equal probability
        if (enemyPools.Count == 0) return;

        var prefabKeys = new List<EnemyShooter2D>(enemyPools.Keys);
        EnemyShooter2D selectedPrefab = prefabKeys[Random.Range(0, prefabKeys.Count)];

        if (!enemyPools.TryGetValue(selectedPrefab, out var pool)) return;

        if (!TryFindNonOverlappingSpawn(out Vector2 spawnPos)) return;

        EnemyShooter2D e = pool.Get();
        e.ResetFromPool(player, spawnPos, ElementType.Fire);
        
        // Apply spawn-time speed multiplier so designers can globally slow/speed enemies
        e.moveSpeed *= spawnMoveSpeedMultiplier;

        CacheParticleCollisionDefaults(e);

        var pc = player != null ? player.GetComponent<PlayerController2D>() : null;
        if (pc != null) OnPlayerElementChanged(pc.currentElement);

        float interval = Random.Range(emissionIntervalMin, emissionIntervalMax);
        StartCoroutine(ApplyEmissionIntervalAndRestartNextFrame(e.transform, interval));
    }

    void CacheParticleCollisionDefaults(EnemyShooter2D e)
    {
        if (e == null) return;

        var systems = e.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            var ps = systems[i];
            if (ps == null) continue;

            if (!particleCollisionDefaults.ContainsKey(ps))
            {
                var col = ps.collision;
                particleCollisionDefaults[ps] = new ParticleCollisionDefault
                {
                    enabled = col.enabled,
                    collidesWith = col.collidesWith
                };
            }
        }
    }

    public void OnPlayerElementChanged(ElementType playerElement)
    {
        for (int i = 0; i < allActiveEnemies.Count; i++)
        {
            var e = allActiveEnemies[i];
            if (e == null) continue;
            if (!e.gameObject.activeInHierarchy) continue;

            bool sameColor = e.currentElement == playerElement;

            var systems = e.GetComponentsInChildren<ParticleSystem>(true);
            for (int j = 0; j < systems.Length; j++)
            {
                var ps = systems[j];
                if (ps == null) continue;

                if (!particleCollisionDefaults.TryGetValue(ps, out var def))
                {
                    var col0 = ps.collision;
                    def = new ParticleCollisionDefault { enabled = col0.enabled, collidesWith = col0.collidesWith };
                    particleCollisionDefaults[ps] = def;
                }

                var collision = ps.collision;

                if (sameColor)
                {
                    collision.enabled = false;
                }
                else
                {
                    collision.enabled = def.enabled;
                    collision.collidesWith = def.collidesWith;
                }
            }
        }
    }

    IEnumerator ApplyEmissionIntervalAndRestartNextFrame(Transform monsterRoot, float interval)
    {
        yield return null;

        var systems = monsterRoot.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            var ps = systems[i];
            if (ps == null) continue;

            var emission = ps.emission;
            bool applied = false;

            int burstCount = emission.burstCount;
            if (burstCount > 0)
            {
                var bursts = new ParticleSystem.Burst[burstCount];
                emission.GetBursts(bursts);

                for (int b = 0; b < bursts.Length; b++)
                {
                    var burst = bursts[b];
                    burst.time = interval;
                    bursts[b] = burst;
                }

                emission.SetBursts(bursts);
                applied = true;
            }

            if (!applied)
            {
                float rate = interval <= 0f ? 0f : (1f / interval);
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(rate);
            }

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
            ps.Simulate(0f, true, true, true);
            ps.Play(true);
        }
    }

    bool TryFindNonOverlappingSpawn(out Vector2 spawnPos)
    {
        spawnPos = Vector2.zero;
        if (spawnPoints == null || spawnPoints.Length == 0) return false;

        LayerMask mask = enemyLayer.value != 0 ? enemyLayer : (1 << gameObject.layer);

        for (int i = 0; i < spawnTryCount; i++)
        {
            Transform sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
            Vector2 basePos = sp.position;

            Vector2 jitter = Random.insideUnitCircle * spawnJitterRadius;
            Vector2 p = basePos + jitter;

            if (Physics2D.OverlapCircle(p, spawnClearRadius, mask) == null)
            {
                spawnPos = p;
                return true;
            }
        }

        return false;
    }

    int GetAliveCount()
    {
        // Clean up null references
        allActiveEnemies.RemoveAll(e => e == null || !e.gameObject.activeInHierarchy);
        return allActiveEnemies.Count;
    }

    /// <summary>
    /// Release an enemy back to its appropriate pool.
    /// </summary>
    public void ReleaseEnemy(EnemyShooter2D enemy)
    {
        if (enemy == null) return;

        // Find which pool this enemy belongs to by checking prefab reference
        foreach (var kvp in enemyPools)
        {
            // Compare by prefab name since instance won't match prefab directly
            if (enemy.name.StartsWith(kvp.Key.name))
            {
                kvp.Value.Release(enemy);
                return;
            }
        }

        Debug.LogWarning($"[EnemyPoolManager] Could not find pool for enemy {enemy.name}");
    }

    void OnDestroy()
    {
        // Clear all pools
        foreach (var pool in enemyPools.Values)
        {
            pool.Clear();
        }
        enemyPools.Clear();
        allActiveEnemies.Clear();
    }
}
