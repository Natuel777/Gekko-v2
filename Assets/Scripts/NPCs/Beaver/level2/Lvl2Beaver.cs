using UnityEngine;
using UnityEngine.UI;

public class Lvl2Beaver : NPCBase
{
    [SerializeField] Image _exclamation;
    [Header("Dam")]
    [SerializeField] private Dialogue _damDialogue;
    [SerializeField] private DamCollider _dam;
    private bool _touched;
    private bool _allCollected;
    [SerializeField] private Transform _pjTP;
    private MissionCollectiblesManager _collMan;
    [Header("Mission Collectibles")]
    [SerializeField] private Transform _collParent;
    [Header("Reward")]
    [SerializeField] private NotificationSO _notificationData;
    [Header("Player detection")]
    [Tooltip("Distancia a la que el Beaver detecta a Gekko: la primera vez que lo detecta, lo llama con un sonido (una sola vez).")]
    [SerializeField] private float _detectionRange = 21f;
    private BugDetection _playerDetection;
    private bool _callPlayed;
    public override Dialogue Dialogue 
    { 
        get
        {
            if (_touched)
            {
                _touched = false;
                return _damDialogue;
            }
            return _dialogue[_currentDialogue];
        }
    }
    protected override void Start()
    {
        base.Start();
        // Created before the saved-mission early return below, so the call also works when the mission is already done.
        _playerDetection = new BugDetection(transform, _playerTransform, _detectionRange);

        if (SaveManager.Instance) 
            if (SaveManager.Instance.Has(LevelFlags.Lvl2_Beaver.ToString()))
            {
                _exclamation.enabled = false;
                Destroy(_collParent.gameObject);
                _currentDialogue = 2;
                return;
            }
        _collMan = new MissionCollectiblesManager(_collParent);
        _collMan.OnAllCollected += AllCollected;
        _collMan.ArtificialOnEnable();

    }
    protected override void Update()
    {
        if (_exclamation.enabled) FollowPlayer();

        else if (_EIndicator.enabled) FollowPlayer();

        if (_interacted)
        {
            _lookAtPlayer.ArtificialUpdate();
            _lookAtPlayer.StartLooking(_playerTransform);
        }

        UpdateCallSound();
    }
    // Calls out to Gekko only the first time he enters the detection range.
    // The flag is only set once the sound can really be played (no AudioManager = try again later).
    private void UpdateCallSound()
    {
        if (_callPlayed || AudioManager.instance == null) return;
        if (!_playerDetection.IsTargetInRange()) return;

        _callPlayed = true;
        AudioManager.instance.Play(SoundNames.BeaverVoice);
    }
    #region Dam
    private void DamTouched()
    {
        _touched = true;
        ScreenFader.Instance.OnFadeMiddle += TPPlayer;
        ScreenFader.Instance.OnFadeCompleted += StartDialogue;
        ScreenFader.Instance.StartFade();
    }
    private void TPPlayer()
    {
        GameManager.Instance.Pj.PjController.Teleport(_pjTP.position);
    }
    #endregion
    private void AllCollected()
    {
        _currentDialogue++;
        _exclamation.enabled = true;
        _allCollected = true;
    }
    public override void Interacted()
    {
        _exclamation.enabled = false;
        base.Interacted();
    }
    private void StartDialogue()
    {
        ScreenFader.Instance.OnFadeMiddle -= TPPlayer;
        ScreenFader.Instance.OnFadeCompleted -= StartDialogue;
        if (UIManager.Instance == null || UIManager.Instance.HasActiveDialogue()) return;
        UIManager.Instance.StartDialogue(this);
    }
    public override void OnDialogueEnd()
    {
        if(_allCollected)
        {
            _currentDialogue++;
            _allCollected = false;
            CollectiblesRegister.RegisterCollectible(_notificationData.Name);
            int count = CollectiblesRegister.GetCollectibleCount(_notificationData.Name);
            UIManager.Instance.notifications.ShowRaspberryCollectible(_notificationData, count);

            var pj = GameManager.Instance.Pj;
            pj.health.SetHealth(pj.health.MaxHealth);
            pj.BlueberryTracker.ActivateBoost(_notificationData.SpeedBoostMultiplier, _notificationData.SpeedBoostTimer);
            if (SaveManager.Instance)
            {
                SaveManager.Instance.Set(LevelFlags.Lvl2_Beaver.ToString());
            }
        }
        base.OnDialogueEnd();
    }
    private void OnEnable()
    {
        _dam.OnTouched += DamTouched;
        if (_collMan == null) return;
        _collMan.OnAllCollected += AllCollected;
        _collMan.ArtificialOnEnable();
    }
    private void OnDisable()
    {
        _dam.OnTouched -= DamTouched;
        if (_collMan == null) return;
        _collMan.OnAllCollected -= AllCollected;
        _collMan.ArtificialOnDisable();
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);
    }
}
