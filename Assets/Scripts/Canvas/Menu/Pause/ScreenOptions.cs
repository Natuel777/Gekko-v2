
public class ScreenOptions :Screens
{
    public void BTN_Audio()
    {
        AudioManager.instance.Play(SoundNames.UiButton);
        ScreenManager.Instance.Push("Canvas_Audio");
    }
    public void BTN_Restart()
    {
        if(GameManager.Instance)
        GameManager.Instance.RestartLvl();
    }

}
