// File: Player/PlayerController2D.cs
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

    [Header("Shoot (Left Click)")]
    public BulletPool bulletPool;
    public Transform firePoint;
    public float bulletSpeed = 14f;
    public float fireCooldown = 0.12f;

    [Header("State")]
    public ElementType currentElement = ElementType.Fire;
    public int hp = 5;

    [Header("Visual")]
    public SpriteRenderer bodyRenderer;

    Rigidbody2D rb;
    Camera cam;

    float dashTimer;
    float dashCd;
    float fireCd;
    bool invulnerable;

    Vector2 dashDir;
    Vector2 dashStartPos;
    Vector2 dashTargetPos;
    bool dashUseTarget;

    // 固定 R_G_B（你專案的對應：Fire/Water/Nature）
    static readonly ElementType[] Cycle = { ElementType.Fire, ElementType.Water, ElementType.Nature };
    int cycleIndex;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearDamping = 8f;

        cam = Camera.main;

        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (firePoint == null) firePoint = transform;

        cycleIndex = GetCycleIndex(currentElement);
        currentElement = Cycle[cycleIndex];
        ApplyElementVisual();
    }

    void Update()
    {
        if (dashCd > 0f) dashCd -= Time.deltaTime;
        if (fireCd > 0f) fireCd -= Time.deltaTime;

        if (dashTimer > 0f)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f)
            {
                invulnerable = false;
            }
        }

        HandleDashInput();
        HandleShootInput();
        AimBodyToMouse();
    }

    void FixedUpdate()
    {
        if (dashTimer > 0f)
        {
            Vector2 cur = rb.position;

            if (dashUseTarget)
            {
                Vector2 toTarget = dashTargetPos - cur;
                float remain = toTarget.magnitude;

                if (remain <= 0.02f)
                {
                    rb.position = dashTargetPos;
                    rb.linearVelocity = Vector2.zero;
                    dashTimer = 0f;
                    invulnerable = false;
                    return;
                }

                rb.linearVelocity = toTarget.normalized * moveSpeed * dashSpeedMultiplier;
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
                    return;
                }

                rb.linearVelocity = dashDir * moveSpeed * dashSpeedMultiplier;
                return;
            }
        }

        Vector2 move = ReadMoveInput();
        rb.linearVelocity = move * moveSpeed;
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

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (dashCd > 0f) return;

            dashCd = dashCooldown;
            dashTimer = dashDuration;
            invulnerable = true;

            Vector2 start = rb.position;
            dashStartPos = start;

            Vector2 mouseWorld = GetMouseWorld2D();
            Vector2 toMouse = mouseWorld - start;
            float dist = toMouse.magnitude;

            if (dist <= dashMaxDistance)
            {
                dashUseTarget = true;
                dashTargetPos = mouseWorld;
                dashDir = dist > 1e-6f ? (toMouse / dist) : Vector2.right;
            }
            else
            {
                dashUseTarget = false;
                dashDir = dist > 1e-6f ? (toMouse / dist) : Vector2.right;
                dashTargetPos = start + dashDir * dashMaxDistance;
            }
        }
    }

    void HandleShootInput()
    {
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (fireCd > 0f) return;
            fireCd = fireCooldown;

            // 先用「當前顏色」射出子彈
            ElementType shotElement = currentElement;

            Vector2 pos = firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;
            Vector2 dir = GetAimDirection(pos);

            if (bulletPool != null)
                bulletPool.Spawn(pos, dir, shotElement, this, bulletSpeed);

            // 射完後固定切到下一個（R->G->B->R...）
            cycleIndex = (cycleIndex + 1) % Cycle.Length;
            currentElement = Cycle[cycleIndex];
            ApplyElementVisual();
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

    public bool CanBeHitBy(ElementType element, Object source)
    {
        if (invulnerable) return false;
        if (element == currentElement) return false;
        return true;
    }

    public void TakeElementHit(ElementType element, int damage, Object source)
    {
        if (!CanBeHitBy(element, source)) return;

        hp -= damage;

        if (hp <= 0)
        {
            Debug.Log("[Player] Dead");
        }
    }
}
