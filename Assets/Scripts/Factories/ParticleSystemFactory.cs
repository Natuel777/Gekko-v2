using UnityEngine;
using System.Collections.Generic;

[DefaultExecutionOrder(-400)]
public class ParticleSystemFactory : Factory<GenericPS, PSType>
{
    [SerializeField] private List<GenericPS> _PSPrefabs;
    [SerializeField] private int _initialStockPerType = 15;

    private Dictionary<PSType, GenericPS> _prefabsByType;
    private Dictionary<PSType, ObjectPool<GenericPS>> _pools;
    private IGameManager _gameManager;

    private void Awake()
    {
        ServiceLocator.Instance.Register<ParticleSystemFactory>(this);

        if(ServiceLocator.Instance.TryGet(out IGameManager gmInterface))
        {
            if(gmInterface is GameManager gameManager)
                _gameManager = gameManager;
        }

        _prefabsByType = new();
        _pools = new();

        foreach(var prefab in _PSPrefabs)
            _prefabsByType[prefab.PSType] = prefab;

        foreach(var type in _prefabsByType.Keys)
            GetOrCreatePool(type);
    }

    public override GenericPS Create(PSType type, Vector3 position, Quaternion rotation)
    {
        var pool = GetOrCreatePool(type);
        var ps = pool.Get(p => p.transform.SetPositionAndRotation(position, rotation));
        return ps;
    }

    public void Return(GenericPS ps)
    {
        if(!_pools.TryGetValue(ps.PSType, out var pool))
        {
            _gameManager?.DestroyObject(ps.gameObject);
            return;
        }

        pool.Return(ps);
    }

    private ObjectPool<GenericPS> GetOrCreatePool(PSType type)
    {
        if(_pools.TryGetValue(type, out var existingPool))
            return existingPool;

        if(!_prefabsByType.TryGetValue(type, out var prefab))
        {
            Debug.LogError($"CannonBulletFactory: no bullet prefab registered for '{type}'.");
            return null;
        }

        var pool = new ObjectPool<GenericPS>(
            () => Instantiate(prefab, transform),
            ps => ps.gameObject.SetActive(true),
            ps => ps.gameObject.SetActive(false),
            _initialStockPerType);
            _pools[type] = pool;
        return pool;
    }
}
