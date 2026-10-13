// Estado terminal: el jefe queda neutralizado (no se destruye), igual que PlantPurifiedState, para que
// las etapas siguientes (cinemática final, Gekko amigo) puedan seguir usando su Transform/posición.
public class BossDefeatedState : IState
{
    private readonly CorruptedGeckoBoss _boss;

    public BossDefeatedState(CorruptedGeckoBoss boss) {_boss = boss;}

    public void Enter() {_boss.MarkDefeated();}

    public void Exit() {}

    public void Update() {}

    public void HandleEvent(CreatureEvent evt, object data = null) {}
}
