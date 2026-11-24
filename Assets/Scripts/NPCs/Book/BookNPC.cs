using System.Collections;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Libro de bitácora (prefab "Libro"). Mientras nadie lo lee da saltitos para llamar la atención. Al apretar E:
//   - Pausa el juego (timeScale 0) para que las torretas no le hagan nada a Gekko mientras lee. Si se abre y cierra el
//     menú de pausa en el medio, la pausa se vuelve a poner sola hasta cerrar el libro.
//   - Pasa a una cámara propia que hace zoom sobre las páginas. Se arma por código mirando de frente a _pages, así
//     funciona donde sea que se ponga el prefab.
//   - El texto del diálogo se escribe en las páginas, dos frases por vista (izquierda / derecha), en vez de usar el panel
//     de abajo. E / click: completa lo que se está escribiendo o pasa de hoja; después de la última vista cierra el libro.
// UIManager sigue manejando el ciclo del diálogo (bloqueo de Gekko, avanzar, cerrar) a través de IDialogueView.
public class BookNPC : NPCBase, IDialogueView
{
    private const int ReadCameraPriority = 100;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("Saltitos (llamar la atención)")]
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

    [Header("Páginas")]
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

    [Header("Cámara de lectura")]
    [Tooltip("Mitad del tamaño a encuadrar (ancho, alto), en unidades locales de _pages.")]
    [SerializeField] private Vector2 _frameHalfSize = new Vector2(1.95f, 1.65f);
    [Tooltip("Corre el centro del encuadre (en unidades locales de _pages). Y positivo baja el libro en pantalla y deja lugar arriba para el HUD (corazones).")]
    [SerializeField] private Vector2 _frameOffset = new Vector2(0f, 0.25f);
    [Range(15f, 80f)]
    [SerializeField] private float _readFov = 35f;

    [Header("Runa de la contratapa")]
    [SerializeField] private DecalProjector _rune;
    [Tooltip("Velocidad del latido del brillo de la runa.")]
    [SerializeField] private float _runePulseSpeed = 2f;
    [Tooltip("Brillo mínimo del latido (1 = no late).")]
    [Range(0f, 1f)]
    [SerializeField] private float _runePulseMin = 0.45f;

    private bool _reading;
    private bool _wasRead;
    private int _spread;
    private bool _busy;         // fundido o escritura en curso
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

    protected override void Start()
    {
        // La instancia de LvlTwo traía estas referencias apuntando a objetos que ya no existen (y NPCBase.Start tiraba
        // NullReference): si faltan, se usan las del propio libro.
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
        // Que dos libros en la misma escena no salten sincronizados.
        _hopClock = Random.Range(0f, _hopInterval);

        if(_nextIndicator != null) _indicatorScale = _nextIndicator.localScale;
        if(_pages != null) _pages.gameObject.SetActive(false);

        // Material propio para hacer latir el brillo sin tocar el de las otras runas.
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

    // ------------------------------------------------------------------ Interacción / diálogo

    public override void ShowInteractUI()
    {
        if(_reading) return;
        base.ShowInteractUI();
    }

    // No llama a base: el libro no gira hacia Gekko ni usa la cámara de NPC (_cam).
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

        if((_spread + 1) * 2 >= SentenceCount) return false; // era la última vista: se cierra el libro

        _spread++;
        StartSpread(turnPage: true);
        return true;
    }

    private int SentenceCount => Dialogue.sentences != null ? Dialogue.sentences.Length : 0;

    private void RestoreGame()
    {
        _reading = false;
        if(_spreadRoutine != null) StopCoroutine(_spreadRoutine);
        _spreadRoutine = null;
        _busy = _typing = _skipTyping = false;

        if(_pages != null) _pages.gameObject.SetActive(false);
        if(_canvas != null) _canvas.enabled = true;

        // Si el menú de pausa está abierto, él se encarga de devolver el timeScale al cerrarse.
        if(!PauseMenuOpen) Time.timeScale = _savedTimeScale;

        if(_readCam != null) _readCam.gameObject.SetActive(false); // el brain vuelve a la cámara de juego con su blend
        if(_brain != null && _brainSaved) _brain.IgnoreTimeScale = _savedIgnoreTimeScale;
        _brainSaved = false;
        _hopClock = 0f;
    }

    private static bool PauseMenuOpen => GameManager.Instance != null && GameManager.Instance.IsPaused;

    // El menú de pausa pone timeScale en 1 al cerrarse: mientras se lee, se vuelve a pausar.
    private void KeepPaused()
    {
        if(!PauseMenuOpen && Time.timeScale != 0f) Time.timeScale = 0f;
    }

    // ------------------------------------------------------------------ Cámara

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

        // Distancia para que entre todo el libro (con la relación de aspecto real de la pantalla).
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

        // Con timeScale en 0 el blend de Cinemachine no avanzaría: el zoom corre en tiempo real mientras se lee.
        var main = Camera.main;
        _brain = main != null ? main.GetComponent<CinemachineBrain>() : null;
        if(_brain != null && !_brainSaved)
        {
            _savedIgnoreTimeScale = _brain.IgnoreTimeScale;
            _brainSaved = true;
            _brain.IgnoreTimeScale = true;
        }
    }

    // ------------------------------------------------------------------ Páginas

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
        string[] sentences = Dialogue.sentences ?? new string[0];
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

    private void UpdateNextIndicator()
    {
        if(_nextIndicator == null) return;

        bool show = !_busy;
        if(_nextIndicator.gameObject.activeSelf != show) _nextIndicator.gameObject.SetActive(show);
        if(show) _nextIndicator.localScale = _indicatorScale * (1f + 0.12f * Mathf.Sin(Time.unscaledTime * 6f));
    }

    // ------------------------------------------------------------------ Saltitos

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
            // Ya leído: flota suave, sin saltar.
            float bob = (Mathf.Sin(_hopClock * 2.4f) * 0.5f + 0.5f) * _hopHeight * 0.12f;
            _visual.localPosition = _restPos + Vector3.up * bob;
            _visual.localRotation = _restRot;
            _visual.localScale = _restScale;
            return;
        }

        const float anticipation = 0.12f; // se agacha antes de saltar
        const float landing = 0.3f;       // rebote al caer
        float cycle = Mathf.Max(_hopInterval, anticipation + _hopDuration + landing);
        float p = _hopClock % cycle;

        float height = 0f;
        float stretch = 0f; // >0 estira hacia arriba, <0 aplasta
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

        // Estira en Y y compensa en X/Z para que parezca que conserva el volumen.
        float sy = 1f + stretch;
        float sxz = 1f / Mathf.Sqrt(Mathf.Max(sy, 0.1f));

        _visual.localPosition = _restPos + Vector3.up * height;
        _visual.localRotation = _restRot * Quaternion.Euler(0f, 0f, tilt);
        _visual.localScale = Vector3.Scale(_restScale, new Vector3(sxz, sy, sxz));
    }

    // ------------------------------------------------------------------ Runa

    private void UpdateRune()
    {
        if(_runeMaterial == null) return;
        float pulse = Mathf.Lerp(_runePulseMin, 1f, Mathf.Sin(Time.unscaledTime * _runePulseSpeed) * 0.5f + 0.5f);
        _runeMaterial.SetColor(EmissionColorId, _runeEmission * pulse);
    }
}
