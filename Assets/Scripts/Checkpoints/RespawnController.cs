using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Orquesta la transición corta de respawn (caída al vacío o muerte): no hay pantalla de
// Game Over, es un iris que se cierra sobre Gekko, un corte negro donde se teletransporta
// al último checkpoint, y el iris que se abre de nuevo. Todo en tiempo sin escala.
public class RespawnController : MonoBehaviour
{
    public enum RespawnMode { Iris, Hojas }

    public static RespawnController Instance { get; private set; }

    [Header("Modo")]
    [SerializeField] private RespawnMode _mode = RespawnMode.Iris;

    [Header("Tiempos (segundos, unscaled)")]
    [SerializeField] private float _fallWatchDuration = 0.65f;
    [SerializeField] private float _irisCloseDuration = 0.6f;
    [SerializeField] private float _blackHoldDuration = 0.6f;
    [SerializeField] private float _irisOpenDuration = 0.7f;

    [Header("Curva de easing (iris)")]
    [SerializeField] private AnimationCurve _easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Iris (shader de UI)")]
    [SerializeField] private Image _irisImage;
    [SerializeField] private float _irisOpenRadius = 1.5f;
    [SerializeField] private float _irisSoftness = 0.01f;
    [SerializeField] private Color _rimColor = new Color(0.498f, 0.965f, 1f, 1f);
    [SerializeField] private float _rimWidth = 0.008f;

    [Header("Runa (UI, centro de pantalla durante el corte negro)")]
    [SerializeField] private CanvasGroup _runeCanvasGroup;
    [SerializeField] private Image _runeImage;
    [SerializeField] private float _runePulseSpeed = 2f;
    [SerializeField] private float _runeFadeDuration = 0.2f;

    [Header("Daño por caída")]
    [SerializeField] private bool _fallCostsHealth = true;
    [SerializeField] private float _fallDamageAmount = 20f;

    private static readonly int CenterId = Shader.PropertyToID("_Center");
    private static readonly int RadiusId = Shader.PropertyToID("_Radius");
    private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
    private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
    private static readonly int RimWidthId = Shader.PropertyToID("_RimWidth");

    private Material _irisMaterial;
    private Camera _mainCamera;
    private CinemachineOrbitalFollow _orbitalFollow;
    private float _savedYDamping;
    private bool _isRunning;

    public bool IsRunning => _isRunning;

    private void Awake()
    {
        Instance = this;
        _mainCamera = Camera.main;

        if(_irisImage != null)
        {
            _irisMaterial = _irisImage.material;
            _irisMaterial.SetFloat(SoftnessId, _irisSoftness);
            _irisMaterial.SetColor(RimColorId, _rimColor);
            _irisMaterial.SetFloat(RimWidthId, _rimWidth);
            _irisMaterial.SetFloat(RadiusId, _irisOpenRadius);
        }

        if(_runeCanvasGroup != null) _runeCanvasGroup.alpha = 0f;
    }

