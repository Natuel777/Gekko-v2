using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

public class MomDuck : NPCBase
{
    private bool _suscribed;
    private Animator _anim;
    [SerializeField] private int _totalDucks;
    [SerializeField] private Transform _pjTP;
    private int _currentDucks;
    private bool _started;
    private bool _finish;
    [Header("Canvas")]
    [SerializeField] Image _exclamation;
    [Header("Recompensa")]
    [SerializeField] private NotificationSO _notificationData;

    [Header("Fila de patitos")]
    [Tooltip("Hacia dónde sale la fila desde mamá (0 = detrás de ella, 90 / -90 = a un costado, 180 = adelante). En LvlOne detrás hay arbustos que los tapan: por eso va a un costado, sobre el pasto.")]
    [Range(-180f, 180f)]
    [SerializeField] private float _rowAngle = -90f;
    [Tooltip("Distancia entre mamá y el primer patito.")]
    [SerializeField] private float _firstDuckDistance = 0.9f;
    [Tooltip("Distancia entre un patito y el siguiente.")]
    [SerializeField] private float _duckSpacing = 0.65f;
    [Tooltip("Corrimiento a los costados, alternado, para que la fila no quede rígida.")]
    [SerializeField] private float _zigzag = 0.3f;
    [Tooltip("Hasta cuánto más arriba o abajo que mamá puede quedar un patito. Si el piso de la fila está más lejos (o no hay piso), usa los duckPos de la escena.")]
    [SerializeField] private float _maxHeightDifference = 1.5f;

    [Header("Plano de la familia")]
    [Tooltip("Cámara para mostrar a la familia. Vacío = se crea una por código que encuadra a mamá y a los patitos que volvieron.")]
    [SerializeField] private CinemachineCamera _familyCam;
    [Tooltip("Segundos que se muestra la familia cada vez que vuelve un patito.")]
    [Min(0f)]
    [SerializeField] private float _familyShotSeconds = 1.5f;
    [Tooltip("Solo para la cámara creada por código: desde dónde mira (0 = de frente a mamá, 90 / -90 = de costado).")]
    [Range(-180f, 180f)]
    [SerializeField] private float _familyCamAngle = 15f;
    [Tooltip("Solo para la cámara creada por código: altura sobre la familia (más alta = mira por encima de las plantas).")]
    [SerializeField] private float _familyCamHeight = 2f;
    [Tooltip("Solo para la cámara creada por código: distancia extra para que entre todo con aire.")]
    [SerializeField] private float _familyCamPadding = 0.8f;

    private readonly List<BabyDuck> _family = new();
    // Mamá gira para mirar al player durante el diálogo: la fila usa la pose del arranque para no torcerse.
    private Vector3 _homePosition;
    private Vector3 _homeForward;
    private bool _familyCamIsRuntime;

    protected override void Start()
    {
        base.Start();
        _anim = GetComponentInChildren<Animator>();
        _anim.SetBool("DuckisWalk", false);
        _suscribed = true;
        Suscribe();

        _homePosition = transform.position;
        _homeForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

        if (SaveManager.Instance)
            if (SaveManager.Instance.Has(LevelFlags.Lvl1_Ducks.ToString()))
            {
                _currentDialogue = 7;
                _exclamation.enabled = false;
                _started = true;
                return;
            }
    }
    protected override void Update()
    {
        if (_exclamation.enabled) FollowPlayer();

        else if (_EIndicator.enabled) FollowPlayer();

        if (_interacted)
        {
            _lookAtPlayer.ArtificialUpdate();
            _lookAtPlayer.StartLooking(_playerTransform);
        }
    }

    private void DuckReturned(BabyDuck baby)
    {
        _currentDialogue++;
        _currentDucks++;
        _family.Add(baby);

        bool last = _currentDucks >= _totalDucks;
        if (last) _finish = true;

        StartCoroutine(FamilyRoutine(baby, last));
    }
    private void FinishedBefore(BabyDuck baby)
    {
        _currentDucks++;
        _family.Add(baby);
        GetSlot(_family.Count - 1, out Vector3 slotPos, out Quaternion slotRot);
        baby.PlaceNextToMom(slotPos, slotRot);
    }

    // Fundido -> el patito aparece en la fila -> plano de la familia. Si faltan patitos, fundido y corte de vuelta
    // al player (en vez de un blend que cruza el mapa). Con el último, el plano pasa suave a la cámara de mamá y
    // arranca su diálogo, sin un segundo fundido encima.
    private IEnumerator FamilyRoutine(BabyDuck baby, bool last)
    {
        Player pj = GameManager.Instance.Pj;
        pj.Inputs(false);
        GameManager.Instance.CanPause = true;

        // UIManager devuelve el control justo después de OnDialogueEnd del patito: se vuelve a bloquear.
        yield return null;
        pj.Inputs(false);

        yield return ScreenFader.Instance.FadeToBlack();

        GetSlot(_family.Count - 1, out Vector3 slotPos, out Quaternion slotRot);
        baby.PlaceNextToMom(slotPos, slotRot);
        if (last) TPPlayer();

        DuckCameraTransition.CutNow();
        CinemachineCamera cam = BeginFamilyCam();
        yield return null;

        yield return ScreenFader.Instance.FadeFromBlack();
        yield return new WaitForSeconds(_familyShotSeconds);

        if (last)
        {
            // Del plano de la familia a la cámara de mamá: las dos son de los patos, así que blend suave, sin
            // fundido. Inputs antes del diálogo: el diálogo los vuelve a bloquear mientras dura.
            pj.Inputs(true);
            GameManager.Instance.CanPause = false;
            StartDialogue();
            cam.Priority = -1;
            yield break;
        }

        yield return ScreenFader.Instance.FadeToBlack();
        DuckCameraTransition.CutNow();
        cam.Priority = -1;
        yield return null;
        DuckCameraTransition.ResetLiveCamera();
        yield return ScreenFader.Instance.FadeFromBlack();

        pj.Inputs(true);
        GameManager.Instance.CanPause = false;
    }

    // Fila que sale de mamá hacia _rowAngle, en zigzag, todos mirando hacia donde mira ella. Se apoya en el piso;
    // si no lo encuentra cerca de la altura de mamá, usa el duckPos armado a mano.
    private void GetSlot(int index, out Vector3 position, out Quaternion rotation)
    {
        Vector3 rowDir = Quaternion.AngleAxis(_rowAngle, Vector3.up) * -_homeForward;
        Vector3 sideDir = Vector3.Cross(Vector3.up, rowDir);
        float along = _firstDuckDistance + index * _duckSpacing;
        float side = (index % 2 == 0 ? 1f : -1f) * _zigzag;
        Vector3 slot = _homePosition + rowDir * along + sideDir * side;

        rotation = Quaternion.LookRotation(_homeForward, Vector3.up);

        Vector3 origin = slot + Vector3.up * _maxHeightDifference;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, _maxHeightDifference * 2f, GameManager.Instance.GroundLayer, QueryTriggerInteraction.Ignore))
        {
            position = hit.point;
            return;
        }

        Transform fallback = LevelOneManager.Instance.ReserveDuckPosition();
        position = fallback != null ? fallback.position : slot;
    }

    private CinemachineCamera BeginFamilyCam()
    {
        if (_familyCam == null)
        {
            // Se arma apagada para que no le gane a la cámara actual ni un frame antes de tener prioridad.
            GameObject go = new GameObject($"{name}_FamilyCam");
            go.SetActive(false);
            _familyCam = go.AddComponent<CinemachineCamera>();
            _familyCam.Lens = _cam.Lens;
            go.SetActive(true);
            _familyCamIsRuntime = true;
        }

        // La creada por código se re-encuadra cada vez, porque la fila crece. Una asignada a mano queda como está.
        if (_familyCamIsRuntime) FrameFamily(_familyCam);

        _familyCam.Priority = 40;
        return _familyCam;
    }

    // Encuadra a mamá y a los patitos que ya volvieron, en tres cuartos desde adelante para que se lea la fila.
    private void FrameFamily(CinemachineCamera cam)
    {
        Bounds bounds = new Bounds(transform.position, Vector3.one * 0.5f);
        foreach (BabyDuck duck in _family)
            if (duck != null) bounds.Encapsulate(duck.transform.position);

        Vector3 center = bounds.center + Vector3.up * 0.3f;
        float radius = Mathf.Max(1f, bounds.extents.magnitude);
        float halfFov = Mathf.Max(5f, cam.Lens.FieldOfView * 0.5f) * Mathf.Deg2Rad;
        float distance = radius / Mathf.Tan(halfFov) + _familyCamPadding;

        Vector3 dir = Quaternion.AngleAxis(_familyCamAngle, Vector3.up) * _homeForward;
        Vector3 camPos = center + dir * distance + Vector3.up * _familyCamHeight;

        cam.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(center - camPos, Vector3.up));
    }

    private void TPPlayer()
    {
        GameManager.Instance.Pj.PjController.Teleport(_pjTP.position);
    }
    private void StartDialogue()
    {
        if (UIManager.Instance == null || UIManager.Instance.HasActiveDialogue()) return;
        UIManager.Instance.StartDialogue(this);
    }

    public override void OnDialogueEnd()
    {
        if (!_started)
        {
            _started = true;
            _currentDialogue++;
            LevelOneManager.Instance.MissionDuck();
        }

        if (_finish == true)
        {
            CollectiblesRegister.RegisterCollectible(_notificationData.Name);
            int count = CollectiblesRegister.GetCollectibleCount(_notificationData.Name);
            UIManager.Instance.notifications.ShowRaspberryCollectible(_notificationData, count);

            var pj = GameManager.Instance.Pj;
            pj.health.SetHealth(pj.health.MaxHealth);
            pj.BlueberryTracker.ActivateBoost(_notificationData.SpeedBoostMultiplier, _notificationData.SpeedBoostTimer);
            _finish = false;
            _currentDialogue++;
            if (SaveManager.Instance)
            {
                SaveManager.Instance.Set(LevelFlags.Lvl1_Ducks.ToString());
            }
        }
        _cam.Priority = 0;
        _interacted = false;
    }
    public override void Interacted()
    {
        _exclamation.enabled = false;
        base.Interacted();
    }
    private void OnEnable()
    {
        if (_suscribed) Suscribe();
    }

    private void Suscribe()
    {
        if (LevelOneManager.Instance == null) return;
        LevelOneManager.Instance.OnDuckReturned -= DuckReturned;
        LevelOneManager.Instance.OnDuckReturned += DuckReturned;
        LevelOneManager.Instance.OnDuckFound -= FinishedBefore;
        LevelOneManager.Instance.OnDuckFound += FinishedBefore;
    }

    private void OnDisable()
    {
        if (LevelOneManager.Instance == null) return;
        LevelOneManager.Instance.OnDuckReturned -= DuckReturned;
        LevelOneManager.Instance.OnDuckFound -= FinishedBefore;
    }
}
