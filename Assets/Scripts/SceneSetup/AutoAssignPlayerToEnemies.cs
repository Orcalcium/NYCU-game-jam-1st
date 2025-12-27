// File: SceneSetup/AutoAssignPlayerToEnemies.cs
using UnityEngine;

public class AutoAssignPlayerToEnemies : MonoBehaviour
{
    public Transform player;

    void Start()
    {
        if (player == null)
        {
            var p = FindFirstObjectByType<PlayerController2D>();
            if (p != null) player = p.transform;
        }
    }
}
