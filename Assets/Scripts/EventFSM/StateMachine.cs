using UnityEngine;

public class StateMachine
{
    private IState _currentState;

    public void SetState(IState newState)
    {
        _currentState?.Exit();
        _currentState = newState;
        _currentState?.Enter();
    }

    public void UpdateState() {_currentState?.Update();}

    public void SendEvent(CreatureEvent evt, object data = null) {_currentState?.HandleEvent(evt, data);}
}
