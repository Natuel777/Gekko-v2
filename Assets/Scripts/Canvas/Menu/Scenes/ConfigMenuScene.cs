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
        ScreenManager.Instance.ButtonSound.Play();
        ScreenManager.Instance.Push("Canvas_Options");
    }
}
