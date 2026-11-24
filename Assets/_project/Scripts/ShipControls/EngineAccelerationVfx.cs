using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Engine glow, SRP lens flare and exhaust plume driven by ship throttle.</summary>
public sealed class EngineAccelerationVfx : MonoBehaviour
{
    [SerializeField] LensFlareDataSRP _engineHalo;
    [SerializeField, Min(0f)] float _lightIntensity = 3f;
    [SerializeField, Min(0f)] float _lightRange = 12f;
    [SerializeField, Min(0f)] float _plumeLength = 2.5f;

    Light[] _lights;
    LensFlareComponentSRP[] _flares;
    ParticleSystem[] _plumes;
    Transform[] _outerFlames;
    Transform[] _innerFlames;
    LensFlareDataSRP _runtimeFlare;
    Material _particleMaterial;
    Material _outerFlameMaterial;
    Material _innerFlameMaterial;
    Texture2D _particleTexture;

    void Awake()
    {
        Transform[] engines = GetComponentsInChildren<Transform>(true);
        int count = 0;
        foreach (Transform t in engines)
            if (t != transform && t.name == "Engine1") count++;

        if (count == 0) return;

        _lights = new Light[count];
        _flares = new LensFlareComponentSRP[count];
        _plumes = new ParticleSystem[count];
        _outerFlames = new Transform[count];
        _innerFlames = new Transform[count];
        _runtimeFlare = CreateRuntimeFlare();
        _particleTexture = CreateSoftGlowTexture();
        _particleMaterial = CreateParticleMaterial(_particleTexture);
        _outerFlameMaterial = CreateFlameMaterial(new Color(1f, 0.42f, 0.12f, 0.38f));
        _innerFlameMaterial = CreateFlameMaterial(new Color(1f, 0.82f, 0.5f, 0.52f));

        int index = 0;
        foreach (Transform engine in engines)
        {
            if (engine == transform || engine.name != "Engine1") continue;

            // The actual nozzle is a child set back at z=-1.83 in this ship
            // model. Starting effects near the Engine1 pivot leaves them
            // trapped inside the engine housing and makes them look circular.
            Transform nozzle = engine.Find("thruster1");
            float nozzleZ = nozzle != null ? nozzle.localPosition.z : -1.4f;
            float exhaustStartZ = nozzleZ - 0.3f;

            var glowObject = new GameObject("Engine Glow");
            glowObject.transform.SetParent(engine, false);
            glowObject.transform.localPosition = new Vector3(0f, 0f, exhaustStartZ);
            Light light = glowObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.48f, 0.12f);
            light.range = _lightRange;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            _lights[index] = light;

            LensFlareComponentSRP flare = glowObject.AddComponent<LensFlareComponentSRP>();
            flare.lensFlareData = _runtimeFlare;
            flare.intensity = 0f;
            flare.scale = 0.24f;
            flare.useOcclusion = false;
            _flares[index] = flare;

            var plumeObject = new GameObject("Engine Exhaust");
            plumeObject.transform.SetParent(engine, false);
            plumeObject.transform.localPosition = new Vector3(0f, 0f, exhaustStartZ);
            plumeObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            ParticleSystem plume = plumeObject.AddComponent<ParticleSystem>();
            ConfigurePlume(plume);
            _plumes[index] = plume;

            _outerFlames[index] = CreateFlameMesh(engine, "Engine Flame Outer", 0.48f,
                new Color(1f, 0.7f, 0.3f, 0.35f), new Color(1f, 0.22f, 0.06f, 0.08f),
                _outerFlameMaterial);
            _innerFlames[index] = CreateFlameMesh(engine, "Engine Flame Core", 0.2f,
                new Color(1f, 0.98f, 0.8f, 0.68f), new Color(1f, 0.52f, 0.16f, 0.12f),
                _innerFlameMaterial);
            _outerFlames[index].localPosition = new Vector3(0f, 0f, exhaustStartZ);
            _innerFlames[index].localPosition = new Vector3(0f, 0f, exhaustStartZ);
            _outerFlames[index].gameObject.SetActive(false);
            _innerFlames[index].gameObject.SetActive(false);
            index++;
        }
    }

    public void SetThrottle(float throttle)
    {
        float amount = Mathf.Clamp01(throttle);
        if (_lights == null) return;

        for (int i = 0; i < _lights.Length; i++)
        {
            _lights[i].intensity = _lightIntensity * amount;
            _flares[i].intensity = amount;
            bool flameOn = amount > 0.015f;
            _outerFlames[i].gameObject.SetActive(flameOn);
            _innerFlames[i].gameObject.SetActive(flameOn);
            float length = _plumeLength * Mathf.Lerp(0.45f, 1f, amount);
            float width = Mathf.Lerp(0.2f, 1f, amount);
            _outerFlames[i].localScale = new Vector3(width, width, length * amount);
            _innerFlames[i].localScale = new Vector3(width, width, length * amount * 0.82f);
            var emission = _plumes[i].emission;
            emission.rateOverTime = 60f * amount;
            if (amount > 0.01f && !_plumes[i].isPlaying) _plumes[i].Play();
            else if (amount <= 0.01f && _plumes[i].isPlaying) _plumes[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    LensFlareDataSRP CreateRuntimeFlare()
    {
        LensFlareDataSRP data = _engineHalo != null
            ? Instantiate(_engineHalo)
            : ScriptableObject.CreateInstance<LensFlareDataSRP>();
        data.name = "EngineHalo Runtime";

        // The supplied EngineHalo asset is currently empty. Give it a soft amber
        // core and two smaller additive elements so it reads as hot engine glow.
        data.elements = new[]
        {
            CreateFlareElement(0.2f, 0.9f),
            CreateFlareElement(0.075f, 0.35f),
            CreateFlareElement(0.035f, 0.18f)
        };
        return data;
    }

    static LensFlareDataElementSRP CreateFlareElement(float size, float alpha)
    {
        var element = new LensFlareDataElementSRP
        {
            flareType = SRPLensFlareType.Circle,
            tintColorType = SRPLensFlareColorType.Constant,
            tint = new Color(1f, 0.42f, 0.08f, alpha),
            uniformScale = size,
            localIntensity = 1f,
            blendMode = SRPLensFlareBlendMode.Additive,
            position = 0f,
            visible = true
        };
        return element;
    }

    void ConfigurePlume(ParticleSystem plume)
    {
        plume.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = plume.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 1f;
        main.startLifetime = 0.32f;
        main.startSpeed = _plumeLength / 0.32f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.3f);
        main.startColor = new Color(1f, 0.65f, 0.2f, 0.95f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 128;

        var emission = plume.emission;
        emission.rateOverTime = 0f;

        var shape = plume.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 2f;
        shape.radius = 0.035f;

        var size = plume.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.55f), new Keyframe(1f, 0f)));

        var color = plume.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.95f, 0.65f), 0f),
                new GradientColorKey(new Color(1f, 0.32f, 0.06f), 0.55f),
                new GradientColorKey(new Color(0.65f, 0.12f, 0.03f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0.45f, 0.65f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = gradient;

        var renderer = plume.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.35f;
        renderer.lengthScale = 5.5f;
        renderer.material = _particleMaterial;
    }

    static Transform CreateFlameMesh(Transform engine, string objectName, float radius,
        Color rootColor, Color tipColor, Material material)
    {
        var flameObject = new GameObject(objectName);
        flameObject.transform.SetParent(engine, false);
        var filter = flameObject.AddComponent<MeshFilter>();
        var renderer = flameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        filter.sharedMesh = BuildFlameMesh(radius, rootColor, tipColor);
        return flameObject.transform;
    }

    static Mesh BuildFlameMesh(float radius, Color rootColor, Color tipColor)
    {
        const int sides = 12;
        float[] positions = { 0f, 0.12f, 0.42f, 0.72f, 1f };
        float[] radii = { 0.34f, 0.72f, 1f, 0.62f, 0.08f };
        var vertices = new Vector3[positions.Length * sides];
        var colors = new Color[vertices.Length];
        var triangles = new int[(positions.Length - 1) * sides * 6];

        for (int ring = 0; ring < positions.Length; ring++)
        {
            Color color = Color.Lerp(rootColor, tipColor, positions[ring]);
            color.a *= 1f - positions[ring] * positions[ring];
            for (int side = 0; side < sides; side++)
            {
                float angle = side * Mathf.PI * 2f / sides;
                int vertex = ring * sides + side;
                vertices[vertex] = new Vector3(Mathf.Cos(angle) * radius * radii[ring],
                    Mathf.Sin(angle) * radius * radii[ring], -positions[ring]);
                colors[vertex] = color;
            }
        }

        int triangle = 0;
        for (int ring = 0; ring < positions.Length - 1; ring++)
        {
            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                int a = ring * sides + side;
                int b = ring * sides + next;
                int c = (ring + 1) * sides + side;
                int d = (ring + 1) * sides + next;
                triangles[triangle++] = a;
                triangles[triangle++] = c;
                triangles[triangle++] = b;
                triangles[triangle++] = b;
                triangles[triangle++] = c;
                triangles[triangle++] = d;
            }
        }

        var mesh = new Mesh { name = "Tapered Engine Flame" };
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    static Material CreateFlameMaterial(Color tint)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;

        var material = new Material(shader) { name = "Engine Flame Transparent" };
        material.color = tint;
        ConfigureAlphaBlending(material);
        return material;
    }

    static Material CreateParticleMaterial(Texture2D softGlowTexture)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;
        var material = new Material(shader) { name = "Engine Exhaust Runtime" };
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", softGlowTexture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", softGlowTexture);
        ConfigureAlphaBlending(material);
        return material;
    }

    static void ConfigureAlphaBlending(Material material)
    {
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_ALPHAMODULATE_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    static Texture2D CreateSoftGlowTexture()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
        {
            name = "Engine Exhaust Soft Glow",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - radius), 2.2f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    void OnDestroy()
    {
        if (_runtimeFlare != null) Destroy(_runtimeFlare);
        if (_particleMaterial != null) Destroy(_particleMaterial);
        if (_outerFlameMaterial != null) Destroy(_outerFlameMaterial);
        if (_innerFlameMaterial != null) Destroy(_innerFlameMaterial);
        if (_particleTexture != null) Destroy(_particleTexture);
        if (_outerFlames != null)
            foreach (Transform flame in _outerFlames)
                if (flame != null && flame.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
                    Destroy(filter.sharedMesh);
        if (_innerFlames != null)
            foreach (Transform flame in _innerFlames)
                if (flame != null && flame.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
                    Destroy(filter.sharedMesh);
    }
}
