using UnityEngine;

public class StrawBerry : Collectible
{
    [SerializeField] private LevelFlags _id;
    private StrawBerryView _sv;
    [SerializeField] private GameObject mainStar;
    [SerializeField] private GameObject starParticles;
    private bool grabbed = false;

    [Header("Feedback al agarrarla")]
    [SerializeField] private SoundNames _pickupSound = SoundNames.RuneActivation;
    [Tooltip("Micro pausa (segundos reales) al agarrarla. 0 = sin pausa.")]
    [SerializeField] private float _hitstopDuration = 0.08f;
    [Tooltip("Fuerza de la sacudida de cámara (las de las barricadas usan 1.5).")]
    [SerializeField] private float _shakeForce = 0.6f;
    [Tooltip("Estallido de curación que sale del centro de Gekko (en el mundo, no hereda la escala chica de Gekko).")]
    [SerializeField] private GameObject _healFlash;
    [Tooltip("Escala del estallido y de la estrella grande sobre Gekko.")]
    [SerializeField] private float _healFlashScale = 1f;
    [Tooltip("Destello en los bordes de la pantalla al curarse (misma viñeta que el daño). Alpha 0 = sin destello.")]
    [SerializeField] private Color _healVignetteColor = new Color(1f, 0.82f, 0.3f, 0.75f);

    private void Start()
    {
        if (SaveManager.Instance)
            if (SaveManager.Instance.Has(_id.ToString())) Destroy(gameObject);

        _sv = new StrawBerryView(transform, this);
    }
    public override CollectibleType CollectibleType => CollectibleType.StrawBerry;
    public override Collectible CreateCollectibleType()
    {
        return GameManager.Instance.factory.Create(CollectibleType, transform.position, transform.rotation);
    }
    private void Update()
    {
        _sv.VirtualUpdate();
    }
    public override void Grab()
    {
        if (grabbed) return;
        grabbed = true;
        GetComponent<Collider>().enabled = false;
        CollectiblesRegister.RegisterCollectible(_notificationData.Name);
        int count = CollectiblesRegister.GetCollectibleCount(_notificationData.Name);
        UIManager.Instance.notifications.ShowRaspberryCollectible(_notificationData, count);

        var pj = GameManager.Instance.Pj;
        pj.health.SetHealth(pj.health.MaxHealth);
        pj.BlueberryTracker.ActivateBoost(_notificationData.SpeedBoostMultiplier, _notificationData.SpeedBoostTimer);
        if (SaveManager.Instance)
            SaveManager.Instance.Set(_id.ToString());
        PlayPickupFeedback(pj);
        _sv.Collect();
    }

    private void PlayPickupFeedback(Player pj)
    {
        if (AudioManager.instance && _pickupSound != SoundNames.None) AudioManager.instance.Play(_pickupSound);

        if (_shakeForce > 0f) EventManager.Trigger<float>("OnCameraShake", _shakeForce);

        if (pj)
        {
            // Centro real de Gekko (su raíz está en los pies y está escalado a 0.17): se usa el collider si hay.
            var col = pj.GetComponentInChildren<Collider>();
            Vector3 center = col ? col.bounds.center : pj.transform.position + Vector3.up * 0.3f;

            // Sin padre: si cuelga de Gekko hereda su escala y queda diminuto.
            if (_healFlash) SpawnFx(_healFlash, center);
            if (mainStar) SpawnFx(mainStar, center); // estrella amarilla grande, la misma de la frutilla
        }

        if (_healVignetteColor.a > 0f && DamageFeedback.Instance) DamageFeedback.Instance.FlashVignette(_healVignetteColor);

        if (_hitstopDuration > 0f) StartCoroutine(Hitstop());
    }

    private void SpawnFx(GameObject prefab, Vector3 position)
    {
        var fx = Instantiate(prefab, position, Quaternion.identity);
        fx.transform.localScale = prefab.transform.localScale * _healFlashScale;
        Destroy(fx, 2f);
    }

    // Igual que el hitstop de DamageFeedback: tiempo real y sin pisar el timeScale si el jugador pausó en el medio.
    private System.Collections.IEnumerator Hitstop()
    {
        if (Time.timeScale <= 0f) yield break; // ya hay otra pausa/hitstop en curso
        float previous = Time.timeScale;
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(_hitstopDuration);
        bool paused = GameManager.Instance != null && GameManager.Instance.CanPause;
        if (!paused) Time.timeScale = previous;
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
