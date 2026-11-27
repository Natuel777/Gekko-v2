using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

// El AudioSource del mismo objeto es el que hace sonar el "habla" de los diálogos (ver TypeLine): el prefab del Canvas lo trae.
[RequireComponent(typeof(AudioSource))]
public class UIManager : MonoBehaviour
{
    // Volumen del AudioSource de respaldo (ResolveTalkSource): el mismo que trae el prefab del Canvas.
    private const float FallbackTalkVolume = 0.647f;

    public static UIManager Instance { get; private set; }

    [Header("Collectibles")]
    public NotificationManager notifications;

    [Header("Dialogue")]
    [SerializeField] private CanvasGroup _dialogueGroup;
    [SerializeField] private TMP_Text _dialogueName;
    [SerializeField] private TMP_Text _dialogueText;
    [SerializeField] private float _typeSpeed = 0.02f;
    [SerializeField] private float _dialogueFadeDuration = 0.2f;
    [SerializeField] private float _closeDistance = 8f;
    [SerializeField] private Image _dialogueImage;
    [Tooltip("Retrato por nombre de hablante (speakerNames del diálogo): en cada línea se muestra el del que habla. " +
             "Si el hablante no está en la lista se usa la imagen del NPC (IDialogueable.Image).")]
    [SerializeField] private SpeakerPortrait[] _speakerPortraits;

    [System.Serializable]
    private struct SpeakerPortrait
    {
        public string speakerName;
        public Sprite image;
    }

    private readonly Queue<string> _sentences = new Queue<string>();
    private readonly Queue<string> _speakerNames = new Queue<string>();
    private IDialogueable _currentDialogue;
    // Diálogo que se dibuja solo (ej. el libro): no se usa el panel de abajo, solo se le pasa el "avanzar".
    private IDialogueView _customView;
    private bool _isTyping;
    private string _currentSentence;
    private Coroutine _typing;
    private AudioSource _audioTalk;
    private AudioClip _audioClip;
    private Sprite _npcImage;
    private int _countLetters;

