using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Panel de misión (briefing y resultado) dibujado con uGUI en lugar de IMGUI.
/// Se construye por código, no necesita EventSystem: los botones se detectan con el ratón
/// del nuevo Input System y también con el teclado (Enter/Espacio, R).
/// Las esquinas redondeadas y los degradados se generan por código (sin imágenes externas).
/// </summary>
public class MissionPanel : MonoBehaviour
{
    static readonly Color Cyan = new Color(0.16f, 0.78f, 1f, 1f);
    static readonly Color Green = new Color(0.2f, 0.92f, 0.6f, 1f);
    static readonly Color Orange = new Color(1f, 0.55f, 0.25f, 1f);

    class Btn
    {
        public RectTransform rect;
        public Image bg, glow;
        public Text label;
        public Action onClick;
        public Color color;
        public bool visible;
    }

    class ThemeItem { public Image image; public float alpha; public float pulse; }
    class Star { public Image image; public float phase, speed, baseAlpha; }

    static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();

    Canvas _canvas;
    CanvasGroup _group;
    RectTransform _card;
    Text _kicker, _title, _subtitle, _intro, _factTag, _factText;
    RectTransform _hintsRoot, _factBox;
    readonly List<GameObject> _hintCards = new List<GameObject>();
    readonly List<Star> _stars = new List<Star>();
    readonly List<ThemeItem> _themed = new List<ThemeItem>();
    Btn _primary, _secondary;
    class LevelCard { public RectTransform rect; public Image fill; public int level; public Sprite normal, bright; }
    readonly List<LevelCard> _levelCards = new List<LevelCard>();
    Action<int> _onLevel;
    bool _levelMode;
    int _preselected = 1;
    Font _font;
    float _shownAt;
    bool _isResult;
    Color _themeColor = Cyan;

    public bool IsVisible { get { return _canvas != null && _canvas.gameObject.activeSelf; } }

    public static MissionPanel Create(Transform parent)
    {
        GameObject go = new GameObject("Mission Panel");
        go.transform.SetParent(parent, false);
        MissionPanel panel = go.AddComponent<MissionPanel>();
        panel.Build();
        return panel;
    }

    // ---------------------------------------------------------------- sprites procedurales

    // Rectángulo redondeado (9-slice) con degradado vertical opcional.
    public static Sprite Rounded(int radius, Color top, Color bottom)
    {
        string key = radius + "|" + top + "|" + bottom;
        Sprite cached;
        if (SpriteCache.TryGetValue(key, out cached) && cached != null) return cached;

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            Color rowColor = Color.Lerp(bottom, top, y / (size - 1f));
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float cx = Mathf.Clamp(px, radius, size - radius);
                float cy = Mathf.Clamp(py, radius, size - radius);
                float dist = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                Color c = rowColor;
                c.a *= alpha;
                pixels[y * size + x] = c;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        SpriteCache[key] = sprite;
        return sprite;
    }

    public static Sprite Solid(int radius) { return Rounded(radius, Color.white, Color.white); }

    // ---------------------------------------------------------------- construcción

