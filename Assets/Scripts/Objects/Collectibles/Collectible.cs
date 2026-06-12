using UnityEngine;

public abstract class Collectible : BaseObject
{
    public string collectibleName;
    protected bool _grabbed;
    public abstract void Grab();
    public virtual Collectible CreateCollectibleType(){return null;}
    public abstract void ReturnToFactory();
}
