
using UnityEngine.SceneManagement;

public class ScreenReafirm : Screens
{
    public void BTN_Menu()
    {
        ScreenManager.Instance.ButtonSound.Play();
        GameManager.Instance.IsPause = false;
        SceneManager.LoadScene(ScenesDictionary.SceneName[ScenesNames.Menu]);
    }
}
