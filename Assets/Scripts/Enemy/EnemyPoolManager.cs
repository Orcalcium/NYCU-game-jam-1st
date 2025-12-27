// File: Combat/EnemyPoolManager.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using GameJam.Common;

public class EnemyPoolManager : MonoBehaviour
{
    public static EnemyPoolManager Instance { get; private set; }

    [Header("Pool")]
    public EnemyShooter2D enemyPrefab;
    public int poolSize = 10;
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

    readonly List<EnemyShooter2D> pool = new();
    readonly Dictionary<ParticleSystem, ParticleCollisionDefault> particleCollisionDefaults = new();
    float spawnTimer;

    void Awake()
    {
        Instance = this;

        for (int i = 0; i < poolSize; i++)
        {
            EnemyShooter2D e = Instantiate(enemyPrefab, transform);
            e.gameObject.SetActive(false);
            pool.Add(e);

            CacheParticleCollisionDefaults(e);
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

    void TrySpawn()
    {
        if (GetAliveCount() >= maxAliveEnemies) return;

        EnemyShooter2D e = GetInactiveEnemy();
        if (e == null) return;

        if (!TryFindNonOverlappingSpawn(out Vector2 spawnPos)) return;

        e.gameObject.SetActive(true);
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
        for (int i = 0; i < pool.Count; i++)
        {
            var e = pool[i];
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

    EnemyShooter2D GetInactiveEnemy()
    {
        for (int i = 0; i < pool.Count; i++)
            if (!pool[i].gameObject.activeInHierarchy) return pool[i];
        return null;
    }

    int GetAliveCount()
    {
        int c = 0;
        for (int i = 0; i < pool.Count; i++)
            if (pool[i].gameObject.activeInHierarchy) c++;
        return c;
    }
}
