using UnityEngine;
using UnityEngine.Rendering;
using Unity.Cinemachine;

/// <summary>
/// Diagnóstico de vibración. Añádelo a "Main Camera". Cada 2 s escribe en la consola:
///  A) variación máx. de la cámara respecto a la nave al final de LateUpdate (debería ser ~0 en cabina);
///  B) lo mismo justo antes de dibujar (lo que realmente se ve);
///  C) distancia entre la cámara y la cámara virtual activa;
///  D) tiempo de frame mínimo/máximo (picos = tirones, no vibración de cámara).
/// </summary>
[DefaultExecutionOrder(32000)]
public class ShakeProbe : MonoBehaviour
{
    [SerializeField] Transform _ship;
    [SerializeField, Min(0.5f)] float _logEverySeconds = 2f;

    Camera _camera;
    Vector3 _lastLocalLate, _lastLocalRender;
    bool _hasLate, _hasRender;
    float _maxLate, _maxRender, _maxVcamGap, _minDt = float.MaxValue, _maxDt, _timer;

    void OnEnable()
    {
        _camera = GetComponent<Camera>();
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
    }

    void OnDisable() { RenderPipelineManager.beginCameraRendering -= OnBeginCamera; }

    bool FindShip()
    {
        if (_ship != null) return true;
        ShipController s = FindAnyObjectByType<ShipController>();
        if (s == null) return false;
        _ship = s.transform;
        return true;
    }

    // Se ejecuta después de CameraManager (misma cola de beginCameraRendering).
    void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam != _camera || !FindShip()) return;
        Vector3 local = _ship.InverseTransformPoint(cam.transform.position);
        if (_hasRender) _maxRender = Mathf.Max(_maxRender, (local - _lastLocalRender).magnitude);
        _lastLocalRender = local;
        _hasRender = true;
    }

    void LateUpdate()
    {
        if (!FindShip()) return;

        Vector3 local = _ship.InverseTransformPoint(transform.position);
        if (_hasLate) _maxLate = Mathf.Max(_maxLate, (local - _lastLocalLate).magnitude);
        _lastLocalLate = local;
        _hasLate = true;

        CinemachineBrain brain = GetComponent<CinemachineBrain>();
        string brainInfo = "sin Brain";
        if (brain != null)
        {
            Component active = brain.ActiveVirtualCamera as Component;
            if (active != null) _maxVcamGap = Mathf.Max(_maxVcamGap, (transform.position - active.transform.position).magnitude);
            brainInfo = $"Brain {brain.UpdateMethod}/{brain.BlendUpdateMethod}, activa: " + (active != null ? active.name : "ninguna");
        }

        float dt = Time.unscaledDeltaTime;
        _minDt = Mathf.Min(_minDt, dt);
        _maxDt = Mathf.Max(_maxDt, dt);
        _timer += dt;
        if (_timer < _logEverySeconds) return;

        Debug.Log($"[ShakeProbe] A) LateUpdate {_maxLate:F5} u | B) al dibujar {_maxRender:F5} u | C) cámara-vcam {_maxVcamGap:F5} u | " +
                  $"D) frame {_minDt * 1000f:F1}-{_maxDt * 1000f:F1} ms | {brainInfo} | |pos nave| {_ship.position.magnitude:F0} u | " +
                  $"vsync {QualitySettings.vSyncCount} | velocidad {FindAnyObjectByType<ShipController>()?.CurrentSpeed:F0} u/s");
        _timer = 0f; _maxLate = 0f; _maxRender = 0f; _maxVcamGap = 0f; _minDt = float.MaxValue; _maxDt = 0f;
    }
}
