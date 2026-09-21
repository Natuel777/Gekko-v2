using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Guarda/aplica los cambios de controles (binding overrides) del CharacterInput.
public static class InputRebindStore
{
    private static CharacterInput _editorInput;

    public static event Action OnChanged;

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

    public static void Save()
    {
        PlayerPrefs.SetString(PlayerPrefsKeys.inputOverridesKey, EditorAsset.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    public static void ResetAll()
    {
        EditorAsset.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey(PlayerPrefsKeys.inputOverridesKey);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }
}
