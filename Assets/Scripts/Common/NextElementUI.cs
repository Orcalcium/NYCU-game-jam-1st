using UnityEngine;
using UnityEngine.UI;
using GameJam.Common;

public class NextElementUI : MonoBehaviour
{
    [Header("Refs")]
    public PlayerController2D player;
    public Image targetImage;

    private ElementCycle elementCycle;

    void Start()
    {
        if (player == null)
        {
            player = Object.FindFirstObjectByType<PlayerController2D>();
        }

        if (player != null)
        {
            elementCycle = player.GetComponent<ElementCycle>();
        }

        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }

        UpdateVisual();
    }

    void Update()
    {
        UpdateVisual();
    }

    public void UpdateVisual()
    {
        if (elementCycle == null || targetImage == null) return;

        ElementType next = elementCycle.PeekNext();
        targetImage.color = GameDefs.ElementToColor(next);
    }
}