    void Build()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 24);

        GameObject canvasObject = new GameObject("Canvas");
        canvasObject.transform.SetParent(transform, false);
        _canvas = canvasObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 3000;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        _group = canvasObject.AddComponent<CanvasGroup>();
        RectTransform root = canvasObject.GetComponent<RectTransform>();

        // Fondo oscuro + estrellas parpadeantes
        Image dim = NewImage(root, "Dim", Vector2.zero, Vector2.zero, new Color(0.005f, 0.01f, 0.03f, 0.86f));
        dim.rectTransform.anchorMin = Vector2.zero;
        dim.rectTransform.anchorMax = Vector2.one;
        dim.rectTransform.offsetMin = dim.rectTransform.offsetMax = Vector2.zero;
        for (int i = 0; i < 90; i++)
        {
            float size = UnityEngine.Random.Range(2f, 4.5f);
            Image star = NewImage(root, "Star", new Vector2(size, size),
                new Vector2(UnityEngine.Random.Range(-960f, 960f), UnityEngine.Random.Range(-540f, 540f)), Color.white);
            _stars.Add(new Star
            {
                image = star,
                phase = UnityEngine.Random.value * 6.28f,
                speed = UnityEngine.Random.Range(0.6f, 2.2f),
                baseAlpha = UnityEngine.Random.Range(0.25f, 0.9f)
            });
        }

        _card = NewImage(root, "Card", new Vector2(1120f, 780f), Vector2.zero, Color.clear).rectTransform;

        // Resplandor exterior (tres capas, pulsa suavemente)
        Themed(Rounded(26, Color.white, Color.white), _card, "Glow3", new Vector2(1190f, 850f), Vector2.zero, 0.05f, 0.04f);
        Themed(Rounded(24, Color.white, Color.white), _card, "Glow2", new Vector2(1160f, 820f), Vector2.zero, 0.09f, 0.05f);
        Themed(Rounded(22, Color.white, Color.white), _card, "Glow1", new Vector2(1138f, 798f), Vector2.zero, 0.16f, 0.06f);

        // Marco doble: borde exterior de color, cuerpo oscuro, línea interior fina, relleno
        Themed(Solid(20), _card, "Border", new Vector2(1124f, 784f), Vector2.zero, 1f, 0f);
        Img(Rounded(18, new Color(0.06f, 0.1f, 0.19f), new Color(0.015f, 0.03f, 0.07f)), _card, "Body",
            new Vector2(1116f, 776f), Vector2.zero, Color.white);
        Themed(Solid(14), _card, "InnerLine", new Vector2(1094f, 754f), Vector2.zero, 0.35f, 0f);
        Img(Rounded(13, new Color(0.045f, 0.085f, 0.165f), new Color(0.012f, 0.026f, 0.06f)), _card, "InnerFill",
            new Vector2(1090f, 750f), Vector2.zero, Color.white);

        // Cabecera con degradado y barra de acento
        Img(Rounded(14, new Color(0.11f, 0.22f, 0.4f), new Color(0.04f, 0.09f, 0.19f)), _card, "Header",
            new Vector2(1058f, 124f), new Vector2(0f, 303f), Color.white);
        Themed(Solid(4), _card, "Accent", new Vector2(8f, 96f), new Vector2(-508f, 303f), 1f, 0f);
        Themed(Solid(4), _card, "AccentR", new Vector2(8f, 96f), new Vector2(508f, 303f), 1f, 0f);

        _kicker = NewText(_card, "Kicker", 22, FontStyle.Bold, Cyan, TextAnchor.MiddleCenter,
            new Vector2(900f, 30f), new Vector2(0f, 338f));
        _title = NewText(_card, "Title", 56, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter,
            new Vector2(940f, 76f), new Vector2(0f, 292f));

        // Separador con rombo central
        Themed(Solid(1), _card, "SepLeft", new Vector2(420f, 2f), new Vector2(-250f, 232f), 0.55f, 0f);
        Themed(Solid(1), _card, "SepRight", new Vector2(420f, 2f), new Vector2(250f, 232f), 0.55f, 0f);
        Image diamond = Themed(Solid(2), _card, "Diamond", new Vector2(14f, 14f), new Vector2(0f, 232f), 1f, 0f);
        diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        // Esquinas tipo "visor"
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            {
                Vector2 c = new Vector2(sx * 540f, sy * 370f);
                Themed(Solid(1), _card, "CornerH", new Vector2(56f, 5f), new Vector2(c.x - sx * 28f, c.y), 1f, 0f);
                Themed(Solid(1), _card, "CornerV", new Vector2(5f, 56f), new Vector2(c.x, c.y - sy * 28f), 1f, 0f);
            }

        _subtitle = NewText(_card, "Subtitle", 28, FontStyle.Italic, new Color(0.65f, 0.82f, 1f), TextAnchor.MiddleCenter,
            new Vector2(1000f, 40f), new Vector2(0f, 196f));
        _intro = NewText(_card, "Intro", 30, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter,
            new Vector2(1000f, 44f), new Vector2(0f, 148f));

        _hintsRoot = NewImage(_card, "Hints", new Vector2(1000f, 400f), new Vector2(0f, -50f), Color.clear).rectTransform;

        // Caja del dato curioso (resultado)
        Image factFrame = Themed(Solid(18), _card, "FactFrame", new Vector2(984f, 254f), new Vector2(0f, -15f), 0.7f, 0f);
        _factBox = factFrame.rectTransform;
        Img(Rounded(16, new Color(0.08f, 0.15f, 0.26f), new Color(0.03f, 0.07f, 0.14f)), _factBox, "FactBody",
            new Vector2(980f, 250f), Vector2.zero, Color.white);
        _factTag = NewText(_factBox, "FactTag", 24, FontStyle.Bold, Cyan, TextAnchor.MiddleCenter,
            new Vector2(900f, 34f), new Vector2(0f, 90f));
        _factText = NewText(_factBox, "FactText", 32, FontStyle.Normal, Color.white, TextAnchor.MiddleCenter,
            new Vector2(900f, 170f), new Vector2(0f, -20f));
        _factText.resizeTextForBestFit = true;
        _factText.resizeTextMinSize = 20;
        _factText.resizeTextMaxSize = 32;

        _primary = NewButton("Primary", new Vector2(430f, 86f));
        _secondary = NewButton("Secondary", new Vector2(360f, 86f));

        canvasObject.SetActive(false);
    }

    Image Img(Sprite sprite, Transform parent, string name, Vector2 size, Vector2 pos, Color color)
    {
        Image img = NewImage(parent, name, size, pos, color);
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        return img;
    }

    // Imagen que toma el color del tema (cian / verde / naranja).
    Image Themed(Sprite sprite, Transform parent, string name, Vector2 size, Vector2 pos, float alpha, float pulse)
    {
        Image img = Img(sprite, parent, name, size, pos, new Color(Cyan.r, Cyan.g, Cyan.b, alpha));
        _themed.Add(new ThemeItem { image = img, alpha = alpha, pulse = pulse });
        return img;
    }

    Image NewImage(Transform parent, string name, Vector2 size, Vector2 pos, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    Text NewText(Transform parent, string name, int size, FontStyle style, Color color,
                 TextAnchor anchor, Vector2 box, Vector2 pos)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = box;
        rt.anchoredPosition = pos;
        Text t = go.GetComponent<Text>();
        t.font = _font;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    Btn NewButton(string name, Vector2 size)
    {
        Image glow = Img(Solid(24), _card, name + "Glow", size + new Vector2(18f, 18f), Vector2.zero,
            new Color(Cyan.r, Cyan.g, Cyan.b, 0.3f));
        Image bg = Img(Rounded(20, Color.white, new Color(0.72f, 0.8f, 0.9f)), glow.transform, name, size, Vector2.zero, Cyan);
        Text label = NewText(bg.transform, "Label", 30, FontStyle.Bold, new Color(0.01f, 0.05f, 0.1f),
            TextAnchor.MiddleCenter, size, Vector2.zero);
        return new Btn { rect = glow.rectTransform, bg = bg, glow = glow, label = label, color = Cyan };
    }

    // ---------------------------------------------------------------- API pública

    public void ShowLevelSelect(int preselected, Action<int> onSelected)
    {
        _isResult = false;
        _levelMode = true;
        _onLevel = onSelected;
        _preselected = Mathf.Clamp(preselected, 1, 3);
        SetTheme(Cyan);
        _kicker.text = "CENTRO DE CONTROL  ·  ASTROPATH";
        _title.text = "ELIGE TU NIVEL";
        _subtitle.text = "¿Hasta dónde quieres llegar?";
        _intro.text = "";

        _factBox.gameObject.SetActive(false);
        _hintsRoot.gameObject.SetActive(true);
        foreach (GameObject g in _hintCards) Destroy(g);
        _hintCards.Clear();
        _levelCards.Clear();
        _themed.RemoveAll(x => x.image == null);

        string[] names = { "EXPLORADOR", "NAVEGANTE", "COMANDANTE" };
        string[] descriptions =
        {
            "Solo planetas del sistema solar.",
            "Planetas y los principales satélites: la Luna, las lunas de Júpiter, Titán y Tritón.",
            "Cualquier cuerpo celeste: planetas, satélites y planetas enanos."
        };
        for (int i = 0; i < 3; i++)
        {
            Vector2 pos = new Vector2((i - 1) * 335f, 5f);
            Image frame = Themed(Solid(20), _hintsRoot, "Level " + (i + 1), new Vector2(310f, 330f), pos, 0.55f, 0f);
            Sprite normalSprite = Rounded(18, new Color(0.1f, 0.18f, 0.31f), new Color(0.04f, 0.09f, 0.18f));
            Sprite brightSprite = Rounded(18, new Color(0.2f, 0.36f, 0.58f), new Color(0.07f, 0.16f, 0.3f));
            Image fill = Img(normalSprite, frame.transform, "Fill", new Vector2(304f, 324f), Vector2.zero, Color.white);
            Image badge = Themed(Rounded(30, Color.white, new Color(0.7f, 0.85f, 0.97f)), frame.transform, "Badge",
                new Vector2(84f, 84f), new Vector2(0f, 95f), 1f, 0f);
            Text num = NewText(badge.transform, "Num", 52, FontStyle.Bold, new Color(0.01f, 0.05f, 0.1f),
                TextAnchor.MiddleCenter, new Vector2(84f, 84f), Vector2.zero);
            num.text = (i + 1).ToString();
            Text name = NewText(frame.transform, "Name", 30, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter,
                new Vector2(280f, 40f), new Vector2(0f, 20f));
            name.text = names[i];
            Text desc = NewText(frame.transform, "Desc", 20, FontStyle.Normal, new Color(0.78f, 0.88f, 1f),
                TextAnchor.UpperCenter, new Vector2(276f, 140f), new Vector2(0f, -90f));
            desc.text = descriptions[i];
            _hintCards.Add(frame.gameObject);
            _levelCards.Add(new LevelCard { rect = frame.rectTransform, fill = fill, level = i + 1, normal = normalSprite, bright = brightSprite });
        }
        ApplyThemeColors();

        SetButton(_primary, null, Vector2.zero, Cyan, null);
        SetButton(_secondary, null, Vector2.zero, Cyan, null);
        Open();
    }

    public void ShowBriefing(string kindPhrase, string[] hints, Action onAccept)
    {
        _levelMode = false;
        _levelCards.Clear();
        _isResult = false;
        SetTheme(Cyan);
        _kicker.text = "CENTRO DE CONTROL  ·  ASTROPATH";
        _title.text = "MISIÓN DE VUELO";
        _subtitle.text = "Tu destino está entre las estrellas";
        _intro.text = "Aterriza en " + kindPhrase + " que...";

        _factBox.gameObject.SetActive(false);
        _hintsRoot.gameObject.SetActive(true);
        foreach (GameObject g in _hintCards) Destroy(g);
        _hintCards.Clear();
        _themed.RemoveAll(x => x.image == null);

        int count = hints != null ? hints.Length : 0;
        float cardHeight = 92f, gap = 14f;
        float top = (count * cardHeight + (count - 1) * gap) * 0.5f - cardHeight * 0.5f;
        for (int i = 0; i < count; i++)
        {
            Vector2 pos = new Vector2(0f, top - i * (cardHeight + gap));
            Image frame = Themed(Solid(16), _hintsRoot, "Hint " + (i + 1), new Vector2(1004f, cardHeight + 4f), pos, 0.45f, 0f);
            Img(Rounded(14, new Color(0.1f, 0.18f, 0.31f), new Color(0.05f, 0.1f, 0.2f)), frame.transform, "Fill",
                new Vector2(1000f, cardHeight), Vector2.zero, Color.white);
            Image badge = Themed(Rounded(12, Color.white, new Color(0.75f, 0.85f, 0.95f)), frame.transform, "Badge",
                new Vector2(68f, cardHeight - 20f), new Vector2(-445f, 0f), 1f, 0f);
            Text num = NewText(badge.transform, "Num", 38, FontStyle.Bold, new Color(0.01f, 0.05f, 0.1f),
                TextAnchor.MiddleCenter, new Vector2(68f, cardHeight - 20f), Vector2.zero);
            num.text = (i + 1).ToString();
            Text body = NewText(frame.transform, "Text", 28, FontStyle.Normal, Color.white, TextAnchor.MiddleLeft,
                new Vector2(860f, cardHeight - 12f), new Vector2(40f, 0f));
            body.resizeTextForBestFit = true;
            body.resizeTextMinSize = 18;
            body.resizeTextMaxSize = 28;
            body.text = Capitalize(hints[i]) + ".";
            _hintCards.Add(frame.gameObject);
        }
        ApplyThemeColors();

        SetButton(_primary, "COMENZAR MISIÓN", new Vector2(0f, -310f), Cyan, onAccept);
        SetButton(_secondary, null, Vector2.zero, Cyan, null);
        Open();
    }

    public void ShowResult(bool success, string heading, string headline, string factTitle, string fact,
                           Action onAnother, Action onExit)
    {
        _levelMode = false;
        _levelCards.Clear();
        _isResult = true;
        SetTheme(success ? Green : Orange);
        _kicker.text = "INFORME DE MISIÓN";
        _title.text = heading;
        _subtitle.text = "";
        _intro.text = headline;
        _intro.rectTransform.sizeDelta = new Vector2(1000f, 90f);
        _intro.rectTransform.anchoredPosition = new Vector2(0f, 170f);

        _hintsRoot.gameObject.SetActive(false);
        _factBox.gameObject.SetActive(true);
        _factTag.text = factTitle;
        _factTag.color = _themeColor;
        _factText.text = fact;

        SetButton(_primary, "OTRA MISIÓN", new Vector2(-215f, -310f), _themeColor, onAnother);
        SetButton(_secondary, "SALIR DEL JUEGO", new Vector2(235f, -310f), new Color(0.6f, 0.68f, 0.8f), onExit);
        Open();
    }

    public void Hide()
    {
        if (_canvas != null) _canvas.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------- interno

    void SetTheme(Color c)
    {
        _themeColor = c;
        _kicker.color = c;
        _title.color = c == Cyan ? Color.white : c;
        _intro.rectTransform.sizeDelta = new Vector2(1000f, 44f);
        _intro.rectTransform.anchoredPosition = new Vector2(0f, 148f);
        ApplyThemeColors();
    }

    void ApplyThemeColors()
    {
        foreach (ThemeItem t in _themed)
            if (t.image != null)
                t.image.color = new Color(_themeColor.r, _themeColor.g, _themeColor.b, t.alpha);
    }

    void SetButton(Btn b, string text, Vector2 pos, Color color, Action onClick)
    {
        b.visible = text != null;
        b.rect.gameObject.SetActive(b.visible);
        if (!b.visible) return;
        b.label.text = text;
        b.rect.anchoredPosition = pos;
        b.color = color;
        b.onClick = onClick;
        b.glow.color = new Color(color.r, color.g, color.b, 0.3f);
    }

    void Open()
    {
        _canvas.gameObject.SetActive(true);
        _shownAt = Time.unscaledTime;
        _group.alpha = 0f;
    }

    static string Capitalize(string s)
    {
        return string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }

    void UpdateLevelSelect(Vector2 mouse, bool click, float pulse)
    {
        int chosen = 0;
        int hovered = 0;
        foreach (LevelCard card in _levelCards)
            if (RectTransformUtility.RectangleContainsScreenPoint(card.rect, mouse, null)) hovered = card.level;
        int highlight = hovered != 0 ? hovered : _preselected;

        foreach (LevelCard card in _levelCards)
        {
            bool on = card.level == highlight;
            card.rect.localScale = Vector3.one * (on ? 1.04f + 0.01f * pulse : 1f);
            card.fill.sprite = on ? card.bright : card.normal;
        }
        if (click && hovered != 0) chosen = hovered;

        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) chosen = 1;
            else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) chosen = 2;
            else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) chosen = 3;
            else if (kb.leftArrowKey.wasPressedThisFrame) _preselected = Mathf.Max(1, _preselected - 1);
            else if (kb.rightArrowKey.wasPressedThisFrame) _preselected = Mathf.Min(3, _preselected + 1);
            else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) chosen = _preselected;
        }
        if (chosen != 0 && _onLevel != null)
        {
            Action<int> callback = _onLevel;
            _onLevel = null;
            _levelMode = false;
            callback(chosen);
        }
    }

    bool Hover(Btn b, Vector2 mouse)
    {
        return b.visible && RectTransformUtility.RectangleContainsScreenPoint(b.rect, mouse, null);
    }

    void Update()
    {
        if (!IsVisible) return;

        float t = Time.unscaledTime - _shownAt;
        _group.alpha = Mathf.Clamp01(t / 0.35f);
        float pop = Mathf.SmoothStep(0.94f, 1f, Mathf.Clamp01(t / 0.35f));
        _card.localScale = new Vector3(pop, pop, 1f);

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2f);
        foreach (ThemeItem th in _themed)
        {
            if (th.pulse <= 0f || th.image == null) continue;
            Color c = th.image.color;
            c.a = th.alpha + th.pulse * (pulse - 0.5f) * 2f;
            th.image.color = c;
        }
        foreach (Star s in _stars)
        {
            Color c = s.image.color;
            c.a = s.baseAlpha * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * s.speed + s.phase));
            s.image.color = c;
        }

        Mouse mouse = Mouse.current;
        Vector2 pos = mouse != null ? mouse.position.ReadValue() : new Vector2(-1f, -1f);
        bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;

        if (_levelMode)
        {
            UpdateLevelSelect(pos, click, pulse);
            return;
        }
        Btn[] buttons = { _primary, _secondary };
        foreach (Btn b in buttons)
        {
            if (!b.visible) continue;
            bool hover = Hover(b, pos);
            float breathe = (b == _primary && !hover) ? 1f + 0.02f * pulse : 1f;
            b.rect.localScale = Vector3.one * (hover ? 1.05f : breathe);
            b.bg.color = hover ? Color.Lerp(b.color, Color.white, 0.35f) : b.color;
            Color g = b.glow.color; g.a = hover ? 0.5f : 0.22f + 0.14f * pulse; b.glow.color = g;
            if (hover && click && b.onClick != null) { b.onClick(); return; }
        }

        Keyboard kb = Keyboard.current;
        if (kb == null || _primary.onClick == null) return;
        if (!_isResult && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame ||
                           kb.spaceKey.wasPressedThisFrame))
            _primary.onClick();
        else if (_isResult && kb.rKey.wasPressedThisFrame)
            _primary.onClick();
    }
}
