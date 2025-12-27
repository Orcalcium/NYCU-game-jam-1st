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

        if (!gameObject.activeSelf) gameObject.SetActive(true);

        transform.position = position;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        if (sr != null) sr.color = GameDefs.ElementToColor(e);

        life = lifeSeconds;

        float spd = overrideSpeed > 0f ? overrideSpeed : speed;

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
        // If the bullet hit a shield (layer 12), just despawn without processing damage
        if (other != null && other.gameObject.layer == LayerMask.GetMask("Shield"))
        {
            Despawn();
            return;
        }

        if (owner != null && other != null && other.transform == (owner as Component)?.transform) return;

        var dmg = other.GetComponentInParent<IElementDamageable>();
        if (dmg == null) return;

        bool canHit = dmg.CanBeHitBy(element, owner);
        if (canHit)
        {
            dmg.TakeElementHit(element, damage, owner);
            Despawn();
            return;
        }

        if (dmg is EnemyShooter2D enemy)
        {
            if (enemy.currentElement == element)
            {
                dmg.TakeElementHit(element, damage, owner);
                Despawn();
                return;
            }
            return;
        }
    }

    public void Despawn()
    {
        rb.linearVelocity = Vector2.zero;
        if (pool != null) pool.Release(this);
        else gameObject.SetActive(false);
    }
}
