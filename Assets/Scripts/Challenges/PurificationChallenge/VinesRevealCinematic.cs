using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

// Plano de cámara de las enredaderas que tapan una superficie escalable (ej. el árbol de la Second Zone en LvlTwo).
// Arranca cuando termina la cinemática de la planta purificada (Challenge.CompletionBeatFinished), a la vez que las
// enredaderas (ChallengeVinesDissolve con trigger AfterPurifiedCinematic, que esperan su delay para dar tiempo al blend).
// La cámara mira a _lookTarget: arranca encuadrando las enredaderas y, a medida que VinesClimbUnlock va mostrando las
// patitas, el target salta a cada una y la cámara las sigue hacia arriba (el damping del Rotation Composer suaviza el
// paneo). Termina un rato después de la última patita. Mientras dura, Gekko y los NPCs quedan congelados, igual que en
// ShieldBreakCinematic.
public class VinesRevealCinematic : MonoBehaviour
{
    [Tooltip("Desafío cuya cinemática de planta purificada, al terminar, dispara este plano.")]
    [SerializeField] private Challenge _challenge;
    [Tooltip("Enredaderas y patitas que se muestran: la cámara sigue cada patita que aparece.")]
    [SerializeField] private VinesClimbUnlock _unlock;

    [Header("Cámara")]
    [Tooltip("Cámara del plano. Tiene que mirar a _lookTarget (Tracking Target + Rotation Composer).")]
    [SerializeField] private CinemachineVirtualCameraBase _camera;
    [Tooltip("Lo que mira la cámara. Arranca donde está (sobre las enredaderas) y se mueve a cada patita que aparece.")]
    [SerializeField] private Transform _lookTarget;
    [Tooltip("Prioridad que toma la cámara durante el plano (la de juego tiene 10).")]
    [SerializeField] private int _shotPriority = 30;

    [Header("Tiempos")]
    [Tooltip("Segundos que se queda en la última patita antes de devolver la cámara.")]
    [Min(0f)]
    [SerializeField] private float _endHold = 1.2f;
    [Tooltip("Tope por si algo no avisa (enredaderas sin disolver, patitas sin aparecer): pasado este tiempo se devuelve la cámara igual.")]
    [Min(1f)]
    [SerializeField] private float _maxSeconds = 15f;

    private Vector3 _lookStart;
    private int _camBasePriority;
    private bool _shotActive, _frozen, _played;
    private Coroutine _routine;

    private void Awake()
    {
        if(_lookTarget != null) _lookStart = _lookTarget.position;
    }

    private void OnEnable()
    {
        if(_challenge == null || _unlock == null || _camera == null)
        {
            Debug.LogWarning($"[{name}] VinesRevealCinematic sin Challenge, VinesClimbUnlock o cámara asignados: no va a mostrar nada.", this);
            return;
        }

        _challenge.CompletionBeatFinished += OnCompletionBeatFinished;
        _unlock.FootprintRevealed += OnFootprintRevealed;
    }

    private void OnDisable()
    {
        if(_challenge != null) _challenge.CompletionBeatFinished -= OnCompletionBeatFinished;
        if(_unlock != null) _unlock.FootprintRevealed -= OnFootprintRevealed;
        EndShot();
    }

    private void OnCompletionBeatFinished()
    {
        // Una sola vez: si las patitas ya están (se recargó a mitad, etc.) no hay nada que mostrar.
        // Si la cinemática se cortó porque se está descargando la escena, este objeto puede estar apagándose.
        if(_played || _unlock.AllFootprintsRevealed || !isActiveAndEnabled) return;
        _played = true;

        _routine = StartCoroutine(ShotRoutine());
    }

    private IEnumerator ShotRoutine()
    {
        if(_lookTarget != null) _lookTarget.position = _lookStart;

        Freeze(true);
        StartShot();

        // Hasta que aparece la última patita (o el tope de seguridad).
        float elapsed = 0f;
        while(!_unlock.AllFootprintsRevealed && elapsed < _maxSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if(_endHold > 0f) yield return new WaitForSeconds(_endHold);

        _routine = null;
        EndShot();
    }

    private void OnFootprintRevealed(Transform footprint)
    {
        if(_shotActive && _lookTarget != null) _lookTarget.position = footprint.position;
    }

    private void StartShot()
    {
        _shotActive = true;
        _camBasePriority = _camera.Priority.Value;
        _camera.Priority = _shotPriority;
    }

    // Idempotente: si se corta la corrutina la cámara no queda tomada ni Gekko congelado.
    private void EndShot()
    {
        if(_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        if(_shotActive)
        {
            _shotActive = false;
            if(_camera != null) _camera.Priority = _camBasePriority;
        }

        Freeze(false);
    }

    // Congela a Gekko y avisa a los NPCs igual que una cinemática del nivel.
    private void Freeze(bool frozen)
    {
        if(_frozen == frozen) return;
        _frozen = frozen;

        Player pj = GameManager.Instance != null ? GameManager.Instance.Pj : null;
        if(pj != null) pj.SetFrozenForCinematic(frozen);
        EventManager.Trigger(frozen ? "OnCinematicStart" : "OnCinematicEnd");
    }
}
