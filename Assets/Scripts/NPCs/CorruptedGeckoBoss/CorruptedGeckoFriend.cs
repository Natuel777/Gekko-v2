using System;
using UnityEngine;

// NPC del gecko amigo purificado: mismo modelo que el Player (sin sus scripts de input/movimiento/lengua,
// ver prefab). Interactuable igual que PuzzleBeaver/MomDuck (IInteractable); por ahora solo expone un
// evento para enganchar la vuelta a la aldea más adelante.
public class CorruptedGeckoFriend : MonoBehaviour, IInteractable
{
    [Tooltip("Objeto que se prende/apaga como prompt de interacción (visual a definir todavía).")]
    [SerializeField] private GameObject _promptRoot;

    // Se dispara al interactuar. Enganchar acá la vuelta a la aldea.
    public event Action OnInteracted;

    private void Awake()
    {
        if(_promptRoot != null) _promptRoot.SetActive(false);
    }

    public void Interacted() => OnInteracted?.Invoke();

    public void ShowInteractUI()
    {
        if(_promptRoot != null) _promptRoot.SetActive(true);
    }

    public void HideInteractUI()
    {
        if(_promptRoot != null) _promptRoot.SetActive(false);
    }
}
