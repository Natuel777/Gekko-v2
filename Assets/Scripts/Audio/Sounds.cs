using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(fileName = "Sound", menuName = "Scriptable Objects/SoundSO")]
public class Sounds : ScriptableObject
{
    public SoundNames _name;

    public AudioClip soundClip;
    public AudioMixerGroup audioMixer;

    [Range(0f,1f)]public float volume = 1f;
    [Range(1f,3f)]public float pitch = 1f;

    public bool loop;

    [HideInInspector] public AudioSource source;
}
