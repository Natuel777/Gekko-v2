
using UnityEngine.SceneManagement;

public class ScreenReafirm : Screens
{
    public void BTN_Menu()
    {
        AudioManager.instance.Play(SoundNames.UiButton);
        if(GameManager.Instance)
        GameManager.Instance.IsPause = false;
        SceneManager.LoadScene(ScenesDictionary.SceneName[ScenesNames.Menu]);
    }
}
