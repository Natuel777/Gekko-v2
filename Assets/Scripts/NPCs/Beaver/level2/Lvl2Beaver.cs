using UnityEngine;
using UnityEngine.UI;

public class Lvl2Beaver : NPCBase
{
    [SerializeField] Image _exclamation;
    [Header("Dam")]
    [SerializeField] private Dialogue _damDialogue;
    [SerializeField] private DamCollider _dam;
    private bool _touched;
    private bool _allCollected;
    [SerializeField] private Transform _pjTP;
    private MissionCollectiblesManager _collMan;
    [Header("Mission Collectibles")]
    [SerializeField] private Transform _collParent;
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
    protected override void Start()
    {
        base.Start();
       // _collMan = new MissionCollectiblesManager(_collParent);
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
    #region Dam
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
    #endregion
    private void AllCollected()
    {
        _currentDialogue++;
        _exclamation.enabled = true;
        _allCollected = true;
    }
    public override void Interacted()
    {
        _exclamation.enabled = false;
        base.Interacted();
    }
    private void StartDialogue()
    {
        ScreenFader.Instance.OnFadeMiddle -= TPPlayer;
        ScreenFader.Instance.OnFadeCompleted -= StartDialogue;
        if (UIManager.Instance == null || UIManager.Instance.HasActiveDialogue()) return;
        UIManager.Instance.StartDialogue(this);
    }
    public override void OnDialogueEnd()
    {
        if(_allCollected)
        {
            _currentDialogue++;
            _allCollected = false;
        }
        base.OnDialogueEnd();
    }
    private void OnEnable()
    {
        _dam.OnTouched += DamTouched;
       // _collMan.OnAllCollected += AllCollected;
       // _collMan.ArtificialOnEnable();
    }
    private void OnDisable()
    {
        _dam.OnTouched -= DamTouched;
       // _collMan.OnAllCollected -= AllCollected;
       // _collMan.ArtificialOnDisable();
    }
}
