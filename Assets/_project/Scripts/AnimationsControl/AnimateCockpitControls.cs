using UnityEngine;
using System.Collections.Generic;

public class NewMonoBehaviourScript : MonoBehaviour
{
    [SerializeField] Transform _joystick;

    [SerializeField] Vector3 _joystickRange = Vector3.zero;

    [SerializeField] List<Transform> _throttles;

    [SerializeField] float _throttleRange = 35;

    [SerializeField] ShipMovementInput _movementInput;
    [SerializeField, Min(0.01f)] float _joystickSmoothTime = 0.12f;

    Vector3 _joystickEuler;
    Vector3 _joystickEulerVelocity;

    IMovementControls ControlInput => _movementInput != null ? _movementInput.MovementControls : null;


    // Update is called once per frame
    void Update()
    {
        IMovementControls controls = ControlInput;
        if (controls == null) return;

        // The cockpit stick is only a visual prop; mouse-and-keyboard flight
        // does not require a joystick object to be assigned here.
        if (_joystick != null)
        {
            Vector3 targetEuler = new Vector3(
                Mathf.Clamp(controls.PitchAmount, -1f, 1f) * _joystickRange.x,
                Mathf.Clamp(controls.YawAmount, -1f, 1f) * _joystickRange.y,
                Mathf.Clamp(controls.RollAmount, -1f, 1f) * _joystickRange.z);
            _joystickEuler = Vector3.SmoothDamp(
                _joystickEuler,
                targetEuler,
                ref _joystickEulerVelocity,
                _joystickSmoothTime,
                Mathf.Infinity,
                Time.deltaTime);
            _joystick.localRotation = Quaternion.Euler(_joystickEuler);
        }

        if (_throttles == null || _throttles.Count == 0) return;

        float throttleAngle = controls.ThrustAmount * _throttleRange;
        foreach (Transform throttle in _throttles)
        {
            if (throttle == null) continue;

            Vector3 throttleRotation = throttle.localEulerAngles;
            throttleRotation.x = throttleAngle;
            throttle.localRotation = Quaternion.Euler(throttleRotation);
        }
    }
}

