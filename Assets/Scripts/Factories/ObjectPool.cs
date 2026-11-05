using System.Collections.Generic;
using System;
using UnityEngine;

public class ObjectPool<T> where T : MonoBehaviour
{
    private List<T> _stock = new List<T>();
    private List<T> _active = new List<T>();
    private Action<T> _On;
    private Action<T> _Off;
    private Func<T> _Factory;
    private IGameManager _gameManager;

    public ObjectPool(Func<T> Factory, Action<T> ObjOn, Action<T> ObjOff, int initialStock = 15)
    {
        _Factory = Factory;
        _On = ObjOn;
        _Off = ObjOff;

        for(int i = 0; i < initialStock; i++)
        {
            var x = _Factory();
            _Off(x);
            _stock.Add(x);
        }

        if(ServiceLocator.Instance.TryGet(out IGameManager gmInterface))
        {
            if(gmInterface is GameManager gameManager)
                _gameManager = gameManager;
        }
    }

    public T Get(Action<T> beforeActivate = null)
    {
        T x = null;

        while(x == null && _stock.Count > 0)
        {
            x = _stock[0];
            _stock.RemoveAt(0);
        }

        if(x == null) x = _Factory();

        _active.Add(x);
        beforeActivate?.Invoke(x);
        _On(x);
        return x;
    }

    public void Return(T obj)
    {
        _Off(obj);
        _stock.Add(obj);
        _active.Remove(obj);
    }

    public void Clear()
    {
        foreach(var obj in _active)
        {
            if(obj != null)
                _gameManager?.DestroyObject(obj.gameObject);
        }
        foreach(var obj in _stock)
        {
            if(obj != null)
                _gameManager?.DestroyObject(obj.gameObject);
        }
        _active.Clear();
        _stock.Clear();
    }
}
