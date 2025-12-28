using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealpackSpawner : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject healpackPrefab;

    [Header("Spawn")]
    public float spawnInterval = 12f;
    public int maxActiveHealpacks = 3;

    [Header("Bounds (world coords)")]
    public float boundsMinX = -12f;
    public float boundsMaxX = 12f;
    public float boundsMinY = -6f;
    public float boundsMaxY = 6f;

    [Header("Anti-overlap")]
    public float spawnClearRadius = 0.6f;
    public int spawnTryCount = 20;
    public LayerMask overlapMask = ~0; // default: everything

    List<GameObject> activeHealpacks = new List<GameObject>();

    float spawnTimer;

    void Start()
    {
        if (healpackPrefab == null)
        {
            Debug.LogWarning("[HealpackSpawner] No healpack prefab assigned.");
            enabled = false;
            return;
        }
        spawnTimer = spawnInterval;
    }

    void Update()
    {
        // Clean up null (destroyed) entries
        activeHealpacks.RemoveAll(x => x == null);

        if (activeHealpacks.Count >= maxActiveHealpacks) return;

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            TrySpawn();
            spawnTimer = spawnInterval;
        }
    }

    void TrySpawn()
    {
        if (healpackPrefab == null) return;
        if (!TryFindNonOverlappingSpawn(out Vector2 pos)) return;

        var hp = Instantiate(healpackPrefab, pos, Quaternion.identity);
        activeHealpacks.Add(hp);
    }

    bool TryFindNonOverlappingSpawn(out Vector2 spawnPos)
    {
        spawnPos = Vector2.zero;
        for (int i = 0; i < spawnTryCount; i++)
        {
            float x = Random.Range(boundsMinX, boundsMaxX);
            float y = Random.Range(boundsMinY, boundsMaxY);
            Vector2 p = new Vector2(x, y);
            if (Physics2D.OverlapCircle(p, spawnClearRadius, overlapMask) == null)
            {
                spawnPos = p;
                return true;
            }
        }
        return false;
    }

    // Draw spawn bounds and clear radius for convenience
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector3 tl = new Vector3(boundsMinX, boundsMaxY, 0f);
        Vector3 tr = new Vector3(boundsMaxX, boundsMaxY, 0f);
        Vector3 bl = new Vector3(boundsMinX, boundsMinY, 0f);
        Vector3 br = new Vector3(boundsMaxX, boundsMinY, 0f);
        Gizmos.DrawLine(tl, tr);
        Gizmos.DrawLine(tr, br);
        Gizmos.DrawLine(br, bl);
        Gizmos.DrawLine(bl, tl);
        Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
        // Optionally visualize spawnClearRadius at last found spawn (editor-only)
    }
}
