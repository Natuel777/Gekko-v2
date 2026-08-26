using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

    [SerializeField] private Sounds[] sounds;
    private List<AudioSource> _sources;
    private List<AudioSource> _pausedSources;
    private float _timePerCheck = 5;
    private bool _paused;

    private void Awake()
    {
        if (!instance) instance = this;
        else Destroy(gameObject);

        DontDestroyOnLoad(gameObject);
        _sources = new();
        StartCoroutine(CheckStatus());
    }
    private IEnumerator CheckStatus()
    {
        while (true)
        {
            if (_sources.Count <= 0) yield return new WaitForSeconds(_timePerCheck);

            foreach (AudioSource source in _sources)
            {
                if (_paused && _pausedSources.Contains(source)) continue;
                if (!source.isPlaying)
                {
                    _sources.Remove(source);
                    Destroy(source);
                }
            }
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
        sound.source.Pause();
    }

    public void PauseAll(List<SoundNames> notToPauseSounds = null)
    {
        foreach (var sound in sounds)
        {
            if (notToPauseSounds != null && notToPauseSounds.Contains(sound._name)) continue;
            sound.source.Pause(); 
        }
        _pausedSources = _sources;
        _paused = true;
    }
    public void UnPauseAll()
    {
        foreach (var sound in sounds) sound.source.UnPause();
        _pausedSources = null;
        _paused = false;
        StartCoroutine(CheckStatus());
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