using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

// Beat de cierre al derrotar al jefe: sube la prioridad de la cámara de cierre, bloquea el input del
// player (deja tiempo para el lengüetazo final) y la baja pasado el tiempo. Mismo patrón que
// PurificationChallenge.CompletionBeat/EndBeat (LittleBarricade.RevealCameraRoutine también lo usa),
// como componente aparte para no tocar esas clases.
public class CorruptedGeckoBossFinishBeat : MonoBehaviour
{
    [Header("Jefe")]
    [SerializeField] private CorruptedGeckoBoss _boss;

    [Header("Cámara")]
    [SerializeField] private CinemachineCamera _finishCamera;
    [Tooltip("Segundos que dura el cierre (cámara + input bloqueado) antes de devolver el control.")]
    [SerializeField] private float _beatSeconds = 3f;

    private bool _beatActive;
    private bool _inputLocked;

    private void OnEnable()
    {
        // Fuerza el reposo: la cámara puede traer cualquier prioridad guardada en la escena.
        if(_finishCamera != null) _finishCamera.Priority = 0;

        if(_boss != null) _boss.Defeated += OnDefeated;
    }

    private void OnDisable()
    {
        if(_boss != null) _boss.Defeated -= OnDefeated;

        // Idempotente: si el jefe se desactiva (o la escena se recarga) a mitad del beat, el input/cámara
        // no deben quedar trabados.
        EndBeat();
    }

    private void OnDefeated() => StartCoroutine(FinishRoutine());

    private IEnumerator FinishRoutine()
    {
        _beatActive = true;

        if(_finishCamera != null) _finishCamera.Priority = 30;

        Player pj = GameManager.Instance != null ? GameManager.Instance.Pj : null;

        if(pj != null)
        {
            pj.Inputs(false);
            _inputLocked = true;
        }

        yield return new WaitForSeconds(_beatSeconds);

        EndBeat();
    }

    private void EndBeat()
    {
        if(!_beatActive) return;
        _beatActive = false;

        if(_finishCamera != null) _finishCamera.Priority = 0;

        if(_inputLocked)
        {
            _inputLocked = false;

            Player pj = GameManager.Instance != null ? GameManager.Instance.Pj : null;
            if(pj != null) pj.Inputs(true);
        }
    }
}
