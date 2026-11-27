using UnityEngine;
using UnityEngine.VFX;
using System.Collections.Generic;

public enum PSType
{
    VenomHit,
    VenomHitSmall,
    TurretBulletHit
}

public class GenericPS : MonoBehaviour
{
    private List<ParticleSystem> _ps;
    private List<VisualEffect> _vfx;
    [SerializeField] private PSType _type;
    [SerializeField] private bool _autoReturnToFactory = false;
    [SerializeField] private SoundNames _soundToPlay = SoundNames.None; 
    [Tooltip("Segundos que vive un efecto hecho con VFX Graph antes de volver al pool: el VFX Graph no avisa cuándo termina, así que se espera este tiempo. Tiene que cubrir el efecto entero (la partícula más larga más las demoras). Solo cuenta si el prefab tiene un VisualEffect.")]
    [SerializeField] private float _vfxDuration = 1f;
    private ParticleSystemFactory _factory;
    private bool _initialized;
    private float _vfxTimeLeft;

    public PSType PSType => _type;

    private void Awake()
    {
        _ps = new List<ParticleSystem>(GetComponentsInChildren<ParticleSystem>(true));
        _vfx = new List<VisualEffect>(GetComponentsInChildren<VisualEffect>(true));
    }

    private void OnDisable() => _initialized = false;

    public void Initialize()
    {
        if(_factory == null)
            ServiceLocator.Instance.TryGet(out _factory);

        _initialized = true;
        _vfxTimeLeft = _vfxDuration;

        foreach(ParticleSystem ps in _ps)
            ps.Play();

        foreach(VisualEffect vfx in _vfx)
            vfx.Reinit();

        if(_soundToPlay != SoundNames.None)
            AudioManager.instance.Play(_soundToPlay);
    }

    private void Update()
    {
        if(!_initialized || !_autoReturnToFactory || _factory == null) return;

        if(IsFinished())
            _factory.Return(this);
    }

    // Particle Systems say when they stop, VFX Graph effects do not: those are given _vfxDuration seconds. Without this a prefab
    // made only of VFX Graph counted as finished at once (TrueForAll of an empty list is true) and went back to the pool the frame
    // after the hit, before it was even drawn.
    private bool IsFinished()
    {
        if(_vfx.Count > 0)
        {
            _vfxTimeLeft -= Time.deltaTime;

            if(_vfxTimeLeft > 0f) return false;
        }

        return _ps.TrueForAll(ps => ps.isStopped);
    }
}