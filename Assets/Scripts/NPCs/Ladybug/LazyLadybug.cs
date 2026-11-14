using System.Collections;
using UnityEngine;

public class LazyLadybug : NPCBase
{
    [SerializeField] private float _speed;
    [SerializeField] private Transform _finalPos;
    [SerializeField] private Transform _model;

    protected override void Start()
    {
        if (SaveManager.Instance)
            if (SaveManager.Instance.Has(LevelFlags.Lvl1_ladybug.ToString()))
            {
                transform.position = _finalPos.position;
                gameObject.layer = 16;
                _EIndicator.enabled = false;
                Destroy(this);
                return;
            }

        _playerTransform = GameManager.Instance.Pj.transform;
        _EIndicator.enabled = false;
        _PJCam = CameraStateManager.Instance.CurrentCamera.transform;
        _lookAtPlayer = new LookAtTarget(_rotationSpeed, _model, lockYAxis: true);
    }

    public override void OnDialogueEnd()
    {
        _interacted = false;
        StartCoroutine(Move());
    }

    private IEnumerator Move()
    {
        GameManager.Instance.Pj.Inputs(false);
        _lookAtPlayer.StartLooking(_finalPos);
        while (true)
        {
            _lookAtPlayer.ArtificialUpdate();
            var dir = _finalPos.position - transform.position;

            transform.position += dir.normalized * _speed * Time.deltaTime;

            if (dir.magnitude < 0.25f)
            {
                GameManager.Instance.Pj.Inputs(true);
                _cam.Priority = 0;
                gameObject.layer = 16;
                if (SaveManager.Instance)
                {
                    SaveManager.Instance.Set(LevelFlags.Lvl1_ladybug.ToString());
                }
                Destroy(this);
            }
            yield return new WaitForEndOfFrame();
        }
       
        
    }
}
