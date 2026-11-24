using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;

/// <summary>
/// Velocímetro y radar como instrumentos de cabina (uGUI, sin IMGUI).
///
/// Por defecto se dibujan sobre la pantalla en las dos esquinas inferiores, con aspecto de pantallas
/// de consola (marco metálico, tornillos, cristal). Para INTEGRARLOS físicamente en la cabina, crea
/// dos objetos vacíos dentro de la cabina (por ejemplo dentro de "Cockpit3_WithInterior"), colócalos
/// donde quieras las pantallas, y arrástralos a "Speed Mount" y "Radar Mount". El instrumento se
/// convierte entonces en un Canvas en espacio de mundo pegado a ese objeto.
/// </summary>
[DefaultExecutionOrder(30)]
public class CockpitHud : MonoBehaviour
{
    [Header("Integración en la cabina (opcional)")]
    [SerializeField] Transform _speedMount;
    [SerializeField] Transform _radarMount;
    [Tooltip("Anchura del panel en unidades de escena cuando va montado en la cabina")]
    [SerializeField, Min(0.01f)] float _speedWorldWidth = 0.5f;
    [SerializeField, Min(0.01f)] float _radarWorldWidth = 0.5f;

    [Header("Colocación automática en la cabina")]
    [Tooltip("Si no hay montajes asignados, los instrumentos se pegan a la cámara de la cabina, sobre las consolas laterales")]
    [SerializeField] bool _autoPlaceInCockpit = false;
    [Tooltip("Dirección en la vista de cabina: x derecha, y arriba, como tangente del ángulo (0,0 = centro de la pantalla)")]
    [SerializeField] Vector2 _speedAim = new Vector2(-0.77f, -0.24f);
    [SerializeField] Vector2 _radarAim = new Vector2(0.79f, -0.24f);
    [Tooltip("Distancia de los paneles a la cámara. Si no se ven (los tapa la cabina), reduce el valor; mínimo 0,31 (plano cercano)")]
    [SerializeField, Min(0.31f)] float _cockpitDistance = 0.6f;
    [Tooltip("Anchura aparente del velocímetro en la vista (tangente). 0,5 = ~1/5 del ancho de la pantalla")]
    [SerializeField, Min(0.05f)] float _panelAngularWidth = 0.5f;

    [Header("Pantalla (cuando no hay montaje)")]
    [SerializeField, Range(0.5f, 1.5f)] float _overlayScale = 1f;
    [Tooltip("El radar es el instrumento principal: se dibuja algo más grande que el velocímetro")]
    [SerializeField, Range(0.8f, 1.6f)] float _radarScale = 1.2f;
    [SerializeField] bool _showBodyNames = true;

    static readonly Color Cyan = new Color(0.25f, 0.88f, 1f, 1f);
    static readonly Color Amber = new Color(1f, 0.6f, 0.25f, 1f);

    const float SpeedW = 360f, SpeedH = 330f;
    const float RadarW = 340f, RadarH = 392f, RadarR = 136f, RadarCy = -6f;

    static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

    Font _font;
    Canvas _speedCanvas, _radarCanvas;
    ShipController _ship;
    readonly List<SolarSystemManager.RadarContact> _contacts = new List<SolarSystemManager.RadarContact>();

    // Velocímetro
    Image _speedFill, _needle, _speedGlow;
    Text _speedValue, _speedUnits, _speedMode;

    // Radar
    RectTransform _sweep;
    Text _radarFooter;
    readonly List<Image> _dots = new List<Image>();
    readonly List<Image> _halos = new List<Image>();
    readonly List<Image> _stalks = new List<Image>();
    readonly List<Text> _names = new List<Text>();
    readonly Text[] _ringLabels = new Text[3];
    RectTransform _radarContent;
    Sprite _dotSprite;
    float _shownSpeed;

    // ---------------------------------------------------------------- sprites procedurales

    static Sprite Disc()
    {
        return RingSprite("disc", 0f);
    }

    // Anillo de radio exterior 1 e interior 'inner' (0 = disco lleno). Textura sin borde, para Image Simple/Filled.
    static Sprite RingSprite(string key, float inner)
    {
        string k = key + inner;
        Sprite cached;
        if (Sprites.TryGetValue(k, out cached) && cached != null) return cached;
        const int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color[] px = new Color[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float outer = Mathf.Clamp01((1f - r) * half);
                float a = outer;
                if (inner > 0f) a *= Mathf.Clamp01((r - inner) * half);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        Sprite sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        Sprites[k] = sp;
        return sp;
    }

    static Sprite Ring(float radiusPx, float thicknessPx)
    {
        return RingSprite("ring", Mathf.Clamp(1f - thicknessPx / radiusPx, 0f, 0.99f));
    }

    // ---------------------------------------------------------------- construcción

    void Awake()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 24);
        _dotSprite = Disc();

        if (_autoPlaceInCockpit && _speedMount == null && _radarMount == null)
        {
            ShipController ship = FindAnyObjectByType<ShipController>();
            CinemachineCamera cockpitCamera = ship != null ? ship.GetComponentInChildren<CinemachineCamera>(true) : null;
            if (cockpitCamera != null)
            {
                _speedMount = MakeMount(cockpitCamera.transform, "Speed Mount", _speedAim);
                _radarMount = MakeMount(cockpitCamera.transform, "Radar Mount", _radarAim);
                _speedWorldWidth = _panelAngularWidth * _cockpitDistance;
                _radarWorldWidth = _speedWorldWidth * (RadarW / SpeedW);   // misma escala de píxel en ambos
            }
        }

