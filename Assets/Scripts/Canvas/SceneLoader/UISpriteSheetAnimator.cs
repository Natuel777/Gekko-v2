using UnityEngine;
using UnityEngine.UI;

// Reproduce una secuencia de sprites (ej. las runas de carga) en un Image de UI.
// El frame se calcula a partir del tiempo acumulado (no avanza de a uno por Update),
// así que un tirón de carga no atrasa la animación: si se salteó medio segundo,
// salta directo al frame que corresponde a ese instante.
[RequireComponent(typeof(Image))]
public class UISpriteSheetAnimator : MonoBehaviour
{
    [Header("Frames")]
    [SerializeField] private Sprite[] _frames;
    [SerializeField] private float _fps = 24f;
    [SerializeField] private bool _loop = true;
    [SerializeField] private bool _playOnEnable = true;

    [Header("Para el botón 'Cargar frames'")]
    [Tooltip("El spritesheet ya cortado en sprites (Sprite Mode = Multiple). Se usa solo para el botón de abajo.")]
    [SerializeField] private Texture2D _sourceSheet;

    private Image _image;
    private float _elapsedTime;
    private bool _isPlaying;

    private void Awake()
    {
        _image = GetComponent<Image>();
        _image.preserveAspect = true;
        _image.raycastTarget = false;
    }

    private void OnEnable()
    {
        _elapsedTime = 0f;
        if (_frames != null && _frames.Length > 0) _image.sprite = _frames[0];
        if (_playOnEnable) Play();
    }

    public void Play()
    {
        _isPlaying = true;
    }

    public void Stop()
    {
        _isPlaying = false;
    }

    public void Restart()
    {
        _elapsedTime = 0f;
        Play();
    }

    private void Update()
    {
        if (!_isPlaying || _frames == null || _frames.Length == 0) return;

        // unscaledDeltaTime: tiene que seguir animando aunque Time.timeScale sea 0
        // (pantalla de carga mientras el juego puede estar en pausa, etc.).
        _elapsedTime += Time.unscaledDeltaTime;

        float frameDuration = 1f / Mathf.Max(_fps, 0.01f);
        int frameIndex = Mathf.FloorToInt(_elapsedTime / frameDuration);

        if (_loop)
        {
            frameIndex %= _frames.Length;
        }
        else if (frameIndex >= _frames.Length)
        {
            frameIndex = _frames.Length - 1;
            _isPlaying = false;
        }

        _image.sprite = _frames[frameIndex];
    }

    // Botón de Inspector: toma el spritesheet asignado en _sourceSheet, junta todos sus
    // sprites hijos (ya cortados por CargaSpriteSheetPostprocessor) y los carga ordenados
    // por nombre (cargando_00, cargando_01, ...) en _frames.
    [ContextMenu("Cargar frames del sprite sheet")]
    private void LoadFramesFromSheet()
    {
#if UNITY_EDITOR
        if (_sourceSheet == null)
        {
            Debug.LogWarning("[UISpriteSheetAnimator] Asigná '_sourceSheet' antes de usar este botón.");
            return;
        }

        string path = UnityEditor.AssetDatabase.GetAssetPath(_sourceSheet);
        var allAssets = UnityEditor.AssetDatabase.LoadAllAssetRepresentationsAtPath(path);

        var sprites = new System.Collections.Generic.List<Sprite>();
        foreach (var asset in allAssets)
            if (asset is Sprite sprite) sprites.Add(sprite);

        sprites.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        _frames = sprites.ToArray();

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[UISpriteSheetAnimator] Cargados {_frames.Length} frames desde '{path}'.");
#endif
    }
}
