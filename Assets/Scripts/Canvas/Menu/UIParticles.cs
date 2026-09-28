using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Partículas de fondo para un Canvas en Screen Space - Overlay (donde un ParticleSystem no se dibuja).
/// Crea un pool de Images con un sprite suave que aparecen con fade, suben flotando con un vaivén y se apagan.
/// Usa tiempo sin escalar, así que sigue animando con Time.timeScale = 0 (pausa).
/// Va en un RectTransform que ocupa todo el canvas, detrás de los botones y textos.
/// </summary>
public class UIParticles : MonoBehaviour
{
    [SerializeField] private Sprite _sprite;
    [SerializeField] private int _count = 60;

    [Header("Rangos por partícula")]
    [SerializeField] private Vector2 _sizeRange = new Vector2(12f, 70f);
    [SerializeField] private Vector2 _lifeRange = new Vector2(7f, 14f);
    [Tooltip("Píxeles por segundo hacia arriba.")]
    [SerializeField] private Vector2 _riseSpeedRange = new Vector2(15f, 45f);
    [SerializeField] private float _swayAmplitude = 30f;
    [Tooltip("Las partículas más grandes se ven más tenues (efecto bokeh).")]
    [SerializeField, Range(0f, 1f)] private float _maxAlpha = 0.9f;

    [Header("Colores")]
    [SerializeField] private Color[] _palette =
    {
        new Color(0.20f, 0.50f, 1.00f),
        new Color(0.40f, 0.75f, 1.00f),
        new Color(0.15f, 0.40f, 0.95f),
        new Color(0.55f, 0.85f, 1.00f),
    };

    private struct Particle
    {
        public RectTransform Rect;
        public Image Image;
        public Vector2 Origin;
        public float Rise, Life, Age, SwayFreq, SwayPhase, Alpha;
        public Color Color;
    }

    private Particle[] _particles;
    private RectTransform _area;

    private void Awake()
    {
        _area = (RectTransform)transform;
        _particles = new Particle[_count];

        for (int i = 0; i < _count; i++)
        {
            var go = new GameObject("Particle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);

            var img = go.GetComponent<Image>();
            img.sprite = _sprite;
            img.raycastTarget = false;

            _particles[i].Rect = (RectTransform)go.transform;
            _particles[i].Image = img;
            Spawn(ref _particles[i], true);
        }
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float now = Time.unscaledTime;

        for (int i = 0; i < _particles.Length; i++)
        {
            ref Particle p = ref _particles[i];

            p.Age += dt;
            if (p.Age >= p.Life) Spawn(ref p, false);

            float t = p.Age / p.Life;
            float fade = Mathf.Sin(t * Mathf.PI);                                   // entra y sale suave
            float twinkle = 0.8f + 0.2f * Mathf.Sin(now * 2f + p.SwayPhase);        // titileo leve

            p.Rect.anchoredPosition = p.Origin + new Vector2(
                Mathf.Sin(p.Age * p.SwayFreq + p.SwayPhase) * _swayAmplitude,
                p.Rise * p.Age);

            Color c = p.Color;
            c.a = p.Alpha * fade * twinkle;
            p.Image.color = c;
        }
    }

    private void Spawn(ref Particle p, bool prewarm)
    {
        Rect area = _area.rect;
        float size = Random.Range(_sizeRange.x, _sizeRange.y);
        float sizeT = Mathf.InverseLerp(_sizeRange.x, _sizeRange.y, size);

        p.Life = Random.Range(_lifeRange.x, _lifeRange.y);
        p.Age = prewarm ? Random.value * p.Life : 0f;   // al inicio la pantalla ya viene poblada
        p.Rise = Random.Range(_riseSpeedRange.x, _riseSpeedRange.y);
        p.SwayFreq = Random.Range(0.4f, 1.1f);
        p.SwayPhase = Random.value * Mathf.PI * 2f;
        p.Alpha = _maxAlpha * Mathf.Lerp(1f, 0.45f, sizeT);
        p.Color = _palette[Random.Range(0, _palette.Length)];

        // anchoredPosition es relativo al centro del rect (los hijos tienen los anchors en el centro)
        p.Origin = new Vector2(Random.Range(-area.width * 0.5f, area.width * 0.5f),
                               Random.Range(-area.height * 0.5f, area.height * 0.5f));
        p.Rect.sizeDelta = new Vector2(size, size);
    }
}
