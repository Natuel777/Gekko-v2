// COPIA DE REFERENCIA portada desde la branch "cin", sin conectar a ninguna escena.
// NO reemplaza a Assets/Scripts/Player/Controller/InteractionManager.cs, que sigue intacto.
// Clase renombrada a GeckoInteractionManager (y su referencia a PlayerController apunta a la
// copia GeckoPlayerController) solo para poder compilar sin pisar el InteractionManager real.
// Es material de referencia para revisar/mergear a mano.
using UnityEngine;

public class GeckoInteractionManager : MonoBehaviour
{
    private bool _canInteract = true;
    private GeckoPlayerController _controller;
    [SerializeField] private float _interactionRange = 1f;
    private IInteractable _currentInteractable;
    private void Update()
    {
        _canInteract = _controller.CanInteract;
        CheckInteraction();
    }
    private void CheckInteraction()
    {
        if (!_canInteract)
        {
            DeactivateUI();
            return;
        }

        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, _interactionRange))
            if (hit.collider.TryGetComponent(out IInteractable interactable))
            {
                _currentInteractable = interactable;

                _currentInteractable.ShowInteractUI();
                return;
            }
        if (_currentInteractable != null) DeactivateUI();
    }
    public void Interact()
    {
        if (!_canInteract) return;
        _currentInteractable?.Interacted();
    }
    private void DeactivateUI()
    {
        if (_currentInteractable != null) _currentInteractable.HideInteractUI();
        _currentInteractable = null;
    }
    public void GetPlayerController(GeckoPlayerController _pjController) => _controller = _pjController;
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * _interactionRange);
    }
}
