using UnityEngine;

[DefaultExecutionOrder(1000)]
public class SunLight : MonoBehaviour
{
    [SerializeField] Transform _ship;

    void Awake()
    {
        if (_ship == null)
        {
            ShipController shipController = FindAnyObjectByType<ShipController>();
            if (shipController != null) _ship = shipController.transform;
        }
    }

    void LateUpdate()
    {
        SolarSystemManager m = SolarSystemManager.Instance;
        if (m == null || _ship == null) return;

        Vector3 dir = _ship.position - m.SunScenePosition;
        float directionLengthSquared = dir.sqrMagnitude;
        if (float.IsNaN(directionLengthSquared) || float.IsInfinity(directionLengthSquared) || directionLengthSquared < 1f) return;
        transform.rotation = Quaternion.LookRotation(dir.normalized);
    }
}

