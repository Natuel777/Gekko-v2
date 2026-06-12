using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AsyncLoader : MonoBehaviour
{
    public static ScenesNames sceneToLoad = default;
    [SerializeField] private string _sceneName = default;
    [SerializeField] private Slider _progressBar = default;
    [SerializeField] private TextMeshProUGUI _percentage = default;

    private void Awake()
    {
        _sceneName = ScenesDictionary.SceneName[sceneToLoad];
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
