using UnityEngine;

public class BeetleAlertState : IState
{
    private readonly HeavyBeetle _beetle;
    private const float FacingThreshold = 15f;
    private float _timer = 0f;
    private readonly float _looakAtThreshold;

    public BeetleAlertState(HeavyBeetle beetle, float lookAtThreshold) 
    {
        _beetle = beetle; 
        _looakAtThreshold = lookAtThreshold;
    }

    public void Enter()
    {
        Debug.Log($"[{_beetle}] entered alert state.");
        _beetle.SetAngry(true);
        _beetle.lookAt.StartLooking(_beetle.playerTransform);
    }

    public void Exit()
    {
        Debug.Log($"[{_beetle}] exit alert state.");
        _beetle.lookAt.StopLooking();
    }

    public void Update()
    {
        _beetle.lookAt.ArtificialUpdate();

        Vector3 dir = _beetle.playerTransform.position - _beetle.transform.position;
        dir.y = 0f;

        if(dir.sqrMagnitude < 0.001f) return;

        if(Vector3.Angle(_beetle.transform.forward, dir.normalized) < FacingThreshold)
        {
            _timer += Time.deltaTime;

            if(_timer >= _looakAtThreshold)
                _beetle.SetState(_beetle.ChargeState);
        }

        else _timer = 0f;
    }

    public void HandleEvent(CreatureEvent evt, object data = null)
    {
        if (evt == CreatureEvent.GekkoExit)
            _beetle.SetState(_beetle.PatrolState);
    }
}