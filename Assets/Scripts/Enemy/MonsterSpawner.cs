// File: Enemy/MonsterSpawner.cs
using UnityEngine;

public class MonsterSpawner : MonoBehaviour
{
    public MonsterPool pool;
    public Transform player;

    [Header("Spawn")]
    public float spawnInterval = 1.2f;
    public float spawnRadius = 6f;

    float timer;

    void Awake()
    {
        if (pool == null) pool = FindFirstObjectByType<MonsterPool>();
    }

    void Update()
    {
        if (pool == null || player == null) return;

        timer += Time.deltaTime;
        if (timer < spawnInterval) return;
        timer = 0f;

        Vector2 r = Random.insideUnitCircle.normalized * spawnRadius;
        Vector3 pos = new Vector3(player.position.x + r.x, player.position.y + r.y, player.position.z);
        pool.Spawn(pos, player);
    }
}
