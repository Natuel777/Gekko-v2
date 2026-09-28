using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

public class LazyLadybug : MonoBehaviour, IDialogueable, IInteractable
{
    [SerializeField] private float _speed;
    [SerializeField] private float _rotationSpeed = 5f;
    [SerializeField] private Transform _finalPos;
    [Header("Canvas")]
    [SerializeField] Dialogue _dialogue;
    [SerializeField] Sprite _imagedialogue;
    [SerializeField] Canvas _canvas;
    [SerializeField] Image _EIndicator;
    [SerializeField] private AudioClip _audioTalk;
    [Header("Cameras")]
    [SerializeField] private CinemachineCamera _bugCam;
    private bool _interacted;
    private Transform _playerTransform;
    private Transform _cam;
    private LookAtTarget _lookAtPlayer;
    public Dialogue Dialogue => _dialogue;
    public Transform Transform => transform;
    public Sprite Image => _imagedialogue;
    public AudioClip AudioClip => _audioTalk;

    private void Start()
    {
        _playerTransform = GameManager.Instance.Pj.transform;
        _EIndicator.enabled = false;
        _cam = CameraStateManager.Instance.CurrentCamera.transform;
        _lookAtPlayer = new LookAtTarget(_rotationSpeed, transform, lockYAxis: true);

    }
    private void Update()
    {
        if (_EIndicator.enabled) FollowPlayer();

       if(_interacted) _lookAtPlayer.StartLooking(_playerTransform);

    }
    private void FollowPlayer()
    {
        Vector3 forward = transform.position - _cam.position;
        Vector3 newForward = new Vector3(forward.x, 0, forward.z);
        _canvas.transform.forward = newForward;
    }

    public void Interacted()
    {
        if (UIManager.Instance == null || UIManager.Instance.HasActiveDialogue()) return;
        UIManager.Instance.StartDialogue(this);

    }
    public void OnDialogueStart()
    {
        _bugCam.Priority = 30;
        _interacted = true;
    }
    public void OnDialogueEnd()
    {
        _interacted = false;
        StartCoroutine(Move());
    }

    public void ShowInteractUI()
    {
        _EIndicator.enabled = true;
    }
    public void HideInteractUI()
    {
        _EIndicator.enabled = false;
    }

    private IEnumerator Move()
    {
        GameManager.Instance.Pj.Inputs(false);
        _lookAtPlayer.StartLooking(_finalPos);

        var dir = _finalPos.position - transform.position;

        transform.position += dir.normalized * _speed * Time.deltaTime;

        if (dir.magnitude < 0.25f)
        {
            GameManager.Instance.Pj.Inputs(true);
            _bugCam.Priority = 0;
            Destroy(this);
        }
        yield return new WaitForEndOfFrame();
        
    }
}
