using System.Collections;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Logbook book ("Libro" prefab). While nobody is reading it, it hops around to get attention. When pressing E:
//   - Pauses the game (timeScale 0) so the turrets can't hurt Gekko while he reads. If the pause menu is opened and closed
//     in the middle, the pause is put back on by itself until the book is closed.
//   - Switches to its own camera that zooms in on the pages. It is built by code, facing _pages head-on, so it
//     works wherever the prefab is placed.
//   - The dialogue text is written on the pages, two sentences per view (left / right), instead of using the bottom
//     panel. E / click: finishes what is being written or turns the page; after the last view the book closes.
// UIManager keeps handling the dialogue cycle (locking Gekko, advancing, closing) through IDialogueView.
public class BookNPC : NPCBase, IDialogueView
{
    private const int ReadCameraPriority = 100;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("Hops (get attention)")]
    [Tooltip("Pivote que se anima: tiene adentro el modelo, las páginas y la runa. Su origen está en la base del libro, así el aplastado apoya en el piso.")]
    [SerializeField] private Transform _visual;
    [Tooltip("Segundos entre un salto y el siguiente.")]
    [Min(0.3f)]
    [SerializeField] private float _hopInterval = 1.6f;
    [Tooltip("Segundos en el aire.")]
    [Min(0.1f)]
    [SerializeField] private float _hopDuration = 0.45f;
    [Tooltip("Altura del salto, en unidades locales del Libro.")]
    [SerializeField] private float _hopHeight = 1.4f;
    [Tooltip("Cuánto se aplasta al agacharse y al caer (0 = nada).")]
    [Range(0f, 0.5f)]
    [SerializeField] private float _squash = 0.22f;
    [Tooltip("Grados que se tambalea en el aire.")]
    [SerializeField] private float _hopTilt = 8f;
    [Tooltip("Después de leerlo una vez deja de saltar y solo flota suave.")]
    [SerializeField] private bool _calmAfterRead = true;

    [Header("Pages")]
    [Tooltip("Centro de las páginas abiertas: forward = hacia adentro de la hoja, up = hacia arriba del texto. La cámara de lectura lo mira de frente.")]
    [SerializeField] private Transform _pages;
    [SerializeField] private TMP_Text _leftPage;
    [SerializeField] private TMP_Text _rightPage;
    [SerializeField] private TMP_Text _leftNumber;
    [SerializeField] private TMP_Text _rightNumber;
    [Tooltip("La E de 'seguir' en la esquina de la hoja derecha. Late cuando terminó de escribirse la vista.")]
    [SerializeField] private Transform _nextIndicator;
    [Tooltip("Segundos por letra al escribirse en la hoja (tiempo real, anda con el juego pausado).")]
    [SerializeField] private float _typeSpeed = 0.022f;
    [Tooltip("Fundido de la tinta al pasar de hoja.")]
    [SerializeField] private float _pageFade = 0.15f;
    [Tooltip("Opcional: sonido al pasar de hoja.")]
    [SerializeField] private AudioClip _pageTurnClip;

    [Header("Talk sounds")]
    [Tooltip("Sonidos con los que 'habla' el libro: suena uno por cada frase que se escribe en las hojas, elegido al azar. Un sonido no puede sonar dos veces seguidas: tiene que esperar un turno.")]
    [SerializeField] private SoundNames[] _talkSounds =
    {
        SoundNames.BookSound1,
        SoundNames.BookSound2,
        SoundNames.BookSound3,
        SoundNames.BookSound4
    };

    [Header("Reading camera")]
    [Tooltip("Mitad del tamaño a encuadrar (ancho, alto), en unidades locales de _pages.")]
    [SerializeField] private Vector2 _frameHalfSize = new Vector2(1.95f, 1.65f);
    [Tooltip("Corre el centro del encuadre (en unidades locales de _pages). Y positivo baja el libro en pantalla y deja lugar arriba para el HUD (corazones).")]
    [SerializeField] private Vector2 _frameOffset = new Vector2(0f, 0.25f);
    [Range(15f, 80f)]
    [SerializeField] private float _readFov = 35f;

    [Header("Back cover rune")]
    [SerializeField] private DecalProjector _rune;
    [Tooltip("Velocidad del latido del brillo de la runa.")]
    [SerializeField] private float _runePulseSpeed = 2f;
    [Tooltip("Brillo mínimo del latido (1 = no late).")]
    [Range(0f, 1f)]
    [SerializeField] private float _runePulseMin = 0.45f;

    private bool _reading;
    private bool _wasRead;
    private int _spread;
    private bool _busy;         // fade or typing in progress
    private bool _typing;
    private bool _skipTyping;
    private Coroutine _spreadRoutine;

    private float _savedTimeScale = 1f;
    private CinemachineCamera _readCam;
    private CinemachineBrain _brain;
    private bool _savedIgnoreTimeScale;
    private bool _brainSaved;

    private Vector3 _restPos;
    private Quaternion _restRot;
    private Vector3 _restScale;
    private float _hopClock;
    private Vector3 _indicatorScale;

    private Material _runeMaterial;
    private Color _runeEmission;

    private AudioSource _audio;
    private NoRepeatSoundPicker _talkPicker;

    protected override void Start()
    {
        // The LvlTwo instance came with these references pointing to objects that no longer exist (and NPCBase.Start threw a
        // NullReference): if they are missing, the book's own ones are used.
        if(_canvas == null) _canvas = GetComponentInChildren<Canvas>(true);
        if(_EIndicator == null && _canvas != null)
        {
            Transform button = _canvas.transform.Find("InteractionButton");
            if(button != null) _EIndicator = button.GetComponent<UnityEngine.UI.Image>();
        }

        base.Start();

        if(_visual != null)
        {
            _restPos = _visual.localPosition;
            _restRot = _visual.localRotation;
            _restScale = _visual.localScale;
        }
        // So two books in the same scene don't hop in sync.
        _hopClock = Random.Range(0f, _hopInterval);

        if(_nextIndicator != null) _indicatorScale = _nextIndicator.localScale;
        if(_pages != null) _pages.gameObject.SetActive(false);

        // Own material to pulse the glow without touching the other runes' one.
        if(_rune != null && _rune.material != null)
        {
            _runeMaterial = new Material(_rune.material);
            _rune.material = _runeMaterial;
            _runeEmission = _runeMaterial.GetColor(EmissionColorId);
        }
    }

    protected override void Update()
    {
        base.Update();
        UpdateRune();

        if(_reading)
        {
            KeepPaused();
            UpdateNextIndicator();
            return;
        }

        UpdateHop();
    }

    private void OnDestroy()
    {
        if(_reading) RestoreGame();
        if(_readCam != null) Destroy(_readCam.gameObject);
        if(_runeMaterial != null) Destroy(_runeMaterial);
    }

    // ------------------------------------------------------------------ Interaction / dialogue

    public override void ShowInteractUI()
    {
        if(_reading) return;
        base.ShowInteractUI();
    }

    // Doesn't call base: the book doesn't turn towards Gekko nor use the NPC camera (_cam).
    public override void OnDialogueStart()
    {
        _reading = true;
        HideInteractUI();
        if(_canvas != null) _canvas.enabled = false;
        ResetVisual();

        _savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;

        ActivateReadCamera();

        if(_pages != null) _pages.gameObject.SetActive(true);
        _spread = 0;
        StartSpread(turnPage: false);
    }

    public override void OnDialogueEnd()
    {
        _wasRead = true;
        RestoreGame();
    }

    public bool AdvanceView()
    {
        if(_typing) { _skipTyping = true; return true; }
        if(_busy) return true;

        if((_spread + 1) * 2 >= SentenceCount) return false; // it was the last view: the book closes

        _spread++;
        StartSpread(turnPage: true);
        return true;
    }

    private int SentenceCount => Dialogue.LocalizedSentences.Length;

    private void RestoreGame()
    {
        _reading = false;
        if(_spreadRoutine != null) StopCoroutine(_spreadRoutine);
        _spreadRoutine = null;
        _busy = _typing = _skipTyping = false;

        if(_pages != null) _pages.gameObject.SetActive(false);
        if(_canvas != null) _canvas.enabled = true;

        // If the pause menu is open, it takes care of giving the timeScale back when it closes.
        if(!PauseMenuOpen) Time.timeScale = _savedTimeScale;

        if(_readCam != null) _readCam.gameObject.SetActive(false); // the brain goes back to the gameplay camera with its blend
        if(_brain != null && _brainSaved) _brain.IgnoreTimeScale = _savedIgnoreTimeScale;
        _brainSaved = false;
        _hopClock = 0f;
    }

    private static bool PauseMenuOpen => GameManager.Instance != null && GameManager.Instance.IsPaused;

    // The pause menu sets timeScale to 1 when it closes: while reading, it is paused again.
    private void KeepPaused()
    {
        if(!PauseMenuOpen && Time.timeScale != 0f) Time.timeScale = 0f;
    }

    // ------------------------------------------------------------------ Camera

    private void ActivateReadCamera()
    {
        if(_pages == null) return;

        if(_readCam == null)
        {
            var go = new GameObject($"{name}_ReadCam");
            _readCam = go.AddComponent<CinemachineCamera>();
        }

        var lens = _readCam.Lens;
        lens.FieldOfView = _readFov;
        lens.NearClipPlane = 0.05f;
        _readCam.Lens = lens;

        // Distance so the whole book fits (using the real aspect ratio of the screen).
        Vector3 scale = _pages.lossyScale;
        float halfW = _frameHalfSize.x * Mathf.Abs(scale.x);
        float halfH = _frameHalfSize.y * Mathf.Abs(scale.y);
        float aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
        float tanV = Mathf.Tan(_readFov * 0.5f * Mathf.Deg2Rad);
        float distance = Mathf.Max(halfH / tanV, halfW / (tanV * aspect));

        Vector3 center = _pages.TransformPoint(new Vector3(_frameOffset.x, _frameOffset.y, 0f));
        _readCam.transform.SetPositionAndRotation(center - _pages.forward * distance, _pages.rotation);
        _readCam.Priority = ReadCameraPriority;
        _readCam.gameObject.SetActive(true);

        // With timeScale at 0 the Cinemachine blend would not advance: the zoom runs in real time while reading.
        var main = Camera.main;
        _brain = main != null ? main.GetComponent<CinemachineBrain>() : null;
        if(_brain != null && !_brainSaved)
        {
            _savedIgnoreTimeScale = _brain.IgnoreTimeScale;
            _brainSaved = true;
            _brain.IgnoreTimeScale = true;
        }
    }

    // ------------------------------------------------------------------ Pages

    private void StartSpread(bool turnPage)
    {
        if(_spreadRoutine != null) StopCoroutine(_spreadRoutine);
        _spreadRoutine = StartCoroutine(SpreadRoutine(turnPage));
    }

    private IEnumerator SpreadRoutine(bool turnPage)
    {
        _busy = true;
        _skipTyping = false;

        if(turnPage)
        {
            yield return FadeInk(1f, 0f);
            PlayPageTurn();
        }

        FillSpread();
        SetInkAlpha(1f);

        _typing = true;
        yield return TypeText(_leftPage);
        yield return TypeText(_rightPage);
        _typing = false;
        _skipTyping = false;
        _busy = false;
        _spreadRoutine = null;
    }

    private void FillSpread()
    {
        string[] sentences = Dialogue.LocalizedSentences;
        int left = _spread * 2;
        int right = left + 1;

        SetPage(_leftPage, left < sentences.Length ? sentences[left] : string.Empty);
        SetPage(_rightPage, right < sentences.Length ? sentences[right] : string.Empty);
        if(_leftNumber != null) _leftNumber.text = left < sentences.Length ? (left + 1).ToString() : string.Empty;
        if(_rightNumber != null) _rightNumber.text = right < sentences.Length ? (right + 1).ToString() : string.Empty;
    }

    private static void SetPage(TMP_Text page, string text)
    {
        if(page == null) return;
        page.text = text.Trim();
        page.maxVisibleCharacters = 0;
    }

    private IEnumerator TypeText(TMP_Text page)
    {
        if(page == null) yield break;

        page.ForceMeshUpdate();
        int total = page.textInfo.characterCount;
        // One talk sound per sentence that really gets written: empty pages and text skipped with E stay silent.
        if(total > 0 && !_skipTyping) PlayTalkSound();
        float shown = 0f;
        while(shown < total && !_skipTyping)
        {
            shown += Time.unscaledDeltaTime / Mathf.Max(_typeSpeed, 0.001f);
            page.maxVisibleCharacters = Mathf.Min(total, (int)shown);
            yield return null;
        }
        page.maxVisibleCharacters = int.MaxValue;
    }

    private IEnumerator FadeInk(float from, float to)
    {
        float t = 0f;
        while(t < _pageFade)
        {
            t += Time.unscaledDeltaTime;
            SetInkAlpha(Mathf.Lerp(from, to, t / _pageFade));
            yield return null;
        }
        SetInkAlpha(to);
    }

    private void SetInkAlpha(float alpha)
    {
        if(_leftPage != null) _leftPage.alpha = alpha;
        if(_rightPage != null) _rightPage.alpha = alpha;
        if(_leftNumber != null) _leftNumber.alpha = alpha;
        if(_rightNumber != null) _rightNumber.alpha = alpha;
    }

    private void PlayPageTurn()
    {
        if(_pageTurnClip == null) return;
        if(_audio == null)
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
        }
        _audio.pitch = Random.Range(0.95f, 1.05f);
        _audio.PlayOneShot(_pageTurnClip);
    }

    // Plays one random talk sound. The picker lives as long as the book, so the "no repeat" rule also holds
    // between readings (the first sound of a new reading can't be the last one of the previous reading).
    private void PlayTalkSound()
    {
        if(AudioManager.instance == null) return;

        _talkPicker ??= new NoRepeatSoundPicker(_talkSounds);
        if(_talkPicker.TryPick(out SoundNames sound)) AudioManager.instance.Play(sound);
    }

    private void UpdateNextIndicator()
    {
        if(_nextIndicator == null) return;

        bool show = !_busy;
        if(_nextIndicator.gameObject.activeSelf != show) _nextIndicator.gameObject.SetActive(show);
        if(show) _nextIndicator.localScale = _indicatorScale * (1f + 0.12f * Mathf.Sin(Time.unscaledTime * 6f));
    }

    // ------------------------------------------------------------------ Hops

    private void ResetVisual()
    {
        if(_visual == null) return;
        _visual.localPosition = _restPos;
        _visual.localRotation = _restRot;
        _visual.localScale = _restScale;
    }

    private void UpdateHop()
    {
        if(_visual == null) return;

        _hopClock += Time.deltaTime;

        if(_calmAfterRead && _wasRead)
        {
            // Already read: floats softly, no hopping.
            float bob = (Mathf.Sin(_hopClock * 2.4f) * 0.5f + 0.5f) * _hopHeight * 0.12f;
            _visual.localPosition = _restPos + Vector3.up * bob;
            _visual.localRotation = _restRot;
            _visual.localScale = _restScale;
            return;
        }

        const float anticipation = 0.12f; // crouches before jumping
        const float landing = 0.3f;       // bounce on landing
        float cycle = Mathf.Max(_hopInterval, anticipation + _hopDuration + landing);
        float p = _hopClock % cycle;

        float height = 0f;
        float stretch = 0f; // >0 stretches upwards, <0 squashes
        float tilt = 0f;

        if(p < anticipation)
        {
            float k = p / anticipation;
            stretch = -_squash * Mathf.Sin(k * Mathf.PI * 0.5f);
        }
        else if(p < anticipation + _hopDuration)
        {
            float x = (p - anticipation) / _hopDuration;
            height = 4f * x * (1f - x) * _hopHeight;
            stretch = _squash * 0.6f * Mathf.Abs(Mathf.Cos(x * Mathf.PI));
            tilt = _hopTilt * Mathf.Sin(x * Mathf.PI * 2f);
        }
        else if(p < anticipation + _hopDuration + landing)
        {
            float k = (p - anticipation - _hopDuration) / landing;
            stretch = -_squash * (1f - k) * (1f - k) * Mathf.Cos(k * Mathf.PI * 2.5f);
        }

        // Stretches in Y and compensates in X/Z so it looks like it keeps its volume.
        float sy = 1f + stretch;
        float sxz = 1f / Mathf.Sqrt(Mathf.Max(sy, 0.1f));

        _visual.localPosition = _restPos + Vector3.up * height;
        _visual.localRotation = _restRot * Quaternion.Euler(0f, 0f, tilt);
        _visual.localScale = Vector3.Scale(_restScale, new Vector3(sxz, sy, sxz));
    }

    // ------------------------------------------------------------------ Rune

    private void UpdateRune()
    {
        if(_runeMaterial == null) return;
        float pulse = Mathf.Lerp(_runePulseMin, 1f, Mathf.Sin(Time.unscaledTime * _runePulseSpeed) * 0.5f + 0.5f);
        _runeMaterial.SetColor(EmissionColorId, _runeEmission * pulse);
    }
}
