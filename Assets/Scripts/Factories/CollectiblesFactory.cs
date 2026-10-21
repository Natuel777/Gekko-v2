using System.Collections.Generic;
using UnityEngine;
using System;

public class CollectiblesFactory : Factory<Collectible, CollectibleType>
{
    [SerializeField] private Collectible[] _collectibles;
    private Dictionary<CollectibleType, Collectible> _collectibleDictionary = new Dictionary<CollectibleType, Collectible>();
    private Dictionary<CollectibleType, ObjectPool<Collectible>> _pools = new Dictionary<CollectibleType, ObjectPool<Collectible>>();

    private void Start()
    {
        GameManager.Instance.factory = this;
        foreach (Collectible c in _collectibles)
            if (!_collectibleDictionary.ContainsKey(c.CollectibleType))
                _collectibleDictionary.Add(c.CollectibleType, c);
    }

    public override Collectible Create(CollectibleType key, Vector3 position, Quaternion rotation)
    {
        if (_collectibleDictionary.TryGetValue(key, out var prefab))
            return Instantiate(prefab, position, rotation);
        return null;
    }

    public void InitializePool(CollectibleType type, Func<Collectible> createFunc, int initialSize = 0)
    {
        if (!_pools.ContainsKey(type))
            _pools[type] = new ObjectPool<Collectible>(createFunc, ObjON, ObjOFF, initialSize);
    }

    public void SpawnFromPool(CollectibleType type, Transform t)
    {
        if (!_pools.TryGetValue(type, out var pool)) return;
        var c = pool.Get();
        c.transform.SetPositionAndRotation(t.position, t.rotation);
    }

    public void ReturnToPool(Collectible collectible)
    {
        if (_pools.TryGetValue(collectible.CollectibleType, out var pool))
            pool.Return(collectible);
    }

    public bool HasPool(CollectibleType type) => _pools.ContainsKey(type);

    public void ClearPools()
    {
        foreach (var pool in _pools.Values)
            pool.Clear();
        _pools.Clear();
    }

    private void ObjON(Collectible c) { c.gameObject.SetActive(true); }
    private void ObjOFF(Collectible c) { c.gameObject.SetActive(false); }
}
