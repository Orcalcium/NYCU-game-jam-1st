using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Healpack : MonoBehaviour
{
    [Tooltip("Amount of HP restored when picked up")]
    public int healAmount = 1;

    [Tooltip("Auto-destroy after this many seconds if not picked up. Set <=0 to disable.")]
    public float lifeTime = 30f;

    void Start()
    {
        if (lifeTime > 0f) Destroy(gameObject, lifeTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;
        var player = other.GetComponent<PlayerController2D>();
        if (player != null)
        {
            player.Heal(healAmount);
            // Optionally: spawn FX / sound here
            Destroy(gameObject);
        }
    }
}
