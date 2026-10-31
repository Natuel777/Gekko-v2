using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

public class NPCBase : MonoBehaviour, IDialogueable, IInteractable
{
    [SerializeField] protected float _rotationSpeed = 5f;
    protected Transform _PJCam;
    protected Transform _playerTransform;
    protected LookAtTarget _lookAtPlayer;
    protected bool _interacted;
    [Header("Canvas")]
    [SerializeField] protected Dialogue[] _dialogue;
    protected int _currentDialogue;
    [SerializeField] protected Sprite _imagedialogue;
    [SerializeField] protected Canvas _canvas;
    [SerializeField] protected Image _EIndicator;
    [SerializeField] protected AudioClip _audioTalk;
    [Header("Cameras")]
    [SerializeField] protected CinemachineCamera _cam;

    public Dialogue Dialogue => _dialogue[_currentDialogue];
    public Transform Transform => transform;
    public Sprite Image => _imagedialogue;
    public AudioClip AudioClip => _audioTalk;
    protected virtual void Start()
    {
        _playerTransform = GameManager.Instance.Pj.transform;
        _EIndicator.enabled = false;
        _PJCam = CameraStateManager.Instance.CurrentCamera.transform;
        _lookAtPlayer = new LookAtTarget(_rotationSpeed, transform, lockYAxis: true);
    }
    protected virtual void Update()
    {
        if (_EIndicator.enabled) FollowPlayer();

        if (_interacted)
        {
            _lookAtPlayer.ArtificialUpdate();
            _lookAtPlayer.StartLooking(_playerTransform);
        }
    }
    protected void FollowPlayer()
    {
        Vector3 forward = transform.position - _PJCam.position;
        Vector3 newForward = new Vector3(forward.x, 0, forward.z);
        _canvas.transform.forward = newForward;
    }
    public virtual void Interacted()
    {
        if (UIManager.Instance == null || UIManager.Instance.HasActiveDialogue()) return;
        UIManager.Instance.StartDialogue(this);
    }
    public virtual void OnDialogueStart()
    {
        _cam.Priority = 30;
        _interacted = true;
    }
    public virtual void OnDialogueEnd()
    {
        _cam.Priority = 0;
        _interacted = false;
    }

    public virtual void ShowInteractUI()
    {
        _EIndicator.enabled = true;
    }
    public virtual void HideInteractUI()
    {
        _EIndicator.enabled = false;
    }
}
