// File: Player/PlayerTargeting.cs
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerTargeting : MonoBehaviour
{
    [Header("Targeting (Mouse Nearest Enemy)")]
    public float maxLockDistance = 999f;

    Camera cam;
    Monster current;

    public Monster CurrentTarget => (current != null && current.gameObject.activeInHierarchy) ? current : null;

    void Awake()
    {
        cam = Camera.main;
    }

    void Update()
    {
        Monster next = FindClosestMonsterToMouse();
        if (next == current) return;

        SetTarget(current, false);
        current = next;
        SetTarget(current, true);
    }

    void SetTarget(Monster m, bool on)
    {
        if (m == null) return;

        MonsterTargetUI ui = m.GetComponentInChildren<MonsterTargetUI>(true);
        if (ui == null) ui = m.gameObject.AddComponent<MonsterTargetUI>();
        ui.SetVisible(on);
    }

    Monster FindClosestMonsterToMouse()
    {
        Vector3 mouseWorld = GetMouseWorldPosition();
        Monster[] all = FindObjectsByType<Monster>(FindObjectsSortMode.None);

        Monster best = null;
        float bestD = float.MaxValue;
        float maxSqr = maxLockDistance * maxLockDistance;

        for (int i = 0; i < all.Length; i++)
        {
            Monster m = all[i];
            if (m == null) continue;
            if (!m.gameObject.activeInHierarchy) continue;

            float d = (m.transform.position - mouseWorld).sqrMagnitude;
            if (d > maxSqr) continue;

            if (d < bestD)
            {
                bestD = d;
                best = m;
            }
        }

        return best;
    }

    Vector3 GetMouseWorldPosition()
    {
        if (cam == null) cam = Camera.main;
        if (Mouse.current == null) return transform.position;

        Vector2 sp2 = Mouse.current.position.ReadValue();
        Vector3 sp = new Vector3(sp2.x, sp2.y, 0f);
        sp.z = Mathf.Abs(cam.transform.position.z - transform.position.z);
        return cam.ScreenToWorldPoint(sp);
    }
}