    public delegate void ActivatingDialogue();
    public event ActivatingDialogue OnActivatingDialogue;
    public delegate void DeactivatingDialogue();
    public event ActivatingDialogue OnDeactivatingDialogue;
    // Se dispara al mostrar cada línea de diálogo, con el nombre de quien la dice.
    public event System.Action<string> OnDialogueLine;

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
        }

        else Destroy(gameObject);

        #region Initialization
        notifications.Initialize(StartCoroutine);

        if(_dialogueGroup != null)
        {
            _dialogueGroup.alpha = 0f;
            _dialogueGroup.interactable = false;
            _dialogueGroup.blocksRaycasts = false;
        }
        _audioTalk = ResolveTalkSource();
        #endregion
    }

    // Si el Canvas no tiene AudioSource (una escena con un Canvas viejo, o con el componente borrado en un merge) se crea uno:
    // sin él cada letra del diálogo tiraba MissingComponentException y el habla no sonaba. Se avisa para arreglar el origen.
    private AudioSource ResolveTalkSource()
    {
        AudioSource source = GetComponent<AudioSource>();
        if(source != null) return source;

        Debug.LogWarning($"[{name}] UIManager no encontró un AudioSource en el Canvas: se creó uno por código (sin grupo de mixer). Revisá que el Canvas venga del prefab Canvas.prefab, que lo trae.", this);

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = FallbackTalkVolume;
        return source;
    }

    public void ActiveDesactiveCanvasGroup(CanvasGroup canvasGroup, bool active)
    {
        StartCoroutine(CanvasGroupCorroutine(canvasGroup, active));
    }

    public void WaitXSeconds(float seconds)
    {
        StartCoroutine(WaitXSecondsCorroutine(seconds));
    }

    private IEnumerator CanvasGroupCorroutine(CanvasGroup canvasGroup, bool active)
    {
        float targetAlpha = active ? 1f : 0f;
        float startAlpha = canvasGroup.alpha;
        float elapsedTime = 0f;
        float duration = 0.5f;

        while(elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / duration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        canvasGroup.interactable = active;
        canvasGroup.blocksRaycasts = active;
    }

    private IEnumerator WaitXSecondsCorroutine(float seconds)
    {
        yield return new WaitForSeconds(seconds);
    }

    #region Dialogue
    public bool HasActiveDialogue() => _currentDialogue != null;
    public void StartDialogue(IDialogueable dialogueable)
    {
        if(_currentDialogue != null || dialogueable == null) return;

        _currentDialogue = dialogueable;
        _npcImage = dialogueable.Image;
        SetDialogueImage(_npcImage);
        _audioClip = dialogueable.AudioClip;
        RunHookSafely(_currentDialogue.OnDialogueStart);
        InvokeSafely(OnActivatingDialogue);

        // El objeto muestra el texto por su cuenta (ver IDialogueView): no se abre el panel ni se encolan frases.
        if(dialogueable is IDialogueView view)
        {
            _customView = view;
            return;
        }

        Dialogue dialogue = _currentDialogue.Dialogue;
        // Frases en el idioma elegido (español / inglés), ver Dialogue.LocalizedSentences.
        string[] sentences = dialogue.LocalizedSentences;
        bool perLineNames = dialogue.speakerNames != null && dialogue.speakerNames.Length == sentences.Length;

        _sentences.Clear();
        _speakerNames.Clear();
        for(int i = 0; i < sentences.Length; i++)
        {
            _sentences.Enqueue(sentences[i]);
            _speakerNames.Enqueue(perLineNames ? dialogue.speakerNames[i] : dialogue.speakerName);
        }

        if(_dialogueGroup != null)
            StartCoroutine(FadeDialogue(true));

        DisplayNext();
    }

    // Avanza a la siguiente frase; si está tipeando, la completa de golpe.
    public void AdvanceDialogue()
    {
        if(_currentDialogue == null) return;

        if(_customView != null)
        {
            bool keepOpen = true;
            try { keepOpen = _customView.AdvanceView(); }
            catch(System.Exception e) { Debug.LogException(e, this); keepOpen = false; }
            if(!keepOpen) EndDialogue();
            return;
        }

        if(_isTyping)
        {
            if(_typing != null) StopCoroutine(_typing);
            _dialogueText.text = _currentSentence;
            _isTyping = false;
            return;
        }

        DisplayNext();
    }

    private void DisplayNext()
    {
        if(_sentences.Count == 0)
        {
            EndDialogue();
            return;
        }

        _currentSentence = _sentences.Dequeue();

        string speaker = _speakerNames.Count > 0 ? _speakerNames.Dequeue() : string.Empty;
        // Se muestra el nombre traducido (ej. Ladybug -> Vaquita de San Antonio); el interno sigue para retratos y eventos.
        if(_dialogueName != null) _dialogueName.text = Localizer.GetSpeakerName(speaker);
        SetDialogueImage(GetSpeakerPortrait(speaker) ?? _npcImage);

        // Aviso de "empieza una línea nueva y la dice X" (ej. FriendGekkoDialogue cambia de plano según quién habla).
        try { OnDialogueLine?.Invoke(speaker); }
        catch(System.Exception e) { Debug.LogException(e); }

        if(_typing != null) StopCoroutine(_typing);
        _typing = StartCoroutine(TypeLine(_currentSentence));
    }

    // Retrato del hablante según _speakerPortraits (sin distinguir mayúsculas). Null si no está en la lista.
    private Sprite GetSpeakerPortrait(string speaker)
    {
        if(_speakerPortraits == null || string.IsNullOrEmpty(speaker)) return null;

        foreach(SpeakerPortrait portrait in _speakerPortraits)
        {
            if(portrait.image != null && string.Equals(portrait.speakerName, speaker, System.StringComparison.OrdinalIgnoreCase))
                return portrait.image;
        }
        return null;
    }

    // Sin sprite se oculta la imagen (si no, el Image de UI se dibuja como un cuadrado blanco).
    private void SetDialogueImage(Sprite sprite)
    {
        if(_dialogueImage == null) return;
        _dialogueImage.sprite = sprite;
        _dialogueImage.enabled = sprite != null;
    }

    private IEnumerator TypeLine(string sentence)
    {
        _isTyping = true;
        _dialogueText.text = string.Empty;

        foreach(char c in sentence)
        {
            _dialogueText.text += c;
            if (!char.IsWhiteSpace(c))
            {
                _countLetters++;

                // Un NPC sin clip asignado (o sin fuente) simplemente no suena, sin tirar errores en cada letra.
                if (_countLetters % 2 == 0 && _audioTalk != null && _audioClip != null)
                {
                    _audioTalk.pitch = Random.Range(0.9f, 1.1f);
                    _audioTalk.PlayOneShot(_audioClip);
                }
            }
            yield return new WaitForSeconds(_typeSpeed);
        }

        _isTyping = false;
    }

    public void EndDialogue()
    {
        if(_currentDialogue == null) return;

        if(_typing != null) StopCoroutine(_typing);
        _isTyping = false;

        if(_dialogueGroup != null && _customView == null)
            StartCoroutine(FadeDialogue(false));
        _customView = null;

        // Pase lo que pase en el hook del NPC o en quien escucha el evento, el cierre se completa: si no, el diálogo quedaba
        // abierto (HasActiveDialogue en true para siempre) o Player y la cámara nunca se enteraban de que terminó.
        RunHookSafely(_currentDialogue.OnDialogueEnd);
        InvokeSafely(OnDeactivatingDialogue);
        _currentDialogue = null;
    }

    // Los hooks de cada NPC (OnDialogueStart / OnDialogueEnd) y quienes escuchan los eventos de diálogo son código ajeno a
    // UIManager: si uno tira una excepción, el resto de la secuencia tiene que correr igual. Se avisa en la consola, que es
    // lo mismo que pasaba con una excepción suelta, pero el estado del diálogo ya no queda a medias.
    private void RunHookSafely(System.Action hook)
    {
        try { hook(); }
        catch(System.Exception e) { Debug.LogException(e, this); }
    }

    private void InvokeSafely(ActivatingDialogue evt)
    {
        if(evt == null) return;

        foreach(ActivatingDialogue handler in evt.GetInvocationList())
        {
            try { handler(); }
            catch(System.Exception e) { Debug.LogException(e, this); }
        }
    }

    // Cierre por distancia: lo llama Player.Update mientras haya diálogo activo.
    public void ArtificialUpdate()
    {
        if(_currentDialogue == null) return;
        if(_currentDialogue is DialogueNPC npc && !npc.CloseByDistance) return;

        Transform player = GameManager.Instance.Pj.transform;
        if(Vector3.Distance(player.position, _currentDialogue.Transform.position) > _closeDistance)
            EndDialogue();
    }

    private IEnumerator FadeDialogue(bool show)
    {
        float targetAlpha = show ? 1f : 0f;
        float startAlpha = _dialogueGroup.alpha;
        float elapsedTime = 0f;

        while(elapsedTime < _dialogueFadeDuration)
        {
            elapsedTime += Time.deltaTime;
            _dialogueGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / _dialogueFadeDuration);
            yield return null;
        }

        _dialogueGroup.alpha = targetAlpha;
        _dialogueGroup.interactable = show;
        _dialogueGroup.blocksRaycasts = show;
    }
    #endregion
}
