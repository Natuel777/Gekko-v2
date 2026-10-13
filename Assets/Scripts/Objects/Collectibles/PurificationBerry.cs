using UnityEngine;

// Arándano de purificación: NO cura ni acelera (eso lo hace Blueberry, sin tocarlo). Se cuenta aparte en
// CollectiblesRegister (clave propia, _notificationData.Name). Al cruzar la PurificationGate con
// suficientes en inventario, reduce permanentemente los golpes necesarios del jefe
// (CorruptedGeckoBoss.BlueberryHitReduction). Sin View/animación de recolección todavía (placeholder).
public class PurificationBerry : Collectible
{
    public override Collectible CreateCollectibleType()
    {
        return GameManager.Instance.factory.Create(_notificationData.Name, transform.position, transform.rotation);
    }

    public override void Grab()
    {
        if(_grabbed) return;
        _grabbed = true;

        GetComponent<Collider>().enabled = false;

        CollectiblesRegister.RegisterCollectible(_notificationData.Name);
        int count = CollectiblesRegister.GetCollectibleCount(_notificationData.Name);
        UIManager.Instance.notifications.ShowOrUpdate(_notificationData, $"{count}");

        if (AudioManager.instance) AudioManager.instance.Play(SoundNames.Purify);

        ReturnToFactory();
    }

    public override void ReturnToFactory()
    {
        Destroy(gameObject);
    }
}
