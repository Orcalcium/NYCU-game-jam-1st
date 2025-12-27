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

        Vector2 spawnPos;
        if (!TryFindNonOverlappingSpawn(out spawnPos)) return;

        e.gameObject.SetActive(true);
        e.ResetFromPool(player, spawnPos, ElementType.Fire);
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
