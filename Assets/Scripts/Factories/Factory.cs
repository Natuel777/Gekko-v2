using System.Collections.Generic;
using UnityEngine;

public abstract class Factory<T> : MonoBehaviour
{
    public abstract T Create(string name, Vector3 position, Quaternion rotation);
}
