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

    [Header("Wall Avoid")]
    [Tooltip("Layers treated as walls/obstacles for movement avoidance")]
    public LayerMask wallLayers;
    [Tooltip("Extra cast padding beyond collider radius/extent")]
    public float wallCastPadding = 0.03f;
    [Tooltip("How far ahead to probe for a wall (scaled by speed and fixedDeltaTime)")]
    public float wallProbeMultiplier = 1.25f;

    [Header("Movement Behavior")]
    [Tooltip("Select the movement behavior for this enemy (Default follows the target, Rampage moves randomly and charges in one-axis)")]
    public MovementMode movementMode = MovementMode.Default;

    public enum MovementMode { Default, Rampage }

    [Tooltip("Speed used when in Rampage mode (units/sec)")]
    public float rampageMoveSpeed = 2.0f;
    [Tooltip("Minimum random direction change interval (seconds)")]
    public float rampageChangeIntervalMin = 0.5f;
    [Tooltip("Maximum random direction change interval (seconds)")]
    public float rampageChangeIntervalMax = 1.5f;

    public enum RampageAxisMode { Dominant, Horizontal, Vertical }
    [Tooltip("When charging, choose which axis to chase along (Dominant picks the axis with larger separation)")]
    public RampageAxisMode rampageAxisMode = RampageAxisMode.Dominant;

    // Runtime state for rampage movement
    float rampageDirTimer;
    Vector2 rampageDir;

    // Charging state requested by attacker components
    bool requestedCharging;

    bool IsCurrentlyCharging()
    {
        if (requestedCharging) return true;

        var zoneAttackers = GetComponents<ZoneAttacker>();
        for (int i = 0; i < zoneAttackers.Length; i++)
        {
            if (zoneAttackers[i] != null && zoneAttackers[i].IsAttacking()) return true;
        }

        return false;
    }

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

    [Header("Clamp (World Bounds)")]
    public float clampX = 13.5f;
    public float clampY = 7.5f;

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

        rampageDir = Random.insideUnitCircle.normalized;
        rampageDirTimer = Random.Range(rampageChangeIntervalMin, rampageChangeIntervalMax);
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

        Vector2 separationPush = ComputePositionPush(pos);
        if (separationPriority && separationPush.sqrMagnitude > (separationPriorityThreshold * separationPriorityThreshold))
        {
            Vector2 sepVel = separationPush;
            float sepMaxSpd = (separationMaxSpeed > 0f) ? separationMaxSpeed : pushMaxSpeed;
            if (sepVel.sqrMagnitude > sepMaxSpd * sepMaxSpd) sepVel = sepVel.normalized * sepMaxSpd;

            sepVel = ClampVelocityToClampBounds(pos, sepVel);
            rb.linearVelocity = sepVel;
            return;
        }

        bool shouldMove = true;
        if (moveTrigger == MoveTriggerMode.MoveWhenCloserThan) shouldMove = dist < activationDistance;
        else if (moveTrigger == MoveTriggerMode.MoveWhenFartherThan) shouldMove = dist > activationDistance;
        else if (moveTrigger == MoveTriggerMode.MoveWhenBetween) shouldMove = (dist > activationMinDistance && dist < activationMaxDistance);

        if (!shouldMove)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 v = ComputeDesiredVelocity(pos, to, dist);

        v += separationPush;

        float maxSpd = Mathf.Max(moveSpeed, pushMaxSpeed);
        if (v.sqrMagnitude > maxSpd * maxSpd) v = v.normalized * maxSpd;

        if (v.sqrMagnitude > 1e-8f && wallLayers.value != 0)
        {
            Vector2 velDir = v.normalized;
            float probeDist = Mathf.Max(0.02f, v.magnitude * Time.fixedDeltaTime * wallProbeMultiplier);
            if (WouldHitWall(pos, velDir, probeDist))
            {
                Vector2 away = (pos - tpos);
                Vector2 awayDir = away.sqrMagnitude > 1e-8f ? away.normalized : (-velDir);

                Vector2 altDir = PickBestUnblockedDir(pos, velDir, awayDir, probeDist);
                if (altDir.sqrMagnitude > 1e-8f)
                {
                    v = altDir * v.magnitude;
                }
            }
        }

        v = ClampVelocityToClampBounds(pos, v);

        rb.linearVelocity = v;
    }

    Vector2 ClampVelocityToClampBounds(Vector2 pos, Vector2 vel)
    {
        if (Mathf.Approximately(Time.fixedDeltaTime, 0f)) return vel;

        Vector2 next = pos + vel * Time.fixedDeltaTime;

        float minX = -Mathf.Abs(clampX);
        float maxX = Mathf.Abs(clampX);
        float minY = -Mathf.Abs(clampY);
        float maxY = Mathf.Abs(clampY);

        float clampedX = Mathf.Clamp(next.x, minX, maxX);
        float clampedY = Mathf.Clamp(next.y, minY, maxY);

        if (Mathf.Approximately(next.x, clampedX) && Mathf.Approximately(next.y, clampedY)) return vel;

        return new Vector2((clampedX - pos.x) / Time.fixedDeltaTime, (clampedY - pos.y) / Time.fixedDeltaTime);
    }

    Vector2 ComputeDesiredVelocity(Vector2 pos, Vector2 to, float dist)
    {
        if (movementMode == MovementMode.Rampage)
        {
            bool charging = IsCurrentlyCharging();
            if (charging && target != null)
            {
                Vector2 delta = ((Vector2)target.position) - pos;

                bool chaseHorizontal = false;
                if (rampageAxisMode == RampageAxisMode.Horizontal) chaseHorizontal = true;
                else if (rampageAxisMode == RampageAxisMode.Vertical) chaseHorizontal = false;
                else
                    chaseHorizontal = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y);

                Vector2 chase = Vector2.zero;
                if (chaseHorizontal)
                {
                    chase.x = Mathf.Sign(delta.x);
                    chase *= rampageMoveSpeed;
                }
                else
                {
                    chase.y = Mathf.Sign(delta.y);
                    chase *= rampageMoveSpeed;
                }

                return chase;
            }

            rampageDirTimer -= Time.fixedDeltaTime;
            if (rampageDirTimer <= 0f || rampageDir == Vector2.zero)
            {
                rampageDir = Random.insideUnitCircle.normalized;
                rampageDirTimer = Random.Range(rampageChangeIntervalMin, rampageChangeIntervalMax);
            }

            return rampageDir * rampageMoveSpeed;
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

        return desired * moveSpeed;
    }

    bool WouldHitWall(Vector2 pos, Vector2 dir, float distance)
    {
        if (dir.sqrMagnitude < 1e-8f) return false;

        float castRadius = GetCastRadius();
        var hit = Physics2D.CircleCast(pos, castRadius, dir, distance + wallCastPadding, wallLayers);
        return hit.collider != null;
    }

    float GetCastRadius()
    {
        if (col == null) return 0.2f;

        if (col is CircleCollider2D cc)
            return Mathf.Max(0.01f, cc.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y));

        if (col is CapsuleCollider2D cap)
        {
            float sx = Mathf.Abs(transform.lossyScale.x);
            float sy = Mathf.Abs(transform.lossyScale.y);
            Vector2 size = cap.size;
            float r = Mathf.Min(size.x * sx, size.y * sy) * 0.5f;
            return Mathf.Max(0.01f, r);
        }

        if (col is BoxCollider2D bc)
        {
            float sx = Mathf.Abs(transform.lossyScale.x);
            float sy = Mathf.Abs(transform.lossyScale.y);
            Vector2 size = bc.size;
            float r = Mathf.Min(size.x * sx, size.y * sy) * 0.5f;
            return Mathf.Max(0.01f, r);
        }

        var b = col.bounds;
        return Mathf.Max(0.01f, Mathf.Min(b.extents.x, b.extents.y));
    }

    Vector2 PickBestUnblockedDir(Vector2 pos, Vector2 currentDir, Vector2 awayDir, float probeDist)
    {
        Vector2 best = Vector2.zero;
        float bestScore = float.NegativeInfinity;

        Vector2 perp = new Vector2(-awayDir.y, awayDir.x);

        Vector2[] candidates =
        {
            awayDir,
            (awayDir + perp).normalized,
            (awayDir - perp).normalized,
            perp,
            -perp,
            (currentDir + perp).normalized,
            (currentDir - perp).normalized,
            -currentDir
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            Vector2 d = candidates[i];
            if (d.sqrMagnitude < 1e-8f) continue;
            if (WouldHitWall(pos, d, probeDist)) continue;

            float score = Vector2.Dot(d, awayDir) * 2.0f + Vector2.Dot(d, currentDir) * 0.35f;

            if (score > bestScore)
            {
                bestScore = score;
                best = d;
            }
        }

        return best;
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
        requestedCharging = false;

        if (col != null) col.enabled = true;

        rb.simulated = true;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.WakeUp();

        if (bodyRenderer != null) bodyRenderer.enabled = true;

        BuildWeakpoints();

        rampageDir = Random.insideUnitCircle.normalized;
        rampageDirTimer = Random.Range(rampageChangeIntervalMin, rampageChangeIntervalMax);

        rb.position += ComputePositionPush(rb.position) * Time.fixedDeltaTime;

        rb.position = new Vector2(
            Mathf.Clamp(rb.position.x, -Mathf.Abs(clampX), Mathf.Abs(clampX)),
            Mathf.Clamp(rb.position.y, -Mathf.Abs(clampY), Mathf.Abs(clampY))
        );
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
        requestedCharging = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        if (col != null) col.enabled = true;
        if (bodyRenderer != null) bodyRenderer.enabled = true;
    }

    public void SetCharging(bool charging)
    {
        requestedCharging = charging;
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
        if (activationMinDistance < 0f) activationMinDistance = 0f;
        if (activationMaxDistance < 0f) activationMaxDistance = 0f;
        if (activationMaxDistance < activationMinDistance) activationMaxDistance = activationMinDistance;
        if (wallProbeMultiplier < 0.1f) wallProbeMultiplier = 0.1f;
        if (wallCastPadding < 0f) wallCastPadding = 0f;

        if (rampageChangeIntervalMin < 0f) rampageChangeIntervalMin = 0f;
        if (rampageChangeIntervalMax < 0f) rampageChangeIntervalMax = 0f;
        if (rampageChangeIntervalMax < rampageChangeIntervalMin) rampageChangeIntervalMax = rampageChangeIntervalMin;

        if (clampX < 0f) clampX = 0f;
        if (clampY < 0f) clampY = 0f;
    }
}
