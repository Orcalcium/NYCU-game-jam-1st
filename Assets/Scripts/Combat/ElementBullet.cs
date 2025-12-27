// File: Combat/ElementBullet.cs
using UnityEngine;
using GameJam.Common;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class ElementBullet : MonoBehaviour
{
    [Header("Bullet")]
    public float speed = 12f;
    public float lifeSeconds = 2.2f;
    public int damage = 1;

    [Header("Visual")]
    public SpriteRenderer sr;

    Rigidbody2D rb;
    float life;
    ElementType element;
    Object owner;
    BulletPool pool;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // 確保是可被 velocity 推動的狀態（避免 Prefab 設錯導致不動）
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.simulated = true;
        rb.constraints = RigidbodyConstraints2D.None;
        rb.linearDamping = 0f;

        var col = GetComponent<Collider2D>();
        col.isTrigger = true;

        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>(true);
        gameObject.SetActive(false);
    }

    public void Init(BulletPool ownerPool, Vector2 position, Vector2 direction, ElementType e, Object bulletOwner, float overrideSpeed = -1f)
    {
        pool = ownerPool;
        element = e;
        owner = bulletOwner;

        // 先啟用，避免 OnEnable/Pool 初始化覆蓋你稍後設定的速度
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        transform.position = position;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        if (sr != null) sr.color = GameDefs.ElementToColor(e);

        life = lifeSeconds;

        float spd = overrideSpeed > 0f ? overrideSpeed : speed;

        // 再次確保可動（避免 Pool 或 Prefab 改過）
        rb.simulated = true;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.WakeUp();

        rb.linearVelocity = direction.normalized * spd;
    }

    void Update()
    {
        life -= Time.deltaTime;
        if (life <= 0f) Despawn();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!gameObject.activeInHierarchy) return;
        if (owner != null && other != null && other.transform == (owner as Component)?.transform) return;

        var dmg = other.GetComponentInParent<IElementDamageable>();
        if (dmg == null) return;

        if (!dmg.CanBeHitBy(element, owner))
            return;

        dmg.TakeElementHit(element, damage, owner);
        Despawn();
    }

    public void Despawn()
    {
        rb.linearVelocity = Vector2.zero;
        if (pool != null) pool.Release(this);
        else gameObject.SetActive(false);
    }
}
