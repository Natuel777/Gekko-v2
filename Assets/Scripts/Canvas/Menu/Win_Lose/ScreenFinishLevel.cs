using UnityEngine;
using UnityEngine.SceneManagement;

public class ScreenFinishLevel : Screens
{
    public void BTN_Restart()
    {
        GameManager.Instance.RestartLvl();
    }
    public void BTN_Menu()
    {
        SceneManager.LoadScene(ScenesDictionary.SceneName[ScenesNames.Menu]);
    }
}
