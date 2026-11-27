using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;

// Mini cinematic of LvlTwo that introduces the corrupted plant (the camp's boss) the first time Gekko reaches the
// arena, like the ones in LvlOne: a ParkourCinematicTrigger at the entrance plays the Timeline, freezes Gekko and the
// turrets ("OnCinematicStart") and can be skipped by holding E. The camera path (the Rig's animation in the Timeline) goes
// through the plates, the mirrors and the turrets before reaching the plant, so the player sees the puzzle they have to solve.
// This component receives the Timeline's signals (SignalReceiver on this same object):
//   - PlantRoar: the plant bites twice, roars (PlantGrowl) and the camera shakes.
//   - ShowTitle / HideBars: title card with the name, and exit of the cinema bars (CinematicLetterbox).
// When it ends (or is skipped) Gekko says a hint about the puzzle, using the usual dialogue panel, with a Gekko voice sound on
// each line (see Hint voice).
public class PlantRevealCinematic : MonoBehaviour, IDialogueable
{
    [SerializeField] private PlayableDirector _director;
    [Tooltip("Animator del modelo de la planta (el que tiene los triggers 'nibble' y 'splittle').")]
    [SerializeField] private Animator _plantAnimator;
    [SerializeField] private CinematicLetterbox _letterbox;

    [Header("Roar")]
    [Tooltip("Ruido de la cámara de la cinemática: se sube un momento para la sacudida.")]
    [SerializeField] private CinemachineBasicMultiChannelPerlin _shake;
    [SerializeField] private float _shakeAmplitude = 1.2f;
    [Min(0.05f)]
    [SerializeField] private float _shakeSeconds = 1.1f;
    [Tooltip("Segundos entre la primera y la segunda mordida.")]
    [Min(0f)]
    [SerializeField] private float _secondBiteDelay = 0.55f;

    [Header("Hint at the end")]
    [SerializeField] private Dialogue _hint;
    [SerializeField] private Sprite _hintImage;
    [Tooltip("Espera después de la cinemática antes de abrir el diálogo (deja que el trigger suelte a Gekko primero).")]
    [Min(0f)]
    [SerializeField] private float _hintDelay = 0.35f;

    [Header("Hint voice")]
    [Tooltip("Sonido de Gekko (SoundNames) que suena cuando dice la pista. None = sin sonido.")]
    [SerializeField] private SoundNames _hintSound = SoundNames.PlayerHmmh;
    [Tooltip("Prendido: el sonido suena en cada línea de la pista. Apagado: suena una sola vez, al empezar (con la primera línea).")]
    [SerializeField] private bool _soundOnEveryLine = true;

    private bool _hintShown;
    private int _hintLines;
    private Coroutine _shakeRoutine;

    #region IDialogueable
    public Dialogue Dialogue => _hint;
    // The line is said by Gekko: with Gekko's own transform, UIManager never cuts it by distance.
    public Transform Transform => GameManager.Instance != null && GameManager.Instance.Pj != null ? GameManager.Instance.Pj.transform : transform;
    public Sprite Image => _hintImage;
    public AudioClip AudioClip => null;
    // UIManager raises OnDialogueLine when it starts each line, the first one included (this hook runs right before it), so the
    // voice follows the lines. The -= avoids a double subscription.
    public void OnDialogueStart()
    {
        _hintLines = 0;
        if(UIManager.Instance == null) return;

        UIManager.Instance.OnDialogueLine -= OnHintLine;
        UIManager.Instance.OnDialogueLine += OnHintLine;
    }

    public void OnDialogueEnd()
    {
        if(UIManager.Instance != null) UIManager.Instance.OnDialogueLine -= OnHintLine;
    }
    #endregion

    private void OnEnable()
    {
        if(_director != null) _director.stopped += OnDirectorStopped;
    }

    private void OnDisable()
    {
        if(_director != null) _director.stopped -= OnDirectorStopped;
        if(UIManager.Instance != null) UIManager.Instance.OnDialogueLine -= OnHintLine;
        SetShake(0f);
    }

    // ------------------------------------------------------------------ Timeline signals

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

    // Strong hit at the start that fades out.
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

    // It ended or was skipped: Gekko says the hint (only once).
    private void OnDirectorStopped(PlayableDirector director)
    {
        SetShake(0f);
        if(_hintShown || !isActiveAndEnabled) return;
        _hintShown = true;
        StartCoroutine(HintRoutine());
    }

    // ParkourCinematicTrigger releases Gekko on the same 'stopped' event: if the dialogue opened earlier, releasing Gekko would
    // give the control back with the dialogue open. That is why it waits a little.
    private IEnumerator HintRoutine()
    {
        if(_hintDelay > 0f) yield return new WaitForSeconds(_hintDelay);

        if(_hint.LocalizedSentences.Length == 0) yield break;
        if(UIManager.Instance == null || UIManager.Instance.HasActiveDialogue()) yield break;

        UIManager.Instance.StartDialogue(this);
    }

    // One Gekko voice sound for the line that is starting: on every line, or only on the first one (see Sound On Every Line).
    private void OnHintLine(string speaker)
    {
        _hintLines++;
        if(!_soundOnEveryLine && _hintLines > 1) return;
        if(_hintSound == SoundNames.None || !AudioManager.instance) return;

        AudioManager.instance.Play(_hintSound);
    }
}
