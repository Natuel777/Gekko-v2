using System;
using UnityEngine;

public class MissionCollectiblesManager
{
    private MisionCollectible[] _collectibles;
    private int _totalCollected;
    public event Action OnAllCollected;
    public int TotalCollected => _totalCollected;
    public int GetLength => _collectibles.Length;
    public MissionCollectiblesManager(Transform objParent)
    {
        _collectibles = objParent.GetComponentsInChildren<MisionCollectible>();

        foreach (MisionCollectible c in _collectibles)
        {
            c.OnCollected += ObjectCollected;
        }
    }
    public void ObjectCollected()
    {
        _totalCollected++;
        Debug.Log(_totalCollected);
        if(_totalCollected >= _collectibles.Length)
        {
            OnAllCollected?.Invoke();
            Debug.Log("all");
        }
    }
    public void ArtificialOnEnable()
    {
        if (_collectibles == null) return;
        foreach (MisionCollectible c in _collectibles)
        {
            c.OnCollected -= ObjectCollected;
            c.OnCollected += ObjectCollected;
        }
    }
    public void ArtificialOnDisable()
    {
        foreach (MisionCollectible c in _collectibles)
        {
            c.OnCollected -= ObjectCollected;
        }
    }
}
