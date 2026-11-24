using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using UnityEngine.Rendering;
using Unity.Cinemachine.TargetTracking;

public class CameraManager : MonoBehaviour
{
    enum VirtualCameras
    {
        CockpitCamera = 0,
        FollowCamera = 1,
        NoCamera = -1
    }

    [SerializeField] List<GameObject> _virtualCameras;
    bool _userCameraSwitchingEnabled = true;
    int _activeCameraIndex;
    CinemachineCamera _followVirtualCamera;
    CinemachineFollow _followComponent;
    CinemachineRotationComposer _rotationComposer;
    CameraTarget _savedFollowTarget;
    Vector3 _savedFollowOffset;
    TrackerSettings _savedTrackerSettings;
    Quaternion _savedFollowRotation;
    bool _savedComposerEnabled;
    Transform _takeoffCameraAnchor;
    bool _takeoffShotActive;
    Vector3 _takeoffShotStartPosition;
    Quaternion _takeoffShotStartRotation;
    public bool HasConfiguredCameras => _virtualCameras != null && _virtualCameras.Count >= 2;
    public bool IsFollowCameraActive => _activeCameraIndex == (int)VirtualCameras.FollowCamera;
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    VirtualCameras CameraKeyPressed
    {
        get
        {
            if (Keyboard.current == null) return VirtualCameras.NoCamera;
            for (int i = 0; i < _virtualCameras.Count; i++)
            {
                var key = Keyboard.current[(Key)(int)Key.Digit1 + i];
                if (key.wasPressedThisFrame)
                {
                    return (VirtualCameras)i;
                }
            }

            return VirtualCameras.NoCamera;
        }
    }

    void Start()
    {
        ConfigureBrain();
        SetActiveCamera(VirtualCameras.CockpitCamera);
    }

    // La nave se mueve en Update; el Brain debe actualizar la cámara en LateUpdate (no en FixedUpdate
    // ni en "Smart Update"), o la cámara queda desfasada respecto a la nave y la cabina vibra.
    // Se asigna por reflexión (1 = LateUpdate en ambos enums) para no depender del nombre exacto de los tipos.
    CinemachineCamera _cockpitVcam;
    Camera _brainCamera;

    void OnEnable() { RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering; }

