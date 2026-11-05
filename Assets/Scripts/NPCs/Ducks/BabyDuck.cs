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
    // Lo llama MomDuck con la pantalla en negro: el patito aparece en su lugar de la fila. La cámara de diálogo
    // se baja acá porque es hija del patito y, si no, viajaría con él hasta mamá.
    public void PlaceNextToMom(Vector3 position, Quaternion rotation)
    {
        _cam.Priority = 0;
        if(_onWater) _anim.SetBool("BabyDuckisWalk", false);
        transform.SetPositionAndRotation(position, rotation);
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
            _currentDialogue++;
            // MomDuck arma la secuencia: fundido, el patito en la fila y el plano de la familia.
            LevelOneManager.Instance.DuckReturned(this);
        }
        else _cam.Priority = 0;
        _interacted = false;
    }
    private void MissionStarted()
    {
        _currentDialogue++;
        _start = true;
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
