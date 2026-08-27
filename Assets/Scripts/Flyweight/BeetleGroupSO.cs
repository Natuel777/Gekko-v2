using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "BeetleGroupSO", menuName = "Scriptable Objects/BeetleGroupSO")]
public class BeetleGroupSO : ScriptableObject
{
    public List<HeavyBeetle> members = new();

    public void Register(HeavyBeetle b) => members.Add(b);
    public void Unregister(HeavyBeetle b) => members.Remove(b);

    public void AlertAll(HeavyBeetle instigator, Transform playerTransform)
    {
        foreach(var b in members)
            if(b != instigator) b.ReceiveGroupAlert(playerTransform);
    }
}