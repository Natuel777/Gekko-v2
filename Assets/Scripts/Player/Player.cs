using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Player : MonoBehaviour
{
    #region variables
    private PlayerInputs _pjInputs;
    private PlayerController _pjController;
    private TongueManager _pjTongue;
    private AimManager _aimM;
    private InteractionManager _interactM;
    private GekkoCollision _collision;
    private DialogueInteractor _interactor;
    private DebugController _debugController;
    private BlueberryComboTracker _blueberryCombo;

    [Header("Health")]
    [SerializeField] private HealthBar _healthBar;
    [HideInInspector] public GekkoHealth health;

    [Header("View")]
    [SerializeField] private PlayerViewer _pjViewer;
    [SerializeField] private Transform _tongue;

    [Header("Testing")]
    [SerializeField] float _rotationSpeed, speed, jumpForce, fallMultiplier, lowJumpMultiplier;

    [Header("HUD")]
    [SerializeField] private BlueberryComboUI _comboUI;
    [SerializeField] private NotificationSO _blueberry;

    [Header("Dialogue")]
    [SerializeField] private float _interactReach = 3f;
    [SerializeField] private float _interactOriginY = 1f;
    [SerializeField] private LayerMask _dialogueLayer;
    #endregion
    #region Properties
    public PlayerController PjController {get{return _pjController;}}
    public TongueManager PjTongue => _pjTongue;
    public DialogueInteractor Interactor => _interactor;
    public PlayerViewer PjViewer => _pjViewer;
    public BlueberryComboTracker BlueberryTracker => _blueberryCombo;
    #endregion


    private void Awake()
    {
        GameManager.Instance.Pj = this;
        health = new GekkoHealth(_healthBar);
        _collision = new GekkoCollision(this);
        _debugController = new DebugController();
    }

    private void OnEnable() 
    {
        health?.ArtificialOnEnable();
    }

    private void Start()
    {
        Transform camTransform = null;
        
        if(CameraStateManager.Instance != null)
        {
            var currentCamera = CameraStateManager.Instance.CurrentCamera;
            
            if(currentCamera != null)
                camTransform = currentCamera.transform;
        }

        _pjController = new PlayerController(GetComponent<Rigidbody>(),
                        transform,
                        GetComponent<CapsuleCollider>(),
                        camTransform,
                        _pjViewer,
                        _tongue);

        _interactM = GetComponentInChildren<InteractionManager>();
        _interactM.GetPlayerController(_pjController);

        _aimM = GetComponent<AimManager>();
        _aimM.SetControllerAndCamera(_pjController, camTransform);

        _pjTongue = GetComponentInChildren<TongueManager>();
        _pjTongue.GetPlayerController(_pjController);

        _pjController.GetTongueManager(_pjTongue);

        CameraFollow cam = camTransform.GetComponent<CameraFollow>();

        cam.SetPJC(_pjController);


        _pjInputs = new PlayerInputs(_pjController, _pjTongue, _aimM,cam, _interactM, this);
        _blueberryCombo = new BlueberryComboTracker(_pjController, health, _blueberry, _pjViewer);
        _blueberryCombo.ArtificialOnEnable();
        UIManager.Instance.notifications.OnBlueberryWindowClosed += _blueberryCombo.ResetCombo;

        _interactor = new DialogueInteractor(transform, _interactReach, _interactOriginY, _dialogueLayer);
        ActivateInputs();

        if(UIManager.Instance != null)
        {
            UIManager.Instance.OnActivatingDialogue += DisablePlayerControl;
            UIManager.Instance.OnDeactivatingDialogue += EnablePlayerControl;
        }
    }

    private void Update()
    {
        _pjController.ArtificialUpdate();
        health?.ArtificialUpdate();
        _debugController.ArtificialUpdate();
        _blueberryCombo?.ArtificialUpdate();

        if (_comboUI != null && _blueberryCombo != null)
            _comboUI.UpdateCombo(_blueberryCombo.ComboCount, _blueberryCombo.BoostActive);

        // Cierre del diálogo por distancia (solo corre si hay uno activo).
        if(UIManager.Instance != null && UIManager.Instance.HasActiveDialogue())
            UIManager.Instance.ArtificialUpdate();
    }

    private void FixedUpdate()
    {
        _pjController.ArtificialFixedUpdate();
    }
    private void LateUpdate()
    {
        _pjController.ArtificialLateUpdate();
    }

    private void OnDisable()
    {
        _pjInputs?.ArtificialDisable();
        health?.ArtificialOnDisable();
        if (UIManager.Instance != null && _blueberryCombo != null)
            UIManager.Instance.notifications.OnBlueberryWindowClosed -= _blueberryCombo.ResetCombo;
    }

    private void OnDestroy()
    {
        _blueberryCombo?.ArtificialOnDisable();
    }
    public void ActivateInputs()
    {
        _pjInputs.ArtificialEnable();
    }
    // Bloqueo de control durante el diálogo. El gateo de cada input (mover/lengua/cámara/
    // salto) vive en PlayerInputs vía UIManager.HasActiveDialogue(); acá solo se frena el
    // movimiento residual y la rotación.
    private void DisablePlayerControl()
    {
        _pjController.RawInput = Vector2.zero;
        _pjController.CancelMovement();
        _pjController.CanRotate = false;
        _pjController.Talking = true;
        _pjInputs.DeactivatePlayerInputs();
    }
    private void EnablePlayerControl()
    {
        _pjController.CanRotate = true;
        _pjController.Talking = false;
        _pjInputs.ReactivatePlayerInputs();
    }
    public void Inputs(bool active)
    {
        if (active) EnablePlayerControl();
        else DisablePlayerControl();
    }
    private void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.position + Vector3.up * _interactOriginY;
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(origin, transform.forward * _interactReach);
        Gizmos.DrawWireSphere(origin + transform.forward * _interactReach, 0.15f);
    }
    private void OnTriggerEnter(Collider other)
    {
        _collision.ArtificialOnTriggerEnter(other);
    }
    private void OnTriggerExit(Collider other)
    {
        _collision.ArtificialOnTriggerExit(other);
    }
    private void OnTriggerStay(Collider other) {_collision.ArtificialOnTriggerStay(other);}

    public void ChangeAim()
    {
        _aimM.SwitchTarget(new Vector2(0,1));
    }
    public void ChangeVariables()
    {
        _pjController.ChangeValues(speed, jumpForce, _rotationSpeed, fallMultiplier, lowJumpMultiplier);
        health.SetHealth(health.MaxHealth);
        health?.ArtificialOnDisable();
        Debug.Log("god Mode");
    }
    public void OnBlueberryCollected() => _blueberryCombo?.OnCollect();
}
