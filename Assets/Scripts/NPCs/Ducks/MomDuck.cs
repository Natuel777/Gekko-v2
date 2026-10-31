using UnityEngine;
using UnityEngine.UI;

public class MomDuck : NPCBase
{
    private bool _suscribed;
    private Animator _anim;
    [SerializeField] private int _totalDucks;
    [SerializeField] private Transform _pjTP;
    private int _currentDucks;
    private bool _started;
    private bool _finish;
    [Header("Canvas")]
    [SerializeField] Image _exclamation;
    [Header("Recompensa")]
    [SerializeField] private NotificationSO _notificationData;

    protected override void Start()
    {
        base.Start();
        _anim = GetComponentInChildren<Animator>();
        _anim.SetBool("DuckisWalk", false);
        _suscribed = true;
        Suscribe();
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
    }

    private void DuckFound()
    {
        _currentDialogue++;
        _currentDucks++;
        if (_currentDucks >= _totalDucks)
        {
            _finish = true;
            ScreenFader.Instance.OnFadeMiddle += TPPlayer;
            ScreenFader.Instance.OnFadeCompleted += StartDialogue;
            ScreenFader.Instance.StartFade();
            LevelOneManager.Instance.MissionDuckFinish();
        }
    }
    private void TPPlayer()
    {
        GameManager.Instance.Pj.PjController.Teleport(_pjTP.position);
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
        if (!_started)
        {
            _started = true;
            _currentDialogue++;
            LevelOneManager.Instance.MissionDuck();
        }

        if (_finish == true)
        {
            CollectiblesRegister.RegisterCollectible(_notificationData.Name);
            int count = CollectiblesRegister.GetCollectibleCount(_notificationData.Name);
            UIManager.Instance.notifications.ShowRaspberryCollectible(_notificationData, count);

            var pj = GameManager.Instance.Pj;
            pj.health.SetHealth(pj.health.MaxHealth);
            pj.BlueberryTracker.ActivateBoost(_notificationData.SpeedBoostMultiplier, _notificationData.SpeedBoostTimer);
            _finish = false;
            _currentDialogue++;
        }
        _cam.Priority = 0;
        _interacted = false;
    }
    public override void Interacted()
    {
        _exclamation.enabled = false;
        base.Interacted();
    }
    private void OnEnable()
    {
        if (_suscribed) Suscribe();
    }

    private void Suscribe()
    {
        if (LevelOneManager.Instance == null) return;
        LevelOneManager.Instance.OnDuckFound -= DuckFound;
        LevelOneManager.Instance.OnDuckFound += DuckFound;
    }

    private void OnDisable()
    {
        if (LevelOneManager.Instance == null) return;
        LevelOneManager.Instance.OnDuckFound -= DuckFound;
    }
}
