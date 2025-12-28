using System.Collections;
using UnityEngine;
using GameJam.Common;

/// <summary>
/// A timed damage zone that fades in, deals damage to targets in range, then fades out and despawns.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TimedDamageZone : MonoBehaviour
{
    [Header("Visuals")]
    [Tooltip("Renderer to fade in/out (auto-found if not assigned)")]
    public Renderer zoneRenderer;

    [Header("Timing")]
    [Tooltip("Time to fade in before activating")]
    public float fadeInDuration = 0.5f;

    [Tooltip("How long the zone stays active after fade in")]
    public float activeDuration = 0.3f;

    [Tooltip("Time to fade out before despawning")]
    public float fadeOutDuration = 0.5f;

    [Header("Animation")]
    [Tooltip("Curve for blink animation inside fade-in (evaluated 0..1 over each blink period)")]
    public AnimationCurve blinkCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("Period of each blink during fade-in (seconds). If <= 0, no blinking is applied.")]
    public float blinkPeriod = 0.12f;

    [Tooltip("Curve for fade out animation (1 to 0)")]
    public AnimationCurve fadeOutCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Header("Damage")]
    [Tooltip("Damage dealt to targets")]
    public int damage = 1;

    [Tooltip("Element type for damage")]
    public ElementType elementType = ElementType.Fire;

    [Header("Collision")]
    [Tooltip("Layer mask for targets that can be damaged")]
    public LayerMask targetMask = -1;

    [SerializeField]
    private Collider2D zoneCollider;
    private Color baseColor;
    private MaterialPropertyBlock propertyBlock;
    private System.Action<TimedDamageZone> onComplete;
    private UnityEngine.Object damageOwner;
    private bool isActive;

    void Awake()
    {
        zoneCollider = GetComponent<Collider2D>();
        zoneCollider.isTrigger = true;

        if (zoneRenderer == null)
        {
            zoneRenderer = GetComponent<Renderer>();
        }

        if (zoneRenderer == null)
        {
            Debug.LogWarning($"[TimedDamageZone] No Renderer found on {gameObject.name}");
        }

        propertyBlock = new MaterialPropertyBlock();
    }

    /// <summary>
    /// Activates the damage zone at the specified position.
    /// </summary>
    public void Activate(Vector3 position, ElementType element, UnityEngine.Object owner, System.Action<TimedDamageZone> completeCallback = null)
    {
        transform.position = position;
        elementType = element;
        damageOwner = owner;
        onComplete = completeCallback;

        baseColor = GameDefs.ElementToColor(elementType);

        // Start with collider disabled and renderer transparent
        zoneCollider.enabled = false;
        SetRendererAlpha(0f);

        gameObject.SetActive(true);
        isActive = false;

        StartCoroutine(ZoneSequence());
    }

    private IEnumerator ZoneSequence()
    {
        // Phase 1: Fade In (with blinking)
        yield return FadeInWithBlink();

        // Phase 2: Activate and allow trigger hits via OnTriggerEnter2D
        zoneCollider.enabled = true;
        Debug.Log($"[TimedDamageZone] Enabling collider on {gameObject.name} for element {elementType}");
        isActive = true;

        yield return new WaitForSeconds(activeDuration);

        // Phase 3: Deactivate and fade out
        zoneCollider.enabled = false;
        Debug.Log($"[TimedDamageZone] Disabling collider on {gameObject.name} for element {elementType}");
        isActive = false;

        yield return FadeOut();

        // Complete
        onComplete?.Invoke(this);
        gameObject.SetActive(false);
    }

    private IEnumerator FadeInWithBlink()
    {
        float elapsed = 0f;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float fadeT = Mathf.Clamp01(elapsed / Mathf.Max(1e-6f, fadeInDuration));

            // Blink value over blink period (0..1). If blinkPeriod <= 0, treat blinkValue as 1.
            float blinkValue = 1f;
            if (blinkPeriod > 1e-6f)
            {
                float phase = (elapsed % blinkPeriod) / blinkPeriod;
                blinkValue = blinkCurve.Evaluate(phase);
            }

            // Final alpha is envelope (fadeT) multiplied by blinkValue
            float alpha = fadeT * Mathf.Clamp01(blinkValue);
            SetRendererAlpha(alpha);
            yield return null;
        }

        SetRendererAlpha(1f);
    }

    private IEnumerator FadeOut()
    {
        float elapsed = 0f;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeOutDuration);
            float curveValue = fadeOutCurve.Evaluate(t);
            SetRendererAlpha(curveValue);
            yield return null;
        }

        SetRendererAlpha(0f);
    }

    private void SetRendererAlpha(float alpha)
    {
        if (zoneRenderer == null) return;

        Color color = baseColor;
        color.a = alpha;

        if (zoneRenderer is SpriteRenderer spriteRenderer)
        {
            spriteRenderer.color = color;
        }
        else
        {
            // For other renderers, use MaterialPropertyBlock
            zoneRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_Color", color);
            zoneRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActive) return;
        if (other == null) return;

        // Filter by target mask
        if ((targetMask.value & (1 << other.gameObject.layer)) == 0)
            return;

        // Attempt to get damageable component
        IElementDamageable damageable = other.GetComponent<IElementDamageable>() ?? other.GetComponentInParent<IElementDamageable>();
        if (damageable == null) return;

        if (!damageable.CanBeHitBy(elementType)) return;

        Debug.Log($"[TimedDamageZone] Hit {other.name} with {GameDefs.ElementToText(elementType)} for {damage} damage");
        damageable.TakeElementHit(elementType, damage, damageOwner);
    }


#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (zoneCollider == null) return;

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);

        if (zoneCollider is CircleCollider2D circleCollider)
        {
            Gizmos.DrawWireSphere(transform.position, circleCollider.radius);
        }
        else if (zoneCollider is BoxCollider2D boxCollider)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(boxCollider.offset, boxCollider.size);
        }
    }
#endif
}
