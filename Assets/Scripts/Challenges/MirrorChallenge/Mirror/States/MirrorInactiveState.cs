public class MirrorInactiveState : IState
{
    private readonly Mirror _parent;

    public MirrorInactiveState(Mirror parent)
    {
        _parent = parent;
    }

    public void Enter()
    {
        _parent.reflect.Activated(false);
        _parent.raise.Activated(false);
    }

    public void Exit()
    {

    }

    public void Update()
    {

    }

    public void HandleEvent(CreatureEvent evt, object data = null)
    {

    }
}
