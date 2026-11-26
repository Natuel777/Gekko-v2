using UnityEngine;
using System.Collections.Generic;

public enum PSType
{
    VenomHit,
    VenomHitSmall
}

public class GenericPS : MonoBehaviour
{
    private List<ParticleSystem> _ps;
    [SerializeField] private PSType _type;
    [SerializeField] private bool _autoReturnToFactory = false;
    [SerializeField] private SoundNames _soundToPlay = SoundNames.None; 
    private ParticleSystemFactory _factory;
    private bool _initialized;

    public PSType PSType => _type;

    private void Awake() => _ps = new List<ParticleSystem>(GetComponentsInChildren<ParticleSystem>(true));

    private void OnDisable() => _initialized = false;

    public void Initialize()
    {
        if(_factory == null)
            ServiceLocator.Instance.TryGet(out _factory);

        _initialized = true;

        foreach(ParticleSystem ps in _ps)
            ps.Play();

        if(_soundToPlay != SoundNames.None)
            AudioManager.instance.Play(_soundToPlay);
    }

    private void Update()
    {
        if(!_initialized || !_autoReturnToFactory || _factory == null) return;

        if(_ps.TrueForAll(ps => ps.isStopped))
            _factory.Return(this);
    }
}