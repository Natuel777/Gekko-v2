using UnityEngine;

public class BeetleDazedState : IState
{
    private readonly HeavyBeetle _beetle;
    private float _dazeTimer = 0f;
    private bool _recovering = false;
    private ParticleSystem _collisionParticle;

    public BeetleDazedState(HeavyBeetle beetle, ParticleSystem col) 
    {
        _beetle = beetle;
        _collisionParticle = col;
    }

    public void Enter()
    {
        _beetle.SetDazed(true);
        _beetle.SetAngry(false);
        // _beetle.SetTurnedInsideOut(true);   // reemplazado por el flip por código
        _beetle.dazedFlip.StartFlip();
        _dazeTimer = _beetle.data.dazeDuration;
        _recovering = false;
        _collisionParticle.Play();
    }

    public void Exit()
    {
        _beetle.SetDazed(false);
        // _beetle.SetTurnedInsideOut(false);  // reemplazado por el flip por código
    }

    public void Update()
    {
        _beetle.dazedFlip.ArtificialUpdate();

        if (!_recovering)
        {
            _dazeTimer -= Time.deltaTime;
            if (_dazeTimer <= 0f)
            {
                _recovering = true;
                _beetle.dazedFlip.StartRecover();
            }
        }
        else if (_beetle.dazedFlip.RecoverDone)
        {
            _beetle.SendEvent(CreatureEvent.DazeExpired);
        }
    }

    public void HandleEvent(CreatureEvent evt, object data = null)
    {
        if(evt == CreatureEvent.DazeExpired)
            _beetle.SetState(_beetle.PatrolState);
    }
}
