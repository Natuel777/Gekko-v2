using UnityEngine;

public class ShooteableObject : MonoBehaviour, IServiceConsumer
{
    public ShooteableObjectDataSO data;
    [SerializeField] private string _name;
    [SerializeField] protected BulletType bulletType;

    protected ParticleSystemFactory PSFactory;
    
    public BulletType BulletType => bulletType;
    public string Name => _name;

    public virtual bool TryResolveService()
    {
        if(PSFactory != null) return true;

        return ServiceLocator.Instance.TryGet(out PSFactory);
    }
}

public enum BulletType
{
    CarniborusPlantVenom,
    BugBullet
}