using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Feedback de golpe: hitstop, destello en viñeta, empujón, temblor de cámara e
// invulnerabilidad con parpadeo. Vive en el Player (necesita su Rigidbody y un
// CinemachineImpulseSource propio). Todo lo que anima usa tiempo sin escala.
public class DamageFeedback : MonoBehaviour
{
    public static DamageFeedback Instance { get; private set; }

    [Header("Hitstop")]
    [SerializeField] private float _hitstopDurationMs = 80f;

    [Header("Viñeta de daño")]
    [SerializeField] private Image _vignetteImage;
    [SerializeField] private Color _damageColor = new Color(0.667f, 0.196f, 0.941f, 0.85f); // #AA32F0
    [SerializeField] private Color _alternateColor = new Color(0.909f, 0.204f, 0.247f, 0.85f); // #E8343F
    [SerializeField] private bool _useAlternateColor = false;
    [SerializeField] private float _flashDuration = 0.4f;
    [SerializeField, Range(0f, 1f)] private float _vignetteRadius = 0.55f;
    [SerializeField, Range(0.01f, 1f)] private float _vignetteSoftness = 0.35f;

    [Header("Viñeta de vida crítica (1 de vida)")]
    [SerializeField] private Color _lowHealthColor = new Color(0.667f, 0.196f, 0.941f, 0.85f);
    [SerializeField] private float _lowHealthMinIntensity = 0.35f;
    [SerializeField] private float _lowHealthMaxIntensity = 0.85f;
    [SerializeField] private float _lowHealthCycleDuration = 1.2f;

    [Header("Empujón")]
    [SerializeField] private float _knockbackDistance = 0.7f;
    [SerializeField] private float _knockbackDuration = 0.25f;

    [Header("Temblor de cámara (Cinemachine Impulse)")]
    [SerializeField] private CinemachineImpulseSource _impulseSource;
    [SerializeField] private float _shakeAmplitude = 0.15f;
    [SerializeField] private float _shakeDuration = 0.36f;

    [Header("Invulnerabilidad")]
    [SerializeField] private float _invulnerabilityDuration = 1f;
    [SerializeField] private float _blinkInterval = 0.12f;
    [SerializeField] private Renderer[] _renderersToBlink;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int RadiusId = Shader.PropertyToID("_Radius");
    private static readonly int SoftnessId = Shader.PropertyToID("_Softness");

    private enum VignetteState { Idle, Flash, LowHealthLoop }

    private Material _vignetteMaterial;
    private Player _player;
    private VignetteState _vignetteState = VignetteState.Idle;
    private float _flashTimer;
    private float _loopTimer;
    private bool _hitstopActive;

    public bool IsInvulnerable { get; private set; }

    private void Awake()
    {
        Instance = this;
        _player = GetComponent<Player>();

        if(_impulseSource == null) _impulseSource = GetComponent<CinemachineImpulseSource>();

        if(_vignetteImage != null)
        {
            _vignetteMaterial = _vignetteImage.material;
            _vignetteMaterial.SetFloat(RadiusId, _vignetteRadius);
            _vignetteMaterial.SetFloat(SoftnessId, _vignetteSoftness);
            _vignetteMaterial.SetFloat(IntensityId, 0f);
        }
    }

    private void OnDestroy()
    {
        if(Instance == this) Instance = null;
    }

