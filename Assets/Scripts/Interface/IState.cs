public interface IState
{
    void Enter();
    void Exit();
    void Update();
    void HandleEvent(CreatureEvent evt, object data = null);
}

public enum CreatureEvent
{
    GekkoEnter,
    GekkoExit,
    ChargeHitPlayer,
    ChargeHitWall,
    ChargeMissed,
    DazeExpired
}
