// File: Enemy/Monster.cs
using UnityEngine;
using GameJam.Common;

public class Monster : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 2.5f;
    public float stopDistance = 0.25f;

    [Header("Element Shields (3 hits total, must break in order)")]
    public ElementType[] shields = new ElementType[3]
    {
        ElementType.Fire,
        ElementType.Water,
        ElementType.Nature
    };

    Transform target;
    int shieldIndex;
    MonsterPool ownerPool;
    bool active;

    public void Init(Transform player, MonsterPool pool)
    {
        target = player;
        ownerPool = pool;
        shieldIndex = 0;
        active = true;

        Debug.Log($"[Monster] Spawned. Shields order: {GameDefs.ElementToText(shields[0])} -> {GameDefs.ElementToText(shields[1])} -> {GameDefs.ElementToText(shields[2])}");
    }

    void Update()
    {
        if (!active || target == null) return;

        Vector3 p = transform.position;
        Vector3 t = target.position;

        Vector3 dir = t - p;
        dir.z = 0f;

        float dist = dir.magnitude;
        if (dist <= stopDistance) return;

        transform.position = Vector3.MoveTowards(p, new Vector3(t.x, t.y, p.z), moveSpeed * Time.deltaTime);
    }

    public void TakeHit(ElementType element)
    {
        if (!active) return;

        ElementType required = shields[Mathf.Clamp(shieldIndex, 0, shields.Length - 1)];

        if (element != required)
        {
            Debug.Log($"[Monster] Hit blocked. Need {GameDefs.ElementToText(required)} but got {GameDefs.ElementToText(element)}");
            return;
        }

        Debug.Log($"[Monster] Shield broken: {GameDefs.ElementToText(required)} (index {shieldIndex + 1}/3)");
        shieldIndex++;

        if (shieldIndex >= shields.Length)
        {
            Die();
        }
        else
        {
            ElementType next = shields[shieldIndex];
            Debug.Log($"[Monster] Next shield: {GameDefs.ElementToText(next)}");
        }
    }

    void Die()
    {
        Debug.Log("[Monster] Defeated -> Return to pool");
        Despawn();
    }

    public void Despawn()
    {
        if (!active) return;
        active = false;

        target = null;

        if (ownerPool != null)
            ownerPool.Release(this);
        else
            gameObject.SetActive(false);
    }
}
