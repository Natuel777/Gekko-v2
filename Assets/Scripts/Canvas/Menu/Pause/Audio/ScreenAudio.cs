using UnityEngine;
using UnityEngine.UI;

public class ScreenAudio : Screens
{
    public override void Activate()
    {
        foreach (var button in _buttons)
        {
            button.interactable = true;
        }
        //AudioManager.Instance.LoadGame();
        AudioManager.Instance.SetMasterVolume(AudioManager.Instance.masterValue);
        AudioManager.Instance.SetMusicVolume(AudioManager.Instance.musicValue);
        AudioManager.Instance.SetSFXVolume(AudioManager.Instance.sfxValue);
    }
    public override void Free()
    {
        //AudioManager.Instance.SaveGame();
        Destroy(gameObject);
    }
}
