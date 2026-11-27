using System.Collections;
using UnityEngine;

// Llama la atención sobre una placa de presión (la primera del desafío de espejos de LvlTwo) hasta que Gekko la pisa:
//  - la runa de la placa late (se prende y se apaga suave),
//  - una columna de luz turquesa sube desde la placa (se ve de lejos),
//  - partículas que suben y una luz que late.
// Al pisarla se apaga todo con un fundido y la runa queda en manos de la placa (PressurePlateRuna la enciende del todo).
// Las otras placas se pueden pisar igual: esto solo resalta, no bloquea.
public class PlateHighlight : MonoBehaviour
{
    [SerializeField] private PressurePlateRuna _plate;

    [Header("Visual (opcionales)")]
    [Tooltip("Columna de luz: LineRenderer vertical. Late con la runa.")]
    [SerializeField] private LineRenderer _beam;
    [SerializeField] private Light _light;
    [SerializeField] private ParticleSystem _particles;

    [Header("Pulso")]
    [Tooltip("Segundos de un latido completo.")]
    [Min(0.1f)]
    [SerializeField] private float _pulseSeconds = 1.6f;
    [Tooltip("Brillo mínimo y máximo de la runa al latir (0..1).")]
    [SerializeField] private Vector2 _runeFade = new Vector2(0.35f, 1f);
    [SerializeField] private Vector2 _lightIntensity = new Vector2(1.5f, 4f);
    [Tooltip("Transparencia mínima y máxima de la columna de luz.")]
    [SerializeField] private Vector2 _beamAlpha = new Vector2(0.55f, 1f);
    [Tooltip("Segundos del fundido al pisar la placa.")]
    [Min(0.05f)]
    [SerializeField] private float _fadeOutSeconds = 0.6f;

    private bool _active;
    private float _time;
    private Gradient _beamGradient;
    private GradientAlphaKey[] _beamAlphaKeys;

    private void Start()
    {
        if(_plate == null)
        {
            Debug.LogWarning($"[{name}] PlateHighlight sin placa asignada.", this);
            enabled = false;
            return;
        }

        if(_beam != null)
        {
            _beamGradient = _beam.colorGradient;
            _beamAlphaKeys = _beamGradient.alphaKeys;
        }

        _plate.Pressed += OnPressed;
        _active = true;
        if(_plate.Runa != null) _plate.Runa.enabled = true;
        if(_particles != null) _particles.Play();
    }

    private void OnDestroy()
    {
        if(_plate != null) _plate.Pressed -= OnPressed;
    }

    private void Update()
    {
        if(!_active) return;

        _time += Time.deltaTime;
        // 0..1..0 suave.
        float pulse = 0.5f - 0.5f * Mathf.Cos(_time * Mathf.PI * 2f / _pulseSeconds);
        Apply(pulse, 1f);
    }

    // pulse: punto del latido (0..1). visibility: 1 = resaltado completo, 0 = apagado (para el fundido final).
    private void Apply(float pulse, float visibility)
    {
        if(_plate.Runa != null) _plate.Runa.fadeFactor = Mathf.Lerp(_runeFade.x, _runeFade.y, pulse) * visibility;
        if(_light != null) _light.intensity = Mathf.Lerp(_lightIntensity.x, _lightIntensity.y, pulse) * visibility;
        if(_beam != null && _beamAlphaKeys != null)
        {
            float alpha = Mathf.Lerp(_beamAlpha.x, _beamAlpha.y, pulse) * visibility;
            var keys = new GradientAlphaKey[_beamAlphaKeys.Length];
            for(int i = 0; i < keys.Length; i++) keys[i] = new GradientAlphaKey(_beamAlphaKeys[i].alpha * alpha, _beamAlphaKeys[i].time);
            _beamGradient.SetKeys(_beamGradient.colorKeys, keys);
            _beam.colorGradient = _beamGradient;
        }
    }

    private void OnPressed(PressurePlateRuna plate)
    {
        if(!_active) return;
        _active = false;
        _plate.Pressed -= OnPressed;
        StartCoroutine(FadeOut());
    }

    // La runa no se toca: la placa la enciende del todo desde el brillo que tenga ahora (PressurePlateRuna.FundirRuna).
    private IEnumerator FadeOut()
    {
        if(_particles != null) _particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        float startLight = _light != null ? _light.intensity : 0f;
        float pulse = 0.5f - 0.5f * Mathf.Cos(_time * Mathf.PI * 2f / _pulseSeconds);

        for(float t = 0f; t < 1f; t += Time.deltaTime / _fadeOutSeconds)
        {
            float k = 1f - Mathf.SmoothStep(0f, 1f, t);
            if(_light != null) _light.intensity = startLight * k;
            if(_beam != null && _beamAlphaKeys != null)
            {
                float alpha = Mathf.Lerp(_beamAlpha.x, _beamAlpha.y, pulse) * k;
                var keys = new GradientAlphaKey[_beamAlphaKeys.Length];
                for(int i = 0; i < keys.Length; i++) keys[i] = new GradientAlphaKey(_beamAlphaKeys[i].alpha * alpha, _beamAlphaKeys[i].time);
                _beamGradient.SetKeys(_beamGradient.colorKeys, keys);
                _beam.colorGradient = _beamGradient;
            }
            yield return null;
        }

        if(_beam != null) _beam.enabled = false;
        if(_light != null) _light.enabled = false;
    }
}