    // Justo antes de dibujar, la cámara principal se iguala EXACTAMENTE a la cámara virtual de la cabina
    // (que va pegada a la nave). Así, pase lo que pase con el orden de ejecución del Brain, la cabina
    // no puede moverse respecto a la cámara dentro de un mismo frame.
    void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (_cockpitVcam == null || _brainCamera == null || cam != _brainCamera) return;
        if (!_cockpitVcam.gameObject.activeInHierarchy) return;
        Transform t = _cockpitVcam.transform;
        cam.transform.SetPositionAndRotation(t.position, t.rotation);
    }

    void ConfigureBrain()
    {
        CinemachineBrain brain = FindAnyObjectByType<CinemachineBrain>();
        if (brain == null) return;
        _brainCamera = brain.GetComponent<Camera>();
        if (_virtualCameras != null && _virtualCameras.Count > 0 && _virtualCameras[0] != null)
            _cockpitVcam = _virtualCameras[0].GetComponentInChildren<CinemachineCamera>(true);
        SetEnumMember(brain, "UpdateMethod", 1);
        SetEnumMember(brain, "BlendUpdateMethod", 1);
    }

    static void SetEnumMember(object target, string name, int value)
    {
        System.Type type = target.GetType();
        System.Reflection.FieldInfo field = type.GetField(name);
        if (field != null && field.FieldType.IsEnum)
        {
            field.SetValue(target, System.Enum.ToObject(field.FieldType, value));
            return;
        }
        System.Reflection.PropertyInfo prop = type.GetProperty(name);
        if (prop != null && prop.CanWrite && prop.PropertyType.IsEnum)
            prop.SetValue(target, System.Enum.ToObject(prop.PropertyType, value));
    }

    void Update()
    {
        if (_userCameraSwitchingEnabled)
            SetActiveCamera(CameraKeyPressed);
    }

    public void SetUserCameraSwitchingEnabled(bool enabled)
    {
        _userCameraSwitchingEnabled = enabled;
    }

    public void ShowCockpitCamera()
    {
        SetActiveCamera(VirtualCameras.CockpitCamera);
    }

    public void ShowFollowCamera()
    {
        SetActiveCamera(VirtualCameras.FollowCamera);
    }

    public void StartTakeoffCameraShot(Vector3 fixedCameraPosition, Quaternion fixedCameraRotation)
    {
        StartFixedExternalShot(fixedCameraPosition, fixedCameraRotation);
        _takeoffShotStartPosition = fixedCameraPosition;
        _takeoffShotStartRotation = fixedCameraRotation;
    }

    /// <summary>Tracks the ship, pans toward it, then moves the external camera into the cockpit.</summary>
    public void UpdateTakeoffCameraShot(Vector3 shipPosition, float progress)
    {
        if (!_takeoffShotActive || _takeoffCameraAnchor == null || _cockpitVcam == null) return;

        float pan = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.5f));
        Vector3 lookDirection = shipPosition - _takeoffShotStartPosition;
        Quaternion panRotation = lookDirection.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(lookDirection.normalized, Vector3.up)
            : _takeoffShotStartRotation;
        Quaternion rotation = Quaternion.Slerp(_takeoffShotStartRotation, panRotation, pan);

        float approach = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((progress - 0.4f) / 0.6f));
        Vector3 cockpitPosition = _cockpitVcam.transform.position;
        Quaternion cockpitRotation = _cockpitVcam.transform.rotation;
        _takeoffCameraAnchor.position = Vector3.Lerp(_takeoffShotStartPosition, cockpitPosition, approach);
        _followVirtualCamera.transform.rotation = Quaternion.Slerp(rotation, cockpitRotation, approach);
    }

    public void ShiftTakeoffCameraOrigin(Vector3 offset)
    {
        if (_takeoffShotActive) _takeoffShotStartPosition += offset;
    }

    public void StartLandingCameraShot(Vector3 fixedCameraPosition, Quaternion fixedCameraRotation)
    {
        StartFixedExternalShot(fixedCameraPosition, fixedCameraRotation);
    }

    void StartFixedExternalShot(Vector3 fixedCameraPosition, Quaternion fixedCameraRotation)
    {
        if (!CacheFollowCamera())
        {
            ShowFollowCamera();
            return;
        }

        if (_takeoffShotActive && _takeoffCameraAnchor != null)
        {
            _takeoffCameraAnchor.position = fixedCameraPosition;
            _followVirtualCamera.transform.rotation = fixedCameraRotation;
            ShowFollowCamera();
            return;
        }

        if (!_takeoffShotActive)
        {
            _savedFollowTarget = _followVirtualCamera.Target;
            _savedFollowOffset = _followComponent.FollowOffset;
            _savedTrackerSettings = _followComponent.TrackerSettings;
            _savedFollowRotation = _followVirtualCamera.transform.rotation;
            _savedComposerEnabled = _rotationComposer != null && _rotationComposer.enabled;
        }

        GameObject anchorObject = new GameObject("Takeoff Camera Anchor");
        Transform followRoot = _followVirtualCamera.transform.parent;
        if (followRoot != null) anchorObject.transform.SetParent(followRoot, true);
        anchorObject.transform.position = fixedCameraPosition;
        _takeoffCameraAnchor = anchorObject.transform;

        _followComponent.FollowOffset = Vector3.zero;
        TrackerSettings tracker = _followComponent.TrackerSettings;
        tracker.PositionDamping = Vector3.zero;
        _followComponent.TrackerSettings = tracker;
        if (_rotationComposer != null) _rotationComposer.enabled = false;
        _followVirtualCamera.transform.rotation = fixedCameraRotation;
        _followVirtualCamera.Target = new CameraTarget
        {
            TrackingTarget = _takeoffCameraAnchor,
            CustomLookAtTarget = true
        };
        _takeoffShotActive = true;
        ShowFollowCamera();
    }

    public void EndFixedExternalShot()
    {
        if (!_takeoffShotActive) return;

        if (CacheFollowCamera())
        {
            _followVirtualCamera.Target = _savedFollowTarget;
            _followComponent.FollowOffset = _savedFollowOffset;
            _followComponent.TrackerSettings = _savedTrackerSettings;
            _followVirtualCamera.transform.rotation = _savedFollowRotation;
            if (_rotationComposer != null) _rotationComposer.enabled = _savedComposerEnabled;
        }
        if (_takeoffCameraAnchor != null)
        {
            if (Application.isPlaying) Destroy(_takeoffCameraAnchor.gameObject);
            else DestroyImmediate(_takeoffCameraAnchor.gameObject);
        }
        _takeoffCameraAnchor = null;
        _takeoffShotActive = false;
    }

    bool CacheFollowCamera()
    {
        if (_followVirtualCamera != null && _followComponent != null) return true;
        if (_virtualCameras == null) return false;

        if (_virtualCameras.Count < 2 || _virtualCameras[1] == null) return false;
        _followVirtualCamera = _virtualCameras[1].GetComponentInChildren<CinemachineCamera>(true);
        if (_followVirtualCamera == null) return false;
        _followComponent = _followVirtualCamera.GetComponent<CinemachineFollow>();
        _rotationComposer = _followVirtualCamera.GetComponent<CinemachineRotationComposer>();
        return _followComponent != null;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        EndFixedExternalShot();
    }

    // Update is called once per frame
    void SetActiveCamera(VirtualCameras activeCamera)
    {
        if (activeCamera == VirtualCameras.NoCamera)
        {
            return;
        }

        _activeCameraIndex = (int)activeCamera;
        if (_virtualCameras == null) return;
        for (int i = 0; i < _virtualCameras.Count; i++)
        {
            GameObject camera = _virtualCameras[i];
            if (camera == null) continue;
            camera.SetActive(i == (int)activeCamera);
        }
    }
}
