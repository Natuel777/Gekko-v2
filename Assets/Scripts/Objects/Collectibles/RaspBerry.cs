using UnityEngine;

public class RaspBerry : Collectible
{
    [SerializeField] private NotificationSO _notificationData;
    [SerializeField] private float _boostDuration = 10f;

    public override Collectible CreateCollectibleType()
    {
        return GameManager.Instance.factory.Create(collectibleName, transform.position, transform.rotation);
    }

    public override void Grab()
    {
        GetComponent<Collider>().enabled = false;
        CollectiblesRegister.RegisterCollectible(collectibleName);
        int count = CollectiblesRegister.GetCollectibleCount(collectibleName);
        UIManager.Instance.notifications.ShowRaspberryCollectible(_notificationData, count);

        var pj = GameManager.Instance.Pj;
        pj.health.SetHealth(pj.health.MaxHealth);
        pj.PjController.ApplySpeedBoost(1.3f, _boostDuration);

        GetComponentInChildren<CollectableView>().Collect();
    }

    public override void ReturnToFactory()
    {
        GameManager.Instance.factory.ReturnToPool(this);
    }
}
