using UnityEngine;

public abstract class CollectableView
{
    protected float _timerCollected = 0;
    [SerializeField] protected float _maxTimeCollected;
    [SerializeField] protected Collectible _collectible;
    public abstract void Collect();

}
