// File: Enemy/MonsterPool.cs
using System.Collections.Generic;
using UnityEngine;

public class MonsterPool : MonoBehaviour
{
    [Header("Pool")]
    public Monster prefab;
    public int prewarmCount = 12;

    readonly Queue<Monster> pool = new Queue<Monster>();

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
            
        Monster m = pool.Count > 0 ? pool.Dequeue() : Instantiate(prefab, transform);
        m.transform.position = position;
        m.gameObject.SetActive(true);
        m.Init(player, this);
        return m;
    }

    public void Release(Monster monster)
    {
        if (monster == null) return;

        monster.gameObject.SetActive(false);
        monster.transform.SetParent(transform, true);
        pool.Enqueue(monster);
    }
}
