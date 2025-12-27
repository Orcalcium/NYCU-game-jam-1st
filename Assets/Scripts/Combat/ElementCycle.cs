// File: Combat/ElementCycle.cs
using UnityEngine;
using GameJam.Common;

public class ElementCycle : MonoBehaviour
{
    int[] cycle = new int[GameDefs.ElementCount] { 0, 1, 2 };
    int index;

    void Awake()
    {
        Shuffle();
        index = 0;
    }

    public ElementType Next()
    {
        int v = cycle[index];
        index++;
        if (index >= cycle.Length)
        {
            Shuffle();
            index = 0;
        }
        return (ElementType)v;
    }

    public ElementType PeekNext()
    {
        return (ElementType)cycle[index];
    }

    void Shuffle()
    {
        for (int i = cycle.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = cycle[i];
            cycle[i] = cycle[j];
            cycle[j] = tmp;
        }
    }
}
