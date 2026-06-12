using UnityEngine;

// Raycast de interacción para iniciar diálogos. Se construye en Player.Start con el
// transform del Gekko (origen del rayo, hacia adelante) y la capa de NPCs interactuables.
// Lo invoca PlayerInputs.InteractInput (tecla E).
public class DialogueInteractor
{
    private readonly Transform _origin;
    private readonly float _reach;
    private readonly float _originY;
    private readonly LayerMask _layer;

    public DialogueInteractor(Transform origin, float reach, float originY, LayerMask layer)
    {
        _origin = origin;
        _reach = reach;
        _originY = originY;
        _layer = layer;
    }

    public void TryInteract()
    {
        if(UIManager.Instance == null || UIManager.Instance.HasActiveDialogue()) return;
        if(_origin == null) return;

        Vector3 rayOrigin = _origin.position + Vector3.up * _originY;
        if(Physics.Raycast(rayOrigin, _origin.forward, out RaycastHit hit, _reach, _layer, QueryTriggerInteraction.Ignore))
        {
            if(hit.transform.TryGetComponent(out IDialogueable dialogueable))
                UIManager.Instance.StartDialogue(dialogueable);
        }
    }
}
