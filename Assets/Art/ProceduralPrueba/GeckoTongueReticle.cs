using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// Retícula de apuntado para la lengua: un punto en pantalla que sigue el mouse (o queda
/// en el centro si el cursor está bloqueado — mismo criterio que GeckoTongue.AimRay(), para
/// que la retícula esté SIEMPRE donde realmente apunta la lengua) y cambia de color según
/// GeckoTongue.CurrentAimTarget, para que el jugador sepa ANTES de disparar si lo que tiene
/// enfrente es comida, un punto de swing válido, o nada.
/// </summary>
public class GeckoTongueReticle : MonoBehaviour
{
    [Tooltip("Si se deja vacío, se busca solo el primer GeckoTongue de la escena.")]
    [SerializeField] private GeckoTongue _tongue;
    [SerializeField] private RectTransform _rect;
    [SerializeField] private Image _image;

    [Header("Colores por estado")]
    [SerializeField] private Color _colorNone = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private Color _colorEdible = new Color(1f, 0.8f, 0.2f, 0.9f);
    [SerializeField] private Color _colorGrapplePoint = new Color(0.3f, 1f, 0.5f, 0.9f);
    [SerializeField] private Color _colorInvalid = new Color(1f, 0.3f, 0.3f, 0.6f);

    private void Awake()
    {
        if (_tongue == null) _tongue = FindAnyObjectByType<GeckoTongue>();
        if (_rect == null) _rect = GetComponent<RectTransform>();
        if (_image == null) _image = GetComponent<Image>();
    }

    private void Update()
    {
        if (_tongue == null) return;

        // Mismo criterio que GeckoTongue.AimRay(): si hay mouse libre, apunta ahí; si el
        // cursor está bloqueado (o no hay mouse), se queda en el centro de la pantalla.
        Vector2 screenPos;
        var mouse = Mouse.current;
        if (mouse != null && Cursor.lockState != CursorLockMode.Locked)
            screenPos = mouse.position.ReadValue();
        else
            screenPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        if (_rect != null) _rect.position = screenPos;

        if (_image == null) return;
        switch (_tongue.CurrentAimTarget)
        {
            case GeckoTongue.AimTargetKind.Edible:
            case GeckoTongue.AimTargetKind.Interactable: _image.color = _colorEdible; break;
            case GeckoTongue.AimTargetKind.GrapplePoint: _image.color = _colorGrapplePoint; break;
            case GeckoTongue.AimTargetKind.InvalidSurface: _image.color = _colorInvalid; break;
            default: _image.color = _colorNone; break;
        }
    }
}
