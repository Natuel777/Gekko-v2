using System.Collections;
using UnityEngine;

public class BabyDuck : NPCBase
{
    [SerializeField] private bool _onWater;
    private Animator _anim;
    private bool _start;
    private bool _found;
    private AudioSource _source;
    [SerializeField] private ParticleSystem _cuack;

    private bool _suscribed;

    protected override void Start()
    {
        base.Start();
        _anim = GetComponentInChildren<Animator>();
        if(!_onWater) _anim.SetBool("BabyDuckisWalk",false);
        else _anim.SetBool("BabyDuckisWalk", true);
        _suscribed = true;
        Suscribe();
        _source = GetComponent<AudioSource>();
        _source.resource = _audioTalk;
        Cuack(true);
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
    private void Teleport()
    {
        Transform pos = LevelOneManager.Instance.DuckFound();
        if(_onWater) _anim.SetBool("BabyDuckisWalk", false);
        transform.position = pos.position;
        transform.rotation = pos.rotation;
        ScreenFader.Instance.OnFadeMiddle -= Teleport;
    }
    public override void OnDialogueStart()
    {
        Cuack(false);
        base.OnDialogueStart();
    }

    public override void OnDialogueEnd()
    {
        if (!_start)
        {
            _cam.Priority = 0;
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
        else _cam.Priority = 0;
        _interacted = false;
    }
    private void MissionStarted()
    {
        _currentDialogue++;
        _start = true;
    }
    private void FinishCam()
    {
        _cam.Priority = 0;
        ScreenFader.Instance.OnFadeCompleted -= FinishCam;
    }
    private void OnEnable()
    {
        if (_suscribed) Suscribe();
    }

    private void Suscribe()
    {
        if (LevelOneManager.Instance == null) return;
        LevelOneManager.Instance.OnMissionDuckStarted -= MissionStarted;
        LevelOneManager.Instance.OnMissionDuckStarted += MissionStarted;

    }

    private void OnDisable()
    {
        if (LevelOneManager.Instance == null) return;
        LevelOneManager.Instance.OnMissionDuckStarted -= MissionStarted;
    }
}
