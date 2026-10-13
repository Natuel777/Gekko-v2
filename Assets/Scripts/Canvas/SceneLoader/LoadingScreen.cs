using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Vive en la escena "SceneLoader": arranca solo al entrar a esta escena, lee la
// escena pendiente (AsyncLoader.GetSceneToLoad(), seteada desde donde se disparó la
// transición) y la carga de fondo mientras se ve esta animación. Reemplaza a la
// barra de progreso vieja, pero reusa el mismo mecanismo de "escena pendiente".
// Sin DOTween: el fade es una corutina a mano, igual que ScreenFader/LoadingTips.
public class LoadingScreen : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TMP_Text _zoneLabel; // "ZONA 1"
    [SerializeField] private TMP_Text _zoneName;  // nombre grande de la zona

    [Header("Tiempos")]
    [SerializeField] private float _fadeInDuration = 0.3f;
    [Tooltip("Mínimo que se ve la pantalla de carga, aunque la escena destino cargue más rápido (para que se llegue a leer al menos un consejo completo).")]
    [SerializeField] private float _minVisibleDuration = 8f;

    private void Awake()
    {
        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
    }

    private IEnumerator Start()
    {
        if (_canvasGroup != null)
            yield return Fade(0f, 1f, _fadeInDuration);

        ScenesNames pending = AsyncLoader.GetSceneToLoad();

        if (!ScenesDictionary.SceneName.TryGetValue(pending, out string sceneName))
        {
            Debug.LogError($"[LoadingScreen] ScenesNames.{pending} no está en ScenesDictionary.");
            yield break;
        }

        if (AudioManager.instance != null) AudioManager.instance.ResetAudio();

        float startTime = Time.unscaledTime;
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

        if (operation == null)
        {
            Debug.LogError($"[LoadingScreen] No se pudo cargar la escena '{sceneName}'. ¿Está en el Build Profile?");
            yield break;
        }

        // Retenemos la activación para que la animación (y los consejos) se vean el tiempo mínimo configurado.
        operation.allowSceneActivation = false;
        Application.backgroundLoadingPriority = ThreadPriority.Normal;

        while (true)
        {
            bool realLoadDone = operation.progress >= 0.9f;
            bool minTimeElapsed = Time.unscaledTime - startTime >= _minVisibleDuration;

            if (realLoadDone && minTimeElapsed) break;
            yield return null;
        }

        Application.backgroundLoadingPriority = ThreadPriority.Low; // valor por defecto de Unity
        operation.allowSceneActivation = true;

        // No hace falta fade-out acá: esta escena (y este GameObject) se descargan apenas
        // la nueva activa. Si la escena destino quiere una entrada suave, lo resuelve ella
        // (ej. el Canvas "fader"/ScreenFader que ya tenés en LvlOne).
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        _canvasGroup.alpha = to;
    }

    // Opcional: llamarlo desde donde se dispara la transición para mostrar el nombre de zona
    // antes de ir a "SceneLoader", o simplemente asignar el texto a mano en el Inspector por ahora.
    public void SetZoneInfo(string label, string name)
    {
        if (_zoneLabel != null) _zoneLabel.text = label;
        if (_zoneName != null) _zoneName.text = name;
    }
}
