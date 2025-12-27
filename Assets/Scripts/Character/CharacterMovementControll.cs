// File: Player/CharacterMovementControll.cs
using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterMovementControll : MonoBehaviour
{
    [Header("Follow")]
    public float followSpeed = 6f;
    public float stopDistance = 0.05f;
    public bool useMoveTowards = true;

    [Header("Hold To Slow")]
    public float holdSlowTimeScale = 0.25f;
    public float timeScaleReturnSpeed = 8f;
    public bool slowAffectsFollow = true;
    public float followSlowMultiplier = 0.35f;

    [Header("Aim")]
    public Transform aimPivot;
    public bool faceByRotation2D = true;
    public Vector2 defaultFacing = Vector2.right;

    [Header("Attack Reference")]
    public PlayerElementAttack attack; // ¡¹ ¦b Controll ¤Þ¥Î Attack

    Camera cam;
    float baseFixedDeltaTime;
    bool forceNormalTime;

    void Awake()
    {
        cam = Camera.main;
        baseFixedDeltaTime = Time.fixedDeltaTime;
        if (aimPivot == null) aimPivot = transform;
        if (attack == null) attack = GetComponent<PlayerElementAttack>();
    }

    void Update()
    {
        bool holdingMouse = Mouse.current != null && Mouse.current.leftButton.isPressed;

        ApplyTimeScale(holdingMouse);
        FollowMouse(holdingMouse);
        UpdateFacing();

        if (Keyboard.current != null)
        {
            if (Keyboard.current.qKey.wasPressedThisFrame) CastSkill("Q");
            if (Keyboard.current.wKey.wasPressedThisFrame) CastSkill("W");
            if (Keyboard.current.eKey.wasPressedThisFrame) CastSkill("E");
        }
    }

    void LateUpdate()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            forceNormalTime = false;
    }

    void CastSkill(string key)
    {
        ForceBackToNormalTime();

        if (attack != null)
            attack.TryAttack(key);
        else
            Debug.Log($"[Skill] {key} Cast (no attack component found)");
    }

    void ForceBackToNormalTime()
    {
        forceNormalTime = true;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = baseFixedDeltaTime;
    }

    void ApplyTimeScale(bool holdingMouse)
    {
        if (forceNormalTime)
        {
            Time.timeScale = 1f;
        }
        else if (holdingMouse)
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

    void FollowMouse(bool holdingMouse)
    {
        Vector3 mouseWorld = GetMouseWorldPosition();
        Vector3 current = transform.position;
        Vector3 target = new Vector3(mouseWorld.x, mouseWorld.y, current.z);

        float dist = Vector3.Distance(current, target);
        if (dist <= stopDistance) return;

        float speed = followSpeed;
        if (slowAffectsFollow && holdingMouse && !forceNormalTime)
            speed *= followSlowMultiplier;

        if (useMoveTowards)
            transform.position = Vector3.MoveTowards(current, target, speed * Time.deltaTime);
        else
            transform.position = Vector3.Lerp(current, target, 1f - Mathf.Exp(-speed * Time.deltaTime));
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
        Vector3 mouseWorld = GetMouseWorldPosition();
        Vector3 p = transform.position;
        Vector2 d = new Vector2(mouseWorld.x - p.x, mouseWorld.y - p.y);
        return d.sqrMagnitude < 1e-6f ? defaultFacing : d.normalized;
    }

    Vector3 GetMouseWorldPosition()
    {
        if (cam == null) cam = Camera.main;
        if (Mouse.current == null) return transform.position;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        Vector3 sp = new Vector3(screenPos.x, screenPos.y, 0f);
        sp.z = Mathf.Abs(cam.transform.position.z - transform.position.z);
        return cam.ScreenToWorldPoint(sp);
    }

    void OnDisable()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = baseFixedDeltaTime;
    }
}