    private void OnDestroy()
    {
        if(Instance == this) Instance = null;
    }

#if UNITY_EDITOR
    private void Update()
    {
        if(Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            TriggerRespawn(false);
    }
#endif

    [ContextMenu("Debug/Probar transición de respawn")]
    private void DebugTriggerRespawn() => TriggerRespawn(false);

    // isFallDeath = true cuando lo dispara una KillZone (aplica el costo de vida si corresponde).
    // false para muerte por daño (la vida ya está en 0) o un respawn manual de debug.
    public void TriggerRespawn(bool isFallDeath)
    {
        if(_isRunning) return;
        StartCoroutine(RespawnSequence(isFallDeath));
    }

    private IEnumerator RespawnSequence(bool isFallDeath)
    {
        _isRunning = true;

        Player pj = GameManager.Instance.Pj;
        GameManager.Instance.CanPause = true;
        pj.Inputs(false);

        if(_mode == RespawnMode.Hojas)
            Debug.LogWarning("[RespawnController] Modo 'Hojas' todavía no está implementado, se usa Iris.");

        // --- Fase 1: caída ---
        FreezeCameraY();

        if(isFallDeath && _fallCostsHealth)
            EventManager.Trigger<float>("OnPlayerDamaged", _fallDamageAmount);

        yield return WaitUnscaled(_fallWatchDuration);

        // --- Fase 2: cierre en iris, centrado en Gekko cayendo ---
        SetIris(_irisOpenRadius, WorldToUV(pj.transform.position));
        yield return AnimateIris(_irisOpenRadius, 0f, _irisCloseDuration, pj.transform);

        RestoreCameraY();

        // --- Fase 3: pantalla negra (radio 0 = todo tapado): teletransporte + runa ---
        bool hasCheckpoint = GameManager.Instance.checkpointManager.TryGetRespawnPose(out Vector3 pos, out Quaternion rot, out float health);

        if(!hasCheckpoint)
        {
            // Sin checkpoint pisado todavía: mismo fallback de siempre (reinicia el nivel).
            GameManager.Instance.RestartLvl();
            _isRunning = false;
            yield break;
        }

        Vector3 oldPos = pj.transform.position;
        pj.PjController.Teleport(pos);
        pj.transform.rotation = rot;
        pj.health.SetHealth(health);
        pj.PjTongue.ResetTongue();
        NotifyCameraWarp(pj.transform, pos - oldPos);

        yield return RuneGlowRoutine(_blackHoldDuration);

        // --- Fase 4: apertura, centrada en Gekko ya en el checkpoint ---
        StartCoroutine(FadeRuneOut(_runeFadeDuration));
        yield return AnimateIris(0f, _irisOpenRadius, _irisOpenDuration, pj.transform);

        pj.Inputs(true);
        GameManager.Instance.CanPause = false;
        // Red de seguridad: durante el respawn CanPause queda en true, y cualquier cosa que haya puesto el
        // timeScale en 0 en el medio (ej. un hitstop) lo deja así creyendo que el juego está pausado.
        Time.timeScale = 1f;
        _isRunning = false;
    }

    #region Iris
    private IEnumerator AnimateIris(float fromRadius, float toRadius, float duration, Transform follow)
    {
        float elapsed = 0f;

        while(elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = _easeCurve.Evaluate(Mathf.Clamp01(elapsed / duration));
            SetIris(Mathf.Lerp(fromRadius, toRadius, t), WorldToUV(follow.position));
            yield return null;
        }

        SetIris(toRadius, WorldToUV(follow.position));
    }

    private void SetIris(float radius, Vector2 centerUV)
    {
        if(_irisMaterial == null) return;
        _irisMaterial.SetFloat(RadiusId, radius);
        _irisMaterial.SetVector(CenterId, centerUV);
    }

    private Vector2 WorldToUV(Vector3 worldPos)
    {
        if(_mainCamera == null) _mainCamera = Camera.main;
        if(_mainCamera == null) return new Vector2(0.5f, 0.5f);

        Vector3 sp = _mainCamera.WorldToScreenPoint(worldPos);
        return new Vector2(Mathf.Clamp01(sp.x / Screen.width), Mathf.Clamp01(sp.y / Screen.height));
    }
    #endregion

    #region Runa
    private IEnumerator RuneGlowRoutine(float duration)
    {
        if(_runeCanvasGroup == null)
        {
            yield return WaitUnscaled(duration);
            yield break;
        }

        float fadeIn = Mathf.Min(0.2f, duration * 0.3f);
        float elapsed = 0f;

        while(elapsed < fadeIn)
        {
            elapsed += Time.unscaledDeltaTime;
            _runeCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeIn);
            yield return null;
        }

        float remaining = duration - fadeIn;
        float t = 0f;

        while(t < remaining)
        {
            t += Time.unscaledDeltaTime;
            float pulse = 0.75f + 0.25f * Mathf.Sin(t * _runePulseSpeed * Mathf.PI * 2f);
            _runeCanvasGroup.alpha = pulse;

            if(_runeImage != null)
                _runeImage.transform.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(t * _runePulseSpeed * Mathf.PI * 2f));

            yield return null;
        }
    }

    private IEnumerator FadeRuneOut(float duration)
    {
        if(_runeCanvasGroup == null) yield break;

        float start = _runeCanvasGroup.alpha;
        float elapsed = 0f;

        while(elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _runeCanvasGroup.alpha = Mathf.Lerp(start, 0f, elapsed / duration);
            yield return null;
        }

        _runeCanvasGroup.alpha = 0f;
    }
    #endregion

    #region Cámara
    // La cámara deja de seguir a Gekko en Y mientras cae, para que se vea caer un instante.
    private void FreezeCameraY()
    {
        CinemachineCamera cam = CameraStateManager.Instance != null ? CameraStateManager.Instance.CurrentCamera : null;
        if(cam == null) return;

        _orbitalFollow = cam.GetComponent<CinemachineOrbitalFollow>();
        if(_orbitalFollow == null) return;

        var tracker = _orbitalFollow.TrackerSettings;
        _savedYDamping = tracker.PositionDamping.y;
        tracker.PositionDamping.y = 50f;
        _orbitalFollow.TrackerSettings = tracker;
    }

    private void RestoreCameraY()
    {
        if(_orbitalFollow == null) return;

        var tracker = _orbitalFollow.TrackerSettings;
        tracker.PositionDamping.y = _savedYDamping;
        _orbitalFollow.TrackerSettings = tracker;
        _orbitalFollow = null;
    }

    // Avisa a las cámaras que siguen a Gekko que se teletransportó, para que no "viajen" desde la caída.
    private void NotifyCameraWarp(Transform pjTransform, Vector3 positionDelta)
    {
        if(CameraStateManager.Instance == null) return;

        foreach(CinemachineCamera cam in CameraStateManager.Instance.Cameras)
        {
            if(cam == null) continue;
            if(cam.Follow == pjTransform || cam.LookAt == pjTransform)
                cam.OnTargetObjectWarped(pjTransform, positionDelta);
        }
    }
    #endregion

    private IEnumerator WaitUnscaled(float seconds)
    {
        float t = 0f;
        while(t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }
}
