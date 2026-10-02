using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

public class MomDuck : MonoBehaviour, IDialogueable, IInteractable
{
    [SerializeField] private float _rotationSpeed = 5f;
    private Animator _anim;
    private Transform _cam;
    private Transform _playerTransform;
    [SerializeField] private int _totalDucks;
    [SerializeField] private Transform _pjTP;
    private int _currentDucks;
    private bool _started;
    private bool _finish;
    private bool _interacted;
    [Header("Canvas")]
    [SerializeField] Dialogue[] _dialogue;
    private int _currentDialogue;
    [SerializeField] Sprite _imagedialogue;
    [SerializeField] Canvas _canvas;
    [SerializeField] Image _EIndicator;
    [SerializeField] Image _exclamation;
    [SerializeField] private AudioClip _audioTalk;
    [Header("Cameras")]
    [SerializeField] private CinemachineCamera _duckCam;
    [Header("Recompensa")]
    [SerializeField] private NotificationSO _notificationData;

    private LookAtTarget _lookAtPlayer;
    public Dialogue Dialogue => _dialogue[_currentDialogue];

    public Transform Transform => transform;

    public Sprite Image => _imagedialogue;

    public AudioClip AudioClip => _audioTalk;

    void Start()
    {
        _anim = GetComponentInChildren<Animator>();
        _anim.SetBool("DuckisWalk", false);
        _EIndicator.enabled = false;
        _playerTransform = GameManager.Instance.Pj.transform;
        _cam = CameraStateManager.Instance.CurrentCamera.transform;
        _lookAtPlayer = new LookAtTarget(_rotationSpeed, transform, lockYAxis: true);
        LevelOneManager.Instance.OnDuckFound += DuckFound;
    }
    private void Update()
    {
        if (_exclamation.enabled) FollowPlayer();

        else if (_EIndicator.enabled) FollowPlayer();

        if (_interacted)
        {
            _lookAtPlayer.ArtificialUpdate();
            _lookAtPlayer.StartLooking(_playerTransform);
        }
        if (Input.GetKeyDown(KeyCode.H)) DuckFound();
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
    public void OnDialogueStart()
    {
        _interacted = true;
        _duckCam.Priority = 30;
    }

    public void OnDialogueEnd()
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
        _duckCam.Priority = 0;
        _interacted = false;
    }
    private void FollowPlayer()
    {
        Vector3 forward = transform.position - _cam.position;
        Vector3 newForward = new Vector3(forward.x, 0, forward.z);
        _canvas.transform.forward = newForward;
    }
    public void Interacted()
    {
        _exclamation.enabled = false;
        if (UIManager.Instance == null || UIManager.Instance.HasActiveDialogue()) return;
        UIManager.Instance.StartDialogue(this);
    }

    public void ShowInteractUI()
    {
        _EIndicator.enabled = true;
    }

    public void HideInteractUI()
    {
        _EIndicator.enabled = false;
    }

    private void OnDisable()
    {
        LevelOneManager.Instance.OnDuckFound -= DuckFound;
    }
}
