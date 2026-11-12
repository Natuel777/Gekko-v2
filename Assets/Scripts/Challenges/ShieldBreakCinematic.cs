using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Splines;

// Cinemática corta cada vez que se rompe un destructor de escudo (torreta, núcleo...) de un Challenge. Es la parte de cámaras
// de BossPlantPhases (LvlOne) pero genérica: escucha Challenge.BreakerBroken, así sirve con cualquier desafío.
//  1. (Opcional) Cámara al destructor que se acaba de romper, ubicada por código entre él y el player.
//  2. Cámara a la planta. Al rato la planta hace 'nibble' y las raíces (VineShield) bajan su tramo: el escudo se retiene
//     (Hold) hasta ese momento, así se ve el recorte en cámara.
// Mientras dura, Gekko, la planta y las torretas quedan congelados ("OnCinematicStart"), igual que en LvlOne.
// La cámara de la planta puede ser una cámara fija o una "Dolly Camera with Spline" (menú Cinemachine): si tiene
// CinemachineSplineDolly, se la hace recorrer su spline durante el plano.
public class ShieldBreakCinematic : MonoBehaviour
{
    [Tooltip("Desafío que avisa cada destructor roto (MirrorChallenge, PurificationChallenge...). De él se toman la planta y el VineShield.")]
    [SerializeField] private Challenge _challenge;

    [Header("Breaker Camera (primero, opcional)")]
    [Tooltip("Cámara que mira al destructor recién roto (la torreta purificándose). Se ubica por código entre él y el player. Vacío = va directo a la cámara de la planta.")]
    [SerializeField] private CinemachineVirtualCameraBase _breakerCam;
    [Min(0f)]
    [SerializeField] private float _breakerCamSeconds = 1.5f;
    [Tooltip("Distancia horizontal de la cámara al destructor, hacia el lado del player.")]
    [SerializeField] private float _breakerCamDistance = 4f;
    [Tooltip("Altura de la cámara sobre el destructor.")]
    [SerializeField] private float _breakerCamHeight = 1.5f;

    [Header("Plant Camera (después: la planta y sus raíces)")]
    [Tooltip("Cámara que muestra la planta mientras bajan las raíces. Fija (como CM_PlantCoreBroken) o 'Dolly Camera with Spline'. Tiene que tener a dónde mirar (Rotation Composer con la planta de Tracking Target, o una rotación fija). Si no tiene Tracking Target se le pone la planta.")]
    [SerializeField] private CinemachineVirtualCameraBase _plantCam;
    [Min(0f)]
    [SerializeField] private float _plantCamSeconds = 2.5f;
    [Tooltip("Segundos desde que arranca la cámara de la planta hasta que la planta hace 'nibble' y las raíces bajan (dar tiempo al blend).")]
    [Min(0f)]
    [SerializeField] private float _vinesDelay = 0.8f;

    [Header("Dolly (solo si la cámara de la planta tiene CinemachineSplineDolly)")]
    [Tooltip("Posición en el spline (0 = principio, 1 = final) al empezar el plano.")]
    [Range(0f, 1f)]
    [SerializeField] private float _dollyFrom = 0f;
    [Tooltip("Posición en el spline al terminar el plano.")]
    [Range(0f, 1f)]
    [SerializeField] private float _dollyTo = 1f;

    [Header("Options")]
    [Tooltip("También al romperse el último. Si el desafío tiene timeline de 'escudo caído', ese timeline toma la cámara igual (como en LvlOne); esto solo baja las raíces y hace el 'nibble'.")]
    [SerializeField] private bool _onLastBreaker = true;
    [Tooltip("Prioridad que toma la cámara activa durante el plano (la de juego tiene 10).")]
    [SerializeField] private int _shotPriority = 30;

    private CinemachineVirtualCameraBase _activeCam;
    private int _camBasePriority;
    private Coroutine _routine;
    private bool _frozen, _vineHeld;

    private CarnivorousPlant Plant => _challenge != null ? _challenge.Plant : null;
    private VineShield Vines => _challenge != null ? _challenge.VineShield : null;

    private void OnEnable()
    {
        if(_challenge == null)
        {
            Debug.LogWarning($"[{name}] ShieldBreakCinematic sin Challenge asignado: no va a mostrar nada.", this);
            return;
        }

        _challenge.BreakerBroken += OnBreakerBroken;
    }

    private void OnDisable()
    {
        if(_challenge != null) _challenge.BreakerBroken -= OnBreakerBroken;
        EndShot();
    }

