
public class ScreenReafirm : Screens
{
    public void BTN_Menu()
    {
        ScreenManager.Instance.ButtonSound.Play();
        GameManager.Instance.IsPause = false;
        GameManager.Instance.RestartLvl();
    }
}
