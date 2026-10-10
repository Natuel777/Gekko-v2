using UnityEngine;

public class BeetleDazedState : IState
{
    private readonly HeavyBeetle _beetle;
    private float _dazeTimer = 0f;
    private bool _recovering = false;
    private Rigidbody _rb;
    private ParticleSystem _collisionParticle;

    public BeetleDazedState(HeavyBeetle beetle, ParticleSystem col, Rigidbody rb) 
    {
        _beetle = beetle;
        _collisionParticle = col;
        _rb = rb;
    }

    public void Enter()
    {
        _beetle.SetDazed(true);
        _beetle.view.SetAngry(false);
        // _beetle.SetTurnedInsideOut(true);   // reemplazado por el flip por código
        _beetle.dazedFlip.StartFlip();
        _dazeTimer = _beetle.data.dazeDuration;
        _recovering = false;

        if(_collisionParticle != null) _collisionParticle.Play();
    }

    public void Exit()
    {
        _beetle.SetDazed(false);
        _rb.isKinematic = false;
    }

    public void Update()
    {
        _beetle.dazedFlip.ArtificialUpdate();

        if(_beetle.dazedFlip.FlipDone && !_rb.isKinematic) _rb.isKinematic = true;

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
