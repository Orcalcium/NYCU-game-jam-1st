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

    readonly List<EnemyShooter2D> pool = new();
    float spawnTimer;

    void Awake()
    {
        for (int i = 0; i < poolSize; i++)
        {
            EnemyShooter2D e = Instantiate(enemyPrefab, transform);
            e.gameObject.SetActive(false);
            e.target = player;
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

        Transform sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
        e.transform.position = sp.position;

        // ★ 隨機指定初始元素
        ElementType randElement = GetRandomElement();
        e.currentElement = randElement;
        e.ApplyElementVisual();

        e.gameObject.SetActive(true);
    }

    EnemyShooter2D GetInactiveEnemy()
    {
        foreach (var e in pool)
        {
            if (!e.gameObject.activeSelf)
                return e;
        }
        return null;
    }

    int GetAliveCount()
    {
        int c = 0;
        foreach (var e in pool)
        {
            if (e.gameObject.activeSelf) c++;
        }
        return c;
    }

    ElementType GetRandomElement()
    {
        // 僅使用三屬性
        ElementType[] elements =
        {
            ElementType.Fire,
            ElementType.Water,
            ElementType.Nature
        };
        return elements[Random.Range(0, elements.Length)];
    }
}
