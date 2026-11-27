using System.Collections;
using UnityEngine;

// Marca en el piso de "pará acá" (MirrorTutorial la pone detrás del espejo de la demostración): un anillo que late y
// partículas que suben. Arranca apagada; Show() la hace aparecer con un pequeño rebote y Hide() la apaga con un fundido.
// El anillo se dibuja con el LineRenderer de este objeto (los puntos se calculan solos con _radius).
[RequireComponent(typeof(LineRenderer))]
public class StandMarker : MonoBehaviour
{
    [Min(0.1f)]
    [SerializeField] private float _radius = 0.9f;
    [Range(12, 96)]
    [SerializeField] private int _segments = 48;
    [SerializeField] private ParticleSystem _particles;

    [Header("Latido")]
    [Min(0.1f)]
    [SerializeField] private float _pulseSeconds = 1.2f;
    [SerializeField] private Vector2 _pulseScale = new Vector2(0.92f, 1.06f);
    [SerializeField] private Vector2 _pulseAlpha = new Vector2(0.45f, 1f);
    [Min(0.05f)]
    [SerializeField] private float _fadeSeconds = 0.4f;

    private LineRenderer _ring;
    private Gradient _gradient;
    private GradientAlphaKey[] _alphaKeys;
    private float _visibility;   // 0..1, sube con Show y baja con Hide
    private bool _shown;
    private float _time;
    private Coroutine _fade;

    public bool Shown => _shown;

    private void Awake()
    {
        _ring = GetComponent<LineRenderer>();
        _ring.loop = true;
        _ring.useWorldSpace = false;
        _ring.positionCount = _segments;
        for(int i = 0; i < _segments; i++)
        {
            float a = i * Mathf.PI * 2f / _segments;
            _ring.SetPosition(i, new Vector3(Mathf.Cos(a) * _radius, 0f, Mathf.Sin(a) * _radius));
        }

        _gradient = _ring.colorGradient;
        _alphaKeys = _gradient.alphaKeys;

        _ring.enabled = false;
        if(_particles != null) _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public void Show()
    {
        if(_shown) return;
        _shown = true;
        _ring.enabled = true;
        if(_particles != null) _particles.Play();
        StartFade(1f);
    }

    public void Hide()
    {
        if(!_shown) return;
        _shown = false;
        if(_particles != null) _particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        StartFade(0f);
    }

    private void StartFade(float target)
    {
        if(_fade != null) StopCoroutine(_fade);
        _fade = StartCoroutine(Fade(target));
    }

    private IEnumerator Fade(float target)
    {
        float from = _visibility;
        for(float t = 0f; t < 1f; t += Time.deltaTime / _fadeSeconds)
        {
            _visibility = Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        _visibility = target;
        if(target <= 0f) _ring.enabled = false;
        _fade = null;
    }

    private void Update()
    {
        if(!_ring.enabled) return;

        _time += Time.deltaTime;
        float pulse = 0.5f - 0.5f * Mathf.Cos(_time * Mathf.PI * 2f / _pulseSeconds);

        // Al aparecer crece desde chico (rebote); después late.
        float pop = Mathf.Lerp(0.4f, 1f, _visibility);
        transform.localScale = Vector3.one * (pop * Mathf.Lerp(_pulseScale.x, _pulseScale.y, pulse));

        float alpha = Mathf.Lerp(_pulseAlpha.x, _pulseAlpha.y, pulse) * _visibility;
        var keys = new GradientAlphaKey[_alphaKeys.Length];
        for(int i = 0; i < keys.Length; i++) keys[i] = new GradientAlphaKey(_alphaKeys[i].alpha * alpha, _alphaKeys[i].time);
        _gradient.SetKeys(_gradient.colorKeys, keys);
        _ring.colorGradient = _gradient;
    }
}
