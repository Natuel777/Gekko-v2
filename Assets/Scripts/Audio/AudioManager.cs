using UnityEngine.Audio;
using UnityEngine;

public class AudioManager : MonoBehaviour//, ISaveLoad
{
    public static AudioManager Instance;
    [SerializeField] AudioMixer audioMixer;
    public float masterValue = 1;
    public float musicValue = 1;
    public float sfxValue = 1;
    private void Awake()
    {
        Instance = this;

       // SaveWithJson.Instance.OnLoad += LoadGame;
       // SaveWithJson.Instance.OnSave += SaveGame;
    }
    public void SetMasterVolume(float value)
    {
        value = Mathf.Clamp(value, 0.0001f, 1);
        audioMixer.SetFloat("MasterVolume", Mathf.Log10(value) * 20);
        masterValue = value;
    }

    public void SetMusicVolume(float value)
    {
        value = Mathf.Clamp(value, 0.0001f, 1);
        audioMixer.SetFloat("MusicVolume", Mathf.Log10(value) * 20);
        musicValue = value;
    }

    public void SetSFXVolume(float value)
    {
        value = Mathf.Clamp(value, 0.0001f, 1);
        audioMixer.SetFloat("SFXVolume", Mathf.Log10(value) * 20);
        sfxValue = value;
    }

   // public void LoadGame()
   // {
   //     masterValue = PlayerPrefs.GetFloat(PlayerPrefsKeys.masterValueKey, masterValue);
   //     musicValue = PlayerPrefs.GetFloat(PlayerPrefsKeys.musicValueKey, musicValue);
   //     sfxValue = PlayerPrefs.GetFloat(PlayerPrefsKeys.sfxValueKey, sfxValue);
   //     LoadVolume();
   // }
   //
   // public void SaveGame()
   // {
   //     PlayerPrefs.SetFloat(PlayerPrefsKeys.masterValueKey, masterValue);
   //     PlayerPrefs.SetFloat(PlayerPrefsKeys.musicValueKey, musicValue);
   //     PlayerPrefs.SetFloat(PlayerPrefsKeys.sfxValueKey, sfxValue);
   // }
   // void LoadVolume()
   // {
   //     audioMixer.SetFloat("MasterVolume", Mathf.Log10(PlayerPrefs.GetFloat(PlayerPrefsKeys.masterValueKey, masterValue)) * 20);
   //     audioMixer.SetFloat("MusicVolume", Mathf.Log10(PlayerPrefs.GetFloat(PlayerPrefsKeys.musicValueKey, musicValue)) * 20);
   //     audioMixer.SetFloat("SFXVolume", Mathf.Log10(PlayerPrefs.GetFloat(PlayerPrefsKeys.sfxValueKey, sfxValue)) * 20);
   // }

}