    private void Update()
    {
        UpdateVignette();

#if UNITY_EDITOR
        if(Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
            DebugTriggerHit();
#endif
    }

    [ContextMenu("Debug/Probar golpe")]
    private void DebugTriggerHit() => TriggerHit();

    // Sin dirección conocida (la mayoría de las fuentes de daño actuales solo mandan
    // el monto, no de dónde vino el golpe): empuja hacia atrás según hacia dónde mira Gekko.
    public void TriggerHit() => TriggerHit(-transform.forward);

    public void TriggerHit(Vector3 fromDirection)
    {
        StartCoroutine(HitstopRoutine());
        StartCoroutine(KnockbackRoutine(fromDirection));
        StartCoroutine(InvulnerabilityRoutine());
        ShakeCamera();
        StartFlash();
    }

    #region Hitstop
    private IEnumerator HitstopRoutine()
    {
        if(_hitstopActive) yield break; // no acumular si llegan dos golpes juntos
        _hitstopActive = true;

        float previousScale = Time.timeScale;
        Time.timeScale = 0f;

        yield return new WaitForSecondsRealtime(_hitstopDurationMs / 1000f);

        Time.timeScale = previousScale > 0f ? previousScale : 1f;
        _hitstopActive = false;
    }
    #endregion

    #region Viñeta
    private void StartFlash()
    {
        if(_vignetteMaterial == null) return;

        _vignetteMaterial.SetColor(ColorId, _useAlternateColor ? _alternateColor : _damageColor);
        _vignetteState = VignetteState.Flash;
        _flashTimer = 0f;
    }

    private void UpdateVignette()
    {
        if(_vignetteMaterial == null) return;

        bool lowHealth = _player != null && _player.health != null
            && _player.health.CurrentHealth <= 1f && _player.health.CurrentHealth > 0f;

        if(_vignetteState == VignetteState.Flash)
        {
            _flashTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_flashTimer / _flashDuration);
            float eased = 1f - (1f - t) * (1f - t); // ease out
            _vignetteMaterial.SetFloat(IntensityId, Mathf.Lerp(1f, 0f, eased));

            if(t >= 1f)
            {
                _loopTimer = 0f;
                _vignetteState = lowHealth ? VignetteState.LowHealthLoop : VignetteState.Idle;
            }

            return;
        }

        if(lowHealth)
        {
            _vignetteState = VignetteState.LowHealthLoop;
            _loopTimer += Time.unscaledDeltaTime;
            float u = Mathf.Repeat(_loopTimer, _lowHealthCycleDuration) / _lowHealthCycleDuration;
            float pulse = 0.5f - 0.5f * Mathf.Cos(u * Mathf.PI * 2f); // ease in-out, 0..1..0
            _vignetteMaterial.SetColor(ColorId, _lowHealthColor);
            _vignetteMaterial.SetFloat(IntensityId, Mathf.Lerp(_lowHealthMinIntensity, _lowHealthMaxIntensity, pulse));
            return;
        }

        if(_vignetteState == VignetteState.LowHealthLoop)
            _vignetteMaterial.SetFloat(IntensityId, 0f);

        _vignetteState = VignetteState.Idle;
    }
    #endregion

    #region Empujón
    private IEnumerator KnockbackRoutine(Vector3 fromDirection)
    {
        if(_player == null || _player.PjController == null) yield break;

        Vector3 dir = Vector3.ProjectOnPlane(fromDirection, Vector3.up);
        if(dir.sqrMagnitude < 0.0001f) dir = -transform.forward;
        dir.Normalize();

        float elapsed = 0f;

        while(elapsed < _knockbackDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(elapsed / _knockbackDuration);
            // Derivada de una posición ease-out cúbica: arranca rápido, frena hasta 0.
            float speed = (3f * _knockbackDistance / _knockbackDuration) * (1f - u) * (1f - u);
            _player.PjController.ApplyKnockbackVelocity(dir * speed);
            yield return null;
        }
    }
    #endregion

    #region Cámara
    private void ShakeCamera()
    {
        if(_impulseSource == null) return;

        _impulseSource.ImpulseDefinition.ImpulseDuration = _shakeDuration;
        _impulseSource.GenerateImpulse(_shakeAmplitude);
    }
    #endregion

    #region Invulnerabilidad
    private IEnumerator InvulnerabilityRoutine()
    {
        IsInvulnerable = true;

        float elapsed = 0f;
        bool visible = true;
        float nextToggle = _blinkInterval;
        SetRenderersEnabled(true);

        while(elapsed < _invulnerabilityDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            if(elapsed >= nextToggle)
            {
                visible = !visible;
                SetRenderersEnabled(visible);
                nextToggle += _blinkInterval;
            }

            yield return null;
        }

        SetRenderersEnabled(true);
        IsInvulnerable = false;
    }

    private void SetRenderersEnabled(bool enabled)
    {
        if(_renderersToBlink == null) return;

        foreach(Renderer r in _renderersToBlink)
            if(r != null) r.enabled = enabled;
    }
    #endregion
}
