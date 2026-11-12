using UnityEngine;

// Espejo: si está activo, las balas de torreta (IReflectable) que lo tocan salen en la dirección contraria. Nada más: no
// sabe nada de ningún desafío. Mismo esquema que ChallengeTurret: el MonoBehaviour es fino y la lógica vive en los behaviours
// (Model/) y en los estados (States/). Lo prenden y apagan otros (placas de presión, etc.) a través de IActivable.
//
// Al activarse se levanta: gira el pivot (_pivot) sobre su eje X local, desde la pose en que está acostado, y al apagarse se
// vuelve a acostar. Sin _pivot es un espejo fijo.
//
// Layout del prefab Mirror: este componente va en la raíz; el pivot es la bisagra (con la chapa como hijo) y el collider que
// refleja es el de la chapa (_surface), que gira con el pivot. Ese collider tiene que ser sólido (Gekko no lo atraviesa): las
// balas son triggers. Si el espejo es fijo se puede poner el collider en este mismo GameObject y dejar _surface vacío.
public class Mirror : MonoBehaviour, IActivable
{
    [Header("Estado")]
    [Tooltip("Si arranca activo (parado y devolviendo balas). Los espejos 'acostados' del puzzle arrancan apagados.")]
    [SerializeField] private bool _startActive = true;

    [Header("Levantar")]
    [Tooltip("Pivot que gira para levantar el espejo: la bisagra, con la chapa (y su collider) como hijo. Vacío = el espejo no se mueve.")]
    [SerializeField] private Transform _pivot;
    [Tooltip("Cuánto gira el pivot sobre su eje X local al activarse, a partir de la rotación que tiene acostado. -90 lo levanta.")]
    [SerializeField] private float _raisedAngle = -90f;
    [Tooltip("Segundos que tarda en levantarse (o en acostarse al apagarse).")]
    [Min(0.01f)]
    [SerializeField] private float _raiseSeconds = 0.6f;

    [Header("Superficie")]
    [Tooltip("Collider de la chapa que refleja (el que gira con el pivot). Vacío = se usa un Collider de este mismo GameObject.")]
    [SerializeField] private Collider _surface;

    private StateMachine _stateMachine;

    #region States
    public MirrorActiveState activeState;
    public MirrorInactiveState inactiveState;
    #endregion

    #region Behaviours
    public MirrorReflectBehaviour reflect;
    public MirrorRaiseBehaviour raise;
    #endregion

    public bool IsActive => reflect.IsActive;

    #region Initialization
    private void Awake()
    {
        _stateMachine = new StateMachine();

        reflect = new MirrorReflectBehaviour();
        raise = new MirrorRaiseBehaviour(_pivot, _raisedAngle, _raiseSeconds);

        activeState = new MirrorActiveState(this);
        inactiveState = new MirrorInactiveState(this);

        SetupSurface();

        // El espejo que nace activo ya está parado: no se levanta a la vista.
        raise.Snap(_startActive);

        // En Awake y no en Start: un Activated() externo temprano no tiene que quedar pisado por el estado inicial.
        _stateMachine.SetState(_startActive ? activeState : inactiveState);
    }

    // La chapa que refleja cuelga del pivot: Unity le avisa los triggers a ella y no a este GameObject, así que se le agrega un
    // MirrorSurface que los pasa para acá.
    private void SetupSurface()
    {
        if(_surface == null)
        {
            if(!TryGetComponent(out Collider _))
                Debug.LogWarning($"[{name}] Mirror sin collider: asigná _surface o poné un Collider en este GameObject, si no no refleja nada.", this);

            return;
        }

        if(!_surface.TryGetComponent(out MirrorSurface surface)) surface = _surface.gameObject.AddComponent<MirrorSurface>();

        surface.Initialize(this);
    }
    #endregion

    private void Update()
    {
        raise.ArtificialUpdate();
    }

    // Lo llaman el collider de este GameObject (si lo tiene) y el MirrorSurface de la chapa.
    private void OnTriggerEnter(Collider other)
    {
        TryReflect(other);
    }

    // Algo tocó la superficie: se lo devuelve si el espejo está activo y es reflejable (ver MirrorReflectBehaviour).
    public bool TryReflect(Collider other) => reflect.TryReflect(other);

    public void Activated(bool active = true) => _stateMachine.SetState(active ? activeState : inactiveState);

    // Lo contrario de lo que está: parado se acuesta y acostado se levanta. Lo usan las placas del puzzle (PressurePlateRuna).
    public void Toggle() => Activated(!IsActive);

    // Para probarlo desde el Inspector (menú de tres puntos del componente) sin placas de presión.
    [ContextMenu("Alternar (Play Mode)")]
    private void TestToggle()
    {
        if(Application.isPlaying) Toggle();
    }
}
