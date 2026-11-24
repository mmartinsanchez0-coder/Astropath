using UnityEngine;

/// <summary>
/// Adds animated, layered particle prominences to Sun2 at runtime.
/// Particle sizes are authored as fractions of the sphere, so they scale with
/// the solar-system manager's runtime sizing of the Sun prefab.
/// </summary>
public sealed class Sun2ParticleEffect : MonoBehaviour
{
    [SerializeField] Material _particleMaterial;

    void Awake()
    {
        CreateLayer("Solar Prominences", 850f, 1.4f, 0.075f, 0.012f, 0.035f, 1800, 0.2f, 0.5f);
        CreateLayer("Solar Corona Sparks", 420f, 2.4f, 0.026f, 0.005f, 0.016f, 1400, 0.11f, 0.3f);
    }

    void CreateLayer(string layerName, float emissionRate, float lifetime, float speed,
                     float minSize, float maxSize, int maxParticles, float noiseStrength, float noiseFrequency)
    {
        GameObject layer = new GameObject(layerName);
        Transform layerTransform = layer.transform;
        layerTransform.SetParent(transform, false);

        ParticleSystem particles = layer.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startLifetime = lifetime;
        main.startSpeed = speed;
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startColor = new Color(1f, 0.72f, 0.28f, 0.92f);
        main.maxParticles = maxParticles;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = emissionRate;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.49f;
        // Emit from a thin spherical volume instead of a flat-looking surface rim.
        shape.radiusThickness = 0.22f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = CreateFlameGradient();

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = noiseStrength;
        noise.frequency = noiseFrequency;
        noise.scrollSpeed = 0.35f;
        noise.damping = true;
        noise.quality = ParticleSystemNoiseQuality.Low;

        ParticleSystemRenderer renderer = layer.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = _particleMaterial;
        renderer.sortingFudge = 1f;

        particles.Play();
    }

    static Gradient CreateFlameGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.98f, 0.74f), 0f),
                new GradientColorKey(new Color(1f, 0.58f, 0.16f), 0.42f),
                new GradientColorKey(new Color(0.95f, 0.16f, 0.025f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.9f, 0.12f),
                new GradientAlphaKey(0.58f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }
}
