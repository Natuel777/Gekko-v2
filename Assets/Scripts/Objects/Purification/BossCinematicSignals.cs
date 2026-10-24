using UnityEngine;

// Puente para los SignalReceiver de los timelines del boss: UnityEvent no puede invocar
// UIManager.StartDialogue directo porque su parámetro es una interfaz (IDialogueable).
public class BossCinematicSignals : MonoBehaviour
{
    [SerializeField] private DialogueNPC _purifiedDialogue;
    [SerializeField] private CreditsScreen _creditsScreen;

    public void PlayPurifiedDialogue()
    {
        if(_purifiedDialogue != null) UIManager.Instance.StartDialogue(_purifiedDialogue);
    }

    // Cierre del nivel: fade a negro permanente + creditos. Reemplaza al fade de ScreenFader en este
    // timeline puntual porque ese vuelve solo a la jugabilidad, y acá el nivel ya termino.
    public void PlayEndingSequence()
    {
        if(_creditsScreen != null) _creditsScreen.Play();
    }
}
