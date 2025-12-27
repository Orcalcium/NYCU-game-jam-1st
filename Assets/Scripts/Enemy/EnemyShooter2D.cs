// File: Combat/EnemyShooter2D.cs
using UnityEngine;
using GameJam.Common;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyShooter2D : MonoBehaviour, IElementDamageable
{
    [Header("Target")]
    public Transform target;
    public float stopDistance = 6.5f;
    public float keepDistance = 0.25f;

    [Header("Move")]
    public float moveSpeed = 3.6f;

    [Header("Shoot")]
    public BulletPool bulletPool;
    public Transform firePoint;
    public float bulletSpeed = 10.5f;
    public float fireCooldown = 0.6f;

    [Header("State")]
    public ElementType currentElement = ElementType.Fire;
    public int hp = 3;

    [Header("Visual")]
    public SpriteRenderer bodyRenderer;

    Rigidbody2D rb;
    float fireCd;
    bool dead;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearDamping = 8f;

        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (firePoint == null) firePoint = transform;

        ApplyElementVisual();
    }

    void Update()
    {
        if (dead) return;
        if (fireCd > 0f) fireCd -= Time.deltaTime;
        TryShoot();
    }

    void FixedUpdate()
    {
        if (dead) { rb.linearVelocity = Vector2.zero; return; }
        if (target == null) { rb.linearVelocity = Vector2.zero; return; }

        Vector2 pos = rb.position;
        Vector2 tpos = (Vector2)target.position;
        Vector2 to = tpos - pos;
        float dist = to.magnitude;

        float minStop = Mathf.Max(0.01f, stopDistance - keepDistance);
        float maxStop = stopDistance + keepDistance;

        if (dist > maxStop)
        {
            Vector2 dir = to / dist;
            rb.linearVelocity = dir * moveSpeed;
        }
        else if (dist < minStop)
        {
            Vector2 dir = dist > 1e-6f ? (-to / dist) : Vector2.zero;
            rb.linearVelocity = dir * (moveSpeed * 0.6f);
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    void TryShoot()
    {
        if (target == null || bulletPool == null) return;
        if (fireCd > 0f) return;

        Vector2 pos = firePoint != null ? (Vector2)firePoint.position : rb.position;
        Vector2 dir = ((Vector2)target.position - pos);
        if (dir.sqrMagnitude < 1e-6f) dir = Vector2.right;
        dir.Normalize();

        bulletPool.Spawn(pos, dir, currentElement, this, bulletSpeed);
        fireCd = fireCooldown;
    }

    public void ApplyElementVisual()
    {
        if (bodyRenderer == null) return;
        bodyRenderer.color = GameDefs.ElementToColor(currentElement);
    }


    public bool CanBeHitBy(ElementType element, Object source)
    {
        // 可依你規則調整：此處允許被任何元素命中（包含同色）
        return !dead;
    }

    public void TakeElementHit(ElementType element, int damage, Object source)
    {
        if (!CanBeHitBy(element, source)) return;

        hp -= damage;

        // 受擊後改變顏色（直接變成命中它的元素）
        currentElement = element;
        ApplyElementVisual();

        if (hp <= 0 && !dead)
        {
            dead = true;
            rb.linearVelocity = Vector2.zero;
            gameObject.SetActive(false);
        }
    }
}
