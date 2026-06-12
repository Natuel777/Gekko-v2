using System.Collections.Generic;
using System;
using UnityEngine;

public class ObjectPool<T> where T : MonoBehaviour
{
    List<T> _stock = new List<T>();
    List<T> _active = new List<T>();
    Action<T> _On;
    Action<T> _Off;
    Func<T> _Factory;

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
    }

    public T Get()
    {
        T x;

        if(_stock.Count > 0)
        {
            x = _stock[0];
            _stock.RemoveAt(0);
        }
        else x = _Factory();

        _active.Add(x);
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
                GameManager.Instance.DestroyObject(obj.gameObject);
        }
        foreach(var obj in _stock)
        {
            if(obj != null)
                GameManager.Instance.DestroyObject(obj.gameObject);
        }
        _active.Clear();
        _stock.Clear();
    }
}
