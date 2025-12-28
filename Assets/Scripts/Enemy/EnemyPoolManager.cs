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

    [Header("Difficulty System")]
    [Tooltip("Starting value for low difficulty threshold")]
    public float baseDifficultyLow = 5f;
    
    [Tooltip("Starting value for high difficulty threshold")]
    public float baseDifficultyHigh = 10f;
    
    [Tooltip("How fast difficulty thresholds increase per second")]
    public float difficultyGrowthRate = 0.5f;
    
    [Tooltip("Multiplier for enemy start threshold (enemy spawns when low >= startThreshold * ratio)")]
    public float startThresholdRatio = 1.0f;

    [Header("Wave Spawning")]
    [Tooltip("How many spawn attempts per wave")]
    public int waveSpawnAttempts = 5;
    
    [Tooltip("Randomness factor (0 = always pick hardest available, 1 = fully random)")]
    [Range(0f, 1f)]
    public float waveRandomness = 0.3f;
    
    [Tooltip("Time between wave spawn checks (seconds)")]
    public float waveCheckInterval = 2f;
    
    [Tooltip("Delay before first wave spawns (seconds)")]
    public float initialSpawnDelay = 3f;

    [Header("Spawn")]
    public Transform[] spawnPoints;
    public Transform player;
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

    [Header("Debug")]
    // Difficulty tracking
    [SerializeField] private float currentDifficulty = 0f;
    [SerializeField] private float difficultyThresholdLow;
    [SerializeField] private float difficultyThresholdHigh;
    private float gameTime = 0f;
    private float waveCheckTimer = 0f;
    private bool hasStartedSpawning = false;

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

        // Initialize difficulty thresholds
        difficultyThresholdLow = baseDifficultyLow;
        difficultyThresholdHigh = baseDifficultyHigh;

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

        Debug.Log($"[EnemyPoolManager] Initialized with {enemyPools.Count} enemy types");
    }

    void Update()
    {
        // Update game time and difficulty thresholds
        gameTime += Time.deltaTime;
        UpdateDifficultyThresholds();

        // Update current difficulty based on active enemies
        CalculateCurrentDifficulty();

        // Wait for initial delay before starting spawns
        if (!hasStartedSpawning)
        {
            if (gameTime >= initialSpawnDelay)
            {
                hasStartedSpawning = true;
                waveCheckTimer = 0f; // Spawn immediately after delay
            }
            return;
        }

        // Check if we should spawn a wave
        waveCheckTimer -= Time.deltaTime;
        if (waveCheckTimer <= 0f)
        {
            waveCheckTimer = waveCheckInterval;
            
            if (ShouldSpawnWave())
            {
                SpawnWave();
            }
        }
    }

    private void UpdateDifficultyThresholds()
    {
        difficultyThresholdLow = baseDifficultyLow + (gameTime * difficultyGrowthRate * 0.5f);
        difficultyThresholdHigh = baseDifficultyHigh + (gameTime * difficultyGrowthRate);
    }

    private void CalculateCurrentDifficulty()
    {
        currentDifficulty = 0f;
        
        // Clean up null/inactive enemies
        allActiveEnemies.RemoveAll(e => e == null || !e.gameObject.activeInHierarchy);
        
        // Sum difficulty of all active enemies
        foreach (var enemy in allActiveEnemies)
        {
            if (enemy != null && enemy.gameObject.activeInHierarchy)
            {
                currentDifficulty += enemy.difficulty;
            }
        }
    }

    private bool ShouldSpawnWave()
    {
        return currentDifficulty < difficultyThresholdLow;
    }

    private void SpawnWave()
    {
        Debug.Log($"[EnemyPoolManager] Spawning wave - Current: {currentDifficulty:F1}, Target: {difficultyThresholdHigh:F1}");
        
        int spawned = 0;
        int attempts = 0;
        
        while (currentDifficulty < difficultyThresholdHigh && attempts < waveSpawnAttempts)
        {
            attempts++;
            
            // Get eligible enemies
            List<EnemyShooter2D> eligible = GetEligibleEnemies();
            
            if (eligible.Count == 0)
            {
                Debug.LogWarning("[EnemyPoolManager] No eligible enemies to spawn!");
                break;
            }
            
            // Select random enemy from eligible list
            EnemyShooter2D selectedPrefab = SelectRandomEnemy(eligible);
            
            if (selectedPrefab == null) continue;
            
            // Try to spawn
            if (TrySpawnEnemy(selectedPrefab))
            {
                spawned++;
                currentDifficulty += selectedPrefab.difficulty;
            }
        }
        
        Debug.Log($"[EnemyPoolManager] Wave complete - Spawned {spawned} enemies, Current difficulty: {currentDifficulty:F1}");
    }

    private List<EnemyShooter2D> GetEligibleEnemies()
    {
        List<EnemyShooter2D> eligible = new List<EnemyShooter2D>();
        
        float threshold = gameTime;
        
        foreach (var prefab in enemyPools.Keys)
        {
            if (prefab.startThreshold <= threshold)
            {
                eligible.Add(prefab);
            }
        }
        
        return eligible;
    }

    private EnemyShooter2D SelectRandomEnemy(List<EnemyShooter2D> eligible)
    {
        if (eligible.Count == 0) return null;
        if (eligible.Count == 1) return eligible[0];
        
        // Apply randomness
        if (waveRandomness >= 1f || Random.value < waveRandomness)
        {
            // Fully random selection
            return eligible[Random.Range(0, eligible.Count)];
        }
        else
        {
            // Weighted selection based on difficulty (prefer harder enemies as threshold grows)
            float totalWeight = 0f;
            foreach (var e in eligible)
            {
                totalWeight += e.difficulty;
            }
            
            float rand = Random.Range(0f, totalWeight);
            float cumulative = 0f;
            
            foreach (var e in eligible)
            {
                cumulative += e.difficulty;
                if (rand <= cumulative)
                {
                    return e;
                }
            }
            
            return eligible[eligible.Count - 1];
        }
    }

    private bool TrySpawnEnemy(EnemyShooter2D prefab)
    {
        if (!enemyPools.TryGetValue(prefab, out var pool)) return false;
        
        if (!TryFindNonOverlappingSpawn(out Vector2 spawnPos)) return false;

        EnemyShooter2D e = pool.Get();
        e.ResetFromPool(player, spawnPos, ElementType.Fire);
        
        // Apply spawn-time speed multiplier
        e.moveSpeed *= spawnMoveSpeedMultiplier;

        CacheParticleCollisionDefaults(e);

        var pc = player != null ? player.GetComponent<PlayerController2D>() : null;
        if (pc != null) OnPlayerElementChanged(pc.currentElement);

        float interval = Random.Range(emissionIntervalMin, emissionIntervalMax);
        StartCoroutine(ApplyEmissionIntervalAndRestartNextFrame(e.transform, interval));
        
        return true;
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
        
        // Recalculate difficulty when enemy despawns
        CalculateCurrentDifficulty();
    }

    private void OnDestroyEnemy(EnemyShooter2D enemy)
    {
        if (enemy != null)
        {
            allActiveEnemies.Remove(enemy);
            Destroy(enemy.gameObject);
        }
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

#if UNITY_EDITOR
    void OnGUI()
    {
        // Debug display
        if (!Application.isPlaying) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label($"<b>Enemy Difficulty System</b>", new GUIStyle(GUI.skin.label) { richText = true });
        GUILayout.Label($"Current Difficulty: {currentDifficulty:F1}");
        GUILayout.Label($"Low Threshold: {difficultyThresholdLow:F1}");
        GUILayout.Label($"High Threshold: {difficultyThresholdHigh:F1}");
        GUILayout.Label($"Active Enemies: {allActiveEnemies.Count}");
        GUILayout.Label($"Game Time: {gameTime:F1}s");
        GUILayout.EndArea();
    }
#endif
}
