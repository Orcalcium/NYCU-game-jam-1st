// File: Combat/BulletPool.cs
using System.Collections.Generic;
using UnityEngine;

public class BulletPool : MonoBehaviour
{
    public ElementBullet prefab;
    public int prewarm = 40;

    readonly Queue<ElementBullet> q = new Queue<ElementBullet>();

    void Awake()
    {
        if (prefab == null) return;

        for (int i = 0; i < prewarm; i++)
        {
            var b = Instantiate(prefab, transform);
            b.gameObject.SetActive(false);
            q.Enqueue(b);
        }
    }

    public ElementBullet Spawn(Vector2 pos, Vector2 dir, GameJam.Common.ElementType e, Object owner, float overrideSpeed = -1f)
    {
        if (prefab == null) return null;

        var b = q.Count > 0 ? q.Dequeue() : Instantiate(prefab, transform);
        b.Init(this, pos, dir, e, owner, overrideSpeed);
        return b;
    }

    public void Release(ElementBullet b)
    {
        if (b == null) return;
        b.gameObject.SetActive(false);
        b.transform.SetParent(transform, true);
        q.Enqueue(b);
    }
}
