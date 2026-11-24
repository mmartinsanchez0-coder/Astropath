using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.UI;

/// <summary>
/// Calcula la posición real de planetas y lunas, muestra cada prefab solo cuando la nave
/// se acerca, y desplaza el "origen flotante" para que la nave nunca se aleje del (0,0,0)
/// (así Unity no pierde precisión con distancias enormes).
/// </summary>
// Después de ShipController (-20) y antes de LateUpdate/Cinemachine: la nave ya se ha movido este frame.
[DefaultExecutionOrder(20)]
public class SolarSystemManager : MonoBehaviour
{
    public static SolarSystemManager Instance { get; private set; }

    [Header("Nave")]
    [SerializeField] Transform _ship;
    [SerializeField] Rigidbody _shipBody;
    [Tooltip("Objetos que deben moverse junto con la nave al recentrar (p. ej. Camera Setup)")]
    [SerializeField] Transform[] _shiftWithShip;
    [Tooltip("Altura inicial sobre la superficie de la Tierra, en unidades")]
    [SerializeField] float _spawnAltitude = 60f;
    [Tooltip("Latitud del punto de salida; por defecto, Barcelona")]
    [SerializeField, Range(-90f, 90f)] float _spawnLatitudeDegrees = 41.3874f;
    [Tooltip("Longitud del punto de salida; por defecto, Barcelona")]
    [SerializeField, Range(-180f, 180f)] float _spawnLongitudeDegrees = 2.1686f;

    [Header("Prefabs (el nombre del prefab = nombre del cuerpo: Sun, Mercury, ..., Triton)")]
    [SerializeField] List<GameObject> _prefabs = new List<GameObject>();

    [Header("Tiempo")]
    [Tooltip("Vacío = fecha de hoy. Formato: 2026-10-29 12:00")]
    [SerializeField] string _startDateUtc = "";
    [Tooltip("0 = planetas quietos en su posición real de hoy. 1 = tiempo real")]
    [SerializeField] float _planetTimeScale = 0f;
    [Tooltip("Segundos simulados por segundo real para las lunas (60 = 1 s es 1 minuto)")]
    [SerializeField] float _moonTimeScale = 60f;

    [Header("Visibilidad")]
    [Tooltip("Un cuerpo aparece cuando la nave está a menos de (diámetro x este valor)")]
    [SerializeField] float _showFactor = 400f;
    [SerializeField] float _maxVisibleDistance = 2800000f;
    [Tooltip("Aumento visual de las lunas para distinguirlas mejor desde la nave")]
    [SerializeField, Min(1f)] float _moonDisplayScale = 4f;
    [Tooltip("Alcance del radar en unidades astronómicas (10 UA ≈ un tercio del sistema solar)")]
    [SerializeField, Min(0.1f)] float _radarRangeAu = 10f;
    [Tooltip("Distancia al origen a partir de la cual se recentra el mundo")]
    [SerializeField] float _shiftThreshold = 100000f;
    [Tooltip("Distancia ANTES del radio de parada (2,08 x radio) a la que empieza el efecto de aterrizaje")]
    [SerializeField, Min(0f)] float _landingApproachDistance = 5000f;
    [Tooltip("Radar: distancia (UA) hasta la que la escala es casi lineal; más allá se comprime de forma logarítmica")]
    [SerializeField, Min(0.001f)] float _radarNearAu = 0.05f;
    // Con coordenadas de ~100000 unidades un float solo tiene precisión de ~0,008 unidades,
    // lo que hace vibrar la cabina y la nave. Se recentra mucho antes aunque el Inspector
    // tenga un valor mayor.
    const float MaxFloatingOriginRadius = 3000f;
    [Header("Misión")]
    [Tooltip("Número de pistas que se muestran al empezar la misión")]
    [SerializeField, Range(1, 5)] int _hintsShown = 3;
    [Tooltip("0 = cualquier destino. Si no, solo cuerpos a menos de estas UA de la Tierra (Eris está a ~96 UA)")]
    [SerializeField, Min(0f)] float _maxTargetDistanceAu = 0f;
    [Tooltip("La nave solo se mueve en el plano del sistema solar: se ignoran inclinaciones orbitales para que todos los cuerpos sean alcanzables")]
    [SerializeField] bool _flattenSystem = true;
    MissionPanel _missionPanel;
    IntroSequence _intro;
    bool _introPlayed;
    int _missionLevel = 1;
    [Tooltip("Activa la sincronización vertical (evita el tirón por frames irregulares con la nave a gran velocidad)")]
    [SerializeField] bool _forceVSync = true;
    [Header("Cinemática de despegue")]
    [Tooltip("Fracción de la altura de la pantalla que ocupa la Tierra en el plano general del despegue")]
    [SerializeField, Range(0.3f, 0.95f)] float _takeoffShotPlanetFill = 0.66f;
    string[] _missionHints;
    string _missionKind;
    [Header("Despegue y aterrizaje automáticos")]
    [SerializeField, Min(0.1f)] float _takeoffDuration = 8f;
    [SerializeField, Min(0.1f)] float _landingMinimumDuration = 4f;
    [SerializeField, Min(0.1f)] float _landingMaximumDuration = 12f;
    [Tooltip("Altura de captura atmosférica como múltiplo del radio visible del cuerpo")]
    [SerializeField, Min(0f)] float _atmosphereHeightFactor = 0.25f;
    [Tooltip("Distancia extra que la nave debe ganar tras despegar antes de permitir un aterrizaje")]
    [SerializeField, Min(0f)] float _departureClearanceDistance = 6000f;

    class Body
    {
        public string id;
        public double diameterUnits;
        public GameObject prefab;
        public GameObject instance;
        public SolarSystemData.Planet planet;
        public SolarSystemData.Moon moon;
        public bool invalidPositionLogged;
        public double x, y, z;      // posición absoluta en el universo (doble precisión)
    }

    readonly List<Body> _bodies = new List<Body>();
    readonly Dictionary<string, Body> _byId = new Dictionary<string, Body>();
    double _startDays;
    double _ox, _oy, _oz;           // posición del universo que corresponde al (0,0,0) de la escena
    Texture2D _mapDotTexture;
    Texture2D _radarBackgroundTexture;
    GUIStyle _mapLabelStyle;
    GUIStyle _mapHeaderStyle;
    GUIStyle _missionTitleStyle;
    GUIStyle _missionBodyStyle;
    GUIStyle _missionButtonStyle;
    string _flightMessage;
    float _flightMessageUntil;
    GameObject _flightStatusPanel;
    Text _flightStatusText;
    enum FlightSequence { MissionBriefing, FreeFlight, Takeoff, Landing, MissionComplete }
    FlightSequence _flightSequence;
    ShipController _shipController;
    Renderer[] _shipRenderers;
    CameraManager _cameraManager;
    Body _sequencePlanet;
    string _lastLandedBodyId;
    string _missionTargetId;
    string _missionHint;
    string _missionResultHeading;
    string _missionResultMessage;
    bool _missionSucceeded;
    bool _restartMissionRequested;
    bool _restoreKinematic;
    double _sequenceStartTime;
    double _sequenceDuration;
    double _sequenceStartX, _sequenceStartY, _sequenceStartZ;
    double _sequenceTargetX, _sequenceTargetY, _sequenceTargetZ;
    Quaternion _sequenceStartRotation, _sequenceTargetRotation;
    Vector3 _launchDirection;
    double _previousShipWorldX, _previousShipWorldY, _previousShipWorldZ;
    bool _hasPreviousShipWorldPosition;

