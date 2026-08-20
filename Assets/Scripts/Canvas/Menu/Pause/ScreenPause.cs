
using UnityEngine;

public class ScreenPause : Screens
{
    public void Initialize()
    {
        EventManager.Subscribe("UnPauseEvent", () => Debug.Log("poronga"));
    }

    public void BTN_Menu()
    {
        ScreenManager.Instance.ButtonSound.Play();
        ScreenManager.Instance.Push("Canvas_Reafirm");
    }

    public void BTN_Options()
    {
        ScreenManager.Instance.ButtonSound.Play();
        ScreenManager.Instance.Push("Canvas_Options");
    }

    public override void BTN_Back()
    {
        base.BTN_Back();
        GameManager.Instance.IsPause = false;
        GameManager.Instance.Pj.ActivateInputs();
        Cursor.lockState = CursorLockMode.Locked;
        EventManager.Trigger("UnPauseEvent");
    }

    public override void Activate()
    {
        gameObject.SetActive(true);
        base.Activate();
    }

    public override void Free()
    {
        gameObject.SetActive(false);
    }
}