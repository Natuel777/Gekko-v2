using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour, ISaveLoad
{
    public static AudioManager instance;

    [SerializeField] AudioMixer audioMixer;
    public float masterValue = 1;
    public float musicValue = 1;
    public float sfxValue = 1;

    [SerializeField] private Sounds[] sounds;
    private List<AudioSource> _sources;
    private List<AudioSource> _pausedSources;
    private float _timePerCheck = 5;

    private void Awake()
    {
        if (!instance) instance = this;
        else Destroy(gameObject);

        DontDestroyOnLoad(gameObject);
        _sources = new();
        _pausedSources = new();
        StartCoroutine(CheckStatus());

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnLoad += LoadGame;
            SaveManager.Instance.OnSave += SaveGame;
        }
    }
    private void Start()
    {
        LoadGame();
    }
    private IEnumerator CheckStatus()
    {
        while (true)
        {
            if (_sources.Count <= 0) yield return new WaitForSeconds(_timePerCheck);
            AudioSource[] sources = _sources.ToArray();
            for (int i = 0; i < sources.Length; i++)
            {
                if (_pausedSources.Count >0 && _pausedSources.Contains(sources[i])) continue;
                if (!sources[i].isPlaying)
                {
                    _sources.Remove(sources[i]);
                    Destroy(sources[i]);
                }
            };
                yield return new WaitForSeconds(_timePerCheck);
        }
    }
    private void Set(Sounds sound)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        _sources.Add(source);
        sound.source = source;
        sound.source.clip = sound.soundClip;
        sound.source.outputAudioMixerGroup = sound.audioMixer;
        sound.source.volume = sound.volume;
        sound.source.pitch = sound.pitch;
        sound.source.loop = sound.loop;
    }
    public bool IsPlaying(SoundNames name)
    {
        Sounds sound = FindSound(name);

        if (sound == null)
        {
            Debug.Log("no se encontro el sonido");
        }
        if(sound.source.isPlaying) return true;
        return false;
    }
    public void Play(SoundNames name, bool loop = false)
    {
        Sounds sound = FindSound(name);

        if (sound == null)
        {
            Debug.Log("no se encontro el sonido");
            return;
        }
        Set(sound);
        sound.source.loop = loop;
        sound.source.Play();
    }

    public void Pause(SoundNames name)
    {
        Sounds sound = FindSound(name);
        if (sound == null)
        {
            Debug.Log("no se encontro el sonido");
            return;
        }
        if(sound.source != null)
        {
            _pausedSources.Add(sound.source);
            sound.source.Pause();
        }
    }
    public void UnPause(SoundNames name)
    {
        Sounds sound = FindSound(name);
        if (sound == null)
        {
            Debug.Log("no se encontro el sonido");
            return;
        }
        if (sound.source != null)
        {
            sound.source.UnPause();
            _pausedSources.Remove(sound.source);
        }
    }

    public void PauseAll(List<SoundNames> notToPauseSounds = null)
    {
        foreach (var sound in sounds)
        {
            if (sound.source == null) continue;
            if (notToPauseSounds != null && notToPauseSounds.Contains(sound._name)) continue;
            sound.source.Pause(); 
        }
        _pausedSources = _sources;
    }
    public void UnPauseAll()
    {
        foreach (var sound in sounds)
        {
            if (sound.source == null) continue;
            sound.source.UnPause();
        }
        _pausedSources.Clear();
        StartCoroutine(CheckStatus());
    }
    public void ResetAudio()
    {
        foreach (var source in _sources)
        {
            if(source.isPlaying)
            source.Stop();
        }
    }
    private Sounds FindSound(SoundNames name)
    {
        foreach (var sound in sounds)
        {
            if(sound._name == name) return sound;
        }
        return null;
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
    public void SaveGame()
    {
        PlayerPrefs.SetFloat(PlayerPrefsKeys.masterValueKey, masterValue);
        PlayerPrefs.SetFloat(PlayerPrefsKeys.musicValueKey, musicValue);
        PlayerPrefs.SetFloat(PlayerPrefsKeys.sfxValueKey, sfxValue);
    }

    public void LoadGame()
    {
        masterValue = PlayerPrefs.GetFloat(PlayerPrefsKeys.masterValueKey, masterValue);
        musicValue = PlayerPrefs.GetFloat(PlayerPrefsKeys.musicValueKey, musicValue);
        sfxValue = PlayerPrefs.GetFloat(PlayerPrefsKeys.sfxValueKey, sfxValue);
        LoadVolume();
    }
    void LoadVolume()
    {
        audioMixer.SetFloat("MasterVolume", Mathf.Log10(PlayerPrefs.GetFloat(PlayerPrefsKeys.masterValueKey, masterValue)) * 20);
        audioMixer.SetFloat("MusicVolume", Mathf.Log10(PlayerPrefs.GetFloat(PlayerPrefsKeys.musicValueKey, musicValue)) * 20);
        audioMixer.SetFloat("SFXVolume", Mathf.Log10(PlayerPrefs.GetFloat(PlayerPrefsKeys.sfxValueKey, sfxValue)) * 20);
    }
}