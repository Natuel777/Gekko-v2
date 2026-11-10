using UnityEngine;

// Gotitas de agua en cada salpicadura. Ejemplo de uso del evento SplineRiver.Splashed: el rio
// avisa donde y con que fuerza salpico algo, y este componente emite particulas ahi (un solo
// ParticleSystem para todo el rio). Lo mismo sirve para reproducir sonidos.
[RequireComponent(typeof(SplineRiver))]
[AddComponentMenu("Gekko/River/River Splash FX")]
public class RiverSplashFX : MonoBehaviour
{
    [SerializeField] Material _material;
    [SerializeField, Min(1)] int _particlesPerSplash = 26;
    [SerializeField, Min(0f)] float _upSpeed = 3.5f;
    [SerializeField, Min(0f)] float _spread = 1.4f;
    [SerializeField] Color _color = new Color(0.92f, 0.98f, 1f, 1f);

    SplineRiver _river;
    ParticleSystem _particles;

    void Awake()
    {
        _river = GetComponent<SplineRiver>();

        var go = new GameObject("Splash Particles (runtime)");
        go.transform.SetParent(transform, false);
        _particles = go.AddComponent<ParticleSystem>();
        _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = _particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.17f);
        main.startColor = _color;
        main.gravityModifier = 1.3f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 600;

        ParticleSystem.EmissionModule emission = _particles.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = _particles.shape;
        shape.enabled = false;

        ParticleSystem.SizeOverLifetimeModule size = _particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        if(_material != null)
            renderer.sharedMaterial = _material;

        _particles.Play();
    }

    void OnEnable()
    {
        if(_river == null)
            _river = GetComponent<SplineRiver>();
        _river.Splashed += OnSplashed;
    }

    void OnDisable()
    {
        _river.Splashed -= OnSplashed;
    }

    void OnSplashed(Vector3 position, float strength)
    {
        int count = Mathf.Max(1, Mathf.RoundToInt(_particlesPerSplash * strength));
        var emit = new ParticleSystem.EmitParams();
        for(int i = 0; i < count; i++)
        {
            Vector2 ring = Random.insideUnitCircle;
            emit.position = position + new Vector3(ring.x, 0f, ring.y) * 0.25f;
            emit.velocity = new Vector3(ring.x * _spread, _upSpeed * Random.Range(0.5f, 1.2f) * Mathf.Sqrt(strength), ring.y * _spread);
            emit.applyShapeToPosition = false;
            _particles.Emit(emit, 1);
        }
    }
}
