using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(PlayableDirector))]
public class PlayTransitionController : MonoBehaviour
{
    [SerializeField] private PlayableDirector _director;

    private ScenesNames _pendingScene;
    private bool _isPlaying;

    private void Awake()
    {
        if (!_director) _director = GetComponent<PlayableDirector>();
        _director.stopped += OnDirectorStopped;
    }

    public void PlayThenLoad(ScenesNames scene)
    {
        if (_isPlaying) return;
        _isPlaying = true;
        _pendingScene = scene;
        _director.time = 0;
        _director.Play();
    }

    private void OnDirectorStopped(PlayableDirector director)
    {
        if (!_isPlaying) return;
        _isPlaying = false;

        AsyncLoader.SetSceneToLoad(_pendingScene);
        SceneManager.LoadScene(ScenesDictionary.SceneName[ScenesNames.LoadScene]);
    }
}
