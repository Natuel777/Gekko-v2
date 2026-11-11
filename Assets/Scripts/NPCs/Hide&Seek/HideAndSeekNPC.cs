using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HideAndSeekNPC : NPCBase
{
    [SerializeField] private Transform _spawn;
    [SerializeField] private Transform _PjSpawn;
    [SerializeField] private Transform[] _hidingSpots;
    [SerializeField] Image _exclamation;
    private AudioSource _source;
    private int _currentHide;
    private bool _hiding;
    private bool _found;
    private bool _finish;
    [Header("Recompensa")]
    [SerializeField] private NotificationSO _notificationData;

    protected override void Start()
    {
        base.Start();
        _source = GetComponent<AudioSource>();
        if (SaveManager.Instance)
        {
            if (SaveManager.Instance.Has(StrawberryFlags.Lvl2_HideSeek_2.ToString()))
            {
                _currentDialogue = 4;
                _exclamation.enabled = false;
                _finish = true;
                return;
            }
            else if (SaveManager.Instance.Has(StrawberryFlags.Lvl2_HideSeek_1.ToString()))
            {
                _currentDialogue = 2;
                _currentHide++;
            }
        }   
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
    public override void Interacted()
    {
        _exclamation.enabled = false;
        if(_hiding)
        {
            _hiding = false;
            _found = true;
        }
        if(_currentHide >= _hidingSpots.Length) _finish = true;
        if (_found)
        {
            Laugh(false);
            ScreenFader.Instance.OnFadeMiddle += Teleport;
            ScreenFader.Instance.OnFadeCompleted += StartDialogue;
            ScreenFader.Instance.StartFade();
        }
        else StartDialogue();
    }
    public override void OnDialogueEnd()
    {
        base.OnDialogueEnd();
        if (!_finish) _currentDialogue++;

        if (_found)
        {
            _found = false;
            GiveStrawberry();
            _currentHide++;
            if(!_finish)
            _exclamation.enabled = true;
        }
        else if (!_hiding && !_finish)
        {
            ScreenFader.Instance.OnFadeMiddle += Teleport;
            ScreenFader.Instance.StartFade();
        }
    }
    private void StartDialogue()
    {
        ScreenFader.Instance.OnFadeCompleted -= StartDialogue;
        if (UIManager.Instance == null || UIManager.Instance.HasActiveDialogue()) return;
        UIManager.Instance.StartDialogue(this);
    }
    private void Teleport()
    {
        if (_found)
        {
            transform.position = _spawn.position;
            transform.rotation = _spawn.rotation;
            GameManager.Instance.Pj.PjController.TeleportWRot(_PjSpawn);
        }
        else
        {
            transform.position = _hidingSpots[_currentHide].position;
            transform.rotation = _hidingSpots[_currentHide].rotation;
            Laugh(true);
            _hiding = true;
        }
        ScreenFader.Instance.OnFadeMiddle -= Teleport;
    }
    private void GiveStrawberry()
    {
        CollectiblesRegister.RegisterCollectible(_notificationData.Name);
        int count = CollectiblesRegister.GetCollectibleCount(_notificationData.Name);
        UIManager.Instance.notifications.ShowRaspberryCollectible(_notificationData, count);

        var pj = GameManager.Instance.Pj;
        pj.health.SetHealth(pj.health.MaxHealth);
        pj.BlueberryTracker.ActivateBoost(_notificationData.SpeedBoostMultiplier, _notificationData.SpeedBoostTimer);

        if (SaveManager.Instance)
        {
            if(_currentHide == 0)
                SaveManager.Instance.Set(StrawberryFlags.Lvl2_HideSeek_1.ToString());
            else if (_currentHide == 1)
                SaveManager.Instance.Set(StrawberryFlags.Lvl2_HideSeek_2.ToString());
        }

    }
    private void Laugh(bool activate)
    {
        if (activate)
        {
            StartCoroutine(LaughSound());
            return;
        }
        StopAllCoroutines();
        _source.Stop();
    }
    private IEnumerator LaughSound()
    {
        while (true)
        {
            _source.Play();
            yield return new WaitForSeconds(2);
        }
    }
}
