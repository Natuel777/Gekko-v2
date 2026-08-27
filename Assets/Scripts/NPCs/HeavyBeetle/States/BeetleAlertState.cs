using UnityEngine;

public class BeetleAlertState : IState
{
    private readonly HeavyBeetle _beetle;
    private const float FacingThreshold = 15f;

    public BeetleAlertState(HeavyBeetle beetle) { _beetle = beetle; }

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
            _beetle.SetState(_beetle.ChargeState);
    }

    public void HandleEvent(CreatureEvent evt, object data = null)
    {
        if (evt == CreatureEvent.GekkoExit)
            _beetle.SetState(_beetle.PatrolState);
    }
}