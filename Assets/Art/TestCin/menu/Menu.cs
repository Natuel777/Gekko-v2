using UnityEngine;
using UnityEngine.SceneManagement;

public class Menu : MonoBehaviour
{
    public void Play()
    {
        AsyncLoader.SetSceneToLoad(ScenesNames.Intentocin);
        SceneManager.LoadScene("SceneLoader");
    }

    public void QuitGame()  
    {
        Application.Quit();
    }
}
