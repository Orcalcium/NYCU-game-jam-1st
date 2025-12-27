// File: Combat/EnemyShooter2D.cs
using UnityEngine;
using GameJam.Common;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyShooter2D : MonoBehaviour, IElementDamageable
{
    [Header("Target")]
    public Transform target;
    public float stopDistance = 6.5f;
    public float keepDistance = 0.25f;

    [Header("Move")]
    [Tooltip("Base move speed for this enemy (lower is slower)")]
    public float moveSpeed = 2.2f; // lowered default speed

    [Header("Shoot")]
    public new ParticleSystem particleSystem;
    public int particleDamage = 1;

    [Header("State")]
    public ElementType currentElement = ElementType.Fire;

    [Header("Weakpoints")]
    public int weakpointCount = 3;
    public EnemyWeakpointDots weakpointDots;
    public SpriteRenderer weakpointDotPrefab;

    [Header("Visual")]
    public SpriteRenderer bodyRenderer;

    [Header("Anti-Overlap (Position Push)")]
    public float pushMinDistance = 0.9f;
    public float pushStrength = 6.0f;
    public float pushMaxSpeed = 3.5f;
    public int pushNeighborsLimit = 12;

    Rigidbody2D rb;
    Collider2D col;

    float fireCd;
    bool dead;

    ElementType[] weakSequence;
    int weakIndex;

    static EnemyShooter2D[] allEnemiesCache = new EnemyShooter2D[0];
    static int allEnemiesCacheFrame = -1;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearDamping = 8f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        col = GetComponent<Collider2D>();
        col.isTrigger = true;

        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (particleSystem == null) particleSystem = GetComponentInChildren<ParticleSystem>(true);

        if (weakpointDots == null)
        {
            weakpointDots = GetComponentInChildren<EnemyWeakpointDots>(true);
            if (weakpointDots == null)
            {
                var go = new GameObject("WeakpointDots");
                go.transform.SetParent(transform, false);
                weakpointDots = go.AddComponent<EnemyWeakpointDots>();
            }
        }

        if (weakpointDotPrefab != null) weakpointDots.dotPrefab = weakpointDotPrefab;

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

        Vector2 desired;
        if (dist > maxStop)
        {
            desired = to / Mathf.Max(1e-6f, dist);
        }
        else if (dist < minStop)
        {
            desired = dist > 1e-6f ? (-to / dist) : Vector2.zero;
            desired *= 0.6f;
        }
        else
        {
            desired = Vector2.zero;
        }

        Vector2 v = desired * moveSpeed;

        // ★ 座標推開：不做 Physics overlap / distance 計算
        v += ComputePositionPush(pos);

        float maxSpd = Mathf.Max(moveSpeed, pushMaxSpeed);
        if (v.sqrMagnitude > maxSpd * maxSpd) v = v.normalized * maxSpd;

        rb.linearVelocity = v;
    }

    Vector2 ComputePositionPush(Vector2 pos)
    {
        if (Time.frameCount != allEnemiesCacheFrame)
        {
            allEnemiesCache = Object.FindObjectsByType<EnemyShooter2D>(FindObjectsSortMode.None);
            allEnemiesCacheFrame = Time.frameCount;
        }

        float minDist = Mathf.Max(0.01f, pushMinDistance);
        float minDist2 = minDist * minDist;

        Vector2 push = Vector2.zero;
        int used = 0;

        for (int i = 0; i < allEnemiesCache.Length; i++)
        {
            var e = allEnemiesCache[i];
            if (e == null) continue;
            if (e == this) continue;
            if (!e.gameObject.activeInHierarchy) continue;
            if (e.dead) continue;

            Vector2 otherPos = e.rb != null ? e.rb.position : (Vector2)e.transform.position;
            Vector2 diff = pos - otherPos;
            float d2 = diff.sqrMagnitude;

            if (d2 < 1e-6f)
            {
                diff = Random.insideUnitCircle.normalized * 0.001f;
                d2 = diff.sqrMagnitude;
            }

            if (d2 >= minDist2) continue;

            float d = Mathf.Sqrt(d2);
            float t = (minDist - d) / minDist;

            push += (diff / d) * t;
            used++;

            if (used >= pushNeighborsLimit) break;
        }

        if (used == 0) return Vector2.zero;

        push /= used;
        return push * pushStrength;
    }

    void OnParticleCollision(GameObject other)
    {
        Debug.Log($"[EnemyShooter2D] Particle hit {other.name} with {GameDefs.ElementToText(currentElement)}");
        if (dead) return;

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

        if (particleSystem != null)
        {
            var main = particleSystem.main;
            main.startColor = GameDefs.ElementToColor(currentElement);
        }
    }

    public void ResetFromPool(Transform newTarget, Vector2 spawnPos, ElementType _ignored)
    {
        target = newTarget;
        transform.position = spawnPos;

        dead = false;
        fireCd = 0f;

        if (col != null) col.enabled = true;

        rb.simulated = true;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.constraints = RigidbodyConstraints2D.None;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.WakeUp();

        if (bodyRenderer != null) bodyRenderer.enabled = true;

        BuildWeakpoints();

        // 生成後先推開一次（只用座標比對）
        rb.position += ComputePositionPush(rb.position) * Time.fixedDeltaTime;
    }

    void BuildWeakpoints()
    {
        weakSequence = GetShuffledTriElements();
        weakIndex = 0;

        currentElement = weakSequence[0];
        ApplyElementVisual();

        if (weakpointDots != null)
        {
            weakpointDots.dotCount = weakSequence.Length;
            weakpointDots.Build(weakSequence);
            weakpointDots.Refresh(0);
        }
    }

    ElementType[] GetShuffledTriElements()
    {
        ElementType[] arr =
        {
            ElementType.Fire,
            ElementType.Water,
            ElementType.Nature
        };

        for (int i = arr.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }

        return arr;
    }

    void OnDisable()
    {
        dead = false;
        fireCd = 0f;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        if (col != null) col.enabled = true;
        if (bodyRenderer != null) bodyRenderer.enabled = true;
    }

    public bool CanBeHitBy(ElementType element, Object source)
    {
        if (dead) return false;

        var pc = source as PlayerController2D;
        if (pc != null)
        {
            if (pc.currentElement == currentElement) return false;
        }

        return true;
    }

    public void TakeElementHit(ElementType element, int damage, Object source)
    {
        if (!CanBeHitBy(element, source)) return;
        if (weakSequence == null || weakSequence.Length == 0) return;

        if (element != currentElement) return;

        weakIndex++;

        if (weakpointDots != null)
            weakpointDots.Refresh(weakIndex);

        if (weakIndex < weakSequence.Length)
        {
            currentElement = weakSequence[weakIndex];
            ApplyElementVisual();
            return;
        }

        dead = true;
        rb.linearVelocity = Vector2.zero;
        gameObject.SetActive(false);
    }
}
