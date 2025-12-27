// File: Enemy/EnemyShooter.cs
using UnityEngine;
using GameJam.Common;
using System;
using Object = UnityEngine.Object;

[Obsolete("Please don't use this")]
public class EnemyShooter : MonoBehaviour, IElementDamageable
{
    [Header("Target")]
    public Transform player;

    [Header("Shoot")]
    public BulletPool bulletPool;
    public float shootInterval = 0.65f;
    public float bulletSpeed = 9.5f;

    [Header("Element")]
    public bool useCycle = true;
    public ElementType fixedElement = ElementType.Water;

    [Header("Stats")]
    public int hp = 3;

    [Header("Visual")]
    public SpriteRenderer bodyRenderer;

    float t;
    ElementCycle cycle;

    void Awake()
    {
        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<SpriteRenderer>(true);
        cycle = GetComponent<ElementCycle>();
        if (cycle == null) cycle = gameObject.AddComponent<ElementCycle>();
        ApplyColor();
    }

    void Update()
    {
        if (player == null) return;
        if (bulletPool == null) return;

        t += Time.deltaTime;
        if (t < shootInterval) return;
        t = 0f;

        Vector3 p0 = transform.position;
        Vector3 pt = player.position;
        Vector2 dir = new Vector2(pt.x - p0.x, pt.y - p0.y);
        if (dir.sqrMagnitude < 1e-6f) dir = Vector2.right;

        ElementType e = useCycle ? cycle.Next() : fixedElement;
        if (!useCycle) fixedElement = e;

        bulletPool.Spawn(p0, dir.normalized, e, this, bulletSpeed);

        if (bodyRenderer != null) bodyRenderer.color = GameDefs.ElementToColor(e);

        Debug.Log($"[Enemy] Shoot -> Element={GameDefs.ElementToText(e)}");
    }

    void ApplyColor()
    {
        if (bodyRenderer == null) return;
        bodyRenderer.color = GameDefs.ElementToColor(useCycle ? ElementType.Fire : fixedElement);
    }

    // Enemy 也可被玩家子彈打（同元素不免疫，這邊先全部吃傷害）
    public bool CanBeHitBy(ElementType element, Object source)
    {
        return true;
    }

    public void TakeElementHit(ElementType element, int damage, Object source)
    {
        hp -= damage;
        Debug.Log($"[Enemy] Hit by {GameDefs.ElementToText(element)} dmg={damage} hp={hp}");
        if (hp <= 0)
        {
            Debug.Log("[Enemy] Dead");
            Destroy(gameObject);
        }
    }
}
