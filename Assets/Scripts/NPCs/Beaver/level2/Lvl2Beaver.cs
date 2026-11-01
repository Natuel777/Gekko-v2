using UnityEngine;
using UnityEngine.UI;

public class Lvl2Beaver : NPCBase
{
    [SerializeField] Image _exclamation;
    [Header("Dam")]
    [SerializeField] private Dialogue _damDialogue;
    [SerializeField] private DamCollider _dam;
    private bool _touched;
    [SerializeField] private Transform _pjTP;
    public override Dialogue Dialogue 
    { 
        get
        {
            if (_touched)
            {
                _touched = false;
                return _damDialogue;
            }
            return _dialogue[_currentDialogue];
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
        base.Interacted();
    }
    
    private void DamTouched()
    {
        _touched = true;
        ScreenFader.Instance.OnFadeMiddle += TPPlayer;
        ScreenFader.Instance.OnFadeCompleted += StartDialogue;
        ScreenFader.Instance.StartFade();
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
    private void OnEnable()
    {
        _dam.OnTouched += DamTouched;
    }
    private void OnDisable()
    {
        _dam.OnTouched -= DamTouched;
    }
}
