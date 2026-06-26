using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoadButton : MonoBehaviour
{
    private ScenesNames _loadScreen = ScenesNames.LoadScene;
    [SerializeField] private ScenesNames _scene = ScenesNames.Menu;

    public void LoadScene()
    {
        if (_loadScreen == _scene) return;
        AsyncLoader.SetSceneToLoad(_scene);
        SceneManager.LoadScene(ScenesDictionary.SceneName[_loadScreen]);
    }

}
