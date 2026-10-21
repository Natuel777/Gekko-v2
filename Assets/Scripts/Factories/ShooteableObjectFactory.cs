using UnityEngine;
using System;
using System.Collections.Generic;

public class ShooteableObjectFactory : Factory<ShooteableObject, BulletType>
{
    public static ShooteableObjectFactory Instance;
    [SerializeField] private ShooteableObject[] _bullets;
    private Dictionary<BulletType, ObjectPool<ShooteableObject>> _bulletsDictionary = new Dictionary<BulletType, ObjectPool<ShooteableObject>>();

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        else Destroy(gameObject);
        
        foreach(ShooteableObject bullet in _bullets)
        {
            if(!_bulletsDictionary.ContainsKey(bullet.BulletType))
            {
                ShooteableObject bulletPrefab = bullet;
                Func<ShooteableObject> createFunc = () => Instantiate(bulletPrefab);
                ObjectPool<ShooteableObject> pool = new ObjectPool<ShooteableObject>(createFunc, ObjON, ObjOFF);
                _bulletsDictionary.Add(bullet.BulletType, pool);
            }
        }
    }

    public override ShooteableObject Create(BulletType key, Vector3 position, Quaternion rotation)
    {
        if(_bulletsDictionary.ContainsKey(key))
        {
            var bullet = _bulletsDictionary[key].Get();
            bullet.transform.position = position;
            bullet.transform.rotation = rotation;
            return bullet;
        }

        return null;
    }

    public ShooteableObject GetBullet(ShooteableObject bulletPrefab, Vector3 position, Quaternion rotation)
    {
        return Create(bulletPrefab.BulletType, position, rotation);
    }

    public T GetBullet<T>(T bulletPrefab, Vector3 position, Quaternion rotation) where T : ShooteableObject
    {
        return Create(bulletPrefab.BulletType, position, rotation) as T;
    }

    public void ReturnBullet(ShooteableObject bullet)
    {
        if(_bulletsDictionary.ContainsKey(bullet.BulletType))
            _bulletsDictionary[bullet.BulletType].Return(bullet);
    }

    private void ObjON(ShooteableObject shooteable) {shooteable.gameObject.SetActive(true);}

    private void ObjOFF(ShooteableObject shooteable) {shooteable.gameObject.SetActive(false);}
}