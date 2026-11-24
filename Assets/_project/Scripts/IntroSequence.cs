using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Intro al arrancar el juego: unas líneas de presentación y, al final, el nombre del juego con
/// letras muy espaciadas, resplandor y un haz de luz horizontal (estilo "título de película").
/// Se salta con clic, Enter o Espacio. Se construye por código con uGUI.
/// </summary>
public class IntroSequence : MonoBehaviour
{
    const string GameTitle = "ASTROPATH";
    const string Tagline = "E X P L O R A     ·     D E S C U B R E     ·     A T E R R I Z A";

    static readonly string[] Lines =
    {
        "Desde siempre, la humanidad ha mirado al cielo.",
        "Ocho planetas. Decenas de lunas.\nMundos de hielo, de fuego y océanos ocultos.",
        "Ahora, el sistema solar está a tu alcance.",
        "Tu nave te espera."
    };

    const float LineTime = 3.1f, FadeTime = 0.7f;
    const float LetterSpacing = 144f, LetterDelay = 0.16f;

    static readonly Color Ice = new Color(0.55f, 0.85f, 1f, 1f);

    Canvas _canvas;
    CanvasGroup _group;
    Font _font, _titleFont;
    Text _line;
    Text[] _letters, _letterGlows;
    Text _tagline;
    Image _streakGlow, _streakCore, _flare, _titleHalo;
    Image[] _titleRules;
    Image[] _stars;
    float[] _starPhase;
    Action _onFinished;
    float _startTime;
    bool _playing;
    static Sprite _streakSprite, _discSprite;

    public bool IsPlaying { get { return _playing; } }

    public static IntroSequence Create(Transform parent)
    {
        GameObject go = new GameObject("Intro Sequence");
        go.transform.SetParent(parent, false);
        IntroSequence intro = go.AddComponent<IntroSequence>();
        intro.Build();
        return intro;
    }

    // ---------------------------------------------------------------- sprites

    static Sprite Streak()
    {
        if (_streakSprite != null) return _streakSprite;
        const int w = 256, h = 32;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float nx = (x + 0.5f) / w * 2f - 1f, ny = (y + 0.5f) / h * 2f - 1f;
                float a = Mathf.Exp(-nx * nx * 2.2f) * Mathf.Exp(-ny * ny * 7f);
                px[y * w + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        _streakSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        return _streakSprite;
    }

    static Sprite Glow()
    {
        if (_discSprite != null) return _discSprite;
        const int s = 128;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color[] px = new Color[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float nx = (x + 0.5f) / s * 2f - 1f, ny = (y + 0.5f) / s * 2f - 1f;
                float r2 = nx * nx + ny * ny;
                px[y * s + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - r2) * Mathf.Clamp01(1f - r2));
            }
        tex.SetPixels(px);
        tex.Apply();
        _discSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return _discSprite;
    }

    // ---------------------------------------------------------------- construcción

