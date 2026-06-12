
public class ScreenOptions :Screens
{
    public void BTN_Audio()
    {
        ScreenManager.Instance.ButtonSound.Play();
        ScreenManager.Instance.Push("Canvas_Audio");
    }

}
