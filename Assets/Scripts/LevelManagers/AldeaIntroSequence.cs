using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

// Intro de la escena Aldea: muestra la plaza con varias casas corruptas, purifica una y cierra yendo directo a la
// pantalla de victoria (reusa EventManager "OnLevelFinished", lo mismo que dispara FinishLevelTrigger en los
// niveles normales). Pensado para el blocking actual, con un lerp de color simple en vez del sistema de
// purificación real (Assets/_Gekko/Purification) — cuando haya arte final de la aldea es cuestión de pasar las
// casas a materiales con ese shader y listo, la secuencia no cambia.
public class AldeaIntroSequence : MonoBehaviour
{
    [Header("Casas")]
    [Tooltip("Casas que arrancan corruptas (se tiñen oscuras apenas empieza la escena).")]
    [SerializeField] private Renderer[] _corruptedHouses;
    [Tooltip("La casa que se purifica durante la secuencia (también arranca corrupta).")]
    [SerializeField] private Renderer _houseToPurify;

    [Header("Colores")]
    [SerializeField] private Color _corruptColor = new Color(0.22f, 0.17f, 0.32f);
    [SerializeField] private Color _purifiedColor = new Color(0.96f, 0.93f, 0.8f);

    [Header("Cámara")]
    [Tooltip("Cámara propia de esta cinemática, registrada en CameraStateManager.")]
    [SerializeField] private CinemachineCamera _introCamera;
    [SerializeField] private string _introCameraTag = "AldeaIntro";
    [Tooltip("De dónde se ve toda la plaza con las casas corruptas.")]
    [SerializeField] private Transform _wideShotPoint;
    [Tooltip("De dónde se ve de cerca la casa que se purifica.")]
    [SerializeField] private Transform _closeShotPoint;

    [Header("Tiempos")]
    [SerializeField] private float _wideShotDuration = 3f;
    [SerializeField] private float _moveToCloseDuration = 1.5f;
    [SerializeField] private float _purifyDuration = 1.5f;
    [SerializeField] private float _holdAfterPurify = 1.5f;

    private void Start()
    {
        foreach(Renderer r in _corruptedHouses)
            TintInstance(r, _corruptColor);

        TintInstance(_houseToPurify, _corruptColor);

        StartCoroutine(Sequence());
    }

    private IEnumerator Sequence()
    {
        if(GameManager.Instance != null && GameManager.Instance.Pj != null)
            GameManager.Instance.Pj.Inputs(false);

        if(_introCamera != null && _wideShotPoint != null)
        {
            _introCamera.transform.SetPositionAndRotation(_wideShotPoint.position, _wideShotPoint.rotation);
            EventManager.Trigger(_introCameraTag);
        }

        yield return new WaitForSeconds(_wideShotDuration);

        if(_introCamera != null && _closeShotPoint != null)
            yield return MoveCamera(_wideShotPoint, _closeShotPoint, _moveToCloseDuration);

        yield return PurifyHouse();

        yield return new WaitForSeconds(_holdAfterPurify);

        FinishSequence();
    }

    private IEnumerator MoveCamera(Transform from, Transform to, float duration)
    {
        float t = 0f;
        while(t < 1f)
        {
            t = Mathf.Min(1f, t + Time.deltaTime / duration);
            _introCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(from.position, to.position, t),
                Quaternion.Slerp(from.rotation, to.rotation, t));
            yield return null;
        }
    }

    private IEnumerator PurifyHouse()
    {
        if(_houseToPurify == null) yield break;

        Material material = _houseToPurify.material;
        float t = 0f;

        while(t < 1f)
        {
            t = Mathf.Min(1f, t + Time.deltaTime / _purifyDuration);
            material.color = Color.Lerp(_corruptColor, _purifiedColor, t);
            yield return null;
        }
    }

    private void FinishSequence()
    {
        if(ScreenFader.Instance == null)
        {
            EventManager.Trigger("OnLevelFinished");
            return;
        }

        ScreenFader.Instance.OnFadeMiddle += OnScreenBlack;
        ScreenFader.Instance.StartFade();
    }

    private void OnScreenBlack()
    {
        if(ScreenFader.Instance != null) ScreenFader.Instance.OnFadeMiddle -= OnScreenBlack;
        EventManager.Trigger("OnLevelFinished");
    }

    // r.material (instancia) para no pisar el asset compartido entre las 6 casas del blocking.
    private static void TintInstance(Renderer r, Color color)
    {
        if(r == null) return;
        r.material.color = color;
    }
}
