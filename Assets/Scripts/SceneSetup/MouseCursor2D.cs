using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class MouseCursor2D : MonoBehaviour
{
    [Tooltip("Camera used to convert screen -> world. Defaults to Main Camera if not set.")]
    public Camera targetCamera;

    [Tooltip("Prefab to use as the cursor. Drag your cursor prefab here.")]
    public GameObject cursorPrefab;

    [Tooltip("If true, the OS cursor will be hidden while this component is active.")]
    public bool hideSystemCursor = true;

    [Tooltip("Offset applied to the instantiated cursor prefab (in world units)")]
    public Vector3 prefabOffset = Vector3.zero;

    GameObject instance;
    bool weHiddenSystemCursor;

    void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;

        if (hideSystemCursor)
        {
            // only hide if currently visible
            weHiddenSystemCursor = Cursor.visible;
            Cursor.visible = false;
        }

        if (cursorPrefab != null)
        {
            instance = Instantiate(cursorPrefab);
            // Keep instantiated object at root so it can have its own sorting settings
            instance.name = cursorPrefab.name + " (MouseCursor)";
        }
    }

    void OnDisable()
    {
        if (weHiddenSystemCursor)
            Cursor.visible = true;

        if (instance != null)
        {
            Destroy(instance);
            instance = null;
        }
    }

    void OnDestroy()
    {
        if (weHiddenSystemCursor)
            Cursor.visible = true;
    }

    void Update()
    {
        if (Mouse.current == null || targetCamera == null) return;

        Vector2 sp = Mouse.current.position.ReadValue();
        float z = -targetCamera.transform.position.z; // convert to world plane z=0
        Vector3 w = targetCamera.ScreenToWorldPoint(new Vector3(sp.x, sp.y, z));

        Vector3 pos = new Vector3(w.x, w.y, 0f) + prefabOffset;

        if (instance != null)
        {
            instance.transform.position = pos;
        }
        else
        {
            // If no prefab assigned, place this GameObject at cursor (useful for attaching a SpriteRenderer directly)
            transform.position = pos;
        }
    }

    /// <summary>
    /// Replace current cursor prefab at runtime.
    /// </summary>
    public void SetCursorPrefab(GameObject prefab)
    {
        if (instance != null) Destroy(instance);
        cursorPrefab = prefab;
        if (cursorPrefab != null)
        {
            instance = Instantiate(cursorPrefab);
            instance.name = cursorPrefab.name + " (MouseCursor)";
        }
    }
}
