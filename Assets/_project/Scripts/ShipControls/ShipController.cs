using UnityEngine;

// Se ejecuta ANTES que SolarSystemManager (+20) y que Cinemachine, que trabaja en LateUpdate.
[DefaultExecutionOrder(-20)]
public class ShipController : MonoBehaviour
{
    [SerializeField] ShipMovementInput _movementInput;
    [Header("Ship movement values")]
    [SerializeField, Min(0f)] float _thrustForce = 10000f;
    [Header("Turning")]
    [SerializeField, Min(0f)] float _mouseDegreesPerInput = 4f;
    [SerializeField, Min(0f)] float _rollTurnRate = 1f;

    [Header("Restricciones de vuelo")]
    [Tooltip("Desactivado: la nave solo gira a izquierda/derecha y vuela nivelada con el plano del sistema solar")]
    [SerializeField] bool _allowPitchAndRoll = false;
    [SerializeField, Min(0f)] float _levelingDegreesPerSecond = 60f;
    [Tooltip("Inclina el morro hacia abajo (grados) SOLO visualmente, para ver mejor los planetas que quedan delante. La trayectoria sigue siendo horizontal. Negativo = morro arriba")]
    [SerializeField, Range(-20f, 30f)] float _cockpitPitchDegrees = 8f;

    [Header("Forward speed")]
    [SerializeField, Min(0f)] float _brakeForce = 10000f;
    [SerializeField, Min(0f)] float _maxForwardSpeed = 100000f;
    [SerializeField, Min(0f)] float _maxReverseSpeed = 100000f;

    Rigidbody _rigidbody;
    EngineAccelerationVfx _engineVfx;
    float _currentSpeed;
    Vector3 _velocity;
    bool _flightControlsEnabled = true;
    float _thrustAmount, _pitchAmount, _rollAmount, _yawAmount;

    /// <summary>Velocidad actual en unidades/segundo (la usa SolarSystemManager).</summary>
    public Vector3 Velocity => _velocity;

    IMovementControls ControlInput => _movementInput.MovementControls;

    void Awake()
    {
        // La nave NO se mueve con la física: se mueve directamente con el Transform en Update.
        // Así la cabina, la cámara y el mundo van siempre sincronizados en el mismo frame
        // (con Rigidbody interpolado y pasos de física de 5 ms la cámara temblaba respecto a la nave).
        // Los planetas son triggers y la detección de llegada es por distancia, no por colisión.
        _rigidbody = GetComponent<Rigidbody>();
        _engineVfx = GetComponent<EngineAccelerationVfx>();
        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = true;
            _rigidbody.interpolation = RigidbodyInterpolation.None;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }
        _currentSpeed = 0f;
    }

    void Update()
    {
        if (!_flightControlsEnabled)
        {
            _thrustAmount = _pitchAmount = _rollAmount = _yawAmount = 0f;
            _velocity = Vector3.zero;
            _engineVfx?.SetThrottle(0f);
            return;
        }

        if (ControlInput != null)
        {
            _thrustAmount = ControlInput.ThrustAmount;
            _rollAmount = ControlInput.RollAmount;
            _pitchAmount = ControlInput.PitchAmount;
            _yawAmount = ControlInput.YawAmount;
        }

        _engineVfx?.SetThrottle(_thrustAmount);

        float dt = Time.deltaTime;
        UpdateForwardSpeed(dt);
        Vector3 moveDirection = UpdateRotation(dt);
        _velocity = moveDirection * _currentSpeed;
        transform.position += _velocity * dt;
    }

    public void SetFlightControlsEnabled(bool enabled)
    {
        _flightControlsEnabled = enabled;
        if (enabled) return;

        _thrustAmount = _pitchAmount = _rollAmount = _yawAmount = 0f;
        _currentSpeed = 0f;
        _velocity = Vector3.zero;
    }

    /// <summary>Velocidad con signo en % del máximo (-100..100). La muestra CockpitHud.</summary>
    public float SpeedPercent
    {
        get
        {
            float limit = _currentSpeed < 0f ? _maxReverseSpeed : _maxForwardSpeed;
            return limit > 0f ? Mathf.Clamp(_currentSpeed / limit * 100f, -100f, 100f) : 0f;
        }
    }

    /// <summary>Velocidad actual con signo en unidades/segundo.</summary>
    public float CurrentSpeed => _currentSpeed;

    void UpdateForwardSpeed(float dt)
    {
        // W accelerates forward. S brakes the current motion without crossing
        // zero; reversing requires a separate reverse control.
        if (_thrustAmount > 0f)
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, _maxForwardSpeed, _thrustAmount * _thrustForce * dt);
        else if (_thrustAmount < 0f)
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, -_thrustAmount * _brakeForce * dt);
        _currentSpeed = Mathf.Clamp(_currentSpeed, -_maxReverseSpeed, _maxForwardSpeed);
    }

    // Aplica el giro de este frame y devuelve la dirección de avance.
    Vector3 UpdateRotation(float dt)
    {
        Quaternion current = transform.rotation;

        if (_allowPitchAndRoll)
        {
            float pitchDegrees = _pitchAmount * _mouseDegreesPerInput;
            float yawDegrees = _yawAmount * _mouseDegreesPerInput;
            float rollDegrees = -_rollAmount * _rollTurnRate * Mathf.Rad2Deg * dt;
            transform.rotation = current * Quaternion.Euler(pitchDegrees, yawDegrees, rollDegrees);
            return transform.forward;
        }

        // Solo guiñada (izquierda/derecha) alrededor del eje vertical del mundo.
        Quaternion yawed = Quaternion.AngleAxis(_yawAmount * _mouseDegreesPerInput, Vector3.up) * current;
        Vector3 flatForward = yawed * Vector3.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 1e-4f) flatForward = Vector3.forward;
        Quaternion heading = Quaternion.LookRotation(flatForward.normalized, Vector3.up);

        // La nave se nivela sola (p. ej. tras el despegue) hasta la inclinación visual configurada.
        Quaternion target = heading * Quaternion.Euler(_cockpitPitchDegrees, 0f, 0f);
        transform.rotation = Quaternion.RotateTowards(yawed, target, _levelingDegreesPerSecond * dt);

        // El movimiento sigue siendo horizontal aunque el morro esté algo inclinado.
        return heading * Vector3.forward;
    }
}
