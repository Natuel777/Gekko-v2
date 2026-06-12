using UnityEngine;

public class Blueberry : Collectible
{
    [SerializeField] private NotificationSO _notificationData;

    public override Collectible CreateCollectibleType()
    {
        return GameManager.Instance.factory.Create(collectibleName, transform.position, transform.rotation);
    }

    public override void Grab()
    {
        GetComponent<Collider>().enabled = false;
        GameManager.Instance.Pj.OnBlueberryCollected();
        GetComponentInChildren<CollectableView>().Collect();
    }

    public override void ReturnToFactory()
    {
        CollectiblesRegister.RegisterCollectible(collectibleName);
        string msg = $"{CollectiblesRegister.GetCollectibleCount(collectibleName)}";
        UIManager.Instance.notifications.ShowOrUpdate(_notificationData, msg);
        //GameManager.Instance.factory.ReturnToPool(this);
        Destroy(gameObject); // Requiere rápida optimización
    }
}
