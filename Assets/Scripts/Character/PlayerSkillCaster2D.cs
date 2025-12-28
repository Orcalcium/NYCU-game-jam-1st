// File: Player/PlayerSkillCaster2D.cs

using System;
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

    float nextPierceTime;
    float nextBlinkTime;
    float nextAoeTime;

    readonly HashSet<int> _hitIds = new HashSet<int>();

    const string LOG_PREFIX = "[PlayerSkillCaster2D]";
    bool _loggedLayerConfigOnce;

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

        // Ensure we have a valid fire point: prefer a child named FirePoint, then player's firePoint, else default to this transform
        if (firePoint == null)
        {
            var child = transform.Find("FirePoint");
            if (child != null) firePoint = child;
            else if (player != null && player.firePoint != null) firePoint = player.firePoint;
            else firePoint = transform;
        }

        LogLayerConfigOnce("Awake");
        LogRefs("Awake");
    }

    void OnValidate()
    {
        LogLayerConfigOnce("OnValidate");
    }

    public SkillType GetCurrentSkill() => currentSkill;
    public float GetAoeRadius() => aoeRadius;

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
        var before = currentSkill;
        currentSkill = currentSkill switch
        {
            SkillType.PierceShot => SkillType.BlinkSlash,
            SkillType.BlinkSlash => SkillType.AoEBlast,
            _ => SkillType.PierceShot
        };
        Debug.Log($"{LOG_PREFIX} CycleSkill: {before} -> {currentSkill}", this);
    }

    public Vector2 GetMouseWorldClamped(Vector2 origin)
    {
        if (!targetCamera) targetCamera = Camera.main;
        if (Mouse.current == null || targetCamera == null)
        {
            Debug.LogWarning($"{LOG_PREFIX} GetMouseWorldClamped: Mouse or Camera missing. origin={origin}", this);
            return origin + Vector2.right;
        }

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector3 w = targetCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 0f));
        Vector2 aim = new Vector2(w.x, w.y);

        Vector2 delta = aim - origin;
        float dist = delta.magnitude;
        if (dist > maxAimRange) aim = origin + delta / dist * maxAimRange;

        return aim;
    }

    ElementType GetPlayerElement()
    {
        if (player != null) return player.currentElement;
        return ElementType.Fire;
    }

    public void CastCurrent(Vector2 origin, Vector2 dir)
    {
        if (dir.sqrMagnitude <= 0.0001f)
            Debug.LogWarning($"{LOG_PREFIX} CastCurrent: dir is near zero. origin={origin}", this);

        ElementType elemBeforeCast = GetPlayerElement();

        Debug.Log($"{LOG_PREFIX} CastCurrent: skill={currentSkill}, elem={elemBeforeCast}, origin={origin}, dir={dir}, time={Time.time:F3}", this);

        bool casted = currentSkill switch
        {
            SkillType.PierceShot => TryCastPierce(origin, dir, elemBeforeCast),
            SkillType.BlinkSlash => TryCastBlink(origin, dir, elemBeforeCast),
            SkillType.AoEBlast => TryCastAoe(origin, dir, elemBeforeCast),
            _ => false
        };

        Debug.Log($"{LOG_PREFIX} CastCurrent result: casted={casted}", this);

        if (casted && player != null)
            player.CycleElementAfterSkill();
    }

    bool TryCastPierce(Vector2 origin, Vector2 dir, ElementType elem)
    {
        if (Time.time < nextPierceTime)
        {
            Debug.Log($"{LOG_PREFIX} TryCastPierce blocked by cooldown. now={Time.time:F3} next={nextPierceTime:F3}", this);
            return false;
        }
        nextPierceTime = Time.time + pierceCooldown;

        if (bulletPool == null || firePoint == null)
        {
            Debug.LogError($"{LOG_PREFIX} TryCastPierce missing refs. bulletPool={(bulletPool ? "OK" : "NULL")} firePoint={(firePoint ? "OK" : "NULL")}", this);
            return false;
        }

        var bulletGO = bulletPool.Spawn(origin, dir, player.currentElement, player);
        if (bulletGO == null)
        {
            Debug.LogError($"{LOG_PREFIX} TryCastPierce bulletPool.Spawn returned NULL", this);
            return false;
        }

        bulletGO.transform.position = firePoint.position;

        Debug.Log($"{LOG_PREFIX} TryCastPierce spawned bullet='{bulletGO.name}' at {firePoint.position}, elem={elem}, dmg={pierceDamage}, speed={pierceBulletSpeed}", bulletGO);

        var eb = bulletGO.GetComponent<ElementBullet>();
        if (eb != null)
        {
            eb.damage = pierceDamage;
            eb.canPierceUnits = true;
            eb.maxPierceHits = pierceMaxHits;
            eb.Init(bulletPool, firePoint.position, dir, elem, player != null ? (UnityEngine.Object)player : this, pierceBulletSpeed, true, pierceMaxHits);
            Debug.Log($"{LOG_PREFIX} TryCastPierce used ElementBullet.Init (ElementBullet found).", bulletGO);
            return true;
        }

        bulletGO.transform.rotation = Quaternion.FromToRotation(Vector3.right, new Vector3(dir.x, dir.y, 0f));
        var rb2d = bulletGO.GetComponent<Rigidbody2D>();
        if (rb2d != null)
        {
            rb2d.linearVelocity = dir * pierceBulletSpeed;
            Debug.Log($"{LOG_PREFIX} TryCastPierce set bullet Rigidbody2D.linearVelocity={rb2d.linearVelocity}", bulletGO);
        }
        else
        {
            Debug.LogWarning($"{LOG_PREFIX} TryCastPierce bullet has no Rigidbody2D.", bulletGO);
        }

        TrySetFieldOrProperty(bulletGO, "currentElement", elem);
        TrySetFieldOrProperty(bulletGO, "element", elem);
        TrySetFieldOrProperty(bulletGO, "damage", pierceDamage);
        TrySetFieldOrProperty(bulletGO, "canPierceUnits", true);
        TrySetFieldOrProperty(bulletGO, "maxPierceHits", pierceMaxHits);

        Debug.LogWarning($"{LOG_PREFIX} TryCastPierce: ElementBullet component NOT found, so damage will depend on the bullet's own collision script. bullet='{bulletGO.name}'", bulletGO);

        return true;
    }

    // Spawn a pierce bullet immediately without checking or setting the pierce skill cooldown.
    // This is intended for burst fire where the outer controller handles cooldown and element cycling.
    public bool SpawnPierceImmediate(Vector2 origin, Vector2 dir, ElementType elem)
    {
        if (bulletPool == null || firePoint == null)
        {
            Debug.LogError($"{LOG_PREFIX} SpawnPierceImmediate missing refs. bulletPool={(bulletPool ? "OK" : "NULL")} firePoint={(firePoint ? "OK" : "NULL")}", this);
            return false;
        }

        var bulletGO = bulletPool.Spawn(origin, dir, elem, player);
        if (bulletGO == null)
        {
            Debug.LogError($"{LOG_PREFIX} SpawnPierceImmediate bulletPool.Spawn returned NULL", this);
            return false;
        }

        bulletGO.transform.position = firePoint.position;

        Debug.Log($"{LOG_PREFIX} SpawnPierceImmediate spawned bullet='{bulletGO.name}' at {firePoint.position}, elem={elem}, dmg={pierceDamage}, speed={pierceBulletSpeed}", bulletGO);

        var eb = bulletGO.GetComponent<ElementBullet>();
        if (eb != null)
        {
            eb.damage = pierceDamage;
            eb.canPierceUnits = true;
            eb.maxPierceHits = pierceMaxHits;
            eb.Init(bulletPool, firePoint.position, dir, elem, player != null ? (UnityEngine.Object)player : this, pierceBulletSpeed, true, pierceMaxHits);
            Debug.Log($"{LOG_PREFIX} SpawnPierceImmediate used ElementBullet.Init (ElementBullet found).", bulletGO);
            return true;
        }

        bulletGO.transform.rotation = Quaternion.FromToRotation(Vector3.right, new Vector3(dir.x, dir.y, 0f));
        var rb2d = bulletGO.GetComponent<Rigidbody2D>();
        if (rb2d != null)
        {
            rb2d.linearVelocity = dir * pierceBulletSpeed;
            Debug.Log($"{LOG_PREFIX} SpawnPierceImmediate set bullet Rigidbody2D.linearVelocity={rb2d.linearVelocity}", bulletGO);
        }
        else
        {
            Debug.LogWarning($"{LOG_PREFIX} SpawnPierceImmediate bullet has no Rigidbody2D.", bulletGO);
        }

        TrySetFieldOrProperty(bulletGO, "currentElement", elem);
        TrySetFieldOrProperty(bulletGO, "element", elem);
        TrySetFieldOrProperty(bulletGO, "damage", pierceDamage);
        TrySetFieldOrProperty(bulletGO, "canPierceUnits", true);
        TrySetFieldOrProperty(bulletGO, "maxPierceHits", pierceMaxHits);

        return true;
    }

    bool TryCastBlink(Vector2 origin, Vector2 dir, ElementType elem)
    {
        if (Time.time < nextBlinkTime)
        {
            Debug.Log($"{LOG_PREFIX} TryCastBlink blocked by cooldown. now={Time.time:F3} next={nextBlinkTime:F3}", this);
            return false;
        }
        nextBlinkTime = Time.time + blinkCooldown;

        UnityEngine.Object source = player != null ? (UnityEngine.Object)player : this;

        Vector2 start = rb ? rb.position : (Vector2)transform.position;

        RaycastHit2D wallHit = Physics2D.Raycast(start, dir, blinkMaxDistance, obstacleLayers);
        Vector2 end = wallHit.collider ? wallHit.point - dir * 0.05f : (start + dir * blinkMaxDistance);

        Debug.Log($"{LOG_PREFIX} TryCastBlink: start={start} end={end} dir={dir} wallHit={(wallHit.collider ? wallHit.collider.name : "none")} obstacleMask={LayerMaskToString(obstacleLayers)} damageMask={LayerMaskToString(damageLayers)}", this);

        _hitIds.Clear();

        float castDist = Vector2.Distance(start, end);
        RaycastHit2D[] hits = Physics2D.CircleCastAll(start, blinkHitRadius, dir, castDist, damageLayers);

        Debug.Log($"{LOG_PREFIX} TryCastBlink CircleCastAll: radius={blinkHitRadius} dist={castDist:F3} hits={hits.Length}", this);

        for (int i = 0; i < hits.Length; i++)
        {
            var c = hits[i].collider;
            if (!c) continue;

            int id = c.GetInstanceID();
            if (_hitIds.Contains(id)) continue;
            _hitIds.Add(id);

            var targetGO = c.attachedRigidbody ? c.attachedRigidbody.gameObject : c.gameObject;

            Debug.Log($"{LOG_PREFIX} TryCastBlink hit[{i}]: collider='{c.name}' targetGO='{targetGO.name}' layer={LayerName(targetGO.layer)} isTrigger={c.isTrigger}", c);

            if (!IsSameElementTarget(targetGO, elem))
            {
                Debug.Log($"{LOG_PREFIX} TryCastBlink skip (element mismatch or missing element) target='{targetGO.name},'", targetGO);
                continue;
            }

            ApplyElementDamage_ExactInterface(targetGO, c, elem, blinkDamage, source);
        }

        if (rb) rb.position = end;
        else transform.position = end;

        return true;
    }

    bool TryCastAoe(Vector2 origin, Vector2 dir, ElementType elem)
    {
        if (Time.time < nextAoeTime)
        {
            Debug.Log($"{LOG_PREFIX} TryCastAoe blocked by cooldown. now={Time.time:F3} next={nextAoeTime:F3}", this);
            return false;
        }
        nextAoeTime = Time.time + aoeCooldown;

        UnityEngine.Object source = player != null ? (UnityEngine.Object)player : this;

        Vector2 casterPos = rb ? rb.position : (Vector2)transform.position;

        Vector2 aim = GetMouseWorldClamped(casterPos);
        Vector2 delta = aim - casterPos;
        float dist = delta.magnitude;
        if (dist > aoeMaxCastRange) aim = casterPos + delta / dist * aoeMaxCastRange;

        Collider2D[] cols = Physics2D.OverlapCircleAll(aim, aoeRadius, damageLayers);

        Debug.Log($"{LOG_PREFIX} TryCastAoe: casterPos={casterPos} aim={aim} radius={aoeRadius} found={cols.Length} damageMask={LayerMaskToString(damageLayers)}", this);

        _hitIds.Clear();

        for (int i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            if (!c) continue;

            int id = c.GetInstanceID();
            if (_hitIds.Contains(id)) continue;
            _hitIds.Add(id);

            var targetGO = c.attachedRigidbody ? c.attachedRigidbody.gameObject : c.gameObject;

            Debug.Log($"{LOG_PREFIX} TryCastAoe overlap[{i}]: collider='{c.name}' targetGO='{targetGO.name}' layer={LayerName(targetGO.layer)} isTrigger={c.isTrigger}", c);

            if (!IsSameElementTarget(targetGO, elem))
            {
                Debug.Log($"{LOG_PREFIX} TryCastAoe skip (element mismatch or missing element) target='{targetGO.name}'", targetGO);
                continue;
            }

            ApplyElementDamage_ExactInterface(targetGO, c, elem, aoeDamage, source);
        }

        return true;
    }

    bool IsSameElementTarget(GameObject go, ElementType playerElem)
    {
        if (go == null) return false;

        if (TryGetElement(go, out ElementType targetElem))
            return targetElem.Equals(playerElem);

        Debug.LogWarning($"{LOG_PREFIX} IsSameElementTarget: target='{go.name}' has NO element field/property found (currentElement/element).", go);
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
        if (dmg == null)
        {
            Debug.LogWarning($"{LOG_PREFIX} ApplyElementDamage: NO IElementDamageable found. targetGO='{targetGO.name}' collider='{(hitCollider ? hitCollider.name : "null")}'", targetGO);
            return;
        }

        bool canHit = false;
        try
        {
            canHit = dmg.CanBeHitBy(element);
        }
        catch (Exception e)
        {
            Debug.LogError($"{LOG_PREFIX} ApplyElementDamage: CanBeHitBy threw. targetGO='{targetGO.name}' err={e}", targetGO);
            return;
        }

        Debug.Log($"{LOG_PREFIX} ApplyElementDamage: targetGO='{targetGO.name}' dmgType='{dmg.GetType().Name}' element={element} damage={damage} source={(source ? source.name : "null")} CanBeHitBy={canHit}", targetGO);

        if (canHit)
        {
            try
            {
                dmg.TakeElementHit(element, damage, source);
                Debug.Log($"{LOG_PREFIX} ApplyElementDamage: TakeElementHit OK. targetGO='{targetGO.name}'", targetGO);
            }
            catch (Exception e)
            {
                Debug.LogError($"{LOG_PREFIX} ApplyElementDamage: TakeElementHit threw. targetGO='{targetGO.name}' err={e}", targetGO);
            }
            return;
        }

        var enemy = (dmg as Component) ? (dmg as Component).GetComponentInParent<EnemyShooter2D>() : null;
        if (enemy != null && enemy.currentElement == element)
        {
            try
            {
                dmg.TakeElementHit(element, damage, source);
                Debug.Log($"{LOG_PREFIX} ApplyElementDamage: Fallback EnemyShooter2D element match. TakeElementHit OK. targetGO='{targetGO.name}'", targetGO);
            }
            catch (Exception e)
            {
                Debug.LogError($"{LOG_PREFIX} ApplyElementDamage: Fallback TakeElementHit threw. targetGO='{targetGO.name}' err={e}", targetGO);
            }
            return;
        }

        Debug.LogWarning($"{LOG_PREFIX} ApplyElementDamage: blocked. CanBeHitBy=false and no EnemyShooter2D element-match fallback. targetGO='{targetGO.name}'", targetGO);
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
            try
            {
                f.SetValue(obj, value);
                Debug.Log($"{LOG_PREFIX} TrySetFieldOrProperty: set {t.Name}.{name} (field) = {value}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LOG_PREFIX} TrySetFieldOrProperty: failed set field {t.Name}.{name}: {e.Message}");
            }
            return;
        }

        var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.CanWrite && (value == null || p.PropertyType.IsAssignableFrom(value.GetType()) || p.PropertyType.IsEnum))
        {
            try
            {
                p.SetValue(obj, value);
                Debug.Log($"{LOG_PREFIX} TrySetFieldOrProperty: set {t.Name}.{name} (prop) = {value}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LOG_PREFIX} TrySetFieldOrProperty: failed set prop {t.Name}.{name}: {e.Message}");
            }
        }
    }

    // Public wrappers so external input handlers can invoke specific skills directly
    public bool CastPierce(Vector2 origin, Vector2 dir)
    {
        return TryCastPierce(origin, dir, GetPlayerElement());
    }

    public bool CastBlink(Vector2 origin, Vector2 dir)
    {
        return TryCastBlink(origin, dir, GetPlayerElement());
    }

    public bool CastAoe(Vector2 origin)
    {
        // TryCastAoe ignores dir and uses mouse; pass Vector2.zero for dir
        return TryCastAoe(origin, Vector2.zero, GetPlayerElement());
    }

    void LogLayerConfigOnce(string from)
    {
        if (_loggedLayerConfigOnce) return;
        _loggedLayerConfigOnce = true;

        Debug.Log($"{LOG_PREFIX} {from} LayerMasks: damageLayers={LayerMaskToString(damageLayers)} obstacleLayers={LayerMaskToString(obstacleLayers)}", this);

        int playerLayer = gameObject.layer;
        Debug.Log($"{LOG_PREFIX} {from} Player layer='{LayerName(playerLayer)}'({playerLayer})", this);

        if (damageLayers.value == 0)
            Debug.LogWarning($"{LOG_PREFIX} damageLayers is EMPTY (value=0). Overlap/Cast will never find targets.", this);

        if (obstacleLayers.value == 0)
            Debug.LogWarning($"{LOG_PREFIX} obstacleLayers is EMPTY (value=0). Blink raycast will ignore walls.", this);
    }

    void LogRefs(string from)
    {
        Debug.Log($"{LOG_PREFIX} {from} Refs: player={(player ? "OK" : "NULL")} rb={(rb ? "OK" : "NULL")} firePoint={(firePoint ? "OK" : "NULL")} bulletPool={(bulletPool ? "OK" : "NULL")} cam={(targetCamera ? "OK" : "NULL")}", this);
    }

    static string LayerMaskToString(LayerMask mask)
    {
        if (mask.value == 0) return $"0 (None)";

        List<string> names = new List<string>();
        for (int i = 0; i < 32; i++)
        {
            int bit = 1 << i;
            if ((mask.value & bit) != 0)
            {
                string n = LayerMask.LayerToName(i);
                if (string.IsNullOrEmpty(n)) n = $"Layer{i}";
                names.Add($"{n}({i})");
            }
        }
        return $"{mask.value} [{string.Join(", ", names)}]";
    }

    static string LayerName(int layer)
    {
        string n = LayerMask.LayerToName(layer);
        return string.IsNullOrEmpty(n) ? $"Layer{layer}" : n;
    }

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
