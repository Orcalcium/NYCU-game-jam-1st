using UnityEngine;

/// <summary>
/// Adds a simple trail (after-image) effect to a 2D character to visually show movement/drag direction.
/// The script will automatically add/configure a TrailRenderer and enable emission only while the
/// source Rigidbody2D is moving above a minimum threshold.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class MovementDragEffect : MonoBehaviour
{
    [Tooltip("Source rigidbody used to detect movement speed")]
    public Rigidbody2D sourceRigidbody;

    [Tooltip("Sprite renderer to sample color/sorting order from (optional)")]
    public SpriteRenderer spriteSource;

    [Header("Trail Settings")]
    public bool enableTrail = true;
    public float trailTime = 0.25f;
    public float startWidth = 1.0f;
    public float endWidth = 0.8f;
    public Color trailColor = new Color(1f, 1f, 1f, 0.6f);
    public float minSpeedForTrail = 0.2f;

    TrailRenderer trail;

    void Awake()
    {
        if (!enableTrail) return;

        trail = GetComponent<TrailRenderer>();
        if (trail == null) trail = gameObject.AddComponent<TrailRenderer>();

        ConfigureTrail();
    }

    void ConfigureTrail()
    {
        if (trail == null) return;

        trail.time = Mathf.Max(0f, trailTime);
        trail.startWidth = startWidth;
        trail.endWidth = endWidth;

        // Use the Sprite default shader so it looks like a sprite trail
        var spriteShader = Shader.Find("Sprites/Default");
        trail.material = new Material(spriteShader) { hideFlags = HideFlags.DontSave };

        // Set color
        trail.startColor = trailColor;
        trail.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);

        // Keep the trail behind the sprite by lowering its sorting order if we have a spriteSource
        if (spriteSource != null)
        {
            trail.sortingLayerID = spriteSource.sortingLayerID;
            trail.sortingOrder = spriteSource.sortingOrder - 1;
        }

        // Prefer to align with the camera view for 2D visuals
#if UNITY_2019_1_OR_NEWER
        trail.alignment = LineAlignment.View;
#endif

        trail.emitting = false;
        trail.autodestruct = false;
    }

    void Update()
    {
        if (!enableTrail || trail == null || sourceRigidbody == null) return;

        bool shouldEmit = sourceRigidbody.linearVelocity.sqrMagnitude > (minSpeedForTrail * minSpeedForTrail);
        if (trail.emitting != shouldEmit) trail.emitting = shouldEmit;

        // If the sprite color changes (element change), update trail color to match
        if (spriteSource != null)
        {
            Color col = spriteSource.color * new Color(1f, 1f, 1f, 1f);
            col.a = trailColor.a; // maintain configured alpha
            trail.startColor = col;
            trail.endColor = new Color(col.r, col.g, col.b, 0f);
        }
    }
}
