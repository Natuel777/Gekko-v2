using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;

// Mini cinemática de LvlTwo que presenta a la planta corrupta (el jefe del campamento) la primera vez que Gekko llega a la
// arena, como las de LvlOne: un ParkourCinematicTrigger en la entrada reproduce el Timeline, congela a Gekko y a las
// torretas ("OnCinematicStart") y se saltea manteniendo E. El recorrido de cámara (animación del Rig en el Timeline) pasa
// por las placas, los espejos y las torretas antes de llegar a la planta, así el jugador ve el puzzle que tiene que resolver.
// Este componente recibe las señales del Timeline (SignalReceiver en este mismo objeto):
//   - PlantRoar: la planta muerde dos veces, ruge (PlantGrowl) y la cámara se sacude.
//   - ShowTitle / HideBars: cartel con el nombre y salida de las barras de cine (CinematicLetterbox).
// Al terminar (o al saltearla) Gekko dice una línea con la pista del puzzle, usando el panel de diálogo de siempre.
public class PlantRevealCinematic : MonoBehaviour, IDialogueable
{
    [SerializeField] private PlayableDirector _director;
    [Tooltip("Animator del modelo de la planta (el que tiene los triggers 'nibble' y 'splittle').")]
    [SerializeField] private Animator _plantAnimator;
    [SerializeField] private CinematicLetterbox _letterbox;

    [Header("Rugido")]
    [Tooltip("Ruido de la cámara de la cinemática: se sube un momento para la sacudida.")]
    [SerializeField] private CinemachineBasicMultiChannelPerlin _shake;
    [SerializeField] private float _shakeAmplitude = 1.2f;
    [Min(0.05f)]
    [SerializeField] private float _shakeSeconds = 1.1f;
    [Tooltip("Segundos entre la primera y la segunda mordida.")]
    [Min(0f)]
    [SerializeField] private float _secondBiteDelay = 0.55f;

    [Header("Pista al terminar")]
    [SerializeField] private Dialogue _hint;
    [SerializeField] private Sprite _hintImage;
    [Tooltip("Espera después de la cinemática antes de abrir el diálogo (deja que el trigger suelte a Gekko primero).")]
    [Min(0f)]
    [SerializeField] private float _hintDelay = 0.35f;

    private bool _hintShown;
    private Coroutine _shakeRoutine;

    #region IDialogueable
    public Dialogue Dialogue => _hint;
    // La línea la dice Gekko: con su propio transform, UIManager nunca la corta por distancia.
    public Transform Transform => GameManager.Instance != null && GameManager.Instance.Pj != null ? GameManager.Instance.Pj.transform : transform;
    public Sprite Image => _hintImage;
    public AudioClip AudioClip => null;
    public void OnDialogueStart() { }
    public void OnDialogueEnd() { }
    #endregion

    private void OnEnable()
    {
        if(_director != null) _director.stopped += OnDirectorStopped;
    }

    private void OnDisable()
    {
        if(_director != null) _director.stopped -= OnDirectorStopped;
        SetShake(0f);
    }

    // ------------------------------------------------------------------ Señales del Timeline

    public void PlantRoar()
    {
        if(AudioManager.instance) AudioManager.instance.Play(SoundNames.PlantGrowl);
        StartCoroutine(BiteTwice());

        if(_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _shakeRoutine = StartCoroutine(ShakeRoutine());
    }

    public void ShowTitle()
    {
        if(_letterbox != null) _letterbox.ShowTitle();
    }

    public void HideBars()
    {
        if(_letterbox != null) _letterbox.HideBars();
    }

    // ------------------------------------------------------------------

    private IEnumerator BiteTwice()
    {
        if(_plantAnimator == null) yield break;

        _plantAnimator.SetTrigger("nibble");
        yield return new WaitForSeconds(_secondBiteDelay);
        if(_plantAnimator != null) _plantAnimator.SetTrigger("nibble");
    }

    // Golpe fuerte al principio y se va apagando.
    private IEnumerator ShakeRoutine()
    {
        for(float t = 0f; t < 1f; t += Time.deltaTime / _shakeSeconds)
        {
            float k = (1f - t) * (1f - t);
            SetShake(_shakeAmplitude * k);
            yield return null;
        }
        SetShake(0f);
        _shakeRoutine = null;
    }

    private void SetShake(float amplitude)
    {
        if(_shake != null) _shake.AmplitudeGain = amplitude;
    }

    // Terminó o se salteó: Gekko dice la pista (una sola vez).
    private void OnDirectorStopped(PlayableDirector director)
    {
        SetShake(0f);
        if(_hintShown || !isActiveAndEnabled) return;
        _hintShown = true;
        StartCoroutine(HintRoutine());
    }

    // ParkourCinematicTrigger suelta a Gekko en el mismo evento 'stopped': si el diálogo se abriera antes, al soltarlo le
    // devolvería el control con el diálogo abierto. Por eso se espera un poco.
    private IEnumerator HintRoutine()
    {
        if(_hintDelay > 0f) yield return new WaitForSeconds(_hintDelay);

        if(_hint.sentences == null || _hint.sentences.Length == 0) yield break;
        if(UIManager.Instance == null || UIManager.Instance.HasActiveDialogue()) yield break;

        UIManager.Instance.StartDialogue(this);
    }
}
