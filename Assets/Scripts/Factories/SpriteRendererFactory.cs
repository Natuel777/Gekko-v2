using UnityEngine;
using System.Collections.Generic;

[DefaultExecutionOrder(-400)]
public class SpriteRendererFactory : Factory<GenericSR, SRType>
{
    [SerializeField] private List<GenericSR> _SRPrefabs;
    [SerializeField] private int _initialStockPerType = 15;

    private Dictionary<SRType, GenericSR> _prefabsByType = new Dictionary<SRType, GenericSR>();
    private Dictionary<SRType, ObjectPool<GenericSR>> _pools = new Dictionary<SRType, ObjectPool<GenericSR>>();
    private IGameManager _gameManager; 

    private void Awake() 
    {
        ServiceLocator.Instance.Register<SpriteRendererFactory>(this);

        if(ServiceLocator.Instance.TryGet(out IGameManager gmInterface))
        {
            if(gmInterface is GameManager gameManager)
                _gameManager = gameManager;
        }

        _prefabsByType = new();
        _pools = new();

        foreach(var prefab in _SRPrefabs)
            _prefabsByType[prefab.SRType] = prefab;

        foreach(var type in _prefabsByType.Keys)
            GetOrCreatePool(type);
    }

    public override GenericSR Create(SRType type, Vector3 position, Quaternion rotation)
    {
        var pool = GetOrCreatePool(type);
        if(pool == null) return null;

        var sr = pool.Get(p => p.transform.SetPositionAndRotation(position, rotation));
        return sr;
    }

    private ObjectPool<GenericSR> GetOrCreatePool(SRType type)
    {
        if(_pools.TryGetValue(type, out var existingPool))
            return existingPool;

        if(!_prefabsByType.TryGetValue(type, out var prefab))
        {
            Debug.LogError($"SpriteRendererFactory: no prefab registered for '{type}'.", this);
            return null;
        }

        var pool = new ObjectPool<GenericSR>(
            () => Instantiate(prefab, transform),
            sr => sr.gameObject.SetActive(true),
            sr => sr.gameObject.SetActive(false),
            _initialStockPerType);
            _pools[type] = pool;
        return pool;
    }

    public void Return(GenericSR sr)
    {
        if(!_pools.TryGetValue(sr.SRType, out var pool))
        {
            _gameManager?.DestroyObject(sr.gameObject);
            return;
        }

        pool.Return(sr);
    }
}