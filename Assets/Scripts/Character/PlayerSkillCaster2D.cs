// File: Player/PlayerSkillCaster2D.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using GameJam.Common;
using System.Reflection;

public class PlayerSkillCaster2D : MonoBehaviour
{
    public enum SkillType
    {
        PierceShot,
        BlinkSlash,
        AoEBlast
    }

    [Header("Refs")]
    [SerializeField] private PlayerController2D player;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Transform firePoint;
    [SerializeField] private BulletPool bulletPool;

    [Header("Targeting")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float maxAimRange = 16f;

    [Header("Layers")]
    [SerializeField] private LayerMask obstacleLayers;
    [SerializeField] private LayerMask damageLayers;

    [Header("Skill Select (Cycle Only)")]
    [SerializeField] private SkillType currentSkill = SkillType.PierceShot;

    [Header("Pierce Shot")]
    [SerializeField] private float pierceBulletSpeed = 14f;
    [SerializeField] private int pierceDamage = 1;
    [SerializeField] private float pierceCooldown = 0.12f;
    [SerializeField] private int pierceMaxHits = -1;

    [Header("Blink Slash")]
    [SerializeField] private float blinkMaxDistance = 6.5f;
    [SerializeField] private float blinkHitRadius = 0.35f;
    [SerializeField] private int blinkDamage = 1;
    [SerializeField] private float blinkCooldown = 0.75f;

    [Header("AoE Blast")]
    [SerializeField] private float aoeMaxCastRange = 7.5f;
    [SerializeField] private float aoeRadius = 2.2f;
    [SerializeField] private int aoeDamage = 1;
    [SerializeField] private float aoeCooldown = 0.9f;
    [SerializeField] private float aoeCastDelay = 1.0f; // seconds before AoE takes effect
    [Header("AoE Indicator")]
    [SerializeField] private float aoeIndicatorLineWidth = 0.06f;
    [SerializeField] private int aoeIndicatorSegments = 48;
    [SerializeField] private int aoeIndicatorSortingOrder = 210;

    float nextPierceTime;
    float nextBlinkTime;
    float nextAoeTime;

    [Header("Runtime Cooldown (0~1, remaining/cooldown)")]
    [SerializeField] private float pierceRemaining01;
    [SerializeField] private float blinkRemaining01;
    [SerializeField] private float aoeRemaining01;

    public float PierceRemaining01 => pierceRemaining01;
    public float BlinkRemaining01 => blinkRemaining01;
    public float AoERemaining01 => aoeRemaining01;

    public float PierceCooldown => pierceCooldown;
    public float BlinkCooldown => blinkCooldown;
    public float AoECooldown => aoeCooldown;

    public float NextPierceTime => nextPierceTime;
    public float NextBlinkTime => nextBlinkTime;
    public float NextAoeTime => nextAoeTime;

    readonly HashSet<int> _hitIds = new HashSet<int>();

    // ===== Space Hold Blink (Indicator + Slow Time) =====
    [Header("Space Hold Blink (Indicator + Slow Time)")]
    [SerializeField, Range(0.02f, 1f)] private float spaceHoldTimeScale = 0.2f;

    [Header("Blink Path Indicator")]
    [SerializeField] private bool useBuiltInBlinkIndicator = true;
    [SerializeField] private LineRenderer blinkLine;
    [SerializeField] private SpriteRenderer blinkEndDot;
    [SerializeField] private float blinkLineWidth = 0.08f;
    [SerializeField] private float blinkEndDotScale = 0.25f;
    [SerializeField] private int blinkIndicatorSortingOrder = 200;

    public bool IsHoldingBlink => _holdingBlink;
    public Vector2 BlinkIndicatorStart => _blinkIndicatorStart;
    public Vector2 BlinkIndicatorEnd => _blinkIndicatorEnd;
    public ElementType BlinkIndicatorElement => _blinkIndicatorElement;

    bool _holdingBlink;
    Vector2 _blinkIndicatorStart;
    Vector2 _blinkIndicatorEnd;
    ElementType _blinkIndicatorElement;

    float _defaultFixedDeltaTime;
    float _defaultTimeScale;

    void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
        player = GetComponent<PlayerController2D>();
        targetCamera = Camera.main;
    }

    void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!player) player = GetComponent<PlayerController2D>();
        if (!targetCamera) targetCamera = Camera.main;

        if (firePoint == null)
        {
            var child = transform.Find("FirePoint");
            if (child != null) firePoint = child;
            else if (player != null && player.firePoint != null) firePoint = player.firePoint;
            else firePoint = transform;
        }

        _defaultFixedDeltaTime = Time.fixedDeltaTime;
        _defaultTimeScale = Time.timeScale;

        EnsureBlinkIndicator();
        SetBlinkIndicatorActive(false);
    }

    void Update()
    {
        float now = Time.time;

        pierceRemaining01 = GetRemaining01(now, nextPierceTime, pierceCooldown);
        blinkRemaining01 = GetRemaining01(now, nextBlinkTime, blinkCooldown);
        aoeRemaining01 = GetRemaining01(now, nextAoeTime, aoeCooldown);

        HandleSpaceHoldBlink();
    }

    void OnDisable()
    {
        if (_holdingBlink)
        {
            _holdingBlink = false;
            RestoreTimeScale();
            SetBlinkIndicatorActive(false);
        }
    }

    static float GetRemaining01(float now, float nextTime, float cd)
    {
        if (cd <= 0f) return 0f;
        float rem = Mathf.Max(0f, nextTime - now);
        return Mathf.Clamp01(rem / cd);
    }

    public SkillType GetCurrentSkill() => currentSkill;
    public float GetAoeRadius() => aoeRadius;

    // Public accessor for AoE max cast range (used by indicators)
    public float GetAoeMaxCastRange() => aoeMaxCastRange;

    public float GetIndicatorLineLength()
    {
        return currentSkill switch
        {
            SkillType.PierceShot => maxAimRange,
            SkillType.BlinkSlash => blinkMaxDistance,
            _ => aoeMaxCastRange
        };
    }

    public void CycleSkill()
    {
        currentSkill = currentSkill switch
        {
            SkillType.PierceShot => SkillType.BlinkSlash,
            SkillType.BlinkSlash => SkillType.AoEBlast,
            _ => SkillType.PierceShot
        };
    }

    public Vector2 GetMouseWorldClamped(Vector2 origin)
    {
        if (!targetCamera) targetCamera = Camera.main;
        if (Mouse.current == null || targetCamera == null) return origin + Vector2.right;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector3 w = targetCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 0f));
        Vector2 aim = new Vector2(w.x, w.y);

        Vector2 delta = aim - origin;
        float dist = delta.magnitude;
        if (dist > maxAimRange) aim = origin + delta / dist * maxAimRange;

        return aim;
    }

    // Returns the world position under the mouse without applying any range clamping.
    public Vector2 GetMouseWorld(Vector2 origin)
    {
        if (!targetCamera) targetCamera = Camera.main;
        if (Mouse.current == null || targetCamera == null) return origin + Vector2.right;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector3 w = targetCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 0f));
        return new Vector2(w.x, w.y);
    }

    ElementType GetPlayerElement()
    {
        if (player != null) return player.currentElement;
        return ElementType.Fire;
    }

    public void CastCurrent(Vector2 origin, Vector2 dir)
    {
        ElementType elemBeforeCast = GetPlayerElement();

        bool casted = currentSkill switch
        {
            SkillType.PierceShot => TryCastPierce(origin, dir, elemBeforeCast),
            SkillType.BlinkSlash => TryCastBlink(origin, dir, elemBeforeCast),
            SkillType.AoEBlast => TryCastAoe(origin, dir, elemBeforeCast),
            _ => false
        };

        if (casted && player != null)
            player.CycleElementAfterSkill();
    }

    bool TryCastPierce(Vector2 origin, Vector2 dir, ElementType elem)
    {
        if (Time.time < nextPierceTime) return false;
        nextPierceTime = Time.time + pierceCooldown;

        if (bulletPool == null || firePoint == null) return false;

        var bulletGO = bulletPool.Spawn(origin, dir, player.currentElement, player);
        if (bulletGO == null) return false;

        bulletGO.transform.position = firePoint.position;

        var eb = bulletGO.GetComponent<ElementBullet>();
        if (eb != null)
        {
            eb.damage = pierceDamage;
            eb.canPierceUnits = true;
            eb.maxPierceHits = pierceMaxHits;
            eb.Init(bulletPool, firePoint.position, dir, elem, player != null ? (UnityEngine.Object)player : this, pierceBulletSpeed, true, pierceMaxHits);
            return true;
        }

        bulletGO.transform.rotation = Quaternion.FromToRotation(Vector3.right, new Vector3(dir.x, dir.y, 0f));
        var rb2d = bulletGO.GetComponent<Rigidbody2D>();
        if (rb2d != null) rb2d.linearVelocity = dir * pierceBulletSpeed;

        TrySetFieldOrProperty(bulletGO, "currentElement", elem);
        TrySetFieldOrProperty(bulletGO, "element", elem);
        TrySetFieldOrProperty(bulletGO, "damage", pierceDamage);
        TrySetFieldOrProperty(bulletGO, "canPierceUnits", true);
        TrySetFieldOrProperty(bulletGO, "maxPierceHits", pierceMaxHits);

        return true;
    }

    public bool SpawnPierceImmediate(Vector2 origin, Vector2 dir, ElementType elem)
    {
        if (bulletPool == null || firePoint == null) return false;

        var bulletGO = bulletPool.Spawn(origin, dir, elem, player);
        if (bulletGO == null) return false;

        bulletGO.transform.position = firePoint.position;

        var eb = bulletGO.GetComponent<ElementBullet>();
        if (eb != null)
        {
            eb.damage = pierceDamage;
            eb.canPierceUnits = true;
            eb.maxPierceHits = pierceMaxHits;
            eb.Init(bulletPool, firePoint.position, dir, elem, player != null ? (UnityEngine.Object)player : this, pierceBulletSpeed, true, pierceMaxHits);
            return true;
        }

        bulletGO.transform.rotation = Quaternion.FromToRotation(Vector3.right, new Vector3(dir.x, dir.y, 0f));
        var rb2d = bulletGO.GetComponent<Rigidbody2D>();
        if (rb2d != null) rb2d.linearVelocity = dir * pierceBulletSpeed;

        TrySetFieldOrProperty(bulletGO, "currentElement", elem);
        TrySetFieldOrProperty(bulletGO, "element", elem);
        TrySetFieldOrProperty(bulletGO, "damage", pierceDamage);
        TrySetFieldOrProperty(bulletGO, "canPierceUnits", true);
        TrySetFieldOrProperty(bulletGO, "maxPierceHits", pierceMaxHits);

        return true;
    }

    bool TryCastBlink(Vector2 origin, Vector2 dir, ElementType elem)
    {
        if (_holdingBlink) return false;
        if (Keyboard.current != null && Keyboard.current.spaceKey != null && Keyboard.current.spaceKey.isPressed) return false;

        if (Time.time < nextBlinkTime) return false;
        nextBlinkTime = Time.time + blinkCooldown;

        UnityEngine.Object source = player != null ? (UnityEngine.Object)player : this;

        Vector2 start = rb ? rb.position : (Vector2)transform.position;
        Vector2 end = ComputeBlinkEnd(start, dir);

        _hitIds.Clear();

        float castDist = Vector2.Distance(start, end);
        RaycastHit2D[] hits = Physics2D.CircleCastAll(start, blinkHitRadius, dir, castDist, damageLayers);

        for (int i = 0; i < hits.Length; i++)
        {
            var c = hits[i].collider;
            if (!c) continue;

            int id = c.GetInstanceID();
            if (_hitIds.Contains(id)) continue;
            _hitIds.Add(id);

            var targetGO = c.attachedRigidbody ? c.attachedRigidbody.gameObject : c.gameObject;
            if (!IsSameElementTarget(targetGO, elem)) continue;

            ApplyElementDamage_ExactInterface(targetGO, c, elem, blinkDamage, source);
        }

        if (rb) rb.position = end;
        else transform.position = end;

        return true;
    }

    bool TryCastAoe(Vector2 origin, Vector2 dir, ElementType elem)
    {
        if (Time.time < nextAoeTime) return false;
        nextAoeTime = Time.time + aoeCooldown;

        UnityEngine.Object source = player != null ? (UnityEngine.Object)player : this;

        Vector2 casterPos = rb ? rb.position : (Vector2)transform.position;

        Vector2 aim = GetMouseWorldClamped(casterPos);
        Vector2 delta = aim - casterPos;
        float dist = delta.magnitude;
        if (dist > aoeMaxCastRange) aim = casterPos + delta / dist * aoeMaxCastRange;

        // Start delayed AoE: show an indicator and apply damage after a short delay while the indicator fills
        StartCoroutine(DoAoeDelayed(aim, elem, aoeCastDelay, aoeRadius, aoeDamage, source));
        return true;
    }

    IEnumerator DoAoeDelayed(Vector2 center, ElementType elem, float delay, float radius, int damage, UnityEngine.Object source)
    {
        // Create indicator GameObject using LineRenderer (partial arc filling)
        var go = new GameObject("AoEIndicator");
        go.transform.position = new Vector3(center.x, center.y, 0f);

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = false;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 4;
        lr.startWidth = aoeIndicatorLineWidth;
        lr.endWidth = aoeIndicatorLineWidth;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.sortingOrder = aoeIndicatorSortingOrder;

        Color c = ElementToColor(elem);
        lr.startColor = c;
        lr.endColor = c;

        int segments = Mathf.Max(8, aoeIndicatorSegments);

        float t = 0f;
        while (t < delay)
        {
            float progress = Mathf.Clamp01(t / delay);
            DrawArc(lr, center, radius, segments, progress);
            t += Time.deltaTime;
            yield return null;
        }

        // final full circle
        DrawArc(lr, center, radius, segments, 1f);

        // Apply damage now
        Collider2D[] cols = Physics2D.OverlapCircleAll(center, radius, damageLayers);

        _hitIds.Clear();

        for (int i = 0; i < cols.Length; i++)
        {
            var ccol = cols[i];
            if (!ccol) continue;

            int id = ccol.GetInstanceID();
            if (_hitIds.Contains(id)) continue;
            _hitIds.Add(id);

            var targetGO = ccol.attachedRigidbody ? ccol.attachedRigidbody.gameObject : ccol.gameObject;
            if (!IsSameElementTarget(targetGO, elem)) continue;

            ApplyElementDamage_ExactInterface(targetGO, ccol, elem, damage, source);
        }

        // Optionally a short linger then destroy
        yield return new WaitForSeconds(0.15f);
        GameObject.Destroy(go);
    }

    void DrawArc(LineRenderer lr, Vector2 center, float radius, int segments, float progress)
    {
        progress = Mathf.Clamp01(progress);
        if (progress >= 1f - 1e-6f)
        {
            // Full circle: use looped line with exactly 'segments' points
            lr.loop = true;
            int points = Mathf.Max(8, segments);
            lr.positionCount = points;
            for (int i = 0; i < points; i++)
            {
                float frac = (float)i / points; // avoid repeating point at the end
                float angle = Mathf.Deg2Rad * (frac * 360f);
                float x = center.x + Mathf.Cos(angle) * radius;
                float y = center.y + Mathf.Sin(angle) * radius;
                lr.SetPosition(i, new Vector3(x, y, 0f));
            }
        }
        else
        {
            lr.loop = false;
            float maxAngle = 360f * progress;
            int points = Mathf.Max(2, Mathf.CeilToInt(segments * progress) + 1);

            lr.positionCount = points;
            for (int i = 0; i < points; i++)
            {
                float frac = (float)i / (points - 1);
                float angle = Mathf.Deg2Rad * (frac * maxAngle);
                float x = center.x + Mathf.Cos(angle) * radius;
                float y = center.y + Mathf.Sin(angle) * radius;
                lr.SetPosition(i, new Vector3(x, y, 0f));
            }
        }
    }

    // ===== Space Hold Blink Implementation =====
    void HandleSpaceHoldBlink()
    {
        if (Keyboard.current == null) return;
        var space = Keyboard.current.spaceKey;
        if (space == null) return;

        if (space.wasPressedThisFrame)
            BeginSpaceBlinkHold();

        if (_holdingBlink)
            UpdateSpaceBlinkHold();

        if (space.wasReleasedThisFrame)
            EndSpaceBlinkHoldAndCast();
    }

    void BeginSpaceBlinkHold()
    {
        _holdingBlink = true;

        Vector2 start = rb ? rb.position : (Vector2)transform.position;
        _blinkIndicatorStart = start;
        _blinkIndicatorElement = GetPlayerElement();

        ApplyTimeScale(spaceHoldTimeScale);
        SetBlinkIndicatorActive(true);

        UpdateSpaceBlinkHold();
    }

    void UpdateSpaceBlinkHold()
    {
        Vector2 start = rb ? rb.position : (Vector2)transform.position;
        _blinkIndicatorStart = start;
        _blinkIndicatorElement = GetPlayerElement();

        Vector2 aim = GetMouseWorldClamped(start);
        Vector2 delta = aim - start;

        Vector2 dir = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.right;
        Vector2 end = ComputeBlinkEnd(start, dir);

        _blinkIndicatorEnd = end;

        UpdateBlinkIndicatorVisuals(start, end, _blinkIndicatorElement);
    }

    void EndSpaceBlinkHoldAndCast()
    {
        if (!_holdingBlink) return;

        _holdingBlink = false;
        RestoreTimeScale();
        SetBlinkIndicatorActive(false);

        Vector2 start = rb ? rb.position : (Vector2)transform.position;

        Vector2 aim = GetMouseWorldClamped(start);
        Vector2 delta = aim - start;
        if (delta.sqrMagnitude <= 0.0001f) return;

        Vector2 dir = delta.normalized;

        ElementType elem = GetPlayerElement();

        bool casted = TryCastBlink(start, dir, elem);

        if (casted && player != null)
            player.CycleElementAfterSkill();
    }

    Vector2 ComputeBlinkEnd(Vector2 start, Vector2 dir)
    {
        RaycastHit2D wallHit = Physics2D.Raycast(start, dir, blinkMaxDistance, obstacleLayers);
        return wallHit.collider ? (wallHit.point - dir * 0.05f) : (start + dir * blinkMaxDistance);
    }

    void ApplyTimeScale(float scale)
    {
        _defaultTimeScale = Time.timeScale;
        _defaultFixedDeltaTime = Time.fixedDeltaTime;

        float s = Mathf.Clamp(scale, 0.01f, 1f);
        Time.timeScale = s;
        Time.fixedDeltaTime = _defaultFixedDeltaTime * s;
    }

    void RestoreTimeScale()
    {
        Time.timeScale = _defaultTimeScale <= 0f ? 1f : _defaultTimeScale;
        Time.fixedDeltaTime = _defaultFixedDeltaTime <= 0f ? 0.02f : _defaultFixedDeltaTime;
    }

    // ===== Built-in Indicator (Line + End Dot) =====
    void EnsureBlinkIndicator()
    {
        if (!useBuiltInBlinkIndicator) return;

        if (blinkLine == null)
        {
            var go = new GameObject("BlinkIndicator_Line");
            go.transform.SetParent(transform, false);

            blinkLine = go.AddComponent<LineRenderer>();
            blinkLine.useWorldSpace = true;
            blinkLine.positionCount = 2;
            blinkLine.numCapVertices = 4;
            blinkLine.numCornerVertices = 4;
            blinkLine.startWidth = blinkLineWidth;
            blinkLine.endWidth = blinkLineWidth;

            var mat = new Material(Shader.Find("Sprites/Default"));
            blinkLine.material = mat;
            blinkLine.sortingOrder = blinkIndicatorSortingOrder;
            blinkLine.enabled = false;
        }

        if (blinkEndDot == null)
        {
            var go = new GameObject("BlinkIndicator_EndDot");
            go.transform.SetParent(transform, false);

            blinkEndDot = go.AddComponent<SpriteRenderer>();
            blinkEndDot.sprite = CreateWhiteDotSprite();
            blinkEndDot.sortingOrder = blinkIndicatorSortingOrder + 1;
            blinkEndDot.enabled = false;
        }
    }

    void SetBlinkIndicatorActive(bool active)
    {
        if (!useBuiltInBlinkIndicator) return;

        EnsureBlinkIndicator();

        if (blinkLine != null) blinkLine.enabled = active;
        if (blinkEndDot != null) blinkEndDot.enabled = active;

        if (!active && blinkLine != null)
        {
            blinkLine.SetPosition(0, Vector3.zero);
            blinkLine.SetPosition(1, Vector3.zero);
        }
    }

    void UpdateBlinkIndicatorVisuals(Vector2 start, Vector2 end, ElementType elem)
    {
        if (!useBuiltInBlinkIndicator) return;

        EnsureBlinkIndicator();

        Color c = ElementToColor(elem);

        if (blinkLine != null)
        {
            blinkLine.startWidth = blinkLineWidth;
            blinkLine.endWidth = blinkLineWidth;
            blinkLine.startColor = c;
            blinkLine.endColor = c;
            blinkLine.SetPosition(0, new Vector3(start.x, start.y, 0f));
            blinkLine.SetPosition(1, new Vector3(end.x, end.y, 0f));
        }

        if (blinkEndDot != null)
        {
            blinkEndDot.color = c;
            blinkEndDot.transform.position = new Vector3(end.x, end.y, 0f);
            blinkEndDot.transform.localScale = Vector3.one * blinkEndDotScale;
        }
    }

    static Color ElementToColor(ElementType elem)
    {
        switch (elem)
        {
            case ElementType.Fire: return new Color(1f, 0.35f, 0.2f, 1f);
            case ElementType.Water: return new Color(0.25f, 0.6f, 1f, 1f);
            case ElementType.Nature: return new Color(0.25f, 1f, 0.4f, 1f);
            default: return Color.white;
        }
    }

    static Sprite CreateWhiteDotSprite()
    {
        var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color white = new Color(1f, 1f, 1f, 1f);

        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                float u = (x + 0.5f) / 16f * 2f - 1f;
                float v = (y + 0.5f) / 16f * 2f - 1f;
                float r2 = u * u + v * v;
                tex.SetPixel(x, y, r2 <= 1f ? white : clear);
            }
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
    }

    bool IsSameElementTarget(GameObject go, ElementType playerElem)
    {
        if (go == null) return false;
        if (TryGetElement(go, out ElementType targetElem))
            return targetElem.Equals(playerElem);
        return false;
    }

    bool TryGetElement(GameObject go, out ElementType element)
    {
        element = default;

        var comps = go.GetComponents<MonoBehaviour>();
        for (int i = 0; i < comps.Length; i++)
        {
            var c = comps[i];
            if (!c) continue;

            if (TryGetFieldOrProperty(c, "currentElement", out object v) || TryGetFieldOrProperty(c, "element", out v))
            {
                if (v is ElementType et)
                {
                    element = et;
                    return true;
                }
            }
        }
        return false;
    }

    void ApplyElementDamage_ExactInterface(GameObject targetGO, Collider2D hitCollider, ElementType element, int damage, UnityEngine.Object source)
    {
        if (!targetGO) return;

        var dmg = hitCollider ? hitCollider.GetComponentInParent<IElementDamageable>() : null;
        if (dmg == null) dmg = targetGO.GetComponentInParent<IElementDamageable>();
        if (dmg == null) return;

        bool canHit;
        try { canHit = dmg.CanBeHitBy(element); }
        catch { return; }

        if (canHit)
        {
            try { dmg.TakeElementHit(element, damage, source); } catch { }
            return;
        }

        var enemy = (dmg as Component) ? (dmg as Component).GetComponentInParent<EnemyShooter2D>() : null;
        if (enemy != null && enemy.currentElement == element)
        {
            try { dmg.TakeElementHit(element, damage, source); } catch { }
        }
    }

    bool TryGetFieldOrProperty(object obj, string name, out object value)
    {
        value = null;
        if (obj == null) return false;

        var t = obj.GetType();

        var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null)
        {
            value = f.GetValue(obj);
            return true;
        }

        var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.CanRead)
        {
            value = p.GetValue(obj);
            return true;
        }

        return false;
    }

    void TrySetFieldOrProperty(GameObject go, string name, object value)
    {
        if (!go) return;

        var comps = go.GetComponents<MonoBehaviour>();
        for (int i = 0; i < comps.Length; i++)
        {
            var c = comps[i];
            if (!c) continue;
            TrySetFieldOrProperty(c, name, value);
        }
    }

    void TrySetFieldOrProperty(object obj, string name, object value)
    {
        if (obj == null) return;

        var t = obj.GetType();

        var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && (value == null || f.FieldType.IsAssignableFrom(value.GetType()) || f.FieldType.IsEnum))
        {
            try { f.SetValue(obj, value); } catch { }
            return;
        }

        var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.CanWrite && (value == null || p.PropertyType.IsAssignableFrom(value.GetType()) || p.PropertyType.IsEnum))
        {
            try { p.SetValue(obj, value); } catch { }
        }
    }

    public bool CastPierce(Vector2 origin, Vector2 dir) => TryCastPierce(origin, dir, GetPlayerElement());
    public bool CastBlink(Vector2 origin, Vector2 dir) => TryCastBlink(origin, dir, GetPlayerElement());
    public bool CastAoe(Vector2 origin) => TryCastAoe(origin, Vector2.zero, GetPlayerElement());

    [Serializable]
    public struct ElementDamagePacket
    {
        public ElementType element;
        public int damage;

        public ElementDamagePacket(ElementType element, int damage)
        {
            this.element = element;
            this.damage = damage;
        }
    }
}
