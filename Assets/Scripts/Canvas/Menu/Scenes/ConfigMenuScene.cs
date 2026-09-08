using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConfigMenuScene : MonoBehaviour
{
    void Start()
    {
        ScreenManager.Instance.Push(new ScreenGO(transform));
    }
    public void BTN_Options()
    {
        AudioManager.instance.Play(SoundNames.UiButton);
        ScreenManager.Instance.Push("Canvas_Options");
    }
}
