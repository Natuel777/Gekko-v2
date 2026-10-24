using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider))]
public class DialogueNPC : MonoBehaviour, IDialogueable
{
    [SerializeField] private Dialogue _dialogue;
    [SerializeField] private Sprite _image;
    [SerializeField] private AudioClip _audioClip;

    public Dialogue Dialogue => _dialogue;
    public Transform Transform => transform;

    public Sprite Image => _image;

    public AudioClip AudioClip => _audioClip;

    // Hooks para futuro (animación de "hablar", girar hacia el player, etc.).
    public void OnDialogueStart() { }
    public void OnDialogueEnd() { }
}