    // Llega antes de que el desafío baje el tramo del escudo: el Hold lo retiene hasta que la cámara muestre la planta.
    private void OnBreakerBroken(IShieldBreaker breaker, int remaining, int total)
    {
        if(remaining == 0 && !_onLastBreaker) return;
        if(_plantCam == null && _breakerCam == null) return;

        EndShot();

        if(Vines != null)
        {
            Vines.Hold();
            _vineHeld = true;
        }

        _routine = StartCoroutine(ShotRoutine(breaker));
    }

    private IEnumerator ShotRoutine(IShieldBreaker breaker)
    {
        Freeze(true);

        Transform breakerTransform = breaker as Object != null ? breaker.transform : null;
        if(_breakerCam != null && breakerTransform != null)
        {
            FrameBreaker(breakerTransform);
            SetCam(_breakerCam);
            yield return new WaitForSeconds(_breakerCamSeconds);
        }

        if(_plantCam != null)
        {
            CinemachineSplineDolly dolly = PreparePlantCam();
            SetCam(_plantCam);

            bool vinesDown = false;
            float duration = Mathf.Max(_plantCamSeconds, 0.01f);
            for(float t = 0f; t < duration; t += Time.deltaTime)
            {
                if(dolly != null) dolly.CameraPosition = Mathf.Lerp(_dollyFrom, _dollyTo, Mathf.SmoothStep(0f, 1f, t / duration));

                if(!vinesDown && t >= Mathf.Min(_vinesDelay, duration))
                {
                    vinesDown = true;
                    LowerVines();
                }

                yield return null;
            }

            if(!vinesDown) LowerVines();
        }
        else
        {
            LowerVines();
        }

        _routine = null;
        EndShot();
    }

    // La planta muerde y las raíces bajan el tramo que quedó retenido.
    private void LowerVines()
    {
        ReleaseVines();

        CarnivorousPlant plant = Plant;
        if(plant != null && !plant.IsPurified && plant.Animator != null) plant.Animator.SetTrigger("nibble");
    }

    private void ReleaseVines()
    {
        if(!_vineHeld) return;
        _vineHeld = false;

        if(Vines != null) Vines.Release();
    }

    // Si es una dolly, arranca en _dollyFrom (en unidades normalizadas) antes de tomar la prioridad, así el blend no viaja
    // desde donde quedó la vez anterior. Si no tiene a quién mirar, mira a la planta.
    private CinemachineSplineDolly PreparePlantCam()
    {
        if(_plantCam is CinemachineCamera cam && cam.Target.TrackingTarget == null && Plant != null)
            cam.Target.TrackingTarget = Plant.transform;

        if(!_plantCam.TryGetComponent(out CinemachineSplineDolly dolly) || dolly.Spline == null) return null;

        // Con Auto Dolly prendido el spline lo maneja Cinemachine (sigue al target): no se pisa.
        if(dolly.AutomaticDolly.Enabled) return null;

        dolly.PositionUnits = PathIndexUnit.Normalized;
        dolly.CameraPosition = _dollyFrom;
        return dolly;
    }

    // Del lado del player (para que se entienda cuál se rompió), un poco más arriba, mirando al destructor.
    private void FrameBreaker(Transform breaker)
    {
        Player pj = GameManager.Instance != null ? GameManager.Instance.Pj : null;
        Vector3 toPlayer = pj != null ? pj.transform.position - breaker.position : -breaker.forward;
        toPlayer.y = 0f;
        if(toPlayer.sqrMagnitude < 0.0001f) toPlayer = Vector3.forward;

        Vector3 camPos = breaker.position + toPlayer.normalized * _breakerCamDistance + Vector3.up * _breakerCamHeight;
        _breakerCam.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(breaker.position - camPos, Vector3.up));
    }

    private void SetCam(CinemachineVirtualCameraBase cam)
    {
        RestoreCam();
        if(cam == null) return;

        _activeCam = cam;
        _camBasePriority = cam.Priority.Value;
        cam.Priority = _shotPriority;
    }

    private void RestoreCam()
    {
        if(_activeCam == null) return;

        _activeCam.Priority = _camBasePriority;
        _activeCam = null;
    }

    // Congela a Gekko y avisa a los NPCs (planta, torretas, escarabajos) igual que una cinemática del nivel.
    private void Freeze(bool frozen)
    {
        if(_frozen == frozen) return;
        _frozen = frozen;

        Player pj = GameManager.Instance != null ? GameManager.Instance.Pj : null;
        if(pj != null) pj.SetFrozenForCinematic(frozen);
        EventManager.Trigger(frozen ? "OnCinematicStart" : "OnCinematicEnd");
    }

    // Idempotente: si se corta la corrutina (o se rompen dos seguidos) la cámara no queda tomada, las raíces no quedan
    // retenidas y Gekko no queda congelado.
    private void EndShot()
    {
        if(_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        RestoreCam();
        ReleaseVines();
        Freeze(false);
    }
}
