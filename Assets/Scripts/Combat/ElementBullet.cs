// File: Combat/ElementBullet.cs
using System.Collections.Generic;
using UnityEngine;
using GameJam.Common;
using System.Text;


[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class ElementBullet : MonoBehaviour
{
    [Header("Bullet")]
    public float speed = 12f;
    public float lifeSeconds = 2.2f;
    public int damage = 1;

    [Header("Pierce")]
    public bool canPierceUnits = false;
    public int maxPierceHits = -1;

    [Header("Visual")]
    public SpriteRenderer sr;

    [Header("Collision")]
    [Tooltip("Layers that will cause the bullet to despawn immediately (bitmask)")]
    public LayerMask NoEffectMask;

    Rigidbody2D rb;
    float life;
    ElementType element;
    Object owner;
    BulletPool pool;

    int pierceHitCount;
    readonly HashSet<int> hitIds = new HashSet<int>();

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
        Init(ownerPool, position, direction, e, bulletOwner, overrideSpeed, canPierceUnits, maxPierceHits);
    }

    public void Init(
        BulletPool ownerPool,
        Vector2 position,
        Vector2 direction,
        ElementType e,
        Object bulletOwner,
        float overrideSpeed,
        bool pierce,
        int pierceLimit = -1
    )
    {
        pool = ownerPool;
        element = e;
        owner = bulletOwner;

        canPierceUnits = pierce;
        maxPierceHits = pierceLimit;

        pierceHitCount = 0;
        hitIds.Clear();

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
        StringBuilder sb = new StringBuilder();

        sb.AppendLine($"[Bullet] Trigger enter: {name}");

        if (!gameObject.activeInHierarchy)
        {
            sb.AppendLine("Inactive in hierarchy, return.");
            Debug.Log(sb.ToString());
            return;
        }

        if (other == null)
        {
            sb.AppendLine("Other collider is null, return.");
            Debug.Log(sb.ToString());
            return;
        }

        sb.AppendLine($"Hit collider: {other.name}, layer: {other.gameObject.layer}");

        if ((NoEffectMask.value & (1 << other.gameObject.layer)) != 0)
        {
            sb.AppendLine("Layer in NoEffectMask, despawn.");
            Debug.Log(sb.ToString());
            Despawn();
            return;
        }

        if (owner != null && other.transform == (owner as Component)?.transform)
        {
            sb.AppendLine("Hit owner itself, ignore.");
            Debug.Log(sb.ToString());
            return;
        }

        var dmg = other.GetComponentInParent<IElementDamageable>();
        if (dmg == null)
        {
            sb.AppendLine("No IElementDamageable found, return.");
            Debug.Log(sb.ToString());
            return;
        }

        sb.AppendLine($"Damageable found: {dmg.GetType().Name}");

        int id = other.GetInstanceID();
        if (hitIds.Contains(id))
        {
            sb.AppendLine($"Already hit instanceID {id}, ignore.");
            Debug.Log(sb.ToString());
            return;
        }

        hitIds.Add(id);
        sb.AppendLine($"Register hit instanceID {id}");

        bool didDamage = false;

        if (dmg is EnemyShooter2D enemy)
        {
            sb.AppendLine("Target is EnemyShooter2D");
            sb.AppendLine($"Enemy element: {enemy.currentElement}, Bullet element: {element}");

            if (enemy.currentElement == element)
            {
                sb.AppendLine("Element matched, dealing damage.");
                dmg.TakeElementHit(element, damage, owner);
                didDamage = true;
            }
            else
            {
                sb.AppendLine("Element mismatch, no damage.");
            }
        }
        else
        {
            bool canHit = dmg.CanBeHitBy(element);
            sb.AppendLine($"Target is non-enemy, CanBeHitBy = {canHit}");

            if (canHit)
            {
                sb.AppendLine("Can hit, dealing damage.");
                dmg.TakeElementHit(element, damage, owner);
                didDamage = true;
            }
            else
            {
                sb.AppendLine("Cannot hit, no damage.");
            }
        }

        if (!didDamage)
        {
            sb.AppendLine("No damage dealt, return.");
            Debug.Log(sb.ToString());
            return;
        }

        if (canPierceUnits)
        {
            pierceHitCount++;
            sb.AppendLine($"Pierce enabled, count = {pierceHitCount}");

            if (maxPierceHits >= 0 && pierceHitCount >= maxPierceHits)
            {
                sb.AppendLine("Max pierce hits reached, despawn.");
                Debug.Log(sb.ToString());
                Despawn();
                return;
            }

            sb.AppendLine("Pierce continues, bullet stays.");
            Debug.Log(sb.ToString());
            return;
        }

        sb.AppendLine("No pierce, despawn.");
        Debug.Log(sb.ToString());
        Despawn();
    }

    public void Despawn()
    {
        rb.linearVelocity = Vector2.zero;
        if (pool != null) pool.Release(this);
        else gameObject.SetActive(false);
    }
}
