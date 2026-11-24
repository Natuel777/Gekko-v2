using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PuzzleBeaver : NPCBase
{
    private bool _suscribed;
    private Animator _anim;
    private bool _started;
    private bool _missionAccepted;
    private bool _finish;
    [Header("Canvas")]
    [SerializeField] Image _exclamation;
    [Header("Reward")]
    [SerializeField] private NotificationSO _notificationData;

    [Header("Player detection")]
    [Tooltip("Distancia a la que el Beaver detecta a Gekko: se gira hacia él y, la primera vez que lo detecta, lo llama con un sonido (una sola vez).")]
    [SerializeField] private float _detectionRange = 6f;

    private BugDetection _playerDetection;
    private bool _playerInRotationRange;
    private bool _callPlayed;


    protected override void Start()
    {
        base.Start();
        _exclamation.enabled = false;
        _anim = GetComponentInChildren<Animator>();
        _suscribed = true;
        Suscribe();
        _playerDetection = new BugDetection(transform, _playerTransform, _detectionRange);
    }


    protected override void Update()
    {
        if(_exclamation.enabled) FollowPlayer();
        
        else if(_EIndicator.enabled) FollowPlayer();

        UpdatePlayerRotation();
    }

    private void UpdatePlayerRotation()
    {
        bool inRange = _playerDetection.IsTargetInRange();

        if(inRange && !_playerInRotationRange)
        {
            _playerInRotationRange = true;
            _lookAtPlayer.StartLooking(_playerTransform);
            PlayCallSound();
        }
        
        else if(!inRange && _playerInRotationRange)
        {
            _playerInRotationRange = false;
            _lookAtPlayer.StopLooking();
        }

        _lookAtPlayer.ArtificialUpdate();
    }

    // Calls out to Gekko only the first time he enters the detection range.
    // The flag is only set once the sound can really be played (no AudioManager = try again on the next entry).
    private void PlayCallSound()
    {
        if(_callPlayed || AudioManager.instance == null) return;

        _callPlayed = true;
        AudioManager.instance.Play(SoundNames.BeaverVoice);
    }

    public void StartAnim()
    {
        if (_missionAccepted) return;
        _started = true;
        _exclamation.enabled = true;
        //_anim.
    }

    public override void Interacted()
    {
        _exclamation.enabled = false;
        base.Interacted();
    }

    private void BridgeFinished()
    {
        _currentDialogue++;
        _exclamation.enabled = true;
        _finish = true;
        // finished animation or dialogue, and give the strawberry
    }  

    public override void OnDialogueStart()
    {
        _anim.SetBool("isTalk",true);
        _cam.Priority = 30;
    }

    public override void OnDialogueEnd()
    {
        _cam.Priority = 0;
        if (_started)
        {
            _started = false;
            LevelOneManager.Instance.BeaverMissionTaken();
            _currentDialogue++;
            _missionAccepted = true;
        }

        if (_finish)
        {
            _anim.SetTrigger("RaiseArm");
            CollectiblesRegister.RegisterCollectible(_notificationData.Name);
            int count = CollectiblesRegister.GetCollectibleCount(_notificationData.Name);
            UIManager.Instance.notifications.ShowRaspberryCollectible(_notificationData, count);

            var pj = GameManager.Instance.Pj;
            pj.health.SetHealth(pj.health.MaxHealth);
            pj.BlueberryTracker.ActivateBoost(_notificationData.SpeedBoostMultiplier, _notificationData.SpeedBoostTimer);
            _finish = false;
            StartCoroutine(Count());
        }
        else _anim.SetBool("isTalk", false);

    }

    private IEnumerator Count()
    {
        yield return new WaitForSeconds(1f);
        _anim.SetBool("isTalk", false);
    }
    private void OnEnable()
    {
        if (_suscribed) Suscribe();
    }
    private void Suscribe()
    {
        if (LevelOneManager.Instance == null) return;
        LevelOneManager.Instance.OnBridgeConstructed -= BridgeFinished;
        LevelOneManager.Instance.OnBridgeConstructed += BridgeFinished;
    }
    private void OnDisable()
    {
        if (LevelOneManager.Instance == null) return;
        LevelOneManager.Instance.OnBridgeConstructed -= BridgeFinished;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);
    }
}
