using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    public NotificationManager notifications;

    [Header("Collectibles")]
    [SerializeField] private GameObject[] _collectiblesCounters;

    [Header("Dialogue")]
    [SerializeField] private CanvasGroup _dialogueGroup;
    [SerializeField] private TMP_Text _dialogueName;
    [SerializeField] private TMP_Text _dialogueText;
    [SerializeField] private float _typeSpeed = 0.02f;
    [SerializeField] private float _dialogueFadeDuration = 0.2f;
    [SerializeField] private float _closeDistance = 8f;
    [SerializeField] private Image _dialogueImage;

    private readonly Queue<string> _sentences = new Queue<string>();
    private IDialogueable _currentDialogue;
    private bool _isTyping;
    private string _currentSentence;
    private Coroutine _typing;
    private AudioSource _audioTalk;
    private AudioClip _audioClip;
    private int _countLetters;

    public delegate void ActivatingDialogue();
    public event ActivatingDialogue OnActivatingDialogue;
    public delegate void DeactivatingDialogue();
    public event ActivatingDialogue OnDeactivatingDialogue;

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
        _audioTalk = GetComponent<AudioSource>();
        #endregion
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
        _dialogueImage.sprite = dialogueable.Image;
        _audioClip = dialogueable.AudioClip;
        _currentDialogue.OnDialogueStart();
        OnActivatingDialogue?.Invoke();

        if(_dialogueName != null) _dialogueName.text = _currentDialogue.Dialogue.speakerName;

        _sentences.Clear();
        foreach(string sentence in _currentDialogue.Dialogue.sentences)
            _sentences.Enqueue(sentence);

        if(_dialogueGroup != null)
            StartCoroutine(FadeDialogue(true));

        DisplayNext();
    }

    // Avanza a la siguiente frase; si está tipeando, la completa de golpe.
    public void AdvanceDialogue()
    {
        if(_currentDialogue == null) return;

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

        if(_typing != null) StopCoroutine(_typing);
        _typing = StartCoroutine(TypeLine(_currentSentence));
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

                if (_countLetters % 2 == 0)
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

        if(_dialogueGroup != null)
            StartCoroutine(FadeDialogue(false));

        _currentDialogue.OnDialogueEnd();
        OnDeactivatingDialogue?.Invoke();
        _currentDialogue = null;
    }

    // Cierre por distancia: lo llama Player.Update mientras haya diálogo activo.
    public void ArtificialUpdate()
    {
        if(_currentDialogue == null) return;

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
