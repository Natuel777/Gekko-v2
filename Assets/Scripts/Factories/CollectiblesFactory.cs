using System.Collections.Generic;
using UnityEngine;
using System;

public class CollectiblesFactory : Factory<Collectible>
{
    [SerializeField] private Collectible[] _collectibles;
    private Dictionary<string, Collectible> _collectibleDictionary = new Dictionary<string, Collectible>();
    private Dictionary<string, ObjectPool<Collectible>> _pools = new Dictionary<string, ObjectPool<Collectible>>();

    private void Start()
    {
        GameManager.Instance.factory = this;
        foreach (Collectible c in _collectibles)
            if (!_collectibleDictionary.ContainsKey(c.CollectibleName))
                _collectibleDictionary.Add(c.CollectibleName, c);
    }

    public override Collectible Create(string name, Vector3 position, Quaternion rotation)
    {
        if (_collectibleDictionary.TryGetValue(name, out var prefab))
            return Instantiate(prefab, position, rotation);
        return null;
    }

    public void InitializePool(string collectibleName, Func<Collectible> createFunc, int initialSize = 0)
    {
        if (!_pools.ContainsKey(collectibleName))
            _pools[collectibleName] = new ObjectPool<Collectible>(createFunc, ObjON, ObjOFF, initialSize);
    }

    public void SpawnFromPool(string collectibleName, Transform t)
    {
        if (!_pools.TryGetValue(collectibleName, out var pool)) return;
        var c = pool.Get();
        c.transform.SetPositionAndRotation(t.position, t.rotation);
    }

    public void ReturnToPool(Collectible collectible)
    {
        if (_pools.TryGetValue(collectible.CollectibleName, out var pool))
            pool.Return(collectible);
    }

    public bool HasPool(string collectibleName) => _pools.ContainsKey(collectibleName);

    public void ClearPools()
    {
        foreach (var pool in _pools.Values)
            pool.Clear();
        _pools.Clear();
    }

    private void ObjON(Collectible c) { c.gameObject.SetActive(true); }
    private void ObjOFF(Collectible c) { c.gameObject.SetActive(false); }
}
