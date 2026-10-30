using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

// Fases del boss de la aldea. Va al lado del PurificationChallenge y escucha su evento CoreBroken, sin tocar
// CarnivorousPlant ni PurificationChallenge (los usa también LvlOne):
//  - Cada núcleo roto (también el último) muestra primero el núcleo purificándose y después la planta, que hace
//    'nibble' mientras las enredaderas del escudo (VineShield) bajan su tramo.
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

    [Header("Core Camera (primero: el núcleo purificándose)")]
    [Tooltip("Cámara que mira al núcleo recién roto mientras se disuelve. Se ubica por código entre el núcleo y el player. Vacío = va directo a la cámara de la planta.")]
    [SerializeField] private CinemachineVirtualCameraBase _camCore;
    [Min(0f)]
    [SerializeField] private float _coreCamSeconds = 2f;
    [Tooltip("Distancia horizontal de la cámara al núcleo, hacia el lado del player.")]
    [SerializeField] private float _coreCamDistance = 4f;
    [Tooltip("Altura de la cámara sobre el núcleo.")]
    [SerializeField] private float _coreCamHeight = 1.5f;

    [Header("Core Broken Camera (después: la planta)")]
    [Tooltip("Cámara que muestra la planta cada vez que se rompe un núcleo. El blend lo hace el CinemachineBrain.")]
    [SerializeField] private CinemachineVirtualCameraBase _camCoreBroken;
    [Min(0f)]
    [SerializeField] private float _camSeconds = 2.5f;
    [Tooltip("Segundos desde que arranca la cámara de la planta hasta que la planta hace 'nibble' y las enredaderas bajan (dar tiempo al blend).")]
    [Min(0f)]
    [SerializeField] private float _nibbleDelay = 0.8f;

    [Header("Vine Shield")]
    [Tooltip("Escudo de enredaderas: sus fases se retienen hasta que la cámara de la planta ya lo está mostrando.")]
    [SerializeField] private VineShield _vineShield;

    private readonly HashSet<PurificationCore> _seenBroken = new();
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

    // CoreBroken llega antes de que PurificationChallenge le pase la fase al escudo: el Hold de acá la retiene hasta
    // que la cámara de la planta lo esté mostrando.
    private void OnCoreBroken(int remaining, int total)
    {
        _phase = total - remaining;
        ApplyPhase();

        EndCam();
        if(_vineShield != null) _vineShield.Hold();
        _camRoutine = StartCoroutine(CoreBrokenCamRoutine(FindNewlyBrokenCore()));
    }

    private PurificationCore FindNewlyBrokenCore()
    {
        if(_challenge == null || _challenge.Cores == null) return null;

        foreach(PurificationCore core in _challenge.Cores)
            if(core != null && core.IsBroken && _seenBroken.Add(core)) return core;

        return null;
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

    // Primero el núcleo purificándose, después la planta: recién ahí las enredaderas bajan y la planta muerde.
    // No bloquea el input: la planta sigue atacando mientras se muestra.
    private IEnumerator CoreBrokenCamRoutine(PurificationCore core)
    {
        if(core != null && _camCore != null)
        {
            FrameCore(core.transform);
            SetCam(_camCore);
            yield return new WaitForSeconds(_coreCamSeconds);
        }

        SetCam(_camCoreBroken);

        float nibbleDelay = Mathf.Min(_nibbleDelay, _camSeconds);
        yield return new WaitForSeconds(nibbleDelay);

        if(_vineShield != null) _vineShield.Release();
        if(_plantAnimator != null && !_plant.IsPurified) _plantAnimator.SetTrigger("nibble");

        yield return new WaitForSeconds(_camSeconds - nibbleDelay);

        _camRoutine = null;
        EndCam();
    }

    // Del lado del player (para que se entienda qué núcleo se rompió), un poco más arriba, mirando al núcleo.
    private void FrameCore(Transform core)
    {
        Vector3 toPlayer = _plant.playerTransform != null ? _plant.playerTransform.position - core.position : -core.forward;
        toPlayer.y = 0f;
        if(toPlayer.sqrMagnitude < 0.0001f) toPlayer = Vector3.forward;

        Vector3 camPos = core.position + toPlayer.normalized * _coreCamDistance + Vector3.up * _coreCamHeight;

        Transform camTransform = _camCore.transform;
        camTransform.position = camPos;
        camTransform.rotation = Quaternion.LookRotation(core.position - camPos, Vector3.up);
    }

    private void SetCam(CinemachineVirtualCameraBase cam)
    {
        RestoreCam();
        if(cam == null) return;

        _activeCam = cam;
        _camBasePriority = cam.Priority.Value;
        cam.Priority = 30;
    }

    private void RestoreCam()
    {
        if(_activeCam == null) return;

        _activeCam.Priority = _camBasePriority;
        _activeCam = null;
    }

    // Idempotente: si se corta la corrutina (o se rompen dos núcleos seguidos) la cámara no queda tomada y el
    // escudo no queda retenido.
    private void EndCam()
    {
        if(_camRoutine != null)
        {
            StopCoroutine(_camRoutine);
            _camRoutine = null;
        }

        RestoreCam();
        if(_vineShield != null) _vineShield.Release();
    }
}
