using System.Collections.Generic;
using UnityEngine;

public abstract class Factory<T, TKey> : MonoBehaviour
{
    public abstract T Create(TKey key, Vector3 position, Quaternion rotation);
}