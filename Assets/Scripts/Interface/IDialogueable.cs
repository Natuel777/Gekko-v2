using UnityEngine;
using UnityEngine.UI;

public interface IDialogueable
{
    Dialogue Dialogue { get; }
    Transform Transform { get; }
    Sprite Image { get; }
    void OnDialogueStart();
    void OnDialogueEnd();
}
