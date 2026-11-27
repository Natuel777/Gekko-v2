using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

// Plano de cámara que muestra el espejo que se levanta la primera vez que Gekko pisa una placa del desafío de espejos (ej. el
// PrimerChallenge de LvlTwo). Cada placa tiene asignada la cámara de la escena que encuadra el espejo que se muestra (se
// pueden mover a mano en el Editor). Secuencia:
//   1. Gekko y las torretas quedan congelados ("OnCinematicStart", igual que VinesRevealCinematic / ShieldBreakCinematic).
//   2. La cámara viaja con un blend suave hasta el espejo. La placa espera a que llegue para alternar los espejos
//      (PressurePlateRuna.BeforeToggle): así se ve cómo se levanta.
//   3. Se queda mirando mientras se levanta y se enciende su runa, y vuelve a la cámara de juego con el mismo blend.
// Pasa una sola vez: las pisadas siguientes alternan los espejos al instante, como siempre.
public class MirrorRevealCinematic : MonoBehaviour
{
    [System.Serializable]
    private class PlateShot
    {
        public PressurePlateRuna plate;
        [Tooltip("Cámara que encuadra el espejo que levanta esta placa.")]
        public CinemachineCamera camera;
    }

    [Tooltip("Placas que disparan el plano (la primera que se pise) y la cámara de cada una.")]
    [SerializeField] private PlateShot[] _shots;
    [Tooltip("Prioridad que toma la cámara durante el plano (la de juego tiene 10).")]
    [SerializeField] private int _shotPriority = 30;
    [Tooltip("Segundos del viaje de la cámara hasta el espejo (y de la vuelta).")]
    [Min(0.1f)]
    [SerializeField] private float _blendSeconds = 1.5f;
    [Tooltip("Segundos mirando el espejo desde que empieza a levantarse (tarda ~0.6 s en levantarse y la runa ~0.8 s más en encenderse).")]
    [Min(0f)]
    [SerializeField] private float _holdSeconds = 2f;

    // Opcional: corre con la cámara todavía en el espejo y Gekko congelado, justo antes de volver a la cámara de juego (ej.
    // MirrorTutorial muestra cómo rebota la bala). Recibe la placa que se pisó.
    public System.Func<PressurePlateRuna, IEnumerator> AfterReveal;

    // El plano está corriendo (Gekko congelado, cámara en el espejo).
    public bool IsPlaying => _routine != null;

    private bool _played, _frozen;
    private CinemachineCamera _activeCam;
    private int _camBasePriority;
    private CinemachineBrain _brain;
    private CinemachineBlendDefinition _savedBlend;
    private bool _blendSaved;
    private Coroutine _routine;

    private void OnEnable()
    {
        if(_shots == null) return;
        foreach(PlateShot shot in _shots)
            if(shot.plate != null) shot.plate.BeforeToggle = OnPlateBeforeToggle;
    }

    private void OnDisable()
    {
        if(_shots != null)
        {
            foreach(PlateShot shot in _shots)
                if(shot.plate != null && shot.plate.BeforeToggle == OnPlateBeforeToggle) shot.plate.BeforeToggle = null;
        }
        EndShot();
    }

    // La placa terminó de hundirse y está por alternar sus espejos: si es la primera, arranca el plano y le pide que espere a
    // que la cámara llegue.
    private float OnPlateBeforeToggle(PressurePlateRuna plate)
    {
        if(_played || !isActiveAndEnabled) return 0f;

        CinemachineCamera cam = FindCamera(plate);
        if(cam == null) return 0f;

        _played = true;
        _routine = StartCoroutine(ShotRoutine(cam, plate));
        return _blendSeconds;
    }

    private CinemachineCamera FindCamera(PressurePlateRuna plate)
    {
        foreach(PlateShot shot in _shots)
            if(shot.plate == plate) return shot.camera;
        return null;
    }

    private IEnumerator ShotRoutine(CinemachineCamera cam, PressurePlateRuna plate)
    {
        Freeze(true);
        SetBlend();
        StartShot(cam);

        // Viaje hasta el espejo + el espejo levantándose.
        yield return new WaitForSeconds(_blendSeconds + _holdSeconds);

        // Extra opcional con la cámara todavía en el espejo (ej. MirrorTutorial muestra el rebote de la bala).
        if(AfterReveal != null)
        {
            IEnumerator extra = null;
            try { extra = AfterReveal(plate); }
            catch(System.Exception e) { Debug.LogException(e, this); }
            if(extra != null) yield return extra;
        }

        StopShot();

        // Vuelta a la cámara de juego: recién al llegar se suelta a Gekko.
        yield return new WaitForSeconds(_blendSeconds);

        _routine = null;
        EndShot();
    }

    private void StartShot(CinemachineCamera cam)
    {
        _activeCam = cam;
        _camBasePriority = cam.Priority.Value;
        cam.Priority = _shotPriority;
    }

    private void StopShot()
    {
        if(_activeCam == null) return;
        _activeCam.Priority = _camBasePriority;
        _activeCam = null;
    }

    // Idempotente: si se corta la corrutina (respawn, cambio de escena) la cámara no queda tomada ni Gekko congelado.
    private void EndShot()
    {
        if(_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        StopShot();
        RestoreBlend();
        Freeze(false);
    }

    // El blend por defecto del brain (1 s) se cambia mientras dura el plano para que el viaje tenga la duración pedida.
    private void SetBlend()
    {
        Camera main = Camera.main;
        _brain = main != null ? main.GetComponent<CinemachineBrain>() : null;
        if(_brain == null || _blendSaved) return;

        _savedBlend = _brain.DefaultBlend;
        _blendSaved = true;
        _brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, _blendSeconds);
    }

    private void RestoreBlend()
    {
        if(_brain != null && _blendSaved) _brain.DefaultBlend = _savedBlend;
        _blendSaved = false;
    }

    // Congela a Gekko y avisa a las torretas igual que una cinemática del nivel.
    private void Freeze(bool frozen)
    {
        if(_frozen == frozen) return;
        _frozen = frozen;

        Player pj = GameManager.Instance != null ? GameManager.Instance.Pj : null;
        if(pj != null) pj.SetFrozenForCinematic(frozen);
        EventManager.Trigger(frozen ? "OnCinematicStart" : "OnCinematicEnd");
    }
}
