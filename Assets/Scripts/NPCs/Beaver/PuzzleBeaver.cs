using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;

public class PuzzleBeaver : MonoBehaviour, IInteractable, IDialogueable
{
    private Animator _anim;
    private Transform _cam;
    private bool _started;
    private bool _missionAccepted;
    private bool _finish;
    [SerializeField] Dialogue[] _dialogues;
    [SerializeField] Sprite _imagedialogue;
    [SerializeField] Canvas _canvas;
    [SerializeField] Image _exclamation;
    [SerializeField] Image _EIndicator;
    private int _currentDialogue;
    [SerializeField] private NotificationSO _notificationData;

    [Header("Rotación hacia el jugador")]
    [SerializeField] private float _detectionRange = 6f;
    [SerializeField] private float _rotationSpeed = 5f;

    private Transform _playerTransform;
    private BugDetection _playerDetection;
    private LookAtTarget _lookAtPlayer;
    private bool _playerInRotationRange;

    public Dialogue Dialogue => _dialogues[_currentDialogue];

    public Transform Transform => transform;

    public Sprite Image => _imagedialogue;

    [Header("Cameras")]
    [SerializeField] private CinemachineCamera _gekkoCam;
    [SerializeField] private CinemachineCamera _beaverCam;

    private void Start()
    {
        _EIndicator.enabled = false;
        _exclamation.enabled = false;
        _anim = GetComponentInChildren<Animator>();
        _cam = CameraStateManager.Instance.CurrentCamera.transform;
        LevelOneManager.Instance.OnBridgeConstructed += BridgeFinished;

        _playerTransform = GameManager.Instance.Pj.transform;
        _playerDetection = new BugDetection(this, _playerTransform, _detectionRange);
        _lookAtPlayer = new LookAtTarget(_rotationSpeed, transform, lockYAxis: true);
    }
    private void Update()
    {
        if (_exclamation.enabled) FollowPlayer();
        else if (_EIndicator.enabled) FollowPlayer();

        UpdatePlayerRotation();
    }

    private void UpdatePlayerRotation()
    {
        bool inRange = _playerDetection.IsTargetInRange();

        if(inRange && !_playerInRotationRange)
        {
            _playerInRotationRange = true;
            _lookAtPlayer.StartLooking(_playerTransform);
        }
        else if(!inRange && _playerInRotationRange)
        {
            _playerInRotationRange = false;
            _lookAtPlayer.StopLooking();
        }

        _lookAtPlayer.ArtificialUpdate();
    }
    public void StartAnim()
    {
        if (_missionAccepted) return;
        _started = true;
        _exclamation.enabled = true;
        //_anim.
    }

    public void Interacted()
    {
        _exclamation.enabled = false;
        if (UIManager.Instance == null || UIManager.Instance.HasActiveDialogue()) return;
        UIManager.Instance.StartDialogue(this);
    }
    private void BridgeFinished()
    {
        _currentDialogue++;
        _exclamation.enabled = true;
        _finish = true;
        //animacion o dialogo de terminado y dar frutilla
    }  
    public void OnDialogueStart()
    {
        //anim
        _beaverCam.Priority = 30;
    }

    public void OnDialogueEnd()
    {
        _beaverCam.Priority = 0;
        if (_started)
        {
            _started = false;
            LevelOneManager.Instance.BeaverMissionTaken();
            _currentDialogue++;
            _missionAccepted = true;
        }
        if(_finish)
        {
            CollectiblesRegister.RegisterCollectible("PF_CollectableBlueberry");
            int count = CollectiblesRegister.GetCollectibleCount("PF_CollectableBlueberry");
            UIManager.Instance.notifications.ShowRaspberryCollectible(_notificationData, count);

            var pj = GameManager.Instance.Pj;
            pj.health.SetHealth(pj.health.MaxHealth);
            pj.PjController.ApplySpeedBoost(1.3f, 10f);
            _finish = false;
        }
    }
    private void FollowPlayer()
    {
        Vector3 forward = transform.position - _cam.position;
        Vector3 newForward = new Vector3(forward.x, 0, forward.z);
        _canvas.transform.forward = newForward;
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
        LevelOneManager.Instance.OnBridgeConstructed -= BridgeFinished;
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);
    }
}
