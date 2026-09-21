using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AsyncLoader : MonoBehaviour
{
    private const string PrefKey = "SceneToLoad";
    public static ScenesNames sceneToLoad = default;
    [SerializeField] private string _sceneName = default;
    [SerializeField] private Slider _progressBar = default;
    [SerializeField] private TextMeshProUGUI _percentage = default;
    [SerializeField] private float _minLoadDuration = 3f;
    [SerializeField] private float _displaySpeed = 0.6f;

    public static void SetSceneToLoad(ScenesNames scene)
    {
        sceneToLoad = scene;
        PlayerPrefs.SetInt(PrefKey, (int)scene);
        PlayerPrefs.Save();
    }

    private void Awake()
    {
        // Recover from Domain Reload: static may have been wiped, PlayerPrefs persists
        if (sceneToLoad == default && PlayerPrefs.HasKey(PrefKey))
            sceneToLoad = (ScenesNames)PlayerPrefs.GetInt(PrefKey);

        if (!ScenesDictionary.SceneName.TryGetValue(sceneToLoad, out _sceneName))
        {
            Debug.LogError($"[AsyncLoader] ScenesNames.{sceneToLoad} no está en ScenesDictionary.");
            return;
        }
        if (!_progressBar) _progressBar = FindAnyObjectByType<Slider>();
        if (!_percentage) _percentage = FindAnyObjectByType<TextMeshProUGUI>();
    }

    private IEnumerator Start()
    {
        yield return null;
        ChargeAsyncScene(_sceneName);
        if(AudioManager.instance != null) AudioManager.instance.ResetAudio();
    }
    private void ChargeAsyncScene(string sceneName)
    {
        AsyncOperation async = SceneManager.LoadSceneAsync(sceneName);
        if (async == null)
        {
            Debug.LogError($"[AsyncLoader] No se pudo cargar la escena '{sceneName}'. ¿Está en el Build Profile?");
            return;
        }
        // Retenemos la activación para poder mostrar una barra de progreso
        // gradual (0->100%) en vez de que salte casi al instante.
        async.allowSceneActivation = false;
        Application.backgroundLoadingPriority = ThreadPriority.High;
        StartCoroutine(ChargeSceneCorrutine(async));
    }

    private IEnumerator ChargeSceneCorrutine(AsyncOperation async)
    {
        float displayedProgress = 0f;
        float elapsed = 0f;

        while (true)
        {
            elapsed += Time.unscaledDeltaTime;

            // Unity deja async.progress clavado en 0.9 mientras allowSceneActivation
            // esté en false y la carga real ya terminó, así que 0.9 = "carga real completa".
            float realProgress01 = Mathf.Clamp01(async.progress / 0.9f);
            float minDurationProgress01 = Mathf.Clamp01(elapsed / _minLoadDuration);
            float target = Mathf.Min(realProgress01, minDurationProgress01);

            displayedProgress = Mathf.MoveTowards(displayedProgress, target, _displaySpeed * Time.unscaledDeltaTime);
            UpdateBar(displayedProgress);

            bool realLoadDone = async.progress >= 0.9f;
            bool minTimeElapsed = elapsed >= _minLoadDuration;
            if (realLoadDone && minTimeElapsed && displayedProgress >= 0.999f)
                break;

            yield return null;
        }

        UpdateBar(1f);
        async.allowSceneActivation = true;
    }

    private void UpdateBar(float t01)
    {
        if (!_progressBar || !_percentage) return;
        _progressBar.value = t01 * 100f;
        _percentage.text = $"{Mathf.Round(t01 * 100f)} %";
    }
}
