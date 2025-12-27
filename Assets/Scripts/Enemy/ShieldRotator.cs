using UnityEngine;

/// <summary>
/// Rotates a shield around the parent object and updates its visual based on the parent's color changes.
/// Requires an EnemyShooter2D component on the parent to subscribe to color change events.
/// </summary>
public class ShieldRotator : MonoBehaviour
{
    [Header("Rotation")]
    [Tooltip("Rotation speed in degrees per second")]
    public float rotationSpeed = 90f;

    [Header("Shield Child")]
    [Tooltip("Child GameObject containing the shield sprite and collider")]
    public Transform shieldChild;

    [Tooltip("Auto-find shield child if not assigned")]
    public bool autoFindChild = true;

    private SpriteRenderer shieldRenderer;
    private Collider2D shieldCollider;
    private EnemyShooter2D enemyShooter;

    void Awake()
    {
        // Get the EnemyShooter2D from parent
        enemyShooter = GetComponentInParent<EnemyShooter2D>();
        
        if (enemyShooter == null)
        {
            Debug.LogWarning($"[ShieldRotator] No EnemyShooter2D found on parent of {gameObject.name}");
        }

        // Find or validate shield child
        SetupShieldChild();

        // Subscribe to color change event
        if (enemyShooter != null)
        {
            enemyShooter.ChangeColor += OnColorChange;
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from color change event
        if (enemyShooter != null)
        {
            enemyShooter.ChangeColor -= OnColorChange;
        }
    }

    void SetupShieldChild()
    {
        // Auto-find first child if not assigned
        if (shieldChild == null && autoFindChild && transform.childCount > 0)
        {
            shieldChild = transform.GetChild(0);
        }

        if (shieldChild == null)
        {
            Debug.LogWarning($"[ShieldRotator] No shield child assigned on {gameObject.name}");
            return;
        }

        // Get components from shield child
        shieldRenderer = shieldChild.GetComponent<SpriteRenderer>();
        if (shieldRenderer == null)
        {
            Debug.LogWarning($"[ShieldRotator] Shield child {shieldChild.name} has no SpriteRenderer");
        }

        shieldCollider = shieldChild.GetComponent<Collider2D>();
        if (shieldCollider == null)
        {
            Debug.LogWarning($"[ShieldRotator] Shield child {shieldChild.name} has no Collider2D");
        }
    }

    void Update()
    {
        // Rotate this object's transform (not the child). This makes the rotator spin while the child's local
        // transform remains relative to the rotator.
        transform.Rotate(Vector3.forward, rotationSpeed * Time.deltaTime, Space.Self);
    }

    /// <summary>
    /// Called when the parent EnemyShooter2D changes color.
    /// Updates the shield's sprite renderer color.
    /// </summary>
    private void OnColorChange(Color newColor)
    {
        if (shieldRenderer != null)
        {
            shieldRenderer.color = newColor;
        }
    }

    /// <summary>
    /// Manually set the shield color (useful for initialization or testing).
    /// </summary>
    public void SetShieldColor(Color color)
    {
        if (shieldRenderer != null)
        {
            shieldRenderer.color = color;
        }
    }

    /// <summary>
    /// Enable or disable the shield child.
    /// </summary>
    public void SetShieldActive(bool active)
    {
        if (shieldChild != null)
        {
            shieldChild.gameObject.SetActive(active);
        }
    }

    /// <summary>
    /// Get the shield's collider (useful for external collision checks).
    /// </summary>
    public Collider2D GetShieldCollider()
    {
        return shieldCollider;
    }

    /// <summary>
    /// Get the shield's renderer (useful for external rendering changes).
    /// </summary>
    public SpriteRenderer GetShieldRenderer()
    {
        return shieldRenderer;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Refresh shield child setup when values change in inspector
        if (Application.isPlaying)
        {
            SetupShieldChild();
        }
    }

    void OnDrawGizmosSelected()
    {
        // Draw a circle to visualize rotation radius
        if (shieldChild != null)
        {
            Gizmos.color = Color.cyan;
            Vector3 childPos = shieldChild.position;
            float radius = Vector3.Distance(transform.position, childPos);
            UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.forward, radius);
        }
    }
#endif
}
