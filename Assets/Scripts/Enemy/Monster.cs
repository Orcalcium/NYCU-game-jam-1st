// File: Enemy/Monster.cs
using UnityEngine;
using GameJam.Common;

public class Monster : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 2.5f;
    public float stopDistance = 0.25f;

    [Header("Separation (avoid overlap)")]
    public float separationRadius = 0.8f;
    public float separationStrength = 1.25f;

    [Header("Element Shields (3 hits total, must break in order)")]
    public ElementType[] shields = new ElementType[3]
    {
        ElementType.Fire,
        ElementType.Water,
        ElementType.Nature
    };

    [Header("Dots")]
    public MonsterElementDots dots;

    [Header("Visual")]
    public SpriteRenderer bodyRenderer;

    Transform target;
    int shieldIndex;
    MonsterPool ownerPool;
    bool active;

    float stunTimer;

    public ElementType CurrentRequiredElement
    {
        get
        {
            int idx = Mathf.Clamp(shieldIndex, 0, shields.Length - 1);
            return shields[idx];
        }
    }

    public void Init(Transform player, MonsterPool pool)
    {
        target = player;
        ownerPool = pool;
        shieldIndex = 0;
        active = true;
        stunTimer = 0f;

        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<SpriteRenderer>(true);

        ShuffleShields();

        if (dots == null) dots = GetComponentInChildren<MonsterElementDots>(true);
        if (dots != null)
        {
            dots.SetOrder(shields);
            dots.ResetAllVisible();
        }

        UpdateBodyColor();
    }

    void Update()
    {
        if (!active || target == null) return;

        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            return;
        }

        Vector3 p = transform.position;
        Vector3 t = target.position;

        Vector3 toPlayer = t - p;
        toPlayer.z = 0f;

        float dist = toPlayer.magnitude;
        if (dist <= stopDistance) return;

        Vector3 dir = toPlayer.normalized;

        Vector3 sep = Vector3.zero;
        Monster[] all = FindObjectsByType<Monster>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            Monster other = all[i];
            if (other == null || other == this) continue;
            if (!other.gameObject.activeInHierarchy) continue;

            Vector3 op = other.transform.position;
            Vector3 away = p - op;
            away.z = 0f;

            float d = away.magnitude;
            if (d <= 0.0001f || d >= separationRadius) continue;

            float w = 1f - (d / separationRadius);
            sep += (away / d) * w;
        }

        Vector3 finalDir = (dir + sep * separationStrength).normalized;

        transform.position = Vector3.MoveTowards(
            p,
            p + finalDir,
            moveSpeed * Time.deltaTime
        );
    }

    public void TakeHit(ElementType element)
    {
        if (!active) return;
        if (shieldIndex < 0 || shieldIndex >= shields.Length) return;

        ElementType required = shields[shieldIndex];

        if (element != required)
        {
            Debug.Log($"[Monster] Blocked. Need {GameDefs.ElementToText(required)} but got {GameDefs.ElementToText(element)}");
            return;
        }

        Debug.Log($"[Monster] Shield broken: {GameDefs.ElementToText(required)} ({shieldIndex + 1}/3)");
        if (dots != null) dots.HideIndex(shieldIndex);

        shieldIndex++;

        if (shieldIndex >= shields.Length)
        {
            Die();
            return;
        }

        UpdateBodyColor();
    }

    public void Stun(float seconds)
    {
        if (!active) return;
        if (seconds <= 0f) return;
        stunTimer = Mathf.Max(stunTimer, seconds);
    }

    void UpdateBodyColor()
    {
        if (bodyRenderer == null) return;
        bodyRenderer.color = GameDefs.ElementToColor(CurrentRequiredElement);
    }

    void Die()
    {
        Debug.Log("[Monster] Defeated -> Return to pool");
        Despawn();
    }

    public void Despawn()
    {
        if (!active) return;
        active = false;
        target = null;

        if (ownerPool != null) ownerPool.Release(this);
        else gameObject.SetActive(false);
    }

    void ShuffleShields()
    {
        for (int i = shields.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            ElementType tmp = shields[i];
            shields[i] = shields[j];
            shields[j] = tmp;
        }
    }
}
