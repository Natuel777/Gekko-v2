using UnityEngine;
using UnityEngine.Rendering.Universal;

public class Mirror : MonoBehaviour, IActivable
{
    [Header("State")]
    [Tooltip("Si arranca activo (parado y devolviendo balas). Los espejos 'acostados' del puzzle arrancan apagados.")]
    [SerializeField] private bool _startActive = true;

    [Header("Raise")]
    [Tooltip("Pivot que gira para levantar el espejo: la bisagra, con la chapa (y su collider) como hijo. Vacío = el espejo no se mueve.")]
    [SerializeField] private Transform _pivot;
    [Tooltip("Cuánto gira el pivot sobre su eje X local al activarse, a partir de la rotación que tiene acostado. -90 lo levanta.")]
    [SerializeField] private float _raisedAngle = -90f;
    [Tooltip("Segundos que tarda en levantarse (o en acostarse al apagarse).")]
    [Min(0.01f)]
    [SerializeField] private float _raiseSeconds = 0.6f;

    [Header("Surface")]
    [Tooltip("Collider de la chapa que refleja (el que gira con el pivot). Vacío = se usa un Collider de este mismo GameObject.")]
    [SerializeField] private Collider _surface;

    [Header("Bounce")]
    [Tooltip("Hacia dónde sale la bala al rebotar: el espejo la apunta a la posición de este Transform, desde donde la toca. Un Empty de la escena, por ejemplo sobre la torreta que tiene que alcanzar (la bala sigue de largo si no choca con nada). Ponelo fuera del pivot y del marco: giran al levantar el espejo y moverían el punto. Vacío = la bala vuelve por donde vino (invierte su dirección).")]
    [SerializeField] private Transform _bounceTarget;

    [Header("Rune")]
    [Tooltip("Decal de la runa (el de la placa de presión) sobre la piedra principal del marco. Se enciende cuando el espejo termina de levantarse. Vacío = sin runa.")]
    [SerializeField] private DecalProjector _rune;
    [Tooltip("Segundos del fade de la runa al encenderse/apagarse.")]
    [Min(0.01f)]
    [SerializeField] private float _runeSeconds = 0.8f;
    [Tooltip("Escala con la que arranca la runa al encenderse (crece hasta 1).")]
    [Range(0.5f, 1f)]
    [SerializeField] private float _runeStartScale = 0.85f;

    private StateMachine _stateMachine;

    #region States
    public MirrorActiveState activeState;
    public MirrorInactiveState inactiveState;
    #endregion

    #region Behaviours
    public MirrorReflectBehaviour reflect;
    public MirrorRaiseBehaviour raise;
    public MirrorRuneBehaviour rune;
    #endregion

    public bool IsActive => reflect.IsActive;

    #region Initialization
    private void Awake()
    {
        _stateMachine = new StateMachine();

        reflect = new MirrorReflectBehaviour(_bounceTarget);
        raise = new MirrorRaiseBehaviour(_pivot, _raisedAngle, _raiseSeconds);
        rune = new MirrorRuneBehaviour(_rune, _runeSeconds, _runeStartScale);

        activeState = new MirrorActiveState(this);
        inactiveState = new MirrorInactiveState(this);

        SetupSurface();

        // A mirror that starts active is already standing: it doesn't visibly raise itself.
        raise.Snap(_startActive);
        rune.Snap(_startActive);

        // In Awake and not in Start: an early external Activated() must not be overwritten by the initial state.
        _stateMachine.SetState(_startActive ? activeState : inactiveState);
    }

    // The reflecting plate hangs from the pivot: Unity sends the triggers to it and not to this GameObject, so a MirrorSurface is added to it
    // to forward them here.
    private void SetupSurface()
    {
        if(_surface == null)
        {
            if(!TryGetComponent(out Collider _))
                Debug.LogWarning($"[{name}] Mirror without a collider: assign _surface or add a Collider to this GameObject, otherwise it reflects nothing.", this);

            return;
        }

        if(!_surface.TryGetComponent(out MirrorSurface surface)) surface = _surface.gameObject.AddComponent<MirrorSurface>();

        surface.Initialize(this);
    }
    #endregion

    private void Update()
    {
        raise.ArtificialUpdate();
        rune.ArtificialUpdate(raise.IsFullyRaised);
    }

    // Called by this GameObject's collider (if it has one) and by the plate's MirrorSurface.
    private void OnTriggerEnter(Collider other)
    {
        TryReflect(other);
    }

    // Something touched the surface: it is sent back if the mirror is active and it is reflectable (see MirrorReflectBehaviour).
    public bool TryReflect(Collider other) => reflect.TryReflect(other);

    public void Activated(bool active = true) => _stateMachine.SetState(active ? activeState : inactiveState);

    // The opposite of its current state: standing, it lies down; lying down, it raises itself. Used by the puzzle's plates (PressurePlateRuna).
    public void Toggle() => Activated(!IsActive);

    // To test it from the Inspector (the component's three-dot menu) without pressure plates.
    [ContextMenu("Toggle (Play Mode)")]
    private void TestToggle()
    {
        if(Application.isPlaying) Toggle();
    }

#if UNITY_EDITOR
    // With the mirror selected: a line to the bounce point, to see where the bullet will go.
    private void OnDrawGizmosSelected()
    {
        if(_bounceTarget == null) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, _bounceTarget.position);
        Gizmos.DrawWireSphere(_bounceTarget.position, 0.25f);
    }
#endif
}
