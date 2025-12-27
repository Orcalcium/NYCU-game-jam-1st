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
    public ParticleSystem particleSystem;
    public int particleDamage = 1;

    [Header("State")]
    public ElementType currentElement = ElementType.Fire;
    public int hp = 3;

    [Header("Visual")]
    public SpriteRenderer bodyRenderer;

    Rigidbody2D rb;
    bool dead;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearDamping = 8f;

        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<SpriteRenderer>(true);
        
        // Get particle system from children if not assigned
        if (particleSystem == null) particleSystem = GetComponentInChildren<ParticleSystem>(true);

        ApplyElementVisual();
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

    void OnParticleCollision(GameObject other)
    {
        if (dead) return;

        // Check if the collision is with a damageable object
        IElementDamageable damageable = other.GetComponent<IElementDamageable>();
        if (damageable != null)
        {
            if (damageable.CanBeHitBy(currentElement, this))
            {
                damageable.TakeElementHit(currentElement, particleDamage, this);
                Debug.Log($"[EnemyShooter2D] Particle hit {other.name} with {GameDefs.ElementToText(currentElement)}");
            }
        }
    }

    public void ApplyElementVisual()
    {
        if (bodyRenderer == null) return;
        bodyRenderer.color = GameDefs.ElementToColor(currentElement);
        
        // Update particle system color if available
        if (particleSystem != null)
        {
            var main = particleSystem.main;
            main.startColor = GameDefs.ElementToColor(currentElement);
        }
    }

    public bool CanBeHitBy(ElementType element, Object source)
    {
        // Can be hit by any element (including same color) as long as not dead
        return !dead;
    }

    public void TakeElementHit(ElementType element, int damage, Object source)
    {
        if (!CanBeHitBy(element, source)) return;

        hp -= damage;

        // Change element when hit
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