        _speedCanvas = MakeCanvas("Speed Instrument", _speedMount, new Vector2(SpeedW, SpeedH), _speedWorldWidth, new Vector2(0f, 0f), 100);
        BuildSpeedometer(_speedCanvas.transform.GetChild(0) as RectTransform);
        _radarCanvas = MakeCanvas("Radar Instrument", _radarMount, new Vector2(RadarW, RadarH), _radarWorldWidth, new Vector2(1f, 0f), 101, _radarScale);
        BuildRadar(_radarCanvas.transform.GetChild(0) as RectTransform);
    }

    // Objeto vacío fijo respecto a la cámara de cabina, en la dirección 'aim', mirando hacia fuera de la cámara
    // (la cara visible de un Canvas en espacio de mundo es la que mira a -Z).
    Transform MakeMount(Transform cam, string name, Vector2 aim)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(cam, false);
        Vector3 p = new Vector3(aim.x, aim.y, 1f) * _cockpitDistance;
        go.transform.localPosition = p;
        go.transform.localRotation = Quaternion.LookRotation(p.normalized, Vector3.up);
        return go.transform;
    }

    // Crea el Canvas (pantalla o espacio de mundo) y devuelve el Canvas con un hijo "Root" donde se dibuja.
    Canvas MakeCanvas(string name, Transform mount, Vector2 size, float worldWidth, Vector2 corner, int order, float extraScale = 1f)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
        Canvas canvas = go.GetComponent<Canvas>();
        GameObject rootGo = new GameObject("Root", typeof(RectTransform));
        RectTransform root = rootGo.GetComponent<RectTransform>();

        if (mount == null)
        {
            go.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            root.SetParent(go.transform, false);
            root.anchorMin = root.anchorMax = root.pivot = corner;
            root.sizeDelta = size;
            root.anchoredPosition = new Vector2(corner.x > 0.5f ? -26f : 26f, 24f);
            root.localScale = Vector3.one * _overlayScale * extraScale;
        }
        else
        {
            go.transform.SetParent(mount, false);
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform crt = go.GetComponent<RectTransform>();
            crt.sizeDelta = size;
            crt.localPosition = Vector3.zero;
            crt.localRotation = Quaternion.identity;
            crt.localScale = Vector3.one * (worldWidth / size.x);
            root.SetParent(go.transform, false);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = size;
            root.anchoredPosition = Vector2.zero;
            go.layer = mount.gameObject.layer;
            rootGo.layer = mount.gameObject.layer;
        }
        return canvas;
    }

    static void SetLayerRecursive(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++) SetLayerRecursive(t.GetChild(i), layer);
    }

    Image Img(Transform parent, string name, Vector2 size, Vector2 pos, Color color, Sprite sprite = null, bool sliced = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    Text Label(Transform parent, string name, string text, int size, FontStyle style, Color color,
               Vector2 box, Vector2 pos, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = box;
        rt.anchoredPosition = pos;
        Text t = go.GetComponent<Text>();
        t.font = _font;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    // Marco metálico con tornillos y cristal oscuro.
    void BuildBezel(RectTransform root, Vector2 size)
    {
        Img(root, "Shadow", size + new Vector2(14f, 14f), new Vector2(0f, -4f), new Color(0f, 0f, 0f, 0.35f),
            MissionPanel.Rounded(28, Color.white, Color.white), true);
        Img(root, "Bezel", size, Vector2.zero, Color.white,
            MissionPanel.Rounded(26, new Color(0.3f, 0.33f, 0.39f), new Color(0.09f, 0.1f, 0.14f)), true);
        Img(root, "BezelEdge", size - new Vector2(10f, 10f), Vector2.zero, new Color(0.02f, 0.03f, 0.05f, 1f),
            MissionPanel.Rounded(22, Color.white, Color.white), true);
        Img(root, "Glass", size - new Vector2(18f, 18f), Vector2.zero, Color.white,
            MissionPanel.Rounded(18, new Color(0.02f, 0.075f, 0.1f), new Color(0.004f, 0.02f, 0.035f)), true);

        float sx = size.x * 0.5f - 13f, sy = size.y * 0.5f - 13f;
        for (int ix = -1; ix <= 1; ix += 2)
            for (int iy = -1; iy <= 1; iy += 2)
            {
                Vector2 p = new Vector2(ix * sx, iy * sy);
                Img(root, "Screw", new Vector2(11f, 11f), p, new Color(0.62f, 0.65f, 0.72f), Disc());
                Img(root, "ScrewHole", new Vector2(5f, 5f), p, new Color(0.15f, 0.16f, 0.2f), Disc());
            }
    }

    void BuildSpeedometer(RectTransform root)
    {
        BuildBezel(root, new Vector2(SpeedW, SpeedH));
        Label(root, "Title", "VELOCIDAD", 18, FontStyle.Bold, Cyan, new Vector2(300f, 24f), new Vector2(0f, 136f));

        Vector2 c = new Vector2(0f, 4f);
        const float R = 112f;

        _speedGlow = Img(root, "Glow", new Vector2(R * 2f + 26f, R * 2f + 26f), c, new Color(Cyan.r, Cyan.g, Cyan.b, 0.07f), Disc());
        Image track = Img(root, "Track", new Vector2(R * 2f, R * 2f), c, new Color(0.1f, 0.3f, 0.36f, 0.45f), Ring(R, 9f));
        track.type = Image.Type.Filled;
        track.fillMethod = Image.FillMethod.Radial360;
        track.fillOrigin = (int)Image.Origin360.Bottom;
        track.fillClockwise = true;
        track.fillAmount = 0.75f;
        track.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);

        _speedFill = Img(root, "Fill", new Vector2(R * 2f, R * 2f), c, Cyan, Ring(R, 9f));
        _speedFill.type = Image.Type.Filled;
        _speedFill.fillMethod = Image.FillMethod.Radial360;
        _speedFill.fillOrigin = (int)Image.Origin360.Bottom;
        _speedFill.fillClockwise = true;
        _speedFill.fillAmount = 0f;
        _speedFill.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);

        // Marcas cada 5 % y números cada 20 %
        for (int i = 0; i <= 20; i++)
        {
            float p = i / 20f;
            float z = 135f - 270f * p;                           // grados CCW desde "arriba"
            Vector2 dir = new Vector2(-Mathf.Sin(z * Mathf.Deg2Rad), Mathf.Cos(z * Mathf.Deg2Rad));
            bool major = i % 4 == 0;
            Image tick = Img(root, "Tick", new Vector2(major ? 3f : 2f, major ? 15f : 8f),
                c + dir * (R - 21f), new Color(0.6f, 0.88f, 0.95f, major ? 0.95f : 0.55f));
            tick.rectTransform.localRotation = Quaternion.Euler(0f, 0f, z);
            if (major)
                Label(root, "Num", (i * 5).ToString(), 14, FontStyle.Bold, new Color(0.7f, 0.9f, 1f, 0.9f),
                    new Vector2(40f, 20f), c + dir * (R - 42f));
        }

        // Aguja
        _needle = Img(root, "Needle", new Vector2(4f, R - 14f), c, new Color(1f, 0.3f, 0.25f, 1f));
        _needle.rectTransform.pivot = new Vector2(0.5f, 0f);
        Img(root, "Hub", new Vector2(24f, 24f), c, new Color(0.7f, 0.74f, 0.8f), Disc());
        Img(root, "HubCore", new Vector2(12f, 12f), c, new Color(0.12f, 0.13f, 0.17f), Disc());

        _speedValue = Label(root, "Value", "0", 44, FontStyle.Bold, Color.white, new Vector2(200f, 52f), c + new Vector2(0f, -62f));
        _speedUnits = Label(root, "Units", "0 u/s", 16, FontStyle.Normal, new Color(0.65f, 0.82f, 0.9f), new Vector2(240f, 22f), c + new Vector2(0f, -98f));
        _speedMode = Label(root, "Mode", "", 13, FontStyle.Bold, Amber, new Vector2(200f, 18f), c + new Vector2(0f, 52f));

        // Reflejo del cristal
        Img(root, "Reflection", new Vector2(SpeedW - 30f, 110f), new Vector2(0f, 90f), new Color(1f, 1f, 1f, 1f),
            MissionPanel.Rounded(18, new Color(1f, 1f, 1f, 0.07f), new Color(1f, 1f, 1f, 0f)), true);
    }

    void BuildRadar(RectTransform root)
    {
        BuildBezel(root, new Vector2(RadarW, RadarH));
        Label(root, "Title", "RADAR DE NAVEGACIÓN", 17, FontStyle.Bold, Cyan, new Vector2(300f, 24f), new Vector2(0f, 168f));

        Vector2 c = new Vector2(0f, RadarCy);
        _radarContent = Img(root, "Content", new Vector2(RadarR * 2f, RadarR * 2f), c, Color.clear).rectTransform;

        Img(root, "OuterRing", new Vector2(RadarR * 2f + 22f, RadarR * 2f + 22f), c, new Color(0.34f, 0.37f, 0.44f), Disc());
        Img(root, "InnerRing", new Vector2(RadarR * 2f + 12f, RadarR * 2f + 12f), c, new Color(0.04f, 0.05f, 0.08f), Disc());
        Img(root, "Screen", new Vector2(RadarR * 2f, RadarR * 2f), c, new Color(0.01f, 0.07f, 0.09f, 1f), Disc());

        Color line = new Color(0.2f, 0.75f, 0.85f, 0.4f);
        for (int i = 1; i <= 3; i++)
        {
            float r = RadarR * i / 3f;
            Img(root, "RangeRing", new Vector2(r * 2f, r * 2f), c, line, Ring(r, 1.5f));
        }
        Img(root, "CrossH", new Vector2(RadarR * 2f - 4f, 1.5f), c, line);
        Img(root, "CrossV", new Vector2(1.5f, RadarR * 2f - 4f), c, line);

        // Rótulos de distancia sobre los anillos (en UA)
        for (int i = 0; i < 3; i++)
        {
            float r = RadarR * (i + 1) / 3f;
            Vector2 p = c + new Vector2(r * 0.7071f, r * 0.7071f);
            _ringLabels[i] = Label(root, "RingLabel", "", 10, FontStyle.Normal, new Color(0.5f, 0.85f, 0.95f, 0.8f),
                new Vector2(60f, 14f), p + new Vector2(4f, -2f), TextAnchor.MiddleLeft);
        }

        // Barrido
        Image sweep = Img(root, "Sweep", new Vector2(RadarR * 2f, RadarR * 2f), c, new Color(0.25f, 0.95f, 1f, 0.13f), Disc());
        sweep.type = Image.Type.Filled;
        sweep.fillMethod = Image.FillMethod.Radial360;
        sweep.fillOrigin = (int)Image.Origin360.Top;
        sweep.fillClockwise = true;
        sweep.fillAmount = 0.14f;
        _sweep = sweep.rectTransform;

        // Marca de proa
        Label(root, "Bow", "PROA", 11, FontStyle.Bold, new Color(0.6f, 0.9f, 1f, 0.9f), new Vector2(60f, 14f), c + new Vector2(0f, RadarR + 4f));

        // Nave en el centro
        Img(root, "ShipHalo", new Vector2(18f, 18f), c, new Color(1f, 1f, 1f, 0.9f), Disc());
        Img(root, "ShipCore", new Vector2(10f, 10f), c, new Color(1f, 0.15f, 0.2f, 1f), Disc());

        _radarFooter = Label(root, "Footer", "", 12, FontStyle.Normal, new Color(0.72f, 0.86f, 0.94f, 0.95f),
            new Vector2(300f, 18f), new Vector2(0f, -170f));

        Img(root, "Reflection", new Vector2(RadarW - 30f, 90f), new Vector2(0f, 146f), Color.white,
            MissionPanel.Rounded(18, new Color(1f, 1f, 1f, 0.06f), new Color(1f, 1f, 1f, 0f)), true);

        // Los contactos se dibujan dentro de _radarContent (centrado en la pantalla del radar)
        _radarContent.SetAsLastSibling();
        _radarContent.sizeDelta = new Vector2(RadarR * 2f, RadarR * 2f);
    }

    // ---------------------------------------------------------------- actualización

    void Update()
    {
        SolarSystemManager manager = SolarSystemManager.Instance;
        bool show = manager != null && manager.ShowFlightHud;
        if (_speedCanvas != null && _speedCanvas.gameObject.activeSelf != show) _speedCanvas.gameObject.SetActive(show);
        if (_radarCanvas != null && _radarCanvas.gameObject.activeSelf != show) _radarCanvas.gameObject.SetActive(show);
        if (!show) return;

        if (_ship == null) _ship = FindAnyObjectByType<ShipController>();
        UpdateSpeedometer();
        UpdateRadar(manager);
    }

    void UpdateSpeedometer()
    {
        if (_ship == null) return;
        float signed = _ship.SpeedPercent;
        _shownSpeed = Mathf.Lerp(_shownSpeed, signed, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
        float mag = Mathf.Abs(_shownSpeed) / 100f;
        bool reverse = _shownSpeed < -0.5f;
        Color tint = reverse ? Amber : Color.Lerp(Cyan, new Color(1f, 0.45f, 0.3f), Mathf.Pow(mag, 3f));

        _speedFill.fillAmount = 0.75f * mag;
        _speedFill.color = tint;
        _speedGlow.color = new Color(tint.r, tint.g, tint.b, 0.05f + 0.07f * mag);
        float z = reverse ? 135f - 270f * mag : 135f - 270f * mag;
        _needle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, z);
        _speedValue.text = Mathf.RoundToInt(Mathf.Abs(signed)).ToString();
        _speedValue.color = reverse ? Amber : Color.white;
        _speedUnits.text = Mathf.Abs(_ship.CurrentSpeed).ToString("N0") + " u/s";
        _speedMode.text = reverse ? "MARCHA ATRÁS" : (mag > 0.995f ? "VELOCIDAD MÁXIMA" : "");
    }

    void UpdateRadar(SolarSystemManager manager)
    {
        _sweep.localRotation = Quaternion.Euler(0f, 0f, -Time.unscaledTime * 110f % 360f);
        _radarFooter.text = "PROA ↑  ·  ALCANCE " + manager.RadarRangeAu.ToString("0.#") + " UA";

        for (int i = 0; i < 3; i++)
        {
            float au = manager.GetRadarRingAu((i + 1) / 3f);
            _ringLabels[i].text = (au < 1f ? au.ToString("0.00") : au.ToString("0.#")) + " UA";
        }

        manager.FillRadarContacts(_contacts);
        EnsurePool(_contacts.Count);
        for (int i = 0; i < _dots.Count; i++)
        {
            bool used = i < _contacts.Count;
            _names[i].gameObject.SetActive(used && _showBodyNames && _contacts[i].showLabel);
            _dots[i].gameObject.SetActive(used);
            _halos[i].gameObject.SetActive(used && _contacts[i].isSun);
            _stalks[i].gameObject.SetActive(used);
            if (!used) continue;

            SolarSystemManager.RadarContact contact = _contacts[i];
            Vector2 p = contact.position * RadarR;
            float s = Mathf.Max(5f, contact.size * 1.15f);
            _dots[i].rectTransform.anchoredPosition = p;
            _dots[i].rectTransform.sizeDelta = new Vector2(s, s);
            _dots[i].color = contact.color;
            if (_names[i].gameObject.activeSelf)
            {
                // El nombre va a la derecha del punto, o a la izquierda si el punto está cerca del borde derecho
                bool left = p.x > RadarR * 0.45f;
                _names[i].alignment = left ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
                _names[i].rectTransform.anchoredPosition = p + new Vector2(left ? -(s * 0.5f + 4f) - 50f : (s * 0.5f + 4f) + 50f, 0f);
                _names[i].text = contact.label;
                _names[i].color = new Color(contact.color.r, contact.color.g, contact.color.b, Mathf.Min(1f, contact.color.a + 0.25f));
            }
            if (contact.isSun)
            {
                _halos[i].rectTransform.anchoredPosition = p;
                _halos[i].rectTransform.sizeDelta = new Vector2(s + 10f, s + 10f);
                _halos[i].color = new Color(1f, 0.42f, 0.06f, 0.3f);
            }

            // Marca de altura: trazo hacia arriba (por encima de la nave) o hacia abajo (por debajo)
            float len = contact.elevation * 14f;
            bool tick = Mathf.Abs(len) > 1.5f;
            _stalks[i].gameObject.SetActive(tick);
            if (tick)
            {
                _stalks[i].rectTransform.anchoredPosition = p + new Vector2(0f, len * 0.5f);
                _stalks[i].rectTransform.sizeDelta = new Vector2(1.5f, Mathf.Abs(len));
                _stalks[i].color = new Color(contact.color.r, contact.color.g, contact.color.b, contact.color.a * 0.6f);
            }
        }
    }

    void EnsurePool(int count)
    {
        while (_dots.Count < count)
        {
            _halos.Add(Img(_radarContent, "Halo", new Vector2(10f, 10f), Vector2.zero, Color.white, _dotSprite));
            _stalks.Add(Img(_radarContent, "Stalk", new Vector2(1.5f, 10f), Vector2.zero, Color.white));
            _dots.Add(Img(_radarContent, "Dot", new Vector2(8f, 8f), Vector2.zero, Color.white, _dotSprite));
            _names.Add(Label(_radarContent, "Name", "", 11, FontStyle.Bold, Color.white, new Vector2(100f, 14f), Vector2.zero));
        }
    }
}
