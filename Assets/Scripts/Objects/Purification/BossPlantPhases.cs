using System.Collections;
using System.Collections.Generic;
using Gekko.Purification;
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

    [Header("Region Camera (primero: la zona purificándose)")]
    [Tooltip("Al romperse un núcleo, mostrar la zona que purifica (su PurifyRegion) mientras avanza la onda, antes de la planta.")]
    [SerializeField] private bool _showRegion = true;
    [Tooltip("Segundos en la zona (la onda de las regiones del boss dura 3 s).")]
    [Min(0f)]
    [SerializeField] private float _regionCamSeconds = 3.2f;
    [Tooltip("Distancia hacia afuera de la zona (del lado opuesto a la planta), en radios de la región.")]
    [SerializeField] private float _regionCamBack = 1.1f;
    [Tooltip("Altura de la cámara sobre el centro de la zona, en radios de la región.")]
    [SerializeField] private float _regionCamHeight = 1f;
    [Tooltip("Hacia dónde mira: el centro de la zona corrido hacia la planta, en radios (así la planta queda al fondo).")]
    [SerializeField] private float _regionLookTowardPlant = 0.25f;
    [Tooltip("Cuánto se acerca la cámara durante el plano (fracción de la distancia).")]
    [Range(0f, 0.5f)]
    [SerializeField] private float _regionPushIn = 0.15f;

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
    private CinemachineCamera _regionCam; // se crea por código la primera vez
    private bool _frozen;
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

        // El comportamiento viejo se descarta: si estaba en plena preparación del disparo, se corta (los huesos vuelven solos).
        if(_plant.spitBehaviour != null) _plant.spitBehaviour.StopSpitting();

        // Mismo PlantBiteWindup que usa la planta (si lo tiene): cada escupida va precedida de su preparación.
        PlantSpitBehaviour spit = new PlantSpitBehaviour(_head, _firePoint, _venomPrefab, interval,
                                                         _plant.data.rotateToPlayer, _plant.data.rotationSpeed,
                                                         _muzzlePS, PlaySpit, _plant.GetComponent<PlantBiteWindup>());
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

    // Primero la zona del núcleo purificándose (o el núcleo, si hay _camCore), después la planta: recién ahí las
    // enredaderas bajan y la planta muerde. Mientras dura, Gekko y la planta quedan congelados ("OnCinematicStart"):
    // si no, la planta le seguía escupiendo con la cámara en otro lado.
    private IEnumerator CoreBrokenCamRoutine(PurificationCore core)
    {
        Freeze(true);

        PurifyRegion region = _showRegion ? FindRegion(core) : null;
        if(region != null)
        {
            CinemachineCamera cam = GetRegionCam();
            FrameRegion(region, out Vector3 from, out Vector3 to, out Vector3 look);
            cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(look - from, Vector3.up));
            SetCam(cam);

            // Acercamiento lento mientras avanza la onda.
            for(float t = 0f; t < _regionCamSeconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / _regionCamSeconds);
                Vector3 pos = Vector3.Lerp(from, to, k);
                cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos, Vector3.up));
                yield return null;
            }
        }
        else if(core != null && _camCore != null)
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

    // Región que purifica este núcleo: la del PurifyTrigger que lo tiene asignado.
    private static PurifyRegion FindRegion(PurificationCore core)
    {
        if(core == null) return null;

        foreach(PurifyTrigger trigger in FindObjectsByType<PurifyTrigger>(FindObjectsSortMode.None))
            if(trigger.Nucleo == core) return trigger.Region;

        return null;
    }

    // Desde afuera de la zona (del lado opuesto a la planta), en alto, mirando hacia la planta: se ve la onda avanzar en
    // primer plano y la planta al fondo, que es el plano que sigue.
    private void FrameRegion(PurifyRegion region, out Vector3 from, out Vector3 to, out Vector3 look)
    {
        Vector3 center = Vector3.zero;
        float radius = 0f;
        int count = Mathf.Max(1, region.CantidadNodos);
        for(int i = 0; i < region.CantidadNodos; i++)
        {
            region.ObtenerNodo(i, out Vector3 c, out _, out float r);
            center += c;
            radius = Mathf.Max(radius, r);
        }
        center /= count;
        if(radius <= 0f) radius = 10f;

        Vector3 toPlant = _plant != null ? _plant.transform.position - center : Vector3.forward;
        toPlant.y = 0f;
        toPlant = toPlant.sqrMagnitude > 0.0001f ? toPlant.normalized : Vector3.forward;

        look = center + toPlant * radius * _regionLookTowardPlant;

        // Primero el lado opuesto a la planta; si algo (piedras, árboles, el terreno) tapa la vista al centro, se prueba
        // rodeando la zona. Si ninguno queda limpio, se usa el primero.
        from = RegionCamPosition(center, toPlant, radius, 0f);
        foreach(float angle in RegionCamAngles)
        {
            Vector3 candidate = RegionCamPosition(center, toPlant, radius, angle);
            if(HasClearView(candidate, center) && HasClearView(candidate, look))
            {
                from = candidate;
                look = center + Quaternion.Euler(0f, angle, 0f) * toPlant * radius * _regionLookTowardPlant;
                break;
            }
        }

        to = Vector3.Lerp(from, look, _regionPushIn);
    }

    private static readonly float[] RegionCamAngles = { 0f, 25f, -25f, 50f, -50f, 75f, -75f };

    private Vector3 RegionCamPosition(Vector3 center, Vector3 toPlant, float radius, float angle)
    {
        Vector3 dir = Quaternion.Euler(0f, angle, 0f) * toPlant;
        return center - dir * radius * _regionCamBack + Vector3.up * radius * _regionCamHeight;
    }

    private static bool HasClearView(Vector3 from, Vector3 to)
    {
        if(Physics.CheckSphere(from, 0.5f, ~0, QueryTriggerInteraction.Ignore)) return false;

        // Termina un poco antes: lo que haya justo en el centro (el núcleo, el piso) no cuenta como obstáculo.
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        return !Physics.SphereCast(from, 0.6f, dir / dist, out _, Mathf.Max(0f, dist - 3f), ~0, QueryTriggerInteraction.Ignore);
    }

    private CinemachineCamera GetRegionCam()
    {
        if(_regionCam != null) return _regionCam;

        // Se arma apagada para que no le gane a la cámara de juego ni un frame antes de tener prioridad.
        var go = new GameObject("CM_RegionPurifying");
        go.SetActive(false);
        go.transform.SetParent(transform, false);
        _regionCam = go.AddComponent<CinemachineCamera>();
        if(_camCoreBroken is CinemachineCamera plantCam) _regionCam.Lens = plantCam.Lens;
        _regionCam.Priority = 0;
        go.SetActive(true);
        return _regionCam;
    }

    // Congela a Gekko y avisa a los NPCs (la planta, los escarabajos) igual que una cinemática del nivel.
    private void Freeze(bool frozen)
    {
        if(_frozen == frozen) return;
        _frozen = frozen;

        Player pj = GameManager.Instance != null ? GameManager.Instance.Pj : null;
        if(pj != null) pj.SetFrozenForCinematic(frozen);
        EventManager.Trigger(frozen ? "OnCinematicStart" : "OnCinematicEnd");
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
        Freeze(false);
    }
}
