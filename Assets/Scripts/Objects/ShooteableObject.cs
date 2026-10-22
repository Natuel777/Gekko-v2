using UnityEngine;

public class ShooteableObject : MonoBehaviour
{
    public ShooteableObjectDataSO data;
    [SerializeField] private string _name;
    [SerializeField] protected BulletType bulletType;
    
    public BulletType BulletType => bulletType;
    public string Name => _name;
}

public enum BulletType
{
    CarniborusPlantVenom,
    BugBullet
}