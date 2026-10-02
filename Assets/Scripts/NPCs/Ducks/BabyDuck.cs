using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

public class BabyDuck : MonoBehaviour, IDialogueable, IInteractable
{
    [SerializeField] private bool _onWater;
    private Animator _anim;
    private bool _found;

    [Header("Canvas")]
    [SerializeField] Dialogue[] _dialogue;
    private int _currentDialogue;
    [SerializeField] Sprite _imagedialogue;
    [SerializeField] Canvas _canvas;
    [SerializeField] Image _EIndicator;
    [SerializeField] private AudioClip _audioTalk;
    [Header("Cameras")]
    [SerializeField] private CinemachineCamera _duckCam;
    public Dialogue Dialogue => _dialogue[_currentDialogue];

    public Transform Transform => transform;

    public Sprite Image => _imagedialogue;

    public AudioClip AudioClip => _audioTalk;

    private void Start()
    {
        _anim = GetComponent<Animator>();
        if(!_onWater) _anim.SetBool("BabyDuckisWalk",false);
        else _anim.SetBool("BabyDuckisWalk", true);
    }

    public void OnDialogueStart()
    {
        _duckCam.Priority = 30;

    }

    public void OnDialogueEnd()
    {
        if(!_found)
        {
            _found = true;
            _currentDialogue++;
        }
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
}
