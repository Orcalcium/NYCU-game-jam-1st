// File: Player/PlayerElementAttack.cs  (只需確保這些欄位是 public，供指示器讀取)
using UnityEngine;
using GameJam.Common;

public class PlayerElementAttack : MonoBehaviour
{
    [Header("General")]
    public float attackCooldown = 0.35f;
    public PlayerTargeting targeting;

    [Header("Q - Dash Through Target (line damage)")]
    public float qExtraDistance = 2.5f;
    public float qHitWidth = 0.8f;

    [Header("W - AoE Around Target")]
    public float wRadius = 2.0f;

    [Header("E - Projectile + Same-Element AoE + Stun")]
    public ElementProjectile projectilePrefab;
    public float eExplosionRadius = 2.4f;
    public float eStunSeconds = 0.6f;

    float cd;

    int[] elementCycle = new int[GameDefs.ElementCount] { 0, 1, 2 };
    int cycleIndex;

    void Awake()
    {
        if (targeting == null) targeting = GetComponent<PlayerTargeting>();
        ShuffleCycle();
        cycleIndex = 0;
    }

    void Update()
    {
        if (cd > 0f) cd -= Time.deltaTime;
    }

    public bool TryCastSkill(SkillKey key, Vector2 aimDir)
    {
        if (cd > 0f) return false;
        cd = attackCooldown;

        Monster target = (targeting != null) ? targeting.CurrentTarget : null;
        if (target == null)
        {
            Debug.Log($"[PlayerAttack] {key} Cast -> No target");
            return true;
        }

        ElementType element = NextElementFromCycle();
        Debug.Log($"[PlayerAttack] {key} Cast -> Element={GameDefs.ElementToText(element)} Target={target.name}");

        switch (key)
        {
            case SkillKey.Q:
                SkillQ(element, aimDir, target);
                break;
            case SkillKey.W:
                SkillW(element, target);
                break;
            case SkillKey.E:
                SkillE(element, target);
                break;
        }

        return true;
    }

    void SkillQ(ElementType element, Vector2 aimDir, Monster target)
    {
        Vector3 p0 = transform.position;
        Vector3 t = target.transform.position;

        Vector2 dir = aimDir.sqrMagnitude < 1e-6f
            ? (new Vector2(t.x - p0.x, t.y - p0.y)).normalized
            : aimDir.normalized;

        Vector3 end = new Vector3(t.x, t.y, p0.z) + (Vector3)(dir * qExtraDistance);

        Debug.Log($"[Skill Q] Dash line: {p0} -> {end} (width {qHitWidth}), Element={GameDefs.ElementToText(element)}");

        Monster[] all = FindObjectsByType<Monster>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            Monster m = all[i];
            if (m == null) continue;
            if (!m.gameObject.activeInHierarchy) continue;

            float d = DistancePointToSegment2D(m.transform.position, p0, end);
            if (d > qHitWidth * 0.5f) continue;

            Debug.Log($"[Skill Q] Hit monster -> TakeHit({GameDefs.ElementToText(element)})");
            m.TakeHit(element);
        }

        transform.position = new Vector3(end.x, end.y, p0.z);
    }

    void SkillW(ElementType element, Monster target)
    {
        Vector3 c = target.transform.position;

        Debug.Log($"[Skill W] AoE at {c}, r={wRadius}, Element={GameDefs.ElementToText(element)}");

        Monster[] all = FindObjectsByType<Monster>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            Monster m = all[i];
            if (m == null) continue;
            if (!m.gameObject.activeInHierarchy) continue;

            float d = (m.transform.position - c).sqrMagnitude;
            if (d > wRadius * wRadius) continue;

            Debug.Log($"[Skill W] Hit monster -> TakeHit({GameDefs.ElementToText(element)})");
            m.TakeHit(element);
        }
    }

    void SkillE(ElementType element, Monster target)
    {
        Vector3 from = transform.position;
        Vector3 to = target.transform.position;

        ElementType match = target.CurrentRequiredElement;

        Debug.Log($"[Skill E] Launch projectile -> explode at {to}, MatchElement={GameDefs.ElementToText(match)}, AttackElement={GameDefs.ElementToText(element)}");

        if (projectilePrefab == null)
        {
            Debug.Log("[Skill E] projectilePrefab missing (no projectile spawned)");
            return;
        }

        ElementProjectile proj = Instantiate(projectilePrefab);
        proj.Init(from, to, element, match, eExplosionRadius, eStunSeconds);
    }

    static float DistancePointToSegment2D(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector2 p = new Vector2(point.x, point.y);
        Vector2 v = new Vector2(a.x, a.y);
        Vector2 w = new Vector2(b.x, b.y);

        Vector2 vw = w - v;
        float l2 = vw.sqrMagnitude;
        if (l2 <= 1e-6f) return Vector2.Distance(p, v);

        float t = Vector2.Dot(p - v, vw) / l2;
        t = Mathf.Clamp01(t);

        Vector2 proj = v + t * vw;
        return Vector2.Distance(p, proj);
    }

    ElementType NextElementFromCycle()
    {
        int v = elementCycle[cycleIndex];
        cycleIndex++;

        if (cycleIndex >= elementCycle.Length)
        {
            ShuffleCycle();
            cycleIndex = 0;
        }

        return (ElementType)v;
    }

    void ShuffleCycle()
    {
        for (int i = elementCycle.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = elementCycle[i];
            elementCycle[i] = elementCycle[j];
            elementCycle[j] = tmp;
        }
    }
}
