// File: Player/PlayerElementAttack.cs
using UnityEngine;
using GameJam.Common;

public class PlayerElementAttack : MonoBehaviour
{
    [Header("Attack")]
    public float attackCooldown = 0.35f;

    float cd;

    int[] elementCycle = new int[GameDefs.ElementCount] { 0, 1, 2 };
    int cycleIndex;

    void Awake()
    {
        ShuffleCycle();
        cycleIndex = 0;
    }

    void Update()
    {
        if (cd > 0f) cd -= Time.deltaTime;
    }

    public bool TryAttack(string skillKey)
    {
        if (cd > 0f) return false;
        cd = attackCooldown;

        ElementType element = NextElementFromCycle();

        Debug.Log($"[PlayerAttack] {skillKey} Cast -> Attack! Element = {GameDefs.ElementToText(element)}");
        return true;
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
