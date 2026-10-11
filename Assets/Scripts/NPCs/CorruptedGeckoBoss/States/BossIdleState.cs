public class BossIdleState : IState
{
    private readonly CorruptedGeckoBoss _boss;

    public BossIdleState(CorruptedGeckoBoss boss) {_boss = boss;}

    public void Enter() {}

    public void Exit() {}

    public void Update() {}

    public void HandleEvent(CreatureEvent evt, object data = null)
    {
        if(evt != CreatureEvent.GekkoEnter) return;

        _boss.BeginPhase(0);
        _boss.SetState(_boss.TandaState);
    }
}
