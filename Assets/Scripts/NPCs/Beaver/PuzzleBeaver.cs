using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

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


    public Dialogue Dialogue => _dialogues[_currentDialogue];

    public Transform Transform => transform;

    public Sprite Image => _imagedialogue;

    private void Start()
    {
        _EIndicator.enabled = false;
        _exclamation.enabled = false;
        _anim = GetComponentInChildren<Animator>();
        _cam = CameraStateManager.Instance.CurrentCamera.transform;
        LevelOneManager.Instance.OnBridgeConstructed += BridgeFinished;
    }
    private void Update()
    {
        if (_exclamation.enabled) FollowPlayer();
        else if (_EIndicator.enabled) FollowPlayer();
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
    }

    public void OnDialogueEnd()
    {
        if(_started)
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
}
