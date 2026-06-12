using UnityEngine;

public class ScreenFinishLevel : Screens
{
    public void BTN_Restart()
    {
        GameManager.Instance.RestartLvl();
    }
}
