// File: Combat/EnemyShooter2D.cs
using System;
using UnityEngine;
using GameJam.Common;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyShooter2D : MonoBehaviour, IElementDamageable
{
    public event Action<Color> ChangeColor;

    [Header("Target")]
    public Transform target;
    public float stopDistance = 6.5f;
    public float keepDistance = 0.25f;

    [Header("Move")]
    [Tooltip("Base move speed for this enemy (lower is slower)")]
    public float moveSpeed = 2.2f;

    public enum MoveTriggerMode { Always, MoveWhenCloserThan, MoveWhenFartherThan, MoveWhenBetween }

    [Header("Behavior")]
    [Tooltip("Controls when the enemy will actively move relative to the target")] 
    public MoveTriggerMode moveTrigger = MoveTriggerMode.Always;
    [Tooltip("Distance threshold for the MoveWhenCloserThan/MoveWhenFartherThan modes")]
    public float activationDistance = 6.0f;
    [Tooltip("Minimum distance for MoveWhenBetween mode (exclusive)")]
    public float activationMinDistance = 2.0f;
    [Tooltip("Maximum distance for MoveWhenBetween mode (exclusive)")]
    public float activationMaxDistance = 8.0f;

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

    [Header("Separation")]
    [Tooltip("When enabled, enemies will prioritize separating from nearby neighbors before moving toward/away from the player")]
    public bool separationPriority = true;
    [Tooltip("Push magnitude threshold (in world units) above which separation takes priority")]
    public float separationPriorityThreshold = 0.01f;
    [Tooltip("Optional maximum speed to use while separating (0 = use pushMaxSpeed)")]
    public float separationMaxSpeed = 0f; // 0 means use pushMaxSpeed

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

        EnsureParticleBroadcasters();

        ApplyElementVisual();
    }

    void EnsureParticleBroadcasters()
    {
        var systems = GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            var ps = systems[i];
            if (ps == null) continue;

            var b = ps.GetComponent<EnemyParticleHitBroadcaster>();
            if (b == null) b = ps.gameObject.AddComponent<EnemyParticleHitBroadcaster>();
            b.owner = this;
            b.particleDamage = particleDamage;
        }
    }

    void FixedUpdate()
    {
        if (dead) { rb.linearVelocity = Vector2.zero; return; }
        if (target == null) { rb.linearVelocity = Vector2.zero; return; }

        Vector2 pos = rb.position;
        Vector2 tpos = (Vector2)target.position;
        Vector2 to = tpos - pos;
        float dist = to.magnitude;

        // Compute separation push (based on other nearby enemies)
        Vector2 separationPush = ComputePositionPush(pos);
        if (separationPriority && separationPush.sqrMagnitude > (separationPriorityThreshold * separationPriorityThreshold))
        {
            // Prioritize separation: move away from neighbors until the push weakens
            Vector2 sepVel = separationPush;
            float sepMaxSpd = (separationMaxSpeed > 0f) ? separationMaxSpeed : pushMaxSpeed;
            if (sepVel.sqrMagnitude > sepMaxSpd * sepMaxSpd) sepVel = sepVel.normalized * sepMaxSpd;
            rb.linearVelocity = sepVel;
            return;
        }

        // Movement activation check based on configured mode
        bool shouldMove = true;
        if (moveTrigger == MoveTriggerMode.MoveWhenCloserThan) shouldMove = dist < activationDistance;
        else if (moveTrigger == MoveTriggerMode.MoveWhenFartherThan) shouldMove = dist > activationDistance;
        else if (moveTrigger == MoveTriggerMode.MoveWhenBetween) shouldMove = (dist > activationMinDistance && dist < activationMaxDistance);

        if (!shouldMove)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

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

        // Add small push to avoid overlaps while moving (non-priority)
        v += separationPush;

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

    public void OnEnemyParticleHit(GameObject other)
    {
        Debug.Log($"[EnemyShooter2D] Particle hit {other.name} with {GameDefs.ElementToText(currentElement)}");
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

        ChangeColor?.Invoke(GameDefs.ElementToColor(currentElement));
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
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.WakeUp();

        if (bodyRenderer != null) bodyRenderer.enabled = true;

        BuildWeakpoints();

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

    public bool CanBeHitBy(ElementType element)
    {
        if (dead) return false;
        if (element == currentElement) return true;
        return false;
    }

    public void TakeElementHit(ElementType element, int damage, Object source)
    {
        if (!CanBeHitBy(element)) return;
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

    void OnValidate()
    {
        // Ensure sensible ranges
        if (activationMinDistance < 0f) activationMinDistance = 0f;
        if (activationMaxDistance < 0f) activationMaxDistance = 0f;
        if (activationMaxDistance < activationMinDistance) activationMaxDistance = activationMinDistance;
    }
}
