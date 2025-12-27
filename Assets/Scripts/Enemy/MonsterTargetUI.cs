// File: Enemy/MonsterTargetUI.cs
using UnityEngine;

public class MonsterTargetUI : MonoBehaviour
{
    [Header("Indicator")]
    public SpriteRenderer indicatorRenderer;
    public int orderInLayer = 10;
    public float scale = 0.9f;
    public Vector3 localOffset = new Vector3(0f, 0.55f, 0f);

    void Awake()
    {
        if (indicatorRenderer == null)
        {
            var go = new GameObject("TargetIndicator");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localOffset;
            go.transform.localScale = Vector3.one * scale;

            indicatorRenderer = go.AddComponent<SpriteRenderer>();
            indicatorRenderer.enabled = false;
            indicatorRenderer.sortingOrder = orderInLayer;
        }
        else
        {
            indicatorRenderer.sortingOrder = orderInLayer;
            indicatorRenderer.enabled = false;
        }
    }

    public void SetVisible(bool visible)
    {
        if (indicatorRenderer != null)
            indicatorRenderer.enabled = visible;
    }
}
