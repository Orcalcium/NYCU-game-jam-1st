// File: Enemy/MonsterPool.cs
using System.Collections.Generic;
using UnityEngine;

public class MonsterPool : MonoBehaviour
{
    [Header("Pool")]
    public Monster prefab;
    public int prewarmCount = 12;

    [Header("Active Limit")]
    public int maxActive = 10;

    readonly Queue<Monster> pool = new Queue<Monster>();
    readonly HashSet<Monster> activeSet = new HashSet<Monster>();

    public int ActiveCount => activeSet.Count;

    void Awake()
    {
        Prewarm();
    }

    void Prewarm()
    {
        if (prefab == null) return;

        for (int i = 0; i < prewarmCount; i++)
        {
            Monster m = Instantiate(prefab, transform);
            m.gameObject.SetActive(false);
            pool.Enqueue(m);
        }
    }

    public Monster Spawn(Vector3 position, Transform player)
    {
        if (prefab == null) return null;
        if (activeSet.Count >= maxActive) return null;

        Monster m = pool.Count > 0 ? pool.Dequeue() : Instantiate(prefab, transform);
        m.transform.position = position;
        m.gameObject.SetActive(true);
        m.Init(player, this);

        activeSet.Add(m);
        return m;
    }

    public void Release(Monster monster)
    {
        if (monster == null) return;

        activeSet.Remove(monster);

        monster.gameObject.SetActive(false);
        monster.transform.SetParent(transform, true);
        pool.Enqueue(monster);
    }
}
