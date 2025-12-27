using UnityEngine;
using GameJam.Common;

public class MonsterElementDots : MonoBehaviour
{
    [Header("Dot Visual")]
    public Sprite dotSprite;
    public float dotScale = 0.18f;
    public float spacing = 0.22f;
    public Vector3 localOffset = new Vector3(0f, -0.55f, 0f);

    const int DOT_SORTING_ORDER = 4;

    Transform[] dots = new Transform[GameDefs.ElementCount];
    SpriteRenderer[] renderers = new SpriteRenderer[GameDefs.ElementCount];

    void Awake()
    {
        BuildIfNeeded();
    }

    void BuildIfNeeded()
    {
        for (int i = 0; i < GameDefs.ElementCount; i++)
        {
            if (dots[i] != null) continue;

            GameObject go = new GameObject($"Dot_{i}");
            go.transform.SetParent(transform, false);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = dotSprite;
            sr.sortingOrder = DOT_SORTING_ORDER;

            dots[i] = go.transform;
            renderers[i] = sr;
        }

        Layout();
    }

    void Layout()
    {
        float startX = -(spacing * (GameDefs.ElementCount - 1)) * 0.5f;
        for (int i = 0; i < GameDefs.ElementCount; i++)
        {
            dots[i].localPosition =
                localOffset + new Vector3(startX + spacing * i, 0f, 0f);
            dots[i].localScale = Vector3.one * dotScale;
        }
    }

    public void SetOrder(ElementType[] order)
    {
        BuildIfNeeded();

        for (int i = 0; i < GameDefs.ElementCount; i++)
        {
            renderers[i].enabled = true;
            renderers[i].color = GameDefs.ElementToColor(order[i]);
            renderers[i].sortingOrder = DOT_SORTING_ORDER;
        }
    }

    public void HideIndex(int index)
    {
        if (index < 0 || index >= GameDefs.ElementCount) return;
        if (renderers[index] != null)
            renderers[index].enabled = false;
    }

    public void ResetAllVisible()
    {
        for (int i = 0; i < GameDefs.ElementCount; i++)
            if (renderers[i] != null)
                renderers[i].enabled = true;
    }
}
