using System.Collections.Generic;
using UnityEngine;

public class InteractionManager : MonoBehaviour
{
    private bool _canInteract = true;
    private PlayerController _controller;
    [SerializeField] private float _interactionRange = 1f, _interactionAngle = 30;
    [SerializeField] private LayerMask _obstacle, _interactable;
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
        Transform target = GetBestTarget(transform, _interactionRange, _interactionAngle, _interactable, _obstacle);
        Debug.Log(target);
        if (target != null && target.TryGetComponent(out IInteractable interactable))
        {
            Debug.Log(interactable);
            _currentInteractable = interactable;
            _currentInteractable.ShowInteractUI();
            return;
        }

        DeactivateUI();
    }
    private  Transform GetBestTarget(Transform startPos, float viewRange, float viewAngle, LayerMask interactable, LayerMask obstacle)
    {
        Collider[] hits = Physics.OverlapSphere(startPos.position, viewRange, interactable);
        Transform best = null;
        float bestAngle = float.MaxValue;

        foreach (var hit in hits)
        {

            if (!StaticMethods.InFOV(startPos, hit.transform.position, viewRange, viewAngle, obstacle))
                continue;

            float angle = Vector3.Angle(startPos.forward, hit.transform.position - startPos.position);
            if (angle < bestAngle)
            {
                bestAngle = angle;
                best = hit.transform;
            }
        }

        return best;
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
    private void OnDrawGizmosSelected()
    {
        // Radio de interacción
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _interactionRange);

        // Cono del ángulo de visión
        Gizmos.color = Color.cyan;
        Vector3 forward = transform.forward;
        float halfAngle = _interactionAngle / 2f;

        Quaternion leftRotation = Quaternion.AngleAxis(-halfAngle, transform.up);
        Quaternion rightRotation = Quaternion.AngleAxis(halfAngle, transform.up);

        Vector3 leftDir = leftRotation * forward;
        Vector3 rightDir = rightRotation * forward;

        Gizmos.DrawLine(transform.position, transform.position + leftDir * _interactionRange);
        Gizmos.DrawLine(transform.position, transform.position + rightDir * _interactionRange);
        Gizmos.DrawLine(transform.position, transform.position + forward * _interactionRange);
    }
}
