using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoadButton : MonoBehaviour
{
    private ScenesNames _loadScreen = ScenesNames.LoadScene;
    [SerializeField] private ScenesNames _scene = ScenesNames.Menu;
    [SerializeField] private PlayTransitionController _transition;

    public void LoadScene()
    {
        if (_loadScreen == _scene) return;

        if (_transition)
        {
            _transition.PlayThenLoad(_scene);
            return;
        }

        AsyncLoader.SetSceneToLoad(_scene);
        SceneManager.LoadScene(ScenesDictionary.SceneName[_loadScreen]);
    }

}
