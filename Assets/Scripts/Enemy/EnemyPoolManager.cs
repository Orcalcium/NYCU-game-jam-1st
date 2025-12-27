// File: Combat/EnemyPoolManager.cs
using UnityEngine;
using System.Collections.Generic;
using GameJam.Common;

public class EnemyPoolManager : MonoBehaviour
{
    [Header("Pool")]
    public EnemyShooter2D enemyPrefab;
    public int poolSize = 10;
    public int maxAliveEnemies = 10;

    [Header("Spawn")]
    public Transform[] spawnPoints;
    public Transform player;
    public float spawnInterval = 2.5f;

    [Header("Spawn Anti-Overlap")]
    public LayerMask enemyLayer;
    public float spawnClearRadius = 0.7f;
    public int spawnTryCount = 20;
    public float spawnJitterRadius = 0.6f;

    [Header("Monster VFX")]
    public float emissionIntervalMin = 3f;
    public float emissionIntervalMax = 5f;

    readonly List<EnemyShooter2D> pool = new();
    float spawnTimer;

    void Awake()
    {
        for (int i = 0; i < poolSize; i++)
        {
            EnemyShooter2D e = Instantiate(enemyPrefab, transform);
            e.gameObject.SetActive(false);
            pool.Add(e);
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

        float interval = Random.Range(emissionIntervalMin, emissionIntervalMax);
        StartCoroutine(ApplyEmissionIntervalAndRestartNextFrame(e.transform, interval));
    }

    System.Collections.IEnumerator ApplyEmissionIntervalAndRestartNextFrame(Transform monsterRoot, float interval)
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
                    burst.repeatInterval = interval;
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
            if (!pool[i].gameObject.activeSelf) return pool[i];
        return null;
    }

    int GetAliveCount()
    {
        int c = 0;
        for (int i = 0; i < pool.Count; i++)
            if (pool[i].gameObject.activeSelf) c++;
        return c;
    }
}
