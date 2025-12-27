// File: Player/CharacterMovementControll.cs
using UnityEngine;
using UnityEngine.InputSystem;
using GameJam.Common;

public class CharacterMovementControll : MonoBehaviour
{
    [Header("Follow")]
    public float followSpeed = 6f;
    public float stopDistance = 0.05f;
    public bool useMoveTowards = true;

    [Header("Hold To Slow (Right Mouse Hold)")]
    public float holdSlowTimeScale = 0.08f;
    public float timeScaleReturnSpeed = 6f;
    public bool slowAffectsFollow = true;
    public float followSlowMultiplier = 0.25f;

    [Header("Aim")]
    public Transform aimPivot;
    public bool faceByRotation2D = true;
    public Vector2 defaultFacing = Vector2.right;

    [Header("Refs")]
    public PlayerElementAttack attack;
    public PlayerTargeting targeting;
    public PlayerSkillIndicator indicator;

    Camera cam;
    float baseFixedDeltaTime;
    bool forceNormalTime;

    SkillKey? activeSkill;

    void Awake()
    {
        cam = Camera.main;
        baseFixedDeltaTime = Time.fixedDeltaTime;

        if (aimPivot == null) aimPivot = transform;
        if (attack == null) attack = GetComponent<PlayerElementAttack>();
        if (targeting == null) targeting = GetComponent<PlayerTargeting>();
        if (indicator == null) indicator = GetComponent<PlayerSkillIndicator>();
    }

    void Update()
    {
        if (Mouse.current == null || Keyboard.current == null) return;

        bool aiming = Mouse.current.rightButton.isPressed;

        ApplyTimeScale(aiming);
        FollowMouse(aiming);
        UpdateFacing();

        if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            CancelAiming();
            return;
        }

        if (!aiming)
        {
            if (activeSkill.HasValue) CancelAiming();
            return;
        }

        if (Keyboard.current.qKey.wasPressedThisFrame) activeSkill = SkillKey.Q;
        if (Keyboard.current.wKey.wasPressedThisFrame) activeSkill = SkillKey.W;
        if (Keyboard.current.eKey.wasPressedThisFrame) activeSkill = SkillKey.E;

        if (indicator != null)
        {
            indicator.SetAiming(true);
            indicator.SetActiveSkill(activeSkill);
            indicator.TickVisuals(GetAimDirection());
        }

        if (Keyboard.current.qKey.wasReleasedThisFrame) TryConfirmOnRelease(SkillKey.Q);
        if (Keyboard.current.wKey.wasReleasedThisFrame) TryConfirmOnRelease(SkillKey.W);
        if (Keyboard.current.eKey.wasReleasedThisFrame) TryConfirmOnRelease(SkillKey.E);
    }

    void LateUpdate()
    {
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            forceNormalTime = false;
    }

    void TryConfirmOnRelease(SkillKey released)
    {
        if (!activeSkill.HasValue) return;
        if (activeSkill.Value != released) return;
        if (Mouse.current == null || !Mouse.current.rightButton.isPressed) return;

        Vector2 dir = GetAimDirection();
        CastSkill(released, dir);

        activeSkill = null;
        if (indicator != null) indicator.SetActiveSkill(null);
    }

    void CancelAiming()
    {
        activeSkill = null;
        if (indicator != null)
        {
            indicator.SetActiveSkill(null);
            indicator.SetAiming(false);
        }
    }

    void CastSkill(SkillKey key, Vector2 dir)
    {
        ForceBackToNormalTime();
        if (attack != null) attack.TryCastSkill(key, dir);
    }

    void ForceBackToNormalTime()
    {
        forceNormalTime = true;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = baseFixedDeltaTime;
    }

    void ApplyTimeScale(bool aiming)
    {
        if (forceNormalTime)
        {
            Time.timeScale = 1f;
        }
        else if (aiming)
        {
            Time.timeScale = Mathf.Clamp(holdSlowTimeScale, 0.01f, 1f);
        }
        else
        {
            float next = Mathf.Lerp(
                Time.timeScale,
                1f,
                1f - Mathf.Exp(-timeScaleReturnSpeed * Time.unscaledDeltaTime)
            );
            Time.timeScale = Mathf.Clamp(next, 0.01f, 1f);
        }

        Time.fixedDeltaTime = baseFixedDeltaTime * Time.timeScale;
    }

    void FollowMouse(bool aiming)
    {
        Vector3 mouseWorld = GetMouseWorldPosition();
        Vector3 current = transform.position;
        Vector3 targetPos = new Vector3(mouseWorld.x, mouseWorld.y, current.z);

        if (Vector3.Distance(current, targetPos) <= stopDistance) return;

        float speed = followSpeed;
        if (slowAffectsFollow && aiming && !forceNormalTime)
            speed *= followSlowMultiplier;

        if (useMoveTowards)
            transform.position = Vector3.MoveTowards(current, targetPos, speed * Time.deltaTime);
        else
            transform.position = Vector3.Lerp(current, targetPos, 1f - Mathf.Exp(-speed * Time.deltaTime));
    }

    void UpdateFacing()
    {
        Vector2 dir = GetAimDirection();
        if (dir.sqrMagnitude < 1e-6f) dir = defaultFacing;

        if (faceByRotation2D)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            aimPivot.rotation = Quaternion.Euler(0f, 0f, angle);
        }
        else
        {
            Vector3 s = aimPivot.localScale;
            s.x = dir.x >= 0f ? Mathf.Abs(s.x) : -Mathf.Abs(s.x);
            aimPivot.localScale = s;
        }
    }

    Vector2 GetAimDirection()
    {
        Vector3 mw = GetMouseWorldPosition();
        Vector3 p = transform.position;
        Vector2 d = new Vector2(mw.x - p.x, mw.y - p.y);
        return d.sqrMagnitude < 1e-6f ? defaultFacing : d.normalized;
    }

    Vector3 GetMouseWorldPosition()
    {
        if (cam == null) cam = Camera.main;
        if (Mouse.current == null) return transform.position;

        Vector2 sp2 = Mouse.current.position.ReadValue();
        Vector3 sp = new Vector3(sp2.x, sp2.y, Mathf.Abs(cam.transform.position.z - transform.position.z));
        return cam.ScreenToWorldPoint(sp);
    }

    void OnDisable()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = baseFixedDeltaTime;
    }
}
