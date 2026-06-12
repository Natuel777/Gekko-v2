using UnityEngine;

public class BeetleRecalibrateState : IState
{
    private readonly HeavyBeetle _beetle;
    private float _timer;
    public BeetleRecalibrateState(HeavyBeetle beetle) {_beetle = beetle;}

    public void Enter() 
    {
        _timer = _beetle.data.recalibrateDelay;
    }
    public void Exit(){}

    public void Update() 
    {
        _timer -= Time.deltaTime;

        if(_timer <= 0)
            _beetle.SetState(_beetle.ChargeState);
    }
    public void HandleEvent(CreatureEvent evt, object data = null)
    {
        if(evt == CreatureEvent.GekkoExit)
            _beetle.SetState(_beetle.PatrolState);
    }
}
