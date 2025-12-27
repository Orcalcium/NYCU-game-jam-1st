// File: Combat/EnemyWeakpointDots.cs
using UnityEngine;
using GameJam.Common;

public class EnemyWeakpointDots : MonoBehaviour
{
    [Header("Dots")]
    public SpriteRenderer dotPrefab;
    public int dotCount = 3;
    public float spacing = 0.22f;
    public Vector2 localOffset = new Vector2(0f, -0.55f);
    public float dotScale = 0.16f;
    public int sortingOrder = 4;   // ★ 固定顯示層級

    SpriteRenderer[] dots;
    ElementType[] sequence;

    public void Build(ElementType[] weakSequence)
    {
        sequence = weakSequence;

        if (dots != null)
        {
            for (int i = 0; i < dots.Length; i++)
                if (dots[i] != null) Destroy(dots[i].gameObject);
        }

        int n = Mathf.Max(0, dotCount);
        dots = new SpriteRenderer[n];

        float totalW = (n - 1) * spacing;
        float startX = -totalW * 0.5f;

        for (int i = 0; i < n; i++)
        {
            var d = Instantiate(dotPrefab, transform);
            d.transform.localPosition =
                new Vector3(startX + i * spacing, 0f, 0f) + (Vector3)localOffset;
            d.transform.localScale = Vector3.one * dotScale;

            // ★ 關鍵：設定顯示層級
            d.sortingOrder = sortingOrder;

            d.gameObject.SetActive(true);
            dots[i] = d;
        }

        Refresh(0);
    }

    public void Refresh(int clearedCount)
    {
        if (dots == null || sequence == null) return;

        for (int i = 0; i < dots.Length; i++)
        {
            if (dots[i] == null) continue;

            if (i < clearedCount)
            {
                dots[i].color = new Color(1f, 1f, 1f, 0.18f);
            }
            else
            {
                ElementType e = sequence[Mathf.Clamp(i, 0, sequence.Length - 1)];
                dots[i].color = GameDefs.ElementToColor(e);
            }
        }
    }
}
