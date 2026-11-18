using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Guarda/aplica los cambios de controles (binding overrides) del CharacterInput.
// Flujo con botón Aplicar: el menú reasigna sobre EditorAsset y marca "pendiente" (MarkPending). Recién Apply() guarda en
// PlayerPrefs y se los aplica al jugador. Si se sale de Opciones sin aplicar, DiscardPending() vuelve a lo guardado.
public static class InputRebindStore
{
    private static CharacterInput _editorInput;
    private static bool _pending;

    // Se guardaron cambios nuevos (los escucha PlayerInputs para recargar sus teclas).
    public static event Action OnChanged;
    // Cambió el estado pendiente/aplicado (para el botón Aplicar y para refrescar las filas del menú).
    public static event Action OnPendingChanged;

    public static bool HasPending => _pending;

    // Instancia usada solo por el menu de opciones para reasignar teclas (nunca se habilita).
    public static InputActionAsset EditorAsset
    {
        get
        {
            if (_editorInput == null)
            {
                _editorInput = new CharacterInput();
                ApplySaved(_editorInput.asset);
            }
            return _editorInput.asset;
        }
    }

    public static void ApplySaved(InputActionAsset asset)
    {
        string json = PlayerPrefs.GetString(PlayerPrefsKeys.inputOverridesKey, "");
        if (string.IsNullOrEmpty(json)) asset.RemoveAllBindingOverrides();
        else asset.LoadBindingOverridesFromJson(json);
    }

    // Una tecla cambió en el menú pero todavía no se aplicó.
    public static void MarkPending()
    {
        _pending = true;
        OnPendingChanged?.Invoke();
    }

    // Botón Aplicar: guarda y aplica al jugador. Además del evento, se lo aplica directo al jugador actual.
    public static void Apply()
    {
        Save();
        if (GameManager.Instance != null && GameManager.Instance.Pj != null)
            GameManager.Instance.Pj.ReloadInputBindings();
        _pending = false;
        OnPendingChanged?.Invoke();
    }

    // Salir de Opciones sin aplicar: el menú vuelve a mostrar (y a usar) lo que estaba guardado.
    public static void DiscardPending()
    {
        if (!_pending) return;
        ApplySaved(EditorAsset);
        _pending = false;
        OnPendingChanged?.Invoke();
    }

    public static void Save()
    {
        PlayerPrefs.SetString(PlayerPrefsKeys.inputOverridesKey, EditorAsset.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    // Restablecer: vuelve a las teclas originales en el menú; queda pendiente hasta Aplicar.
    public static void ResetAll()
    {
        EditorAsset.RemoveAllBindingOverrides();
        MarkPending();
    }
}
