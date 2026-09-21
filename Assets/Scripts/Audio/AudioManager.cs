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
    public bool musicEnabled = true;
    private const float MutedDb = -80f;

    [SerializeField] private int _minPoolSize = 5;
    [SerializeField] private Sounds[] sounds;
    private List<AudioSource> _sources;
    private List<AudioSource> _pausedSources;
    private Dictionary<SoundNames, List<AudioSource>> _activeSourcesByName = new();

    private void Awake()
    {
        if (!instance) instance = this;
        else Destroy(gameObject);

        DontDestroyOnLoad(gameObject);
        _sources = new();
        _pausedSources = new(); 
        _activeSourcesByName = new();
        CreatePool(_minPoolSize);

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
    private void CreatePool(int amount)
    {
        for (int i = 0; i < amount ; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            _sources.Add(source);
        }
    }
    private AudioSource GetAvailableSource()
    {
        foreach (var kvp in _activeSourcesByName)
        {
            kvp.Value.RemoveAll(s => s == null || (!s.isPlaying && !_pausedSources.Contains(s)));
        }
        foreach (var source in _sources)
        {
            bool isPaused = _pausedSources.Contains(source);
            if (!source.isPlaying && !isPaused)
                return source;
        }

        AudioSource newSource = gameObject.AddComponent<AudioSource>();
        _sources.Add(newSource);
        return newSource;
    }
    private void Set(Sounds sound, out AudioSource source)
    {
        source = GetAvailableSource();
        source.clip = sound.soundClip;
        source.outputAudioMixerGroup = sound.audioMixer;
        source.volume = sound.volume;
        source.pitch = sound.pitch;
        source.loop = sound.loop;
    }
    public void Play(SoundNames name, bool loop = false)
    {
        Sounds sound = FindSound(name);

        if (sound == null)
        {
            Debug.Log("no se encontro el sonido");
            return;
        }
        Set(sound, out AudioSource source);
        source.loop = loop;
        source.Play();

        if (!_activeSourcesByName.TryGetValue(name, out var list))
        {
            list = new List<AudioSource>();
            _activeSourcesByName[name] = list;
        }
        list.Add(source);
    }

    public void Pause(SoundNames name)
    {
        if (!_activeSourcesByName.TryGetValue(name, out var list)) return;

        foreach (var source in list)
        {
            if (source == null) continue;
            _pausedSources.Add(source);
            source.Pause();
        }
    }
    public void UnPause(SoundNames name)
    {
        if (!_activeSourcesByName.TryGetValue(name, out var list)) return;

        foreach (var source in list)
        {
            if (source == null) continue;
            source.UnPause();
            _pausedSources.Remove(source);
        }
    }
    public bool IsPlaying(SoundNames name)
    {
        if (!_activeSourcesByName.TryGetValue(name, out var list)) return false;

        foreach (var source in list)
        {
            if (source != null && source.isPlaying) return true;
        }
        return false;
    }
    public void PauseAll(List<SoundNames> notToPauseSounds = null)
    {
        foreach (var kvp in _activeSourcesByName)
        {
            if (notToPauseSounds != null && notToPauseSounds.Contains(kvp.Key)) continue;

            foreach (var source in kvp.Value)
            {
                if (source == null) continue;
                source.Pause();
                if (!_pausedSources.Contains(source))
                    _pausedSources.Add(source);
            }
        }
    }
    public void UnPauseAll()
    {
        foreach (var kvp in _activeSourcesByName)
        {
            foreach (var source in kvp.Value)
            {
                if (source == null) continue;
                source.UnPause();
            }
        }
        _pausedSources.Clear();
    }
    public void ResetAudio()
    {
        foreach (var source in _sources)
        {
            if(source.isPlaying)
            source.Stop();
        }
        _pausedSources.Clear();
        _activeSourcesByName.Clear();

        if (_sources.Count > _minPoolSize)
        {
            int excess = _sources.Count - _minPoolSize;
            for (int i = 0; i < excess; i++)
            {
                int lastIndex = _sources.Count - 1;
                AudioSource toRemove = _sources[lastIndex];
                _sources.RemoveAt(lastIndex);
                Destroy(toRemove);
            }
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
        musicValue = value;
        audioMixer.SetFloat("MusicVolume", musicEnabled ? Mathf.Log10(value) * 20 : MutedDb);
    }

    public void SetMusicEnabled(bool enabled)
    {
        musicEnabled = enabled;
        PlayerPrefs.SetInt(PlayerPrefsKeys.musicEnabledKey, enabled ? 1 : 0);
        audioMixer.SetFloat("MusicVolume", enabled ? Mathf.Log10(Mathf.Clamp(musicValue, 0.0001f, 1)) * 20 : MutedDb);
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
        PlayerPrefs.SetInt(PlayerPrefsKeys.musicEnabledKey, musicEnabled ? 1 : 0);
    }

    public void LoadGame()
    {
        masterValue = PlayerPrefs.GetFloat(PlayerPrefsKeys.masterValueKey, masterValue);
        musicValue = PlayerPrefs.GetFloat(PlayerPrefsKeys.musicValueKey, musicValue);
        sfxValue = PlayerPrefs.GetFloat(PlayerPrefsKeys.sfxValueKey, sfxValue);
        musicEnabled = PlayerPrefs.GetInt(PlayerPrefsKeys.musicEnabledKey, 1) == 1;
        LoadVolume();
    }
    void LoadVolume()
    {
        audioMixer.SetFloat("MasterVolume", Mathf.Log10(PlayerPrefs.GetFloat(PlayerPrefsKeys.masterValueKey, masterValue)) * 20);
        audioMixer.SetFloat("MusicVolume", musicEnabled ? Mathf.Log10(PlayerPrefs.GetFloat(PlayerPrefsKeys.musicValueKey, musicValue)) * 20 : MutedDb);
        audioMixer.SetFloat("SFXVolume", Mathf.Log10(PlayerPrefs.GetFloat(PlayerPrefsKeys.sfxValueKey, sfxValue)) * 20);
    }
}