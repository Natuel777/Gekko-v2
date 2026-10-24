using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

// Fases del boss de la aldea. Va al lado del PurificationChallenge y escucha su evento CoreBroken, sin tocar
// CarnivorousPlant ni PurificationChallenge (los usa también LvlOne):
//  - Cada núcleo roto muestra la planta con una cámara de Cinemachine y la hace 'nibble'.
//  - Fase = núcleos rotos. Con cada fase la planta dispara más seguido y detecta al player desde más lejos.
//    Se logra reemplazando spitBehaviour y detection (campos públicos de la planta) por versiones con los
//    valores nuevos, armadas con las mismas referencias que usa la planta.
public class BossPlantPhases : MonoBehaviour
{
    [SerializeField] private PurificationChallenge _challenge;
    [SerializeField] private CarnivorousPlant _plant;

    [Header("Plant References (las mismas que tiene la planta)")]
    [SerializeField] private Animator _plantAnimator;
    [SerializeField] private Transform _head;
    [SerializeField] private Transform _firePoint;
    [SerializeField] private BugBullet _venomPrefab;
    [SerializeField] private GameObject _muzzlePS;

    [Header("Phases")]
    [Tooltip("Segundos entre escupidas según los núcleos rotos: [0] al empezar, [1] tras el primero, etc. Si faltan fases se repite la última.")]
    [SerializeField] private float[] _shootIntervalByPhase = { 3.5f, 2.6f, 1.8f, 1.2f };
    [Tooltip("Rango en el que la planta detecta y ataca al player, con el mismo índice que la cadencia.")]
    [SerializeField] private float[] _detectionRangeByPhase = { 8f, 12f, 16f, 20f };

    [Header("Core Broken Camera")]
    [Tooltip("Cámara que muestra la planta cada vez que se rompe un núcleo. El blend lo hace el CinemachineBrain.")]
    [SerializeField] private CinemachineVirtualCameraBase _camCoreBroken;
    [Min(0f)]
    [SerializeField] private float _camSeconds = 2.5f;
    [Tooltip("Segundos desde que arranca la cámara hasta que la planta hace 'nibble' (dar tiempo al blend).")]
    [Min(0f)]
    [SerializeField] private float _nibbleDelay = 0.8f;

    private CinemachineVirtualCameraBase _activeCam;
    private int _camBasePriority;
    private Coroutine _camRoutine;
    private int _phase;

    private void OnEnable()
    {
        if(_challenge != null) _challenge.CoreBroken += OnCoreBroken;
    }

    private void OnDisable()
    {
        if(_challenge != null) _challenge.CoreBroken -= OnCoreBroken;
        EndCam();
    }

    // Un frame de espera: la planta arma su detección en Start (necesita el Player) y hay que pisarla después.
    private IEnumerator Start()
    {
        yield return null;
        ApplyPhase();
    }

    private void OnCoreBroken(int remaining, int total)
    {
        _phase = total - remaining;
        ApplyPhase();

        // El último núcleo (remaining == 0) lo muestra el timeline de "escudo caído" en PurificationChallenge,
        // que encadena esta misma cámara con la de la planta enojada. Acá solo se maneja el resto.
        if(remaining <= 0) return;

        EndCam();
        _camRoutine = StartCoroutine(CoreBrokenCamRoutine());
    }

    private void ApplyPhase()
    {
        if(_plant == null || _plant.IsPurified || _plant.playerTransform == null) return;

        float interval = GetPhaseValue(_shootIntervalByPhase, _plant.data.shootInterval);
        float range = GetPhaseValue(_detectionRangeByPhase, _plant.data.detectionRange);

        // Si estaba atacando, el nuevo comportamiento retoma el blanco; si no, el FSM lo arranca al entrar en rango.
        bool wasInRange = _plant.detection != null && _plant.detection.IsTargetInRange();

        PlantSpitBehaviour spit = new PlantSpitBehaviour(_head, _firePoint, _venomPrefab, interval,
                                                         _plant.data.rotateToPlayer, _plant.data.rotationSpeed,
                                                         _muzzlePS, PlaySpit);
        if(wasInRange) spit.StartSpitting(_plant.playerTransform);

        _plant.spitBehaviour = spit;
        _plant.detection = new BugDetection(_plant.transform, _plant.playerTransform, range);
    }

    private float GetPhaseValue(float[] values, float fallback)
    {
        if(values == null || values.Length == 0) return fallback;

        return values[Mathf.Min(_phase, values.Length - 1)];
    }

    // Mismo trigger que usa CarnivorousPlant al escupir.
    private void PlaySpit()
    {
        if(_plantAnimator != null) _plantAnimator.SetTrigger("splittle");
    }

    // No bloquea el input: la planta sigue atacando mientras se muestra.
    private IEnumerator CoreBrokenCamRoutine()
    {
        if(_camCoreBroken != null)
        {
            _activeCam = _camCoreBroken;
            _camBasePriority = _camCoreBroken.Priority.Value;
            _camCoreBroken.Priority = 30;
        }

        float nibbleDelay = Mathf.Min(_nibbleDelay, _camSeconds);
        yield return new WaitForSeconds(nibbleDelay);

        if(_plantAnimator != null && !_plant.IsPurified) _plantAnimator.SetTrigger("nibble");

        yield return new WaitForSeconds(_camSeconds - nibbleDelay);

        _camRoutine = null;
        EndCam();
    }

    // Idempotente: si se corta la corrutina (o se rompen dos núcleos seguidos) la cámara no queda tomada.
    private void EndCam()
    {
        if(_camRoutine != null)
        {
            StopCoroutine(_camRoutine);
            _camRoutine = null;
        }

        if(_activeCam == null) return;

        _activeCam.Priority = _camBasePriority;
        _activeCam = null;
    }
}
