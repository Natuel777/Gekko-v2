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
        VolumeManager.Instance.LoadGame();
        VolumeManager.Instance.SetMasterVolume(VolumeManager.Instance.masterValue);
        VolumeManager.Instance.SetMusicVolume(VolumeManager.Instance.musicValue);
        VolumeManager.Instance.SetSFXVolume(VolumeManager.Instance.sfxValue);
    }
    public override void Free()
    {
        VolumeManager.Instance.SaveGame();
        Destroy(gameObject);
    }
}
