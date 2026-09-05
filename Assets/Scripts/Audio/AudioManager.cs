using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

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
}