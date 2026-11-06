using UnityEngine;

// Espejo: si está activo, las balas de torreta (IReflectable) que lo tocan salen en la dirección contraria. Nada más: no
// sabe nada de ningún desafío. Mismo esquema que ChallengeTurret: el MonoBehaviour es fino y la lógica vive en el behaviour
// (Model/) y en los estados (States/). Lo prenden y apagan otros (placas de presión, etc.) a través de IActivable.
//
// Tiene que ir en el mismo GameObject que su collider, y ese collider tiene que ser sólido (Gekko no lo atraviesa): las
// balas son triggers, y OnTriggerEnter le llega a este objeto por ese collider.
[RequireComponent(typeof(Collider))]
public class Mirror : MonoBehaviour, IActivable
{
    [Tooltip("Si arranca activo (devolviendo balas). Los espejos 'acostados' del puzzle arrancan apagados.")]
    [SerializeField] private bool _startActive = true;

    private StateMachine _stateMachine;

    #region States
    public MirrorActiveState activeState;
    public MirrorInactiveState inactiveState;
    #endregion

    #region Behaviours
    public MirrorReflectBehaviour reflect;
    #endregion

    public bool IsActive => reflect.IsActive;

    #region Initialization
    private void Awake()
    {
        _stateMachine = new StateMachine();

        reflect = new MirrorReflectBehaviour();

        activeState = new MirrorActiveState(this);
        inactiveState = new MirrorInactiveState(this);

        // En Awake y no en Start: un Activated() externo temprano no tiene que quedar pisado por el estado inicial.
        _stateMachine.SetState(_startActive ? activeState : inactiveState);
    }
    #endregion

    private void OnTriggerEnter(Collider other)
    {
        reflect.TryReflect(other);
    }

    public void Activated(bool active = true) => _stateMachine.SetState(active ? activeState : inactiveState);

    // Para probarlo desde el Inspector (menú de tres puntos del componente) sin placas de presión.
    [ContextMenu("Alternar (Play Mode)")]
    private void TestToggle()
    {
        if(Application.isPlaying) Activated(!IsActive);
    }
}
