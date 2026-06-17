using UnityEngine;

public class Blueberry : Collectible
{
    public override Collectible CreateCollectibleType()
    {
        return GameManager.Instance.factory.Create(_notificationData.Name, transform.position, transform.rotation);
    }

    public override void Grab()
    {
        GetComponent<Collider>().enabled = false;
        GameManager.Instance.Pj.OnBlueberryCollected();
        GetComponentInChildren<CollectableView>().Collect();
    }

    public override void ReturnToFactory()
    {
        CollectiblesRegister.RegisterCollectible(_notificationData.Name);
        string msg = $"{CollectiblesRegister.GetCollectibleCount(_notificationData.Name)}";
        UIManager.Instance.notifications.ShowOrUpdate(_notificationData, msg);
        //GameManager.Instance.factory.ReturnToPool(this);
        Destroy(gameObject); // Requiere rápida optimización
    }
}
