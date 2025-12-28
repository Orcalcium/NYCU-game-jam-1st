// File: Player/PlayerController2D.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using GameJam.Common;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController2D : MonoBehaviour, IElementDamageable
{
    [Header("Move (WASD)")]
    public float moveSpeed = 5.5f;

    [Header("Dash (Shift)")]
    public float dashSpeedMultiplier = 2.4f;
    public float dashDuration = 0.26f;
    public float dashCooldown = 0.75f;
    public float dashMaxDistance = 4.5f;

    [Header("Dash Damage (Along Path)")]
    public int dashDamage = 1;
    public float dashDamageRadius = 0.4f;
    public LayerMask dashDamageLayers;

    [Header("Shoot (Left Click)")]
    public BulletPool bulletPool;
    public Transform firePoint;
    public float bulletSpeed = 14f;
    public float fireCooldown = 0.12f;
    [Tooltip("Number of bullets in one burst")]
    public int burstCount = 3;
    [Tooltip("Time between bullets inside a burst (seconds)")]
    public float burstInterval = 0.05f;

    [Header("State")]
    public ElementType currentElement = ElementType.Fire;
    public int hp = 5;

    [Header("Visual")]
    public SpriteRenderer bodyRenderer;

    [Header("Camera")]
    public float cameraDamping = 1f;
    public float cameraOrthoSize = 8f;
    [Tooltip("If enabled, Player will automatically configure Main Camera to follow the player at Awake(). Disabled by default so you can assign/adjust the camera manually.")]
    public bool autoSetupCamera = false;

    [Header("Clamp (World Bounds)")]
    public float clampX = 13.5f;
    public float clampY = 7.5f;

    [Header("Skill Aim (Slow Time + Indicator)")]
    public float aimTimeScale = 0.15f;
    public float indicatorLineWidth = 0.06f;
    public int indicatorCircleSegments = 48;

    Rigidbody2D rb;
    Camera cam;
    Collider2D col;

    float dashTimer;
    float dashCd;
    float fireCd;
    bool invulnerable;
    bool isBursting;

    Vector2 dashDir;
    Vector2 dashStartPos;
    Vector2 dashTargetPos;
    bool dashUseTarget;

    public PlayerSkillCaster2D PlayerSkillCaster2D;

    static readonly ElementType[] Cycle = { ElementType.Fire, ElementType.Water, ElementType.Nature };
    int cycleIndex;

    bool isAimingSkill;
    float baseFixedDeltaTime;

    LineRenderer indicatorLR;

    readonly HashSet<int> dashHitIds = new HashSet<int>();

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearDamping = 8f;

        col = GetComponent<Collider2D>();
        cam = Camera.main;

        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (firePoint == null) firePoint = transform;

        if (PlayerSkillCaster2D == null) PlayerSkillCaster2D = GetComponent<PlayerSkillCaster2D>();

        cycleIndex = GetCycleIndex(currentElement);
        currentElement = Cycle[cycleIndex];
        ApplyElementVisual();

        EnemyPoolManager.Instance?.OnPlayerElementChanged(currentElement);

        if (autoSetupCamera)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                var cf = mainCam.GetComponent<CameraFollow2D>();
                if (cf == null) cf = mainCam.gameObject.AddComponent<CameraFollow2D>();
                cf.target = transform;
                cf.smoothTime = cameraDamping;
                mainCam.orthographicSize = cameraOrthoSize;
            }
        }

        var drag = GetComponent<MovementDragEffect>();
        if (drag == null) drag = gameObject.AddComponent<MovementDragEffect>();
        drag.sourceRigidbody = rb;
        drag.spriteSource = bodyRenderer;
        drag.enableTrail = true;
        drag.trailTime = 0.22f;
        drag.startWidth = 0.36f;
        drag.endWidth = 0.06f;
        drag.trailColor = new Color(1f, 1f, 1f, 0.55f);
        drag.minSpeedForTrail = 0.2f;

        baseFixedDeltaTime = Time.fixedDeltaTime;
        SetupIndicator();
    }

    void Update()
    {
        if (dashCd > 0f) dashCd -= Time.deltaTime;
        if (fireCd > 0f) fireCd -= Time.deltaTime;

        if (GameJam.UI.GameUIManager.Instance != null)
        {
            float progress = 1f;
            if (fireCooldown > 0f)
            {
                progress = Mathf.Clamp01(1f - (fireCd / fireCooldown));
            }
            GameJam.UI.GameUIManager.Instance.UpdateFireCooldown(progress);
        }

        if (dashTimer > 0f)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f)
            {
                invulnerable = false;
                if (col != null) col.enabled = true;
            }
        }

        HandleSkillInput();
        HandleDashInput();
        AimBodyToMouse();

        if (isAimingSkill)
            UpdateSkillIndicator();
    }

    public void HandleSkillInput()
    {
        if (Mouse.current == null) return;
        if (PlayerSkillCaster2D == null) return;

        Vector2 origin = (firePoint ? (Vector2)firePoint.position : rb.position);
        Vector2 aimWorld = PlayerSkillCaster2D.GetMouseWorldClamped(origin);
        Vector2 dir = aimWorld - origin;
        if (dir.sqrMagnitude > 1e-6f) dir.Normalize();

        // Left click -> shoot bullet (Pierce Shot) - automatic while held, respects fireCooldown
        if (Mouse.current.leftButton.isPressed && fireCd <= 0f && !isBursting)
        {
            // capture aim and element at burst start
            Vector2 burstDir = dir.sqrMagnitude > 0.0001f ? dir : Vector2.right;
            ElementType burstElem = currentElement;

            fireCd = fireCooldown; // set cooldown between bursts
            StartCoroutine(BurstFire(origin, burstDir, burstElem));
        }

        // Right click -> AoE Blast (press to aim, release to cast)
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            isAimingSkill = true;
            ApplyAimSlowTime(true);
            SetIndicatorEnabled(true);
            UpdateSkillIndicator();
        }

        if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            if (isAimingSkill)
            {
                isAimingSkill = false;
                ApplyAimSlowTime(false);
                SetIndicatorEnabled(false);

                bool casted = PlayerSkillCaster2D.CastAoe(origin);
                if (casted) CycleElementAfterSkill();
            }
        }

        // Space -> Blink Slash (dash attack)
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            bool used = false;
            if (dir.sqrMagnitude > 0.0001f)
            {
                used = PlayerSkillCaster2D.CastBlink(origin, dir);
            }
            else
            {
                used = PlayerSkillCaster2D.CastBlink(origin, Vector2.right);
            }

            if (used) CycleElementAfterSkill();
        }
    }

    void FixedUpdate()
    {
        if (dashTimer > 0f)
        {
            Vector2 cur = rb.position;

            float dashSpeed = moveSpeed * dashSpeedMultiplier;
            float stepDist = dashSpeed * Time.fixedDeltaTime;

            if (dashUseTarget)
            {
                Vector2 toTarget = dashTargetPos - cur;
                float remain = toTarget.magnitude;

                if (remain <= 0.02f)
                {
                    rb.position = ClampPos(dashTargetPos);
                    rb.linearVelocity = Vector2.zero;
                    dashTimer = 0f;
                    invulnerable = false;
                    if (col != null) col.enabled = true;
                    return;
                }

                Vector2 dir = toTarget.normalized;
                float castDist = Mathf.Min(remain, stepDist);
                DashDamageAlongPath(cur, dir, castDist);

                rb.linearVelocity = dir * dashSpeed;
                rb.position = ClampPos(rb.position);
                return;
            }
            else
            {
                float traveled = Vector2.Distance(dashStartPos, cur);
                if (traveled >= dashMaxDistance)
                {
                    rb.linearVelocity = Vector2.zero;
                    dashTimer = 0f;
                    invulnerable = false;
                    if (col != null) col.enabled = true;
                    return;
                }

                float remain = Mathf.Max(0f, dashMaxDistance - traveled);
                float castDist = Mathf.Min(remain, stepDist);
                DashDamageAlongPath(cur, dashDir, castDist);

                rb.linearVelocity = dashDir * dashSpeed;
                rb.position = ClampPos(rb.position);
                return;
            }
        }

        Vector2 move = ReadMoveInput();
        rb.linearVelocity = move * moveSpeed;
        rb.position = ClampPos(rb.position);
    }

    void DashDamageAlongPath(Vector2 start, Vector2 dir, float dist)
    {
        if (dist <= 0.0001f) return;

        RaycastHit2D[] hits = Physics2D.CircleCastAll(start, dashDamageRadius, dir, dist, dashDamageLayers);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D other = hits[i].collider;
            if (!other) continue;

            int id = other.GetInstanceID();
            if (dashHitIds.Contains(id)) continue;
            dashHitIds.Add(id);

            if (col != null && other == col) continue;

            var dmg = other.GetComponentInParent<IElementDamageable>();
            if (dmg == null) continue;

            bool canHit = dmg.CanBeHitBy(currentElement);
            if (canHit)
            {
                dmg.TakeElementHit(currentElement, dashDamage, this);
                continue;
            }

            if (dmg is EnemyShooter2D enemy)
            {
                if (enemy.currentElement == currentElement)
                {
                    dmg.TakeElementHit(currentElement, dashDamage, this);
                }
            }
        }
    }

    Vector2 ClampPos(Vector2 p)
    {
        p.x = Mathf.Clamp(p.x, -clampX, clampX);
        p.y = Mathf.Clamp(p.y, -clampY, clampY);
        return p;
    }

    Vector2 ReadMoveInput()
    {
        if (Keyboard.current == null) return Vector2.zero;

        float x = 0f;
        float y = 0f;

        if (Keyboard.current.aKey.isPressed) x -= 1f;
        if (Keyboard.current.dKey.isPressed) x += 1f;
        if (Keyboard.current.sKey.isPressed) y -= 1f;
        if (Keyboard.current.wKey.isPressed) y += 1f;

        Vector2 v = new Vector2(x, y);
        if (v.sqrMagnitude > 1f) v.Normalize();
        return v;
    }

    void HandleDashInput()
    {
        if (Keyboard.current == null) return;

        // Primary movement dash moved to Left Shift to avoid conflict with Blink (Space)
        if (Keyboard.current.leftShiftKey.wasPressedThisFrame)
        {
            if (dashCd > 0f) return;

            dashCd = dashCooldown;
            dashTimer = dashDuration;
            invulnerable = true;
            if (col != null) col.enabled = false;

            dashHitIds.Clear();

            Vector2 start = rb.position;
            dashStartPos = start;

            Vector2 mouseWorld = GetMouseWorld2D();
            Vector2 toMouse = mouseWorld - start;
            float dist = toMouse.magnitude;

            if (dist <= dashMaxDistance)
            {
                dashUseTarget = true;
                dashTargetPos = ClampPos(mouseWorld);
                dashDir = dist > 1e-6f ? (toMouse / dist) : Vector2.right;
            }
            else
            {
                dashUseTarget = false;
                dashDir = dist > 1e-6f ? (toMouse / dist) : Vector2.right;
                dashTargetPos = ClampPos(start + dashDir * dashMaxDistance);
            }
        }
    }

    void AimBodyToMouse()
    {
        Vector2 dir = GetAimDirection((Vector2)transform.position);
        if (dir.sqrMagnitude < 1e-6f) return;
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, ang);
    }

    Vector2 GetAimDirection(Vector2 origin)
    {
        Vector2 mouseWorld = GetMouseWorld2D();
        Vector2 dir = mouseWorld - origin;
        if (dir.sqrMagnitude < 1e-6f) return Vector2.right;
        return dir.normalized;
    }

    Vector2 GetMouseWorld2D()
    {
        if (cam == null) cam = Camera.main;
        if (Mouse.current == null || cam == null) return (Vector2)transform.position + Vector2.right;

        Vector2 sp = Mouse.current.position.ReadValue();
        Vector3 w = cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, -cam.transform.position.z));
        return new Vector2(w.x, w.y);
    }

    void ApplyElementVisual()
    {
        if (bodyRenderer == null) return;
        bodyRenderer.color = GameDefs.ElementToColor(currentElement);
    }

    int GetCycleIndex(ElementType e)
    {
        for (int i = 0; i < Cycle.Length; i++)
            if (Cycle[i] == e) return i;
        return 0;
    }

    public void CycleElementAfterSkill()
    {
        cycleIndex = (cycleIndex + 1) % Cycle.Length;
        currentElement = Cycle[cycleIndex];
        ApplyElementVisual();
        EnemyPoolManager.Instance?.OnPlayerElementChanged(currentElement);
    }

    public bool CanBeHitBy(ElementType element)
    {
        if (invulnerable) return false;
        if (element == currentElement) return false;
        return true;
    }

    public void TakeElementHit(ElementType element, int damage, Object source)
    {
        if (!CanBeHitBy(element)) return;

        hp -= damage;

        if (hp <= 0)
        {
            Debug.Log("[Player] Dead");
        }
    }

    void ApplyAimSlowTime(bool enabled)
    {
        if (enabled)
        {
            Time.timeScale = Mathf.Clamp(aimTimeScale, 0.01f, 1f);
            Time.fixedDeltaTime = baseFixedDeltaTime * Time.timeScale;
        }
        else
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = baseFixedDeltaTime;
        }
    }

    void SetupIndicator()
    {
        var go = new GameObject("SkillIndicator");
        go.transform.SetParent(transform, false);
        indicatorLR = go.AddComponent<LineRenderer>();
        indicatorLR.useWorldSpace = true;
        indicatorLR.loop = false;
        indicatorLR.enabled = false;
        indicatorLR.positionCount = 2;
        indicatorLR.startWidth = indicatorLineWidth;
        indicatorLR.endWidth = indicatorLineWidth;
        indicatorLR.numCapVertices = 8;

        // Ensure the LineRenderer has a visible material for 2D (Sprites/Default)
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.hideFlags = HideFlags.DontSave;
        indicatorLR.material = mat;
        indicatorLR.sortingOrder = 1000;
    }

    void SetIndicatorEnabled(bool enabled)
    {
        if (indicatorLR != null) indicatorLR.enabled = enabled;
    }

    void UpdateSkillIndicator()
    {
        if (indicatorLR == null || PlayerSkillCaster2D == null) return;

        Vector2 origin = (firePoint ? (Vector2)firePoint.position : rb.position);
        Vector2 aimWorld = PlayerSkillCaster2D.GetMouseWorldClamped(origin);
        Vector2 dir = aimWorld - origin;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        dir.Normalize();

        Color c = GameDefs.ElementToColor(currentElement);
        indicatorLR.startColor = c;
        indicatorLR.endColor = c;

        // If the player is actively aiming (right-click hold), show AoE indicator
        var skill = isAimingSkill ? PlayerSkillCaster2D.SkillType.AoEBlast : PlayerSkillCaster2D.GetCurrentSkill();

        if (skill == PlayerSkillCaster2D.SkillType.AoEBlast)
        {
            float radius = PlayerSkillCaster2D.GetAoeRadius();
            DrawCircle(aimWorld, radius);
        }
        else
        {
            float len = PlayerSkillCaster2D.GetIndicatorLineLength();
            DrawLine(origin, origin + dir * len);
        }
    }

    void DrawLine(Vector2 a, Vector2 b)
    {
        indicatorLR.loop = false;
        indicatorLR.positionCount = 2;
        indicatorLR.SetPosition(0, new Vector3(a.x, a.y, 0f));
        indicatorLR.SetPosition(1, new Vector3(b.x, b.y, 0f));
    }

    void DrawCircle(Vector2 center, float radius)
    {
        int seg = Mathf.Max(8, indicatorCircleSegments);
        indicatorLR.loop = false;
        indicatorLR.positionCount = seg + 1;

        for (int i = 0; i <= seg; i++)
        {
            float t = (float)i / seg;
            float ang = t * Mathf.PI * 2f;
            float x = center.x + Mathf.Cos(ang) * radius;
            float y = center.y + Mathf.Sin(ang) * radius;
            indicatorLR.SetPosition(i, new Vector3(x, y, 0f));
        }
    }

    IEnumerator BurstFire(Vector2 origin, Vector2 dir, ElementType elem)
    {
        isBursting = true;
        bool anyFired = false;

        for (int i = 0; i < burstCount; i++)
        {
            bool fired = false;
            if (PlayerSkillCaster2D != null)
            {
                fired = PlayerSkillCaster2D.SpawnPierceImmediate(origin, dir, elem);
            }
            else if (bulletPool != null)
            {
                var b = bulletPool.Spawn(origin, dir, elem, this, bulletSpeed);
                if (b != null)
                {
                    b.damage = 1;
                    fired = true;
                }
            }

            if (fired) anyFired = true;

            // Wait between burst shots; respects timeScale (use WaitForSecondsRealtime if you want to ignore timeScale)
            if (i < burstCount - 1)
                yield return new WaitForSeconds(burstInterval);
        }

        if (anyFired)
        {
            CycleElementAfterSkill();
        }

        isBursting = false;
    }
}
