using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    [Tooltip("Transform to follow")]
    public Transform target;

    [Tooltip("Smooth time for the damp (smaller = snappier)")]
    public float smoothTime = 0.08f;

    [Tooltip("Optional offset from the target position")]
    public Vector3 offset = Vector3.zero;

    [Tooltip("Keep camera's local Z position unchanged")]
    public bool maintainZ = true;

    Vector3 velocity;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.position + offset;
        if (maintainZ) desired.z = transform.position.z;

        transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, Mathf.Max(0.0001f, smoothTime));
    }
}
