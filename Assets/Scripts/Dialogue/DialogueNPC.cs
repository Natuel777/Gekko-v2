using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider))]
public class DialogueNPC : MonoBehaviour, IDialogueable
{
    [SerializeField] private Dialogue _dialogue;

    public Dialogue Dialogue => _dialogue;
    public Transform Transform => transform;

    public Sprite Image => throw new System.NotImplementedException();

    public AudioClip AudioClip => throw new System.NotImplementedException();

    // Hooks para futuro (animación de "hablar", girar hacia el player, etc.).
    public void OnDialogueStart() { }
    public void OnDialogueEnd() { }
}
