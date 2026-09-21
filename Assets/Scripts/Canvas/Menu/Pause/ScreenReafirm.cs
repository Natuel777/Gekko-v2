
using UnityEngine.SceneManagement;

public class ScreenReafirm : Screens
{
    public void BTN_Menu()
    {
        if(GameManager.Instance)
        GameManager.Instance.IsPause = false;
        if (AudioManager.instance)
        {
            AudioManager.instance.ResetAudio();
            AudioManager.instance.Play(SoundNames.UiButton);
        }
        SceneManager.LoadScene(ScenesDictionary.SceneName[ScenesNames.Menu]);
    }
}
