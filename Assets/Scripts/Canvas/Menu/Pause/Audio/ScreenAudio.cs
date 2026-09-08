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
        AudioManager.instance.LoadGame();
        AudioManager.instance.SetMasterVolume(AudioManager.instance.masterValue);
        AudioManager.instance.SetMusicVolume(AudioManager.instance.musicValue);
        AudioManager.instance.SetSFXVolume(AudioManager.instance.sfxValue);
    }
    public override void Free()
    {
        AudioManager.instance.SaveGame();
        Destroy(gameObject);
    }
}
