using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

public class BabyDuck : MonoBehaviour, IDialogueable, IInteractable
{
    [SerializeField] private float _rotationSpeed = 5f;
    [SerializeField] private bool _onWater;
    private Animator _anim;
    private bool _start;
    private bool _found;
    private Transform _cam;
    private bool _interacted;
    private Transform _playerTransform;
    private AudioSource _source;
    [SerializeField] private ParticleSystem _cuack;

    [Header("Canvas")]
    [SerializeField] Dialogue[] _dialogue;
    private int _currentDialogue;
    [SerializeField] Sprite _imagedialogue;
    [SerializeField] Canvas _canvas;
    [SerializeField] Image _EIndicator;
    [SerializeField] private AudioClip _audioTalk;
    [Header("Cameras")]
    [SerializeField] private CinemachineCamera _duckCam;
    private LookAtTarget _lookAtPlayer;
    public Dialogue Dialogue => _dialogue[_currentDialogue];

    public Transform Transform => transform;

    public Sprite Image => _imagedialogue;

    public AudioClip AudioClip => _audioTalk;

    private void Start()
    {
        _anim = GetComponentInChildren<Animator>();
        if(!_onWater) _anim.SetBool("BabyDuckisWalk",false);
        else _anim.SetBool("BabyDuckisWalk", true);
        _cam = CameraStateManager.Instance.CurrentCamera.transform;
        _playerTransform = GameManager.Instance.Pj.transform;
        _EIndicator.enabled = false;
        _lookAtPlayer = new LookAtTarget(_rotationSpeed, transform, lockYAxis: true);
        LevelOneManager.Instance.OnMissionDuckStarted += MissionStarted;
        _source = GetComponent<AudioSource>();
        _source.resource = _audioTalk;
        Cuack(true);
    }
    private void Update()
    {
        if (_EIndicator.enabled) FollowPlayer();
        if (_interacted)
        {
            _lookAtPlayer.ArtificialUpdate();
            _lookAtPlayer.StartLooking(_playerTransform);
        }

    }
    private void Cuack(bool activate)
    {
        if(activate)
        {
            StartCoroutine(CuackSound());
            _cuack.Play();
            return;
        }
        StopAllCoroutines();
        _source.Stop();
        _cuack.Stop();
    }
    private IEnumerator CuackSound()
    {
        while (true)
        {
            _source.Play();
            yield return new WaitForSeconds(2);
        }
    }
    private void FollowPlayer()
    {
        Vector3 forward = transform.position - _cam.position;
        Vector3 newForward = new Vector3(forward.x, 0, forward.z);
        _canvas.transform.forward = newForward;
    }
    private void Teleport()
    {
        Transform pos = LevelOneManager.Instance.DuckFound();
        if(_onWater) _anim.SetBool("BabyDuckisWalk", false);
        transform.position = pos.position;
        transform.rotation = pos.rotation;
        ScreenFader.Instance.OnFadeMiddle -= Teleport;
    }
    public void OnDialogueStart()
    {
        Cuack(false);
        _interacted = true;
        _duckCam.Priority = 30;
    }

    public void OnDialogueEnd()
    {
        if (!_start)
        {
            _duckCam.Priority = 0;
            Cuack(true);
        }
        else if (!_found)
        {
            _found = true;
            Cuack(false);
            ScreenFader.Instance.OnFadeMiddle += Teleport;
            ScreenFader.Instance.OnFadeCompleted += FinishCam;
            ScreenFader.Instance.StartFade();
            _currentDialogue++;
        }
        else _duckCam.Priority = 0;
        _interacted = false;
    }
    private void MissionStarted()
    {
        _currentDialogue++;
        _start = true;
    }
    private void FinishCam()
    {
        _duckCam.Priority = 0;
        ScreenFader.Instance.OnFadeCompleted -= FinishCam;
    }
    public void Interacted()
    {
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
        LevelOneManager.Instance.OnMissionDuckStarted -= MissionStarted;
    }
}
