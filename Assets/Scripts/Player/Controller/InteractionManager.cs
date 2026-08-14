using UnityEngine;

public class InteractionManager : MonoBehaviour
{
    private bool _canInteract = true;
    private PlayerController _controller;
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
    public void GetPlayerController(PlayerController _pjController) => _controller = _pjController;
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * _interactionRange);
    }
}
