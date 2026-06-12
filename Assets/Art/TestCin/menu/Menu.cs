using UnityEngine;
using UnityEngine.SceneManagement;

public class Menu : MonoBehaviour
{
    public void Play()
    {
        SceneManager.LoadScene("LvlParcialBlocking 1");
    }

    public void QuitGame()  
    {
        Application.Quit();
    }
}
