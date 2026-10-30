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

    // Hooks para futuro (animación de "hablar", girar hacia el player, etc.). Virtual: alguna
    // instancia particular (ej. FriendGekkoDialogue) puede necesitar reaccionar al cierre del diálogo.
    public virtual void OnDialogueStart() { }
    public virtual void OnDialogueEnd() { }

    // Si UIManager corta el diálogo cuando el player se aleja. Los diálogos que arrancan solos
    // (sin que el player se acerque) pueden apagarlo para no cerrarse en el primer frame.
    public virtual bool CloseByDistance => true;
}