    public bool ShowFlightHud => _flightSequence == FlightSequence.FreeFlight &&
                                 (_cameraManager == null || !_cameraManager.IsFollowCameraActive);

    /// <summary>Posición del Sol en coordenadas de la escena (para orientar la luz).</summary>
    public Vector3 SunScenePosition { get { return new Vector3((float)(-_ox), (float)(-_oy), (float)(-_oz)); } }

    void Awake()
    {
        Instance = this;
        CreateFlightStatusOverlay();
        _missionPanel = MissionPanel.Create(transform);
        _intro = IntroSequence.Create(transform);
        if (_forceVSync)
        {
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;
        }
        if (FindAnyObjectByType<CockpitHud>() == null) gameObject.AddComponent<CockpitHud>();

        if (_ship == null)
        {
            ShipController shipController = FindAnyObjectByType<ShipController>();
            if (shipController != null) _ship = shipController.transform;
        }
        if (_ship != null) _shipController = _ship.GetComponent<ShipController>();
        if (_ship != null) _shipRenderers = _ship.GetComponentsInChildren<Renderer>(true);
        if (_shipBody == null && _ship != null) _shipBody = _ship.GetComponent<Rigidbody>();
        GameObject cameraManagerObject = GameObject.Find("CameraManager");
        _cameraManager = cameraManagerObject != null
            ? cameraManagerObject.GetComponent<CameraManager>()
            : null;
        if (_cameraManager == null || !_cameraManager.HasConfiguredCameras)
        {
            CameraManager[] cameraManagers = FindObjectsByType<CameraManager>();
            foreach (CameraManager candidate in cameraManagers)
            {
                if (candidate == null || !candidate.HasConfiguredCameras) continue;
                _cameraManager = candidate;
                break;
            }
        }
        if (_shiftWithShip == null || _shiftWithShip.Length == 0)
        {
            GameObject cameraSetup = GameObject.Find("Camera Setup");
            if (cameraSetup != null) _shiftWithShip = new[] { cameraSetup.transform };
        }
        var prefabByName = new Dictionary<string, GameObject>();
        foreach (GameObject p in _prefabs)
        {
            if (p == null) continue;
            prefabByName[p.name] = p;
            if (p.name == "Sun2") prefabByName["Sun"] = p;
        }

        AddBody("Sun", SolarSystemData.SunDiameterKm, prefabByName, null, null);
        foreach (var p in SolarSystemData.Planets) AddBody(p.Id, p.DiameterKm, prefabByName, p, null);
        foreach (var m in SolarSystemData.Moons) AddBody(m.Id, m.DiameterKm, prefabByName, null, m);

        _startDays = ParseStartDays();
    }

    void AddBody(string id, double diameterKm, Dictionary<string, GameObject> prefabs,
                 SolarSystemData.Planet planet, SolarSystemData.Moon moon)
    {
        var b = new Body
        {
            id = id,
            diameterUnits = diameterKm * SolarSystemData.KmToUnits,
            planet = planet,
            moon = moon
        };
        if (!prefabs.TryGetValue(id, out b.prefab))
            Debug.LogWarning("SolarSystemManager: falta el prefab '" + id + "' en la lista (el nombre debe coincidir exactamente).");
        _bodies.Add(b);
        _byId[id] = b;
    }

    void Start()
    {
        if (_ship == null)
        {
            Debug.LogError("SolarSystemManager: no se encontró una nave con ShipController en la escena.");
            enabled = false;
            return;
        }
        PlaceShipAtEarth();
        BeginMissionBriefing();
    }

    void Update()
    {
        if (_restartMissionRequested)
        {
            _restartMissionRequested = false;
            RestartMissionNow();
            return;
        }

        if (_flightStatusPanel != null && !string.IsNullOrEmpty(_flightMessage) && Time.time > _flightMessageUntil)
        {
            _flightStatusPanel.SetActive(false);
            _flightMessage = null;
        }

        // Vuelo libre: la nave ya se movió en ShipController.Update (mismo frame). Se recentra el
        // mundo y se comprueban llegadas AQUÍ, antes de LateUpdate, para que cámara, radar y
        // planetas vean siempre el mismo origen.
        if (_flightSequence == FlightSequence.FreeFlight)
        {
            ShiftOriginIfNeeded();
            double t = Time.timeAsDouble;
            ComputeUniverse(_startDays + t * _planetTimeScale / 86400.0,
                            _startDays + t * _moonTimeScale / 86400.0);
            TryBeginBodyLanding();
        }
    }

