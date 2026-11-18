using UnityEngine;
using UnityEngine.UI;

// Corazón del HUD. Además de lleno/vacío tiene feedback animado (todo con tiempo sin escala, para que se vea aunque
// el golpe haga hitstop con timeScale 0):
//  - Explota al perderse: crece, se pone blanco, suelta pedacitos y queda vacío.
//  - Pop al volver a llenarse (curación).
//  - Temblor corto (lo dispara HealthBar en todos los corazones al recibir daño).
//  - Latido doble "pum-pum" cuando es el último corazón (1 de vida).
// Escala y rotación no las toca el HorizontalLayoutGroup de "Hearts", así que se pueden animar sin pelear con el layout.
public class HeartIcon : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private Sprite _fullSprite;
    [SerializeField] private Sprite _emptySprite;

    [Header("Feedback")]
    [Tooltip("Silueta blanca del corazón lleno, para el destello al perderse/llenarse.")]
    [SerializeField] private Sprite _flashSprite;
    [SerializeField] private float _explodeScale = 0.5f;
    [SerializeField] private float _explodeDuration = 0.35f;
    [SerializeField] private int _shardCount = 6;
    [SerializeField] private float _shardSize = 10f;
    [SerializeField] private float _popScale = 0.35f;
    [SerializeField] private float _shakeAngle = 10f;
    [SerializeField] private float _shakeDuration = 0.3f;
    [Tooltip("Tiempo entre latidos (doble golpe) con 1 de vida.")]
    [SerializeField] private float _beatPeriod = 0.9f;
    [SerializeField] private float _beatScale = 0.22f;
    [Tooltip("Respiración en reposo: cuánto crece (0.02 = 2%). Muy leve a propósito.")]
    [SerializeField] private float _breathScale = 0.02f;
    [SerializeField] private float _breathPeriod = 3f;
    [Tooltip("Desfase entre corazones (en fracción del período) para que respiren como una onda y no todos juntos.")]
    [SerializeField] private float _breathPhasePerHeart = 0.12f;

    private const float ExplodeSwapTime = 0.08f; // momento en que pasa a vacío (en el pico del golpe)
    private const float PopDuration = 0.3f;
    private const float ShardLife = 0.55f;

    private bool _initialized;
    private bool _isFull = true;
    private Vector3 _baseScale;
    private Quaternion _baseRotation;
    private Image _flash;

    private float _explodeT = -1f;
    private bool _pendingEmpty;
    private float _popT = -1f;
    private float _shakeT = -1f;
    private bool _heartbeat;
    private float _beatTime;

    private RectTransform[] _shards;
    private Image[] _shardImages;
    private Vector2[] _shardVel;
    private float[] _shardSpin;
    private float _shardT = -1f;

    private void Awake() => EnsureInit();

    private void EnsureInit()
    {
        if (_initialized) return;
        _initialized = true;
        _baseScale = transform.localScale;
        _baseRotation = transform.localRotation;

        if (_flashSprite)
        {
            var go = new GameObject("Flash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            _flash = go.GetComponent<Image>();
            _flash.sprite = _flashSprite;
            _flash.preserveAspect = _image && _image.preserveAspect;
            _flash.raycastTarget = false;
            _flash.color = new Color(1f, 1f, 1f, 0f);
        }

        _shards = new RectTransform[_shardCount];
        _shardImages = new Image[_shardCount];
        _shardVel = new Vector2[_shardCount];
        _shardSpin = new float[_shardCount];
        for (int i = 0; i < _shardCount; i++)
        {
            var go = new GameObject("Shard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            _shards[i] = (RectTransform)go.transform;
            _shards[i].sizeDelta = Vector2.one * _shardSize;
            _shardImages[i] = go.GetComponent<Image>();
            _shardImages[i].sprite = _fullSprite;
            _shardImages[i].raycastTarget = false;
            go.SetActive(false);
        }
    }

    // animate = false al inicializar (sin efectos); true durante el juego.
    public void SetFull(bool full, bool animate = false)
    {
        EnsureInit();
        if (!_image) return;

        if (!animate)
        {
            _isFull = full;
            _pendingEmpty = false;
            _explodeT = -1f;
            _popT = -1f;
            _image.sprite = full ? _fullSprite : _emptySprite;
            return;
        }

        if (full == _isFull) return;
        _isFull = full;

        if (!full)
        {
            // Se pierde: se ve lleno hasta el pico del golpe y ahí pasa a vacío (ver Update).
            _pendingEmpty = true;
            _explodeT = 0f;
            _popT = -1f;
            SpawnShards();
        }
        else
        {
            _pendingEmpty = false;
            _explodeT = -1f;
            _image.sprite = _fullSprite;
            _popT = 0f;
        }
    }

    public void Shake()
    {
        EnsureInit();
        _shakeT = 0f;
    }

    public void SetHeartbeat(bool on)
    {
        if (_heartbeat == on) return;
        _heartbeat = on;
        _beatTime = 0f;
    }

    private void OnDisable()
    {
        // Que no quede a mitad de una animación (ej. al desactivarse el HUD).
        if (!_initialized) return;
        transform.localScale = _baseScale;
        transform.localRotation = _baseRotation;
        if (_pendingEmpty && _image) { _image.sprite = _emptySprite; _pendingEmpty = false; }
        _explodeT = _popT = _shakeT = _shardT = -1f;
        if (_flash) _flash.color = new Color(1f, 1f, 1f, 0f);
        if (_shards != null) foreach (var s in _shards) if (s) s.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!_initialized) return;
        float dt = Time.unscaledDeltaTime;
        float scaleAdd = 0f;
        float flashAlpha = 0f;
        float angle = 0f;

        if (_explodeT >= 0f)
        {
            _explodeT += dt;
            if (_explodeT < ExplodeSwapTime)
            {
                float u = _explodeT / ExplodeSwapTime;
                scaleAdd += _explodeScale * u;
                flashAlpha = Mathf.Max(flashAlpha, u);
            }
            else
            {
                if (_pendingEmpty) { _image.sprite = _emptySprite; _pendingEmpty = false; }
                float u = Mathf.Clamp01((_explodeT - ExplodeSwapTime) / (_explodeDuration - ExplodeSwapTime));
                float ease = 1f - (1f - u) * (1f - u);
                scaleAdd += _explodeScale * (1f - ease);
                flashAlpha = Mathf.Max(flashAlpha, 1f - ease);
                if (u >= 1f) _explodeT = -1f;
            }
        }

        if (_popT >= 0f)
        {
            _popT += dt;
            float u = Mathf.Clamp01(_popT / PopDuration);
            scaleAdd += _popScale * Mathf.Sin(u * Mathf.PI);
            flashAlpha = Mathf.Max(flashAlpha, 0.8f * (1f - u));
            if (u >= 1f) _popT = -1f;
        }

        if (_shakeT >= 0f)
        {
            _shakeT += dt;
            float u = Mathf.Clamp01(_shakeT / _shakeDuration);
            angle += Mathf.Sin(_shakeT * 60f) * _shakeAngle * (1f - u);
            if (u >= 1f) _shakeT = -1f;
        }

        if (_heartbeat && _isFull)
        {
            // Doble golpe "pum-pum": un pico fuerte, otro más suave un poco después, y pausa.
            _beatTime += dt;
            float p = Mathf.Repeat(_beatTime, _beatPeriod) / _beatPeriod;
            scaleAdd += _beatScale * (Bump(p, 0f, 0.12f) + 0.6f * Bump(p, 0.2f, 0.12f));
        }
        else if (_breathScale > 0f && _breathPeriod > 0f)
        {
            // Respiración muy leve en reposo, desfasada según la posición del corazón en la fila.
            float phase = transform.GetSiblingIndex() * _breathPhasePerHeart;
            float p = Time.unscaledTime / _breathPeriod + phase;
            scaleAdd += _breathScale * 0.5f * (1f - Mathf.Cos(p * Mathf.PI * 2f));
        }

        transform.localScale = _baseScale * (1f + scaleAdd);
        transform.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, angle);
        if (_flash) _flash.color = new Color(1f, 1f, 1f, flashAlpha);

        UpdateShards(dt);
    }

    // Pulso suave (0..1..0) que empieza en 'start' y dura 'width' (en fracción del período).
    private static float Bump(float p, float start, float width)
    {
        float u = (p - start) / width;
        if (u < 0f || u > 1f) return 0f;
        return Mathf.Sin(u * Mathf.PI);
    }

    private void SpawnShards()
    {
        if (_shards == null || _shards.Length == 0) return;
        _shardT = 0f;
        for (int i = 0; i < _shards.Length; i++)
        {
            // Abanico hacia arriba/los costados, después caen por la "gravedad".
            float a = Mathf.Lerp(20f, 160f, (i + Random.value * 0.8f) / _shards.Length) * Mathf.Deg2Rad;
            float speed = Random.Range(120f, 220f);
            _shardVel[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed;
            _shardSpin[i] = Random.Range(-540f, 540f);
            _shards[i].anchoredPosition = Vector2.zero;
            _shards[i].localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            _shards[i].localScale = Vector3.one;
            _shardImages[i].color = Color.white;
            _shards[i].gameObject.SetActive(true);
        }
    }

    private void UpdateShards(float dt)
    {
        if (_shardT < 0f) return;
        _shardT += dt;
        float u = Mathf.Clamp01(_shardT / ShardLife);
        for (int i = 0; i < _shards.Length; i++)
        {
            _shardVel[i] += Vector2.down * 600f * dt;
            _shards[i].anchoredPosition += _shardVel[i] * dt;
            _shards[i].localRotation *= Quaternion.Euler(0f, 0f, _shardSpin[i] * dt);
            _shards[i].localScale = Vector3.one * (1f - 0.5f * u);
            _shardImages[i].color = new Color(1f, 1f, 1f, 1f - u * u);
        }
        if (u >= 1f)
        {
            _shardT = -1f;
            foreach (var s in _shards) s.gameObject.SetActive(false);
        }
    }
}
