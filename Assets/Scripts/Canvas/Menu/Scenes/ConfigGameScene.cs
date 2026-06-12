using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConfigGameScene : MonoBehaviour
{
    public Transform mainGame;

    void Start()
    {
        ScreenManager.Instance.Push(new ScreenGO(mainGame));
    }

    void Update()
    {
        // if (Input.GetKeyDown(KeyCode.P))
        // {
        //     var screenPause = Instantiate(Resources.Load<ScreenPause>("Canvas_Pause"));
        //     ScreenManager.Instance.Push(screenPause);
        // }
        // else if (Input.GetKeyDown(KeyCode.Escape))
        // {
        //     ScreenManager.Instance.Pop();
        // }
    }
    public void BTN_Pause()
    {
        ScreenManager.Instance.Push("Canvas_Pause");
    }
    public void BTN_PauseMenu()
    {
        ScreenManager.Instance.Push("Canvas_Options");
    }
}
