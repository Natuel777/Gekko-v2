using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "BeetleGroupSO", menuName = "Scriptable Objects/BeetleGroupSO")]
public class BeetleGroupSO : ScriptableObject
{
    private readonly List<HeavyBeetle> _members = new();

    public void Register(HeavyBeetle b) => _members.Add(b);
    public void Unregister(HeavyBeetle b) => _members.Remove(b);

    public void AlertAll(HeavyBeetle instigator, Transform playerTransform)
    {
        foreach(var b in _members)
            if(b != instigator) b.ReceiveGroupAlert(playerTransform);
    }
}
