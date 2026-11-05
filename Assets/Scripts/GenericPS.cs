using UnityEngine;
using System.Collections.Generic;

public enum PSType
{
    VenomHit
}

public class GenericPS : MonoBehaviour
{
    private List<ParticleSystem> _ps;
    [SerializeField] private PSType _type;
    [SerializeField] private bool _autoReturnToFactory = false;
    private ParticleSystemFactory _factory;

    public PSType PSType => _type;

    private void Awake() => _ps = new List<ParticleSystem>(GetComponentsInChildren<ParticleSystem>(true));

    public void Initialize()
    {
        if(_factory == null)
            ServiceLocator.Instance.TryGet(out _factory);

        foreach(ParticleSystem ps in _ps)
            ps.Play();
    }

    private void Update()
    {
        if(_factory == null)
        {
            Debug.LogWarning("[GenericPS] Factory is null.");
            return;
        }

        if(!_autoReturnToFactory) return;

        if(_ps.TrueForAll(ps => ps.isStopped))
            _factory.Return(this);
    }
}