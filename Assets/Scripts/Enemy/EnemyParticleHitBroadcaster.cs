// File: Combat/EnemyParticleHitBroadcaster.cs
using UnityEngine;
using GameJam.Common;

[RequireComponent(typeof(ParticleSystem))]
public class EnemyParticleHitBroadcaster : MonoBehaviour
{
    [Header("Owner")]
    public EnemyShooter2D owner;
    public int particleDamage = 1;

    [Header("Broadcast")]
    public string hitMessageName = "OnEnemyParticleHit";

    ParticleSystem ps;

    void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        if (owner == null) owner = GetComponentInParent<EnemyShooter2D>(true);
    }

    void OnParticleCollision(GameObject other)
    {
        if (owner == null) return;

        var root = owner.transform;
        if (root == null) return;

        root.SendMessage(hitMessageName, other, SendMessageOptions.DontRequireReceiver);

        IElementDamageable damageable = other.GetComponent<IElementDamageable>();
        if (damageable == null) damageable = other.GetComponentInParent<IElementDamageable>();

        if (damageable == null) return;

        if (damageable.CanBeHitBy(owner.currentElement, owner))
        {
            damageable.TakeElementHit(owner.currentElement, particleDamage, owner);
        }
    }
}
