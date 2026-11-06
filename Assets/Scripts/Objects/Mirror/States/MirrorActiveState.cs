public class MirrorActiveState : IState
{
    private readonly Mirror _parent;

    public MirrorActiveState(Mirror parent)
    {
        _parent = parent;
    }

    public void Enter()
    {
        _parent.reflect.Activated(true);
    }

    public void Exit()
    {
        _parent.reflect.Activated(false);
    }

    public void Update()
    {

    }

    public void HandleEvent(CreatureEvent evt, object data = null)
    {

    }
}
