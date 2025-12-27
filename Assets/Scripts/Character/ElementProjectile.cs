// File: Player/Skills/ElementProjectile.cs
using UnityEngine;
using GameJam.Common;

public class ElementProjectile : MonoBehaviour
{
    public float speed = 12f;
    public float explodeRadius = 2.2f;
    public float arriveThreshold = 0.15f;
    public float stunSeconds = 0.5f;

    Vector3 targetPos;
    ElementType element;
    ElementType matchElementForAoE;
    bool initialized;

    public void Init(Vector3 from, Vector3 to, ElementType attackElement, ElementType matchElement, float radius, float stun)
    {
        transform.position = from;
        targetPos = to;
        element = attackElement;
        matchElementForAoE = matchElement;
        explodeRadius = radius;
        stunSeconds = stun;
        initialized = true;
    }

    void Update()
    {
        if (!initialized) return;

        Vector3 p = transform.position;
        Vector3 next = Vector3.MoveTowards(p, targetPos, speed * Time.deltaTime);
        transform.position = next;

        if ((targetPos - next).sqrMagnitude <= arriveThreshold * arriveThreshold)
        {
            Explode();
        }
    }

    void Explode()
    {
        Debug.Log($"[Projectile] Explode at {transform.position}, AttackElement={GameDefs.ElementToText(element)}, Match={GameDefs.ElementToText(matchElementForAoE)}");

        Monster[] all = FindObjectsByType<Monster>(FindObjectsSortMode.None);
        Vector3 c = transform.position;

        for (int i = 0; i < all.Length; i++)
        {
            Monster m = all[i];
            if (m == null) continue;
            if (!m.gameObject.activeInHierarchy) continue;

            float d = (m.transform.position - c).sqrMagnitude;
            if (d > explodeRadius * explodeRadius) continue;

            if (m.CurrentRequiredElement != matchElementForAoE) continue;

            Debug.Log($"[Projectile] Hit (same-element) monster -> TakeHit({GameDefs.ElementToText(element)}) + Stun({stunSeconds}s)");
            m.TakeHit(element);
            m.Stun(stunSeconds);
        }

        Destroy(gameObject);
    }
}
