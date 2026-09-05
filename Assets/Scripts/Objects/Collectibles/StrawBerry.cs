using UnityEditor.SceneManagement;
using UnityEngine;

public class StrawBerry : Collectible
{
    private StrawBerryView _sv;
    [SerializeField] private GameObject mainStar;
    [SerializeField] private GameObject starParticles;

    private void Start()
    {
        _sv = new StrawBerryView(transform, this);
    }
    public override Collectible CreateCollectibleType()
    {
        return GameManager.Instance.factory.Create(_notificationData.Name, transform.position, transform.rotation);
    }
    private void Update()
    {
        _sv.VirtualUpdate();
    }
    public override void Grab()
    {
        GetComponent<Collider>().enabled = false;
        CollectiblesRegister.RegisterCollectible(_notificationData.Name);
        int count = CollectiblesRegister.GetCollectibleCount(_notificationData.Name);
        UIManager.Instance.notifications.ShowRaspberryCollectible(_notificationData, count);

        var pj = GameManager.Instance.Pj;
        pj.health.SetHealth(pj.health.MaxHealth);
        pj.BlueberryTracker.ActivateBoost(_notificationData.SpeedBoostMultiplier, _notificationData.SpeedBoostTimer);

        _sv.Collect();
    }
    public void Particles()
    {
        GameObject mainStar_ps = Instantiate(mainStar, transform.position, Quaternion.identity);
        GameObject starParticles_ps = Instantiate(starParticles, transform.position, Quaternion.identity);

        Destroy(mainStar_ps, 2f);
        Destroy(starParticles_ps, 2f);
    }

    public override void ReturnToFactory()
    {
        Destroy(gameObject);
        //GameManager.Instance.factory.ReturnToPool(this);
    }
}
