using UnityEngine;

public abstract class Collectible : BaseObject
{
    private string _collectibleName;
    protected bool _grabbed;
    [SerializeField] protected NotificationSO _notificationData;
    public string CollectibleName => _collectibleName;
    public abstract CollectibleType CollectibleType { get; }
    protected void Awake()
    {
        _collectibleName = _notificationData.Name;
    }
    public abstract void Grab();
    public virtual Collectible CreateCollectibleType(){return null;}
    public abstract void ReturnToFactory();
}

public enum CollectibleType
{
    Blueberry,
    StrawBerry,
    PurificationBerry
}
