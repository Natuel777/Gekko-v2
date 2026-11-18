using UnityEngine;

public class Blueberry : Collectible
{
    private BlueberryView _bv;
    [SerializeField] private Transform _transform;
    private bool grabbed = false;
    [SerializeField] private Collider _col2;

    [Header("Feedback al agarrarlo")]
    [SerializeField] private SoundNames _pickupSound = SoundNames.PlayerSlurp;
    [Tooltip("Cuánto sube el tono por cada arándano seguido del combo (0.07 = +7%).")]
    [SerializeField] private float _comboPitchStep = 0.07f;
    [SerializeField] private int _comboPitchMaxSteps = 5;
    [SerializeField] private GameObject _pickupParticles;

    private void Start()
    {
        _bv = new BlueberryView(GetComponentInChildren<Renderer>(), transform,this);
    }
    public override CollectibleType CollectibleType => CollectibleType.Blueberry;
    public override Collectible CreateCollectibleType()
    {
        return GameManager.Instance.factory.Create(CollectibleType, transform.position, transform.rotation);
    }

    public override void Grab()
    {
        if (grabbed) return;
        grabbed = true;
        GetComponent<Collider>().enabled = false;
        _col2.enabled = false;
        GameManager.Instance.Pj.OnBlueberryCollected();
        PlayPickupFeedback();
        _bv.Collect();
    }

    private void PlayPickupFeedback()
    {
        // Sonido: cada arándano seguido del combo suena un poco más agudo (se siente que vas llegando).
        if (AudioManager.instance && _pickupSound != SoundNames.None)
        {
            var tracker = GameManager.Instance.Pj.BlueberryTracker;
            int combo = tracker != null ? tracker.ComboCount : 0;
            int steps = Mathf.Clamp(combo - 1, 0, _comboPitchMaxSteps);
            AudioManager.instance.PlayPitched(_pickupSound, 1f + _comboPitchStep * steps);
        }

        if (_pickupParticles)
        {
            var ps = Instantiate(_pickupParticles, transform.position, Quaternion.identity);
            Destroy(ps, 2f);
        }
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
