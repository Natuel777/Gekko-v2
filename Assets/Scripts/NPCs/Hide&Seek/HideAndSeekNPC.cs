using UnityEngine;
using UnityEngine.UI;

public class HideAndSeekNPC : NPCBase
{
    [SerializeField] private Transform[] _hidingSpots;
    [SerializeField] Image _exclamation;
    private int _currentHide;
    private bool _hiding;
    private bool _found;
    private bool _finish;
    [Header("Recompensa")]
    [SerializeField] private NotificationSO _notificationData;
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
        base.Interacted();
    }
    public override void OnDialogueEnd()
    {
        if (!_finish) _currentDialogue++;

        if (_found)
        {
            _found = false;
            GiveStrawberry();
        }
        else if (!_hiding && !_finish)
        {
            ScreenFader.Instance.OnFadeMiddle += Teleport;
            ScreenFader.Instance.StartFade();
        }
        base.OnDialogueEnd();
    }
    private void Teleport()
    {
        transform.position = _hidingSpots[_currentHide].position;
        transform.rotation = _hidingSpots[_currentHide].rotation;
        ScreenFader.Instance.OnFadeMiddle -= Teleport;
        _hiding = true;
    }
    private void GiveStrawberry()
    {
        CollectiblesRegister.RegisterCollectible(_notificationData.Name);
        int count = CollectiblesRegister.GetCollectibleCount(_notificationData.Name);
        UIManager.Instance.notifications.ShowRaspberryCollectible(_notificationData, count);

        var pj = GameManager.Instance.Pj;
        pj.health.SetHealth(pj.health.MaxHealth);
        pj.BlueberryTracker.ActivateBoost(_notificationData.SpeedBoostMultiplier, _notificationData.SpeedBoostTimer);
    }
}
