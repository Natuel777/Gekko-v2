using UnityEngine;

public class BeetleDazedState : IState
{
    private readonly HeavyBeetle _beetle;
    private float _dazeTimer = 0f;

    public BeetleDazedState(HeavyBeetle beetle) {_beetle = beetle;}

    public void Enter()
    {
        _beetle.SetDazed(true);
        _beetle.SetAngry(false);
        _beetle.SetTurnedInsideOut(true);
        _dazeTimer = _beetle.data.dazeDuration;
    }

    public void Exit()
    {
        _beetle.SetDazed(false);
        _beetle.SetTurnedInsideOut(false);
    }

    public void Update() 
    {
        _dazeTimer -= Time.deltaTime;

        if(_dazeTimer <= 0)
            _beetle.SendEvent(CreatureEvent.DazeExpired);
    }

    public void HandleEvent(CreatureEvent evt, object data = null) 
    {
        if(evt == CreatureEvent.DazeExpired)
            _beetle.SetState(_beetle.PatrolState);
    }
}
