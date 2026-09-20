
using UnityEngine.SceneManagement;

public class ScreenReafirm : Screens
{
    public void BTN_Menu()
    {
        if(AudioManager.instance) AudioManager.instance.Play(SoundNames.UiButton);
        if(GameManager.Instance)
        GameManager.Instance.IsPause = false;
        SceneManager.LoadScene(ScenesDictionary.SceneName[ScenesNames.Menu]);
        if (AudioManager.instance) AudioManager.instance.ResetAudio();
    }
}