    void CreateFlightStatusOverlay()
    {
        GameObject canvasObject = new GameObject("Flight Status Overlay");
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2000;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        _flightStatusPanel = new GameObject("Flight Status Panel");
        _flightStatusPanel.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = _flightStatusPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.zero;
        panelRect.pivot = Vector2.zero;
        panelRect.anchoredPosition = new Vector2(24f, 24f);
        panelRect.sizeDelta = new Vector2(280f, 56f);
        Image panelImage = _flightStatusPanel.AddComponent<Image>();
        panelImage.color = new Color(0.015f, 0.035f, 0.065f, 0.92f);
        panelImage.raycastTarget = false;

        GameObject accentObject = new GameObject("Accent");
        accentObject.transform.SetParent(_flightStatusPanel.transform, false);
        RectTransform accentRect = accentObject.AddComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.sizeDelta = new Vector2(4f, 0f);
        accentRect.anchoredPosition = Vector2.zero;
        Image accentImage = accentObject.AddComponent<Image>();
        accentImage.color = new Color(0.12f, 0.82f, 1f, 1f);
        accentImage.raycastTarget = false;

        GameObject textObject = new GameObject("Status Text");
        textObject.transform.SetParent(_flightStatusPanel.transform, false);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 4f);
        textRect.offsetMax = new Vector2(-10f, -4f);
        _flightStatusText = textObject.AddComponent<Text>();
        _flightStatusText.font = Font.CreateDynamicFontFromOSFont("Arial", 24);
        if (_flightStatusText.font == null)
            _flightStatusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _flightStatusText.fontSize = 24;
        _flightStatusText.fontStyle = FontStyle.Bold;
        _flightStatusText.color = Color.white;
        _flightStatusText.alignment = TextAnchor.MiddleLeft;
        _flightStatusText.raycastTarget = false;
        _flightStatusPanel.SetActive(false);
    }

    void ShowFlightStatus(string message, float duration)
    {
        _flightMessage = message;
        _flightMessageUntil = Time.time + duration;
        if (_flightStatusText != null)
        {
            _flightStatusText.text = message;
            _flightStatusText.fontSize = 24;
            _flightStatusText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _flightStatusText.verticalOverflow = VerticalWrapMode.Truncate;
        }
        if (_flightStatusPanel != null)
        {
            RectTransform rect = _flightStatusPanel.GetComponent<RectTransform>();
            if (rect != null) rect.sizeDelta = new Vector2(280f, 56f);
            _flightStatusPanel.SetActive(true);
        }
    }

    double ParseStartDays()
    {
        DateTime when = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(_startDateUtc))
        {
            if (!DateTime.TryParse(_startDateUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out when))
            {
                Debug.LogWarning("SolarSystemManager: fecha no válida, se usa la de hoy.");
                when = DateTime.UtcNow;
            }
        }
        DateTime j2000 = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        return (when - j2000).TotalDays;
    }

    void ComputeUniverse(double planetDays, double moonDays)
    {
        foreach (Body b in _bodies)
        {
            if (b.planet != null)
            {
                SolarSystemData.PlanetPosition(b.planet, planetDays, out b.x, out b.y, out b.z);
                if (_flattenSystem) b.y = 0.0;
            }
            else if (b.moon == null)
            {
                b.x = 0; b.y = 0; b.z = 0;          // el Sol está en el centro
            }
        }
        foreach (Body b in _bodies)
        {
            if (b.moon == null) continue;
            Body parent = _byId[b.moon.Parent];
            double dx, dy, dz;
            SolarSystemData.MoonOffset(b.moon, moonDays, out dx, out dy, out dz);
            if (_flattenSystem) dy = 0.0;
            b.x = parent.x + dx;
            b.y = parent.y + dy;
            b.z = parent.z + dz;
        }
    }

    void PlaceShipAtEarth()
    {
        ComputeUniverse(_startDays, _startDays);
        Body earth = _byId["Earth"];

        // Con el sistema aplanado la nave sale del ecuador terrestre (dentro del plano de los planetas),
        // así el despegue es radial y horizontal y la cámara ve la nave venir de frente.
        float latitude = (_flattenSystem ? 0f : _spawnLatitudeDegrees) * Mathf.Deg2Rad;
        float longitude = _spawnLongitudeDegrees * Mathf.Deg2Rad;
        Vector3 launchDirection = new Vector3(
            Mathf.Cos(latitude) * Mathf.Sin(longitude),
            Mathf.Sin(latitude),
            Mathf.Cos(latitude) * Mathf.Cos(longitude)
        ).normalized;
        _launchDirection = launchDirection;
        double r = earth.diameterUnits * 0.5 + _spawnAltitude;

        // El origen de escena queda sobre España, a la altura configurada.
        _ox = earth.x + launchDirection.x * r;
        _oy = earth.y + launchDirection.y * r;
        _oz = earth.z + launchDirection.z * r;

        _ship.position = Vector3.zero;
        _ship.rotation = Quaternion.LookRotation(launchDirection);
        if (_shipBody != null)
        {
            _shipBody.position = Vector3.zero;
            if (!_shipBody.isKinematic)
            {
                _shipBody.linearVelocity = Vector3.zero;
                _shipBody.angularVelocity = Vector3.zero;
            }
        }
        Physics.SyncTransforms();
    }

    void BeginTakeoff(Body earth)
    {
        double radius = GetDisplayedRadius(earth);
        double atmosphereHeight = Math.Max(radius * _atmosphereHeightFactor, 100.0);
        double targetAltitude = Math.Max(atmosphereHeight + _departureClearanceDistance, 300.0);
        double targetRadius = radius + targetAltitude;
        // Keep the launch planet disarmed as a landing target until the ship has
        // cleared the departure zone. Otherwise the arrival detector can turn the
        // cinematic takeoff straight back into an automatic landing.
        _lastLandedBodyId = earth.id;
        // Con el sistema aplanado la nave termina el despegue en el plano del sistema solar
        // (y = 0) y mirando hacia fuera en horizontal, para poder volar a todos los cuerpos.
        Vector3 takeoffDirection = _launchDirection;
        Quaternion takeoffRotation = _ship.rotation;
        if (_flattenSystem)
        {
            takeoffDirection = new Vector3(_launchDirection.x, 0f, _launchDirection.z);
            if (takeoffDirection.sqrMagnitude < 1e-4f) takeoffDirection = Vector3.forward;
            takeoffDirection.Normalize();
            takeoffRotation = Quaternion.LookRotation(takeoffDirection, Vector3.up);
        }
        BeginFlightSequence(FlightSequence.Takeoff, earth,
            earth.x + takeoffDirection.x * targetRadius,
            earth.y + takeoffDirection.y * targetRadius,
            earth.z + takeoffDirection.z * targetRadius,
            takeoffRotation, _takeoffDuration);
        // Plano general: la cámara se coloca SOBRE LA TRAYECTORIA, a la distancia a la que la Tierra
        // entera ocupa ~2/3 de la pantalla (se supone un FOV vertical de ~60°). La nave sale de la
        // superficie y cruza el plano general; la cámara la sigue y se acerca hasta entrar en cabina.
        Vector3 startScene = _ship.position;
        Vector3 endScene = new Vector3(
            (float)(earth.x + takeoffDirection.x * targetRadius - _ox),
            (float)(earth.y + takeoffDirection.y * targetRadius - _oy),
            (float)(earth.z + takeoffDirection.z * targetRadius - _oz));
        Vector3 earthScene = new Vector3((float)(earth.x - _ox), (float)(earth.y - _oy), (float)(earth.z - _oz));
        Vector3 path = endScene - startScene;
        float halfAngle = Mathf.Clamp(_takeoffShotPlanetFill * 30f, 5f, 60f) * Mathf.Deg2Rad;
        float shotDistance = (float)(radius / Math.Sin(halfAngle));
        // Punto de la trayectoria a 'shotDistance' del centro de la Tierra (raíz mayor de la ecuación).
        Vector3 fromEarth = startScene - earthScene;
        float qa = path.sqrMagnitude;
        float qb = 2f * Vector3.Dot(path, fromEarth);
        float qc = fromEarth.sqrMagnitude - shotDistance * shotDistance;
        float disc = qb * qb - 4f * qa * qc;
        float s01 = disc > 0f && qa > 1e-3f ? (-qb + Mathf.Sqrt(disc)) / (2f * qa) : 0.5f;
        s01 = Mathf.Clamp(s01, 0.1f, 0.92f);
        Vector3 pathDirection = path.normalized;
        Vector3 pathSide = Vector3.Cross(pathDirection, Vector3.up).normalized;
        Vector3 cameraPosition = startScene + path * s01 + pathSide * 35f;
        Quaternion cameraRotation = Quaternion.LookRotation(earthScene - cameraPosition, Vector3.up);
        _cameraManager?.StartTakeoffCameraShot(cameraPosition, cameraRotation);
    }

    // Intersección del segmento p0->p1 (movimiento de la nave en un paso de física) con una
    // esfera. Devuelve true si la nave ENTRA en la esfera durante el paso (aunque termine
    // muy dentro de ella) y t (0..1) es la fracción del segmento en la que cruza la superficie.
    // Solo cuenta si la nave se está acercando.
    static bool SegmentEntersSphere(double p0x, double p0y, double p0z,
                                    double p1x, double p1y, double p1z,
                                    double cx, double cy, double cz,
                                    double radius, out double t)
    {
        t = 0.0;
        double dx = p1x - p0x, dy = p1y - p0y, dz = p1z - p0z;
        double fx = p0x - cx, fy = p0y - cy, fz = p0z - cz;
        double a = dx * dx + dy * dy + dz * dz;
        if (a < 1e-9) return false;                         // nave quieta
        double b = fx * dx + fy * dy + fz * dz;
        if (b >= 0.0) return false;                         // se aleja
        double c = fx * fx + fy * fy + fz * fz - radius * radius;
        if (c <= 0.0) return true;                          // ya estaba dentro y se acerca
        double disc = b * b - a * c;
        if (disc < 0.0) return false;                       // pasa de largo
        t = (-b - Math.Sqrt(disc)) / a;
        return t <= 1.0;
    }

    void TryBeginBodyLanding()
    {
        if (_ship == null) return;

        Vector3 scenePosition = _ship.position;
        double shipX = _ox + scenePosition.x;
        double shipY = _oy + scenePosition.y;
        double shipZ = _oz + scenePosition.z;
        // Posición del paso de física anterior, en coordenadas absolutas (no le afecta el recentrado).
        double prevX = _hasPreviousShipWorldPosition ? _previousShipWorldX : shipX;
        double prevY = _hasPreviousShipWorldPosition ? _previousShipWorldY : shipY;
        double prevZ = _hasPreviousShipWorldPosition ? _previousShipWorldZ : shipZ;
        _previousShipWorldX = shipX;
        _previousShipWorldY = shipY;
        _previousShipWorldZ = shipZ;
        _hasPreviousShipWorldPosition = true;

        Vector3 shipVelocity = _shipController != null ? _shipController.Velocity : Vector3.zero;

        // Sol: zona crítica = diámetro visible x 1,04.
        Body sun = _byId["Sun"];
        double sunCaptureDistance = GetDisplayedRadius(sun) * 2.0 * 1.04;
        double sunT;
        if (SegmentEntersSphere(prevX, prevY, prevZ, shipX, shipY, shipZ,
                                sun.x, sun.y, sun.z, sunCaptureDistance, out sunT))
        {
            Vector3 sunEntry = new Vector3(
                (float)(prevX + (shipX - prevX) * sunT - _ox),
                (float)(prevY + (shipY - prevY) * sunT - _oy),
                (float)(prevZ + (shipZ - prevZ) * sunT - _oz));
            BeginSunExplosion(sun, sunEntry);
            return;
        }

        Body nearest = null;
        double nearestT = double.MaxValue;
        double entryX = 0.0, entryY = 0.0, entryZ = 0.0;

        foreach (Body body in _bodies)
        {
            if (body.planet == null && body.moon == null) continue;

            double radius = GetDisplayedRadius(body);

            if (body.id == _lastLandedBodyId)
            {
                double ldx = shipX - body.x;
                double ldy = shipY - body.y;
                double ldz = shipZ - body.z;
                double landedDistance = Math.Sqrt(ldx * ldx + ldy * ldy + ldz * ldz);
                float outwardSpeed = landedDistance > 0.001
                    ? Vector3.Dot(shipVelocity, new Vector3((float)(ldx / landedDistance),
                        (float)(ldy / landedDistance), (float)(ldz / landedDistance)))
                    : 0f;
                // Keep the launch body locked until the ship has actually moved
                // outward beyond the clearance radius, not just rotated or drifted.
                if (landedDistance <= radius + GetAtmosphereHeight(radius) + _departureClearanceDistance ||
                    outwardSpeed <= 0.5f)
                    continue;
                _lastLandedBodyId = null;
            }

            // El aterrizaje EMPIEZA un poco antes del radio de parada (2,08 x radio), para que se
            // vea la maniobra de aproximación. Se comprueba el SEGMENTO recorrido en el paso de
            // física, no solo la posición final: a alta velocidad la nave avanza miles de unidades
            // por paso y antes atravesaba el planeta antes de que se detectara.
            double holdDistance = radius * 2.0 * 1.04;
            double triggerDistance = holdDistance + _landingApproachDistance;
            double t;
            if (!SegmentEntersSphere(prevX, prevY, prevZ, shipX, shipY, shipZ,
                                     body.x, body.y, body.z, triggerDistance, out t))
                continue;
            if (t >= nearestT) continue;

            nearest = body;
            nearestT = t;
            entryX = prevX + (shipX - prevX) * t;
            entryY = prevY + (shipY - prevY) * t;
            entryZ = prevZ + (shipZ - prevZ) * t;
        }

        // La secuencia arranca en el punto exacto donde la nave cruzó la zona de captura.
        if (nearest != null) BeginLanding(nearest, entryX, entryY, entryZ);
    }

    void BeginLanding(Body planet, double shipX, double shipY, double shipZ)
    {
        double dx = shipX - planet.x;
        double dy = shipY - planet.y;
        double dz = shipZ - planet.z;
        double length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        Vector3 outward = length > 0.001
            ? new Vector3((float)(dx / length), (float)(dy / length), (float)(dz / length))
            : -_ship.forward;

        double radius = GetDisplayedRadius(planet);
        // Stop the landing sequence on the same atmospheric boundary that
        // triggered it. Previously the cinematic drove the ship to radius +
        // clearance, which could put it inside the visible body.
        double targetRadius = radius * 2.0 * 1.04;
        Vector3 approachDirection = -outward;
        Vector3 up = Mathf.Abs(Vector3.Dot(approachDirection, Vector3.up)) > 0.98f
            ? Vector3.forward : Vector3.up;
        Quaternion landingRotation = Quaternion.LookRotation(approachDirection, up);
        Vector3 targetScene = new Vector3(
            (float)(planet.x + outward.x * targetRadius - _ox),
            (float)(planet.y + outward.y * targetRadius - _oy),
            (float)(planet.z + outward.z * targetRadius - _oz));
        Vector3 entryScene = new Vector3((float)(shipX - _ox), (float)(shipY - _oy), (float)(shipZ - _oz));
        float localDistance = Vector3.Distance(entryScene, targetScene);
        float duration = Mathf.Clamp(localDistance / 1500f, _landingMinimumDuration, _landingMaximumDuration);

        BeginFlightSequence(FlightSequence.Landing, planet,
            planet.x + outward.x * targetRadius,
            planet.y + outward.y * targetRadius,
            planet.z + outward.z * targetRadius,
            landingRotation, duration);
        // Empezar el movimiento en el punto de entrada y no en la posición del paso actual
        // (que a gran velocidad puede estar ya dentro del planeta).
        _sequenceStartX = shipX;
        _sequenceStartY = shipY;
        _sequenceStartZ = shipZ;
        Vector3 planetCenter = new Vector3(
            (float)(planet.x - _ox),
            (float)(planet.y - _oy),
            (float)(planet.z - _oz));
        // Cámara pegada detrás del punto de entrada, casi sobre el eje de aproximación:
        // la nave aparece grande delante de la cámara y se aleja hacia el planeta.
        Vector3 cameraSide = Vector3.Cross(outward, up).normalized;
        if (cameraSide.sqrMagnitude < 0.001f) cameraSide = Vector3.right;
        float cameraBack = 160f;
        Vector3 cameraPosition = entryScene + outward * cameraBack
            + cameraSide * 40f + Vector3.up * 25f;
        Vector3 cameraFocus = targetScene;
        Quaternion cameraRotation = Quaternion.LookRotation(cameraFocus - cameraPosition, up);
        _cameraManager?.StartLandingCameraShot(cameraPosition, cameraRotation);
    }

    void BeginSunExplosion(Body sun, Vector3 shipScenePosition)
    {
        if (_flightSequence != FlightSequence.FreeFlight) return;

        Vector3 sunScenePosition = new Vector3((float)(sun.x - _ox), (float)(sun.y - _oy), (float)(sun.z - _oz));
        Vector3 outward = shipScenePosition - sunScenePosition;
        if (outward.sqrMagnitude < 0.001f) outward = -_ship.forward;
        else outward.Normalize();

        if (_shipController != null) _shipController.SetFlightControlsEnabled(false);
        if (_shipBody != null)
        {
            _shipBody.linearVelocity = Vector3.zero;
            _shipBody.angularVelocity = Vector3.zero;
            _shipBody.isKinematic = true;
        }
        if (_shipRenderers != null)
            foreach (Renderer shipRenderer in _shipRenderers)
                if (shipRenderer != null) shipRenderer.enabled = false;

        SpawnShipExplosion(shipScenePosition);
        _missionSucceeded = false;
        _missionResultHeading = "ZONA CRÍTICA DEL SOL";
        _missionResultMessage = "Has entrado en la zona crítica del Sol.";
        _missionPanel?.ShowResult(false, _missionResultHeading,
            "Has entrado en la zona crítica del Sol: la nave no ha resistido el calor.",
            "DATO CURIOSO · EL SOL", MissionContent.RandomSunFact(), StartAnotherMission, ExitGame);
        if (_flightStatusPanel != null) _flightStatusPanel.SetActive(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Vector3 cameraPosition = shipScenePosition + outward * 5000f;
        Vector3 lookDirection = -outward;
        Vector3 cameraUp = Mathf.Abs(Vector3.Dot(lookDirection, Vector3.up)) > 0.98f
            ? Vector3.forward : Vector3.up;
        _cameraManager?.SetUserCameraSwitchingEnabled(false);
        _cameraManager?.StartLandingCameraShot(cameraPosition, Quaternion.LookRotation(lookDirection, cameraUp));
        _flightSequence = FlightSequence.MissionComplete;
        ForceFollowCamera();
    }

    void SpawnShipExplosion(Vector3 scenePosition)
    {
        GameObject explosionObject = new GameObject("Ship Explosion");
        explosionObject.transform.SetParent(transform, true);
        explosionObject.transform.position = scenePosition;
        ParticleSystem particles = explosionObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.duration = 1.6f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(120f, 600f);
        main.startSize = new ParticleSystem.MinMaxCurve(12f, 55f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.42f, 0.08f, 1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)120) });
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 16f;
        shape.radiusThickness = 0.75f;

        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (particleShader == null) particleShader = Shader.Find("Particles/Standard Unlit");
        if (particleShader != null)
        {
            Material explosionMaterial = new Material(particleShader);
            if (explosionMaterial.HasProperty("_BaseColor"))
                explosionMaterial.SetColor("_BaseColor", new Color(1f, 0.46f, 0.08f, 1f));
            if (explosionMaterial.HasProperty("_Color"))
                explosionMaterial.SetColor("_Color", new Color(1f, 0.46f, 0.08f, 1f));
            particleRenderer.material = explosionMaterial;
            Destroy(explosionMaterial, 5f);
        }
        particles.Play();
        Destroy(explosionObject, 5f);
    }

    float DisplayScale(Body b)
    {
        return (b.moon != null || MissionContent.IsDwarfPlanet(b.id)) ? Mathf.Max(1f, _moonDisplayScale) : 1f;
    }

    double GetDisplayedRadius(Body body)
    {
        double displayScale = DisplayScale(body);
        double radius = body.diameterUnits * 0.5 * displayScale;
        if (body.instance != null && body.instance.activeInHierarchy)
        {
            Renderer[] renderers = body.instance.GetComponentsInChildren<Renderer>();
            foreach (Renderer bodyRenderer in renderers)
            {
                if (bodyRenderer == null || !bodyRenderer.enabled) continue;
                Vector3 extents = bodyRenderer.bounds.extents;
                radius = Math.Max(radius, Math.Max(extents.x, Math.Max(extents.y, extents.z)));
            }
        }
        return radius;
    }

    double GetAtmosphereHeight(double displayedRadius)
    {
        return Math.Max(displayedRadius * _atmosphereHeightFactor, 100.0);
    }

    void BeginFlightSequence(FlightSequence sequence, Body planet,
                             double targetX, double targetY, double targetZ,
                             Quaternion targetRotation, float duration)
    {
        _flightSequence = sequence;
        _sequencePlanet = planet;
        Vector3 currentPosition = _ship.position;
        _sequenceStartX = _ox + currentPosition.x;
        _sequenceStartY = _oy + currentPosition.y;
        _sequenceStartZ = _oz + currentPosition.z;
        _sequenceTargetX = targetX;
        _sequenceTargetY = targetY;
        _sequenceTargetZ = targetZ;
        _sequenceStartRotation = _ship.rotation;
        _sequenceTargetRotation = targetRotation;
        _sequenceStartTime = Time.timeAsDouble;
        _sequenceDuration = Math.Max(0.1, duration);
        ShowFlightStatus(sequence == FlightSequence.Takeoff ? "Despegando" : "Aterrizando",
                         (float)_sequenceDuration);

        if (_shipController != null) _shipController.SetFlightControlsEnabled(false);
        if (_shipBody != null)
        {
            _restoreKinematic = _shipBody.isKinematic;
            if (!_shipBody.isKinematic)
            {
                _shipBody.linearVelocity = Vector3.zero;
                _shipBody.angularVelocity = Vector3.zero;
            }
            _shipBody.isKinematic = true;
        }
        if (_cameraManager != null)
        {
            _cameraManager.SetUserCameraSwitchingEnabled(false);
            _cameraManager.ShowFollowCamera();
        }
    }

    void UpdateFlightSequence()
    {
        double elapsed = Time.timeAsDouble - _sequenceStartTime;
        float progress = Mathf.Clamp01((float)(elapsed / _sequenceDuration));
        float easedProgress = progress * progress * (3f - 2f * progress);
        double absoluteX = _sequenceStartX + (_sequenceTargetX - _sequenceStartX) * easedProgress;
        double absoluteY = _sequenceStartY + (_sequenceTargetY - _sequenceStartY) * easedProgress;
        double absoluteZ = _sequenceStartZ + (_sequenceTargetZ - _sequenceStartZ) * easedProgress;
        Vector3 localPosition = new Vector3(
            (float)(absoluteX - _ox),
            (float)(absoluteY - _oy),
            (float)(absoluteZ - _oz));
        Quaternion rotation = Quaternion.Slerp(_sequenceStartRotation, _sequenceTargetRotation, easedProgress);

        if (_shipBody != null) _shipBody.position = localPosition;
        _ship.position = localPosition;
        _ship.rotation = rotation;
        if (_shipBody != null) _shipBody.rotation = rotation;

        if (_flightSequence == FlightSequence.Takeoff)
            _cameraManager?.UpdateTakeoffCameraShot(localPosition, progress);

        if (progress < 1f) return;

        if (_flightSequence == FlightSequence.Landing)
        {
            if (_sequencePlanet != null) _lastLandedBodyId = _sequencePlanet.id;
            ShowMissionResult(_sequencePlanet);
            if (_shipBody != null) _shipBody.isKinematic = true;
            // Landing ends the mission: leave controls locked and keep the external view.
            _flightSequence = FlightSequence.MissionComplete;
            _sequencePlanet = null;
            ForceFollowCamera();
            return;
        }

        if (_flightSequence == FlightSequence.Takeoff && _shipBody != null)
        {
            _shipBody.isKinematic = _restoreKinematic;
            if (!_shipBody.isKinematic)
            {
                _shipBody.linearVelocity = Vector3.zero;
                _shipBody.angularVelocity = Vector3.zero;
            }
        }
        if (_flightSequence == FlightSequence.Takeoff && _flightStatusPanel != null)
        {
            _flightStatusPanel.SetActive(false);
            _flightMessage = null;
        }
        if (_shipController != null) _shipController.SetFlightControlsEnabled(true);
        if (_cameraManager != null)
        {
            _cameraManager.EndFixedExternalShot();
            _cameraManager.ShowCockpitCamera();
            _cameraManager.SetUserCameraSwitchingEnabled(true);
        }
        _hasPreviousShipWorldPosition = false;
        _flightSequence = FlightSequence.FreeFlight;
        _sequencePlanet = null;
    }

    void ForceFollowCamera()
    {
        if (_cameraManager != null) _cameraManager.ShowFollowCamera();
    }

    void BeginMissionBriefing()
    {
        _missionResultHeading = null;
        _missionResultMessage = null;
        _flightMessage = null;
        if (_flightStatusPanel != null) _flightStatusPanel.SetActive(false);
        _flightSequence = FlightSequence.MissionBriefing;
        _shipController?.SetFlightControlsEnabled(false);
        if (_shipBody != null && !_shipBody.isKinematic)
        {
            _shipBody.linearVelocity = Vector3.zero;
            _shipBody.angularVelocity = Vector3.zero;
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Body earth = _byId["Earth"];
        double radius = GetDisplayedRadius(earth);
        double targetAltitude = Math.Max(GetAtmosphereHeight(radius) + _departureClearanceDistance, 300.0);
        float cameraLead = (float)(targetAltitude - _spawnAltitude + 20.0);
        Quaternion cameraRotation = Quaternion.LookRotation(-_launchDirection, Vector3.up);
        _cameraManager?.SetUserCameraSwitchingEnabled(false);
        _cameraManager?.StartTakeoffCameraShot(
            _ship.position + _launchDirection * cameraLead, cameraRotation);

        // Primera vez: intro del juego. Después, selección de nivel y misión.
        if (!_introPlayed && _intro != null)
        {
            _introPlayed = true;
            _intro.Play(ShowLevelSelect);
        }
        else ShowLevelSelect();
    }

    void ShowLevelSelect()
    {
        if (_flightSequence != FlightSequence.MissionBriefing) return;
        _missionPanel?.ShowLevelSelect(_missionLevel, OnLevelSelected);
    }

    // Nivel 1: planetas. Nivel 2: + satélites principales. Nivel 3: cualquier cuerpo celeste.
    void OnLevelSelected(int level)
    {
        if (_flightSequence != FlightSequence.MissionBriefing) return;
        _missionLevel = level;

        List<Body> destinations = new List<Body>();
        Body home = _byId["Earth"];
        double maxDistance = _maxTargetDistanceAu * SolarSystemData.AuToUnits;
        foreach (Body candidate in _bodies)
        {
            if (candidate.id == "Sun" || candidate.id == "Earth") continue;
            if (candidate.planet == null && candidate.moon == null) continue;
            if (!MissionContent.HasHints(candidate.id)) continue;
            if (MissionContent.LevelOf(candidate.id) > level) continue;
            if (maxDistance > 0.0)
            {
                double ex = candidate.x - home.x, ey = candidate.y - home.y, ez = candidate.z - home.z;
                if (Math.Sqrt(ex * ex + ey * ey + ez * ez) > maxDistance) continue;
            }
            destinations.Add(candidate);
        }
        if (destinations.Count == 0)
        {
            _missionPanel?.Hide();
            BeginTakeoff(_byId["Earth"]);
            return;
        }

        Body target = destinations[UnityEngine.Random.Range(0, destinations.Count)];
        _missionTargetId = target.id;
        _missionKind = MissionContent.GetKindPhrase(target.id);
        _missionHints = MissionContent.PickHints(target.id, _hintsShown);
        _missionPanel?.ShowBriefing(_missionKind, _missionHints, AcceptMission);
    }

    void AcceptMission()
    {
        if (_flightSequence != FlightSequence.MissionBriefing) return;
        _missionPanel?.Hide();
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = false;
        BeginTakeoff(_byId["Earth"]);
    }

    void StartAnotherMission()
    {
        _restartMissionRequested = true;
    }

    void RestartMissionNow()
    {
        _cameraManager?.EndFixedExternalShot();
        _cameraManager?.SetUserCameraSwitchingEnabled(false);
        if (_shipRenderers != null)
            foreach (Renderer shipRenderer in _shipRenderers)
                if (shipRenderer != null) shipRenderer.enabled = true;
        if (_shipBody != null)
        {
            _shipBody.isKinematic = true;   // la nave se mueve con el Transform, no con la física
        }
        _shipController?.SetFlightControlsEnabled(true);
        _lastLandedBodyId = null;
        _hasPreviousShipWorldPosition = false;
        _flightSequence = FlightSequence.FreeFlight;
        PlaceShipAtEarth();
        BeginMissionBriefing();
    }

    static void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void ShowMissionResult(Body arrivedBody)
    {
        if (arrivedBody == null) return;
        _missionSucceeded = arrivedBody.id == _missionTargetId;
        string arrivedName = MissionContent.GetName(arrivedBody.id);
        string fact = MissionContent.RandomFact(arrivedBody.id);
        _missionResultHeading = _missionSucceeded ? "MISIÓN COMPLETADA" : "MISIÓN NO COMPLETADA";
        string headline = _missionSucceeded
            ? "¡Objetivo cumplido! Has aterrizado en " + arrivedName + "."
            : "Has llegado a " + arrivedName + ", pero la misión era llegar a " +
              MissionContent.GetName(_missionTargetId) + ".";
        _missionResultMessage = headline;
        if (_flightStatusPanel != null) _flightStatusPanel.SetActive(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _missionPanel?.ShowResult(_missionSucceeded, _missionResultHeading, headline,
            "DATO CURIOSO · " + arrivedName.ToUpperInvariant(), fact, StartAnotherMission, ExitGame);
    }

    void LateUpdate()
    {
        SanitizeShipTransform();
        double t = Time.timeAsDouble;
        ComputeUniverse(_startDays + t * _planetTimeScale / 86400.0,
                        _startDays + t * _moonTimeScale / 86400.0);

        // En vuelo libre el recentrado lo hace Update. Aquí solo se recentra con la nave
        // cinemática (briefing, despegue, aterrizaje) y ANTES de colocarla, para que su
        // posición ya use el origen nuevo.
        bool shiftedOrigin = _flightSequence != FlightSequence.FreeFlight && ShiftOriginIfNeeded();

        if (_flightSequence == FlightSequence.MissionBriefing)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ForceFollowCamera();
        }
        else if (_flightSequence == FlightSequence.Takeoff || _flightSequence == FlightSequence.Landing)
        {
            UpdateFlightSequence();
            // CameraManager.Start can run after SolarSystemManager.Start and
            // reset the shot to the cockpit. Reassert the external cinematic view.
            if (_flightSequence == FlightSequence.Takeoff)
                ForceFollowCamera();
        }
        else if (_flightSequence == FlightSequence.MissionComplete)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ForceFollowCamera();
        }

        double sx = _ox + _ship.position.x;
        double sy = _oy + _ship.position.y;
        double sz = _oz + _ship.position.z;

        foreach (Body b in _bodies)
        {
            if (b.prefab == null) continue;

            double dx = b.x - sx, dy = b.y - sy, dz = b.z - sz;
            if (!IsFinite(dx) || !IsFinite(dy) || !IsFinite(dz) ||
                !IsFinite(b.diameterUnits) || b.diameterUnits <= 0.0)
            {
                if (b.instance != null) b.instance.SetActive(false);
                if (!b.invalidPositionLogged)
                {
                    Debug.LogError("SolarSystemManager: posición o tamaño no finito para " + b.id + "; se oculta para evitar corromper sus bounds.");
                    b.invalidPositionLogged = true;
                }
                continue;
            }
            double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            double show = Math.Min(b.diameterUnits * _showFactor, _maxVisibleDistance);

            bool visible = b.instance != null && b.instance.activeSelf;
            if (!visible && dist < show) Show(b);
            else if (visible && dist > show * 1.1) b.instance.SetActive(false);

            if (b.instance != null && b.instance.activeSelf)
            {
                double px = b.x - _ox, py = b.y - _oy, pz = b.z - _oz;
                if (!IsFinite(px) || !IsFinite(py) || !IsFinite(pz) ||
                    Math.Abs(px) > float.MaxValue || Math.Abs(py) > float.MaxValue || Math.Abs(pz) > float.MaxValue)
                {
                    b.instance.SetActive(false);
                    Debug.LogError("SolarSystemManager: coordenadas fuera del rango de Unity para " + b.id + "; se oculta el cuerpo.");
                    continue;
                }
                b.instance.transform.position = new Vector3((float)px, (float)py, (float)pz);
            }
        }

        // Sync after the ship and all celestial transforms have been updated,
        // so physics never processes a frame with mixed pre/post-shift bounds.
        if (shiftedOrigin) Physics.SyncTransforms();
    }

    void Show(Body b)
    {
        if (b.instance == null)
        {
            b.instance = Instantiate(b.prefab, transform);
            b.instance.name = b.id;
            float displayScale = DisplayScale(b);
            b.instance.transform.localScale = Vector3.one * (float)b.diameterUnits * displayScale;
            if (displayScale > 1f)
            {
                SphereCollider collider = b.instance.GetComponent<SphereCollider>();
                if (collider != null) collider.radius /= displayScale;
            }
            foreach (Collider bodyCollider in b.instance.GetComponentsInChildren<Collider>(true))
                if (bodyCollider != null) bodyCollider.isTrigger = true;
            b.instance.hideFlags |= HideFlags.HideInHierarchy;
        }
        b.instance.SetActive(true);
    }

    bool ShiftOriginIfNeeded()
    {
        if (_ship == null) return false;

        Vector3 d = _ship.position;
        float threshold = Mathf.Min(_shiftThreshold, MaxFloatingOriginRadius);
        if (d.sqrMagnitude < threshold * threshold) return false;

        _ox += d.x; _oy += d.y; _oz += d.z;
        // La nave es cinemática y se mueve con el Transform: se desplaza el Transform y el cuerpo a la vez.
        _ship.position -= d;
        if (_shipBody != null) _shipBody.position = _ship.position;
        // Tell Cinemachine the target teleported so its damping history moves
        // with the floating origin instead of pulling the camera across the scene.
        CinemachineCore.OnTargetObjectWarped(_ship, -d);
        if (_shiftWithShip != null)
            foreach (Transform tr in _shiftWithShip)
                if (tr != null && tr != _ship) tr.position -= d;
        _cameraManager?.ShiftTakeoffCameraOrigin(-d);
        return true;
    }

    void SanitizeShipTransform()
    {
        if (_ship == null) return;

        Vector3 p = _ship.position;
        if (!IsFinite(p.x) || !IsFinite(p.y) || !IsFinite(p.z))
        {
            Debug.LogError("SolarSystemManager: la posición de la nave dejó de ser finita; se recoloca en el origen.");
            if (_shipBody != null)
            {
                _shipBody.position = Vector3.zero;
                if (!_shipBody.isKinematic)
                {
                    _shipBody.linearVelocity = Vector3.zero;
                    _shipBody.angularVelocity = Vector3.zero;
                }
            }
            else _ship.position = Vector3.zero;
        }

        Quaternion q = _ship.rotation;
        if (!IsFinite(q.x) || !IsFinite(q.y) || !IsFinite(q.z) || !IsFinite(q.w))
        {
            Debug.LogError("SolarSystemManager: la rotación de la nave dejó de ser finita; se restablece.");
            _ship.rotation = Quaternion.identity;
            if (_shipBody != null && !_shipBody.isKinematic) _shipBody.angularVelocity = Vector3.zero;
        }
    }

    static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public struct RadarContact
    {
        public Vector2 position;   // dentro del círculo unidad; (0,1) = justo delante de la nave
        public float elevation;    // -1..1 (por debajo / por encima del plano de la nave)
        public float size;         // tamaño del punto en píxeles de referencia
        public Color color;
        public bool isSun;
        public string label;       // nombre corto del cuerpo
        public bool showLabel;
    }

    public float RadarRangeAu => _radarRangeAu;

    /// <summary>Distancia (UA) que representa un anillo del radar situado a 'fraction' del radio (0..1).</summary>
    public float GetRadarRingAu(float fraction)
    {
        double near = Math.Max(0.001, _radarNearAu);
        double range = Math.Max(0.1f, _radarRangeAu);
        return (float)(near * (Math.Exp(fraction * Math.Log(1.0 + range / near)) - 1.0));
    }

    /// <summary>
    /// Rellena los contactos del radar (lo dibuja CockpitHud). La proa apunta hacia arriba y la nave está
    /// en el centro; la dirección es la real en 3D y la distancia se comprime de forma continua
    /// (casi lineal cerca, logarítmica lejos); lo que queda fuera de alcance se pega al borde.
    /// </summary>
    public void FillRadarContacts(List<RadarContact> contacts)
    {
        contacts.Clear();
        if (_ship == null || _bodies.Count == 0) return;

        double shipX = _ox + _ship.position.x;
        double shipY = _oy + _ship.position.y;
        double shipZ = _oz + _ship.position.z;
        double radarRange = Math.Max(0.1f, _radarRangeAu) * SolarSystemData.AuToUnits;
        Quaternion worldToShip = Quaternion.Inverse(_flattenSystem ? Quaternion.Euler(0f, _ship.eulerAngles.y, 0f) : _ship.rotation);

        foreach (Body body in _bodies)
        {
            if (body.planet == null && body.moon == null && body.id != "Sun") continue;
            double dx = body.x - shipX;
            double dy = body.y - shipY;
            double dz = body.z - shipZ;
            double distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (distance < 1.0) continue;
            bool outOfRange = distance > radarRange;
            if (outOfRange && body.moon != null) continue;      // las lunas lejanas no se dibujan

            Vector3 u = worldToShip * new Vector3((float)(dx / distance), (float)(dy / distance), (float)(dz / distance));
            float radarDistance = GetRadarMapDistance(distance, radarRange, 1f);
            Color color = GetRadarBodyColor(body);
            if (outOfRange) color.a = 0.45f;
            contacts.Add(new RadarContact
            {
                position = new Vector2(u.x, u.z) * radarDistance,
                elevation = Mathf.Clamp(u.y, -1f, 1f),
                size = GetRadarDotSize(body),
                color = color,
                isSun = body.id == "Sun",
                label = body.id == "Sun" ? "Sol" : MissionContent.GetShortName(body.id),
                // Las lunas solo se rotulan cuando la nave está relativamente cerca (evita amontonar nombres)
                showLabel = body.moon == null || distance < 0.25 * SolarSystemData.AuToUnits
            });
        }
    }

    void EnsureMapGuiResources()
    {
        if (_mapDotTexture == null)
        {
            const int textureSize = 32;
            _mapDotTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                name = "Solar system map dot",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color[] pixels = new Color[textureSize * textureSize];
            for (int py = 0; py < textureSize; py++)
            for (int px = 0; px < textureSize; px++)
            {
                float dx = (px + 0.5f - textureSize * 0.5f) / (textureSize * 0.5f);
                float dy = (py + 0.5f - textureSize * 0.5f) / (textureSize * 0.5f);
                float alpha = Mathf.Clamp01((1f - Mathf.Sqrt(dx * dx + dy * dy)) * textureSize * 0.5f);
                pixels[py * textureSize + px] = new Color(1f, 1f, 1f, alpha);
            }
            _mapDotTexture.SetPixels(pixels);
            _mapDotTexture.Apply();
        }

        if (_radarBackgroundTexture == null)
        {
            const int textureSize = 256;
            _radarBackgroundTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                name = "Navigation radar background",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color[] pixels = new Color[textureSize * textureSize];
            for (int py = 0; py < textureSize; py++)
            for (int px = 0; px < textureSize; px++)
            {
                float dx = (px + 0.5f - textureSize * 0.5f) / (textureSize * 0.5f);
                float dy = (py + 0.5f - textureSize * 0.5f) / (textureSize * 0.5f);
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                Color pixel = Color.clear;
                if (radius <= 1f)
                {
                    pixel = new Color(0.02f, 0.09f, 0.12f, 0.72f);
                    float ringDistance = Mathf.Min(Mathf.Abs(radius - 1f),
                        Mathf.Min(Mathf.Abs(radius - 0.66f), Mathf.Abs(radius - 0.33f)));
                    if (ringDistance < 0.012f || Mathf.Abs(dx) < 0.004f || Mathf.Abs(dy) < 0.004f)
                        pixel = new Color(0.2f, 0.7f, 0.78f, 0.45f);
                }
                pixels[py * textureSize + px] = pixel;
            }
            _radarBackgroundTexture.SetPixels(pixels);
            _radarBackgroundTexture.Apply();
        }

        if (_mapLabelStyle == null)
            _mapLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleLeft };
        if (_mapHeaderStyle == null)
            _mapHeaderStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        if (_missionTitleStyle == null)
            _missionTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.35f, 0.86f, 1f) }
            };
        if (_missionBodyStyle == null)
            _missionBodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20, alignment = TextAnchor.MiddleCenter, wordWrap = true,
                normal = { textColor = new Color(0.9f, 0.94f, 1f) }
            };
        if (_missionButtonStyle == null)
            _missionButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
    }

    void DrawMapDot(Vector2 point, float size, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(new Rect(point.x - size * 0.5f, point.y - size * 0.5f, size, size), _mapDotTexture);
    }

    static float GetRadarDotSize(Body body)
    {
        if (body.id == "Sun") return 24f;
        if (body.moon != null) return 3.5f;
        double earthDiameterUnits = 12742.0 * SolarSystemData.KmToUnits;
        return Mathf.Clamp(6f * Mathf.Sqrt((float)(body.diameterUnits / earthDiameterUnits)), 3f, 14f);
    }

    static Color GetRadarBodyColor(Body body)
    {
        if (body.id == "Sun") return new Color(1f, 0.72f, 0.12f, 1f);
        if (body.moon != null) return new Color(0.78f, 0.86f, 0.93f, 1f);
        return GetMapPlanetColor(body.id);
    }

    static Vector2 GetStableRadarDirection(string bodyId)
    {
        int angleIndex = 17;
        if (bodyId != null)
            foreach (char character in bodyId) angleIndex = (angleIndex * 31 + character) % 360;
        float angle = angleIndex * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    // Escala continua y creciente: r = R * ln(1 + d/d0) / ln(1 + Dmax/d0).
    // Para d << d0 es prácticamente lineal (proporción real cerca de la nave); para d >> d0
    // crece de forma logarítmica, así que lo lejano se "encoge" sin salirse del radar.
    float GetRadarMapDistance(double distance, double radarRange, float mapRadius)
    {
        double near = Math.Max(0.001, _radarNearAu) * SolarSystemData.AuToUnits;
        double clamped = Math.Min(distance, radarRange);
        double fraction = Math.Log(1.0 + clamped / near) / Math.Log(1.0 + radarRange / near);
        return (float)(fraction * mapRadius);
    }

    static Color GetMapPlanetColor(string id)
    {
        switch (id)
        {
            case "Mercury": return new Color(0.68f, 0.62f, 0.56f);
            case "Venus": return new Color(0.96f, 0.72f, 0.35f);
            case "Earth": return new Color(0.18f, 0.58f, 1f);
            case "Mars": return new Color(0.93f, 0.31f, 0.19f);
            case "Jupiter": return new Color(0.84f, 0.57f, 0.34f);
            case "Saturn": return new Color(0.91f, 0.79f, 0.48f);
            case "Uranus": return new Color(0.42f, 0.85f, 0.86f);
            case "Neptune": return new Color(0.25f, 0.42f, 0.96f);
            case "Ceres": return new Color(0.7f, 0.7f, 0.72f);
            case "Eris": return new Color(0.88f, 0.9f, 1f);
            case "Haumea": return new Color(0.8f, 0.9f, 0.95f);
            case "Makemake": return new Color(0.82f, 0.5f, 0.38f);
            default: return Color.white;
        }
    }

    void OnDestroy()
    {
        if (Application.isPlaying)
        {
            if (_mapDotTexture != null) Destroy(_mapDotTexture);
            if (_radarBackgroundTexture != null) Destroy(_radarBackgroundTexture);
        }
        else
        {
            if (_mapDotTexture != null) DestroyImmediate(_mapDotTexture);
            if (_radarBackgroundTexture != null) DestroyImmediate(_radarBackgroundTexture);
        }
    }

    [ContextMenu("Escribir posiciones en la consola")]
    void LogPositions()
    {
        foreach (Body b in _bodies)
            Debug.Log(b.id + ": X=" + b.x.ToString("F0") + "  Y=" + b.y.ToString("F0") + "  Z=" + b.z.ToString("F0"));
    }
}

