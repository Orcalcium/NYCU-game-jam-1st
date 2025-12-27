// File: Player/PlayerSkillIndicator.cs
using UnityEngine;
using GameJam.Common;

public class PlayerSkillIndicator : MonoBehaviour
{
    [Header("Refs")]
    public PlayerElementAttack attack;
    public PlayerTargeting targeting;

    [Header("Rendering")]
    public int sortingOrder = 50;

    [Header("Circle Sprites (assign a simple ring/circle sprite)")]
    public Sprite circleSprite;
    public float circleAlpha = 0.25f;

    [Header("Line")]
    public float lineAlpha = 0.35f;
    public float lineWidth = 0.08f;

    bool aiming;
    SkillKey? activeSkill;

    LineRenderer line;
    SpriteRenderer circle;

    void Awake()
    {
        if (attack == null) attack = GetComponent<PlayerElementAttack>();
        if (targeting == null) targeting = GetComponent<PlayerTargeting>();

        BuildVisuals();
        SetAiming(false);
        SetActiveSkill(null);
    }

    void BuildVisuals()
    {
        if (line == null)
        {
            GameObject go = new GameObject("SkillLine");
            go.transform.SetParent(transform, false);
            line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.sortingOrder = sortingOrder;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.enabled = false;
        }

        if (circle == null)
        {
            GameObject go = new GameObject("SkillCircle");
            go.transform.SetParent(transform, false);
            circle = go.AddComponent<SpriteRenderer>();
            circle.sprite = circleSprite;
            circle.sortingOrder = sortingOrder;
            circle.enabled = false;
        }
    }

    public void SetAiming(bool on)
    {
        aiming = on;
        if (!aiming)
        {
            if (line != null) line.enabled = false;
            if (circle != null) circle.enabled = false;
        }
    }

    public void SetActiveSkill(SkillKey? skill)
    {
        activeSkill = skill;

        if (!aiming || !activeSkill.HasValue)
        {
            if (line != null) line.enabled = false;
            if (circle != null) circle.enabled = false;
            return;
        }
    }

    public void TickVisuals(Vector2 aimDir)
    {
        if (!aiming || !activeSkill.HasValue) return;
        if (attack == null) return;

        Monster target = (targeting != null) ? targeting.CurrentTarget : null;

        if (activeSkill.Value == SkillKey.Q)
        {
            if (line == null) return;

            Vector3 p0 = transform.position;

            Vector2 dir = aimDir.sqrMagnitude < 1e-6f ? Vector2.right : aimDir.normalized;

            Vector3 through = target != null ? target.transform.position : (p0 + (Vector3)(dir * 2f));
            Vector3 end = new Vector3(through.x, through.y, p0.z) + (Vector3)(dir * attack.qExtraDistance);

            line.enabled = true;
            circle.enabled = false;

            var c = line.startColor;
            c.a = lineAlpha;
            line.startColor = c;
            line.endColor = c;

            line.startWidth = Mathf.Max(0.01f, attack.qHitWidth * 0.12f);
            line.endWidth = line.startWidth;

            line.SetPosition(0, new Vector3(p0.x, p0.y, 0f));
            line.SetPosition(1, new Vector3(end.x, end.y, 0f));
            return;
        }

        if (activeSkill.Value == SkillKey.W)
        {
            if (circle == null) return;

            Vector3 cpos = target != null ? target.transform.position : transform.position;

            circle.enabled = true;
            if (line != null) line.enabled = false;

            circle.color = new Color(1f, 1f, 1f, circleAlpha);
            circle.transform.position = new Vector3(cpos.x, cpos.y, 0f);

            float d = attack.wRadius * 2f;
            circle.transform.localScale = new Vector3(d, d, 1f);
            return;
        }

        if (activeSkill.Value == SkillKey.E)
        {
            if (circle == null) return;

            Vector3 cpos = target != null ? target.transform.position : GetMouseWorldFallback();

            circle.enabled = true;
            if (line != null) line.enabled = false;

            circle.color = new Color(1f, 1f, 1f, circleAlpha);
            circle.transform.position = new Vector3(cpos.x, cpos.y, 0f);

            float d = attack.eExplosionRadius * 2f;
            circle.transform.localScale = new Vector3(d, d, 1f);
            return;
        }
    }

    Vector3 GetMouseWorldFallback()
    {
        Camera cam = Camera.main;
        if (cam == null) return transform.position;

        Vector3 p = transform.position;
        Vector3 sp = Input.mousePosition;
        sp.z = Mathf.Abs(cam.transform.position.z - p.z);
        Vector3 mw = cam.ScreenToWorldPoint(sp);
        return new Vector3(mw.x, mw.y, p.z);
    }
}