    void Build()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 24);
        _titleFont = Resources.Load<Font>("Fonts/Transcorner");
        if (_titleFont == null) _titleFont = _font;

        GameObject canvasObject = new GameObject("Canvas");
        canvasObject.transform.SetParent(transform, false);
        _canvas = canvasObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 3500;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        _group = canvasObject.AddComponent<CanvasGroup>();
        RectTransform root = canvasObject.GetComponent<RectTransform>();

        Image bg = NewImage(root, "Bg", Vector2.zero, Vector2.zero, new Color(0.002f, 0.006f, 0.02f, 1f), null);
        bg.rectTransform.anchorMin = Vector2.zero;
        bg.rectTransform.anchorMax = Vector2.one;
        bg.rectTransform.offsetMin = bg.rectTransform.offsetMax = Vector2.zero;

        _stars = new Image[140];
        _starPhase = new float[_stars.Length];
        for (int i = 0; i < _stars.Length; i++)
        {
            float size = UnityEngine.Random.Range(1.5f, 4f);
            _stars[i] = NewImage(root, "Star", new Vector2(size, size),
                new Vector2(UnityEngine.Random.Range(-960f, 960f), UnityEngine.Random.Range(-540f, 540f)),
                new Color(1f, 1f, 1f, 0.5f), null);
            _starPhase[i] = UnityEngine.Random.value * 6.28f;
        }

        _line = NewText(root, "Line", 46, FontStyle.Normal, new Color(0.9f, 0.96f, 1f, 0f),
            new Vector2(1760f, 220f), Vector2.zero);
        _line.lineSpacing = 1.2f;

        // Título: resplandor + letras sueltas con mucho espaciado
        _titleHalo = NewImage(root, "TitleHalo", new Vector2(1500f, 360f), new Vector2(0f, 40f),
            new Color(0.2f, 0.55f, 1f, 0f), Glow());
        _letters = new Text[GameTitle.Length];
        _letterGlows = new Text[GameTitle.Length];
        for (int i = 0; i < GameTitle.Length; i++)
        {
            Vector2 pos = new Vector2((i - (GameTitle.Length - 1) * 0.5f) * LetterSpacing, 50f);
            _letterGlows[i] = NewText(root, "LetterGlow", 108, FontStyle.Normal, new Color(0.3f, 0.7f, 1f, 0f),
                new Vector2(140f, 150f), pos);
            _letterGlows[i].font = _titleFont;
            _letterGlows[i].text = GameTitle[i].ToString();
            _letters[i] = NewText(root, "Letter", 104, FontStyle.Normal, new Color(1f, 1f, 1f, 0f),
                new Vector2(140f, 150f), pos);
            _letters[i].font = _titleFont;
            _letters[i].text = GameTitle[i].ToString();
        }

        // Marco fino de luz para dar al título una composición de logotipo tecnológico.
        _titleRules = new Image[4];
        _titleRules[0] = NewImage(root, "WordmarkRuleTop", new Vector2(1360f, 2f), new Vector2(0f, 164f),
            new Color(0.22f, 0.76f, 1f, 0f), null);
        _titleRules[1] = NewImage(root, "WordmarkRuleBottom", new Vector2(1360f, 2f), new Vector2(0f, -42f),
            new Color(0.22f, 0.76f, 1f, 0f), null);
        _titleRules[2] = NewImage(root, "WordmarkEndLeft", new Vector2(2f, 22f), new Vector2(-680f, 153f),
            new Color(0.22f, 0.76f, 1f, 0f), null);
        _titleRules[3] = NewImage(root, "WordmarkEndRight", new Vector2(2f, 22f), new Vector2(680f, 153f),
            new Color(0.22f, 0.76f, 1f, 0f), null);

        // Haz de luz bajo el título
        _streakGlow = NewImage(root, "StreakGlow", new Vector2(1500f, 90f), new Vector2(0f, -62f),
            new Color(0.25f, 0.6f, 1f, 0f), Streak());
        _streakCore = NewImage(root, "StreakCore", new Vector2(1250f, 10f), new Vector2(0f, -62f),
            new Color(0.85f, 0.95f, 1f, 0f), Streak());
        _flare = NewImage(root, "Flare", new Vector2(230f, 230f), new Vector2(0f, -62f),
            new Color(0.7f, 0.9f, 1f, 0f), Glow());

        _tagline = NewText(root, "Tagline", 25, FontStyle.Normal, new Color(Ice.r, Ice.g, Ice.b, 0f),
            new Vector2(1500f, 40f), new Vector2(0f, -135f));
        _tagline.text = Tagline;

        canvasObject.SetActive(false);
    }

    Image NewImage(Transform parent, string name, Vector2 size, Vector2 pos, Color color, Sprite sprite)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    Text NewText(Transform parent, string name, int size, FontStyle style, Color color, Vector2 box, Vector2 pos)
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
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    // ---------------------------------------------------------------- reproducción

    public void Play(Action onFinished)
    {
        _onFinished = onFinished;
        _startTime = Time.unscaledTime;
        _playing = true;
        _group.alpha = 1f;
        _canvas.gameObject.SetActive(true);
    }

    void Finish()
    {
        if (!_playing) return;
        _playing = false;
        _canvas.gameObject.SetActive(false);
        Action callback = _onFinished;
        _onFinished = null;
        if (callback != null) callback();
    }

    static float Ramp(float t, float start, float duration)
    {
        return Mathf.Clamp01((t - start) / duration);
    }

    static Color WithAlpha(Color c, float a) { return new Color(c.r, c.g, c.b, a); }

    void Update()
    {
        if (!_playing) return;

        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        if ((kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) ||
            (mouse != null && mouse.leftButton.wasPressedThisFrame))
        {
            Finish();
            return;
        }

        float t = Time.unscaledTime - _startTime;
        float linesEnd = Lines.Length * LineTime;

        // Estrellas
        for (int i = 0; i < _stars.Length; i++)
        {
            float a = 0.15f + 0.6f * (0.5f + 0.5f * Mathf.Sin(t * (0.8f + (i % 5) * 0.3f) + _starPhase[i]));
            _stars[i].color = new Color(1f, 1f, 1f, a);
        }

        // Frases de presentación sencillas, sin el desplazamiento en perspectiva.
        if (t < linesEnd)
        {
            int index = Mathf.Min((int)(t / LineTime), Lines.Length - 1);
            float local = t - index * LineTime;
            float alpha = Mathf.Min(Ramp(local, 0f, FadeTime), 1f - Ramp(local, LineTime - FadeTime, FadeTime));
            _line.text = Lines[index];
            _line.color = new Color(0.9f, 0.96f, 1f, alpha);
            _line.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(-14f, 6f, local / LineTime));
        }
        else _line.color = WithAlpha(_line.color, 0f);

        // Título
        float tt = t - linesEnd;
        for (int i = 0; i < _letters.Length; i++)
        {
            float a = Mathf.SmoothStep(0f, 1f, Ramp(tt, 0.3f + i * LetterDelay, 0.7f));
            _letters[i].color = new Color(1f, 1f, 1f, a);
            _letterGlows[i].color = new Color(0.3f, 0.7f, 1f, a * 0.32f);
            float s = Mathf.Lerp(1.12f, 1f, a);
            _letters[i].rectTransform.localScale = new Vector3(s, s, 1f);
            _letterGlows[i].rectTransform.localScale = new Vector3(s * 1.03f, s * 1.03f, 1f);
        }

        float titleDone = 0.3f + _letters.Length * LetterDelay + 0.7f;
        float haloAlpha = Mathf.SmoothStep(0f, 1f, Ramp(tt, 0.4f, titleDone));
        _titleHalo.color = new Color(0.2f, 0.55f, 1f, 0.22f * haloAlpha);
        foreach (Image rule in _titleRules)
            rule.color = new Color(0.22f, 0.76f, 1f, 0.72f * haloAlpha);

        float streak = Mathf.SmoothStep(0f, 1f, Ramp(tt, titleDone - 0.4f, 1.3f));
        float flicker = 0.92f + 0.08f * Mathf.Sin(t * 7f);
        _streakGlow.color = new Color(0.25f, 0.6f, 1f, 0.75f * streak * flicker);
        _streakGlow.rectTransform.localScale = new Vector3(streak, 1f, 1f);
        _streakCore.color = new Color(0.85f, 0.95f, 1f, 0.95f * streak);
        _streakCore.rectTransform.localScale = new Vector3(streak, 1f, 1f);
        _flare.color = new Color(0.7f, 0.9f, 1f, 0.7f * streak * flicker);

        float tagAlpha = Mathf.SmoothStep(0f, 1f, Ramp(tt, titleDone + 0.8f, 1.2f));
        _tagline.color = new Color(Ice.r, Ice.g, Ice.b, 0.9f * tagAlpha);

        // Final: se mantiene unos segundos y se apaga
        float hold = titleDone + 0.8f + 1.2f + 2.6f;
        if (tt > hold)
        {
            float fade = 1f - Ramp(tt, hold, 0.9f);
            _group.alpha = fade;
            if (fade <= 0f) Finish();
        }
    }
}
