using UnityEngine;
using UnityEngine.UI;

public interface IDialogueable
{
    Dialogue Dialogue { get; }
    Transform Transform { get; }
    Sprite Image { get; }
    AudioClip AudioClip { get; }
    void OnDialogueStart();
    void OnDialogueEnd();
}
