using UnityEngine;

public class RaspBerry : Collectible
{

    public override Collectible CreateCollectibleType()
    {
        return GameManager.Instance.factory.Create(_notificationData.Name, transform.position, transform.rotation);
    }

    public override void Grab()
    {
        GetComponent<Collider>().enabled = false;
        CollectiblesRegister.RegisterCollectible(_notificationData.Name);
        int count = CollectiblesRegister.GetCollectibleCount(_notificationData.Name);
        UIManager.Instance.notifications.ShowRaspberryCollectible(_notificationData, count);

        var pj = GameManager.Instance.Pj;
        pj.health.SetHealth(pj.health.MaxHealth);
        pj.PjController.ApplySpeedBoost(_notificationData.SpeedBoostMultiplier, _notificationData.SpeedBoostTimer);

        GetComponentInChildren<CollectableView>().Collect();
    }

    public override void ReturnToFactory()
    {
        GameManager.Instance.factory.ReturnToPool(this);
    }
}
