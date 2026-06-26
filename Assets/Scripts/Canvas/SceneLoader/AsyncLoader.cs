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
    }
    private void ChargeAsyncScene(string sceneName)
    {
        AsyncOperation async = SceneManager.LoadSceneAsync(sceneName);
        if (async == null)
        {
            Debug.LogError($"[AsyncLoader] No se pudo cargar la escena '{sceneName}'. ¿Está en el Build Profile?");
            return;
        }
        Application.backgroundLoadingPriority = ThreadPriority.High;
        StartCoroutine(ChargeSceneCorrutine(async));
    }

    private IEnumerator ChargeSceneCorrutine(AsyncOperation async)
    {
        while (!async.isDone)
        {
            if (_progressBar && _percentage)
            {
                _progressBar.value = async.progress * 100f;
                _percentage.text = $"{Mathf.Round(async.progress * 100f)} %";
            }
            yield return new WaitForEndOfFrame();
        }
        async.allowSceneActivation = true;
    }
}
