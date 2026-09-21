using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class RebindRow : MonoBehaviour
{
    [SerializeField] private string _actionName;
    [SerializeField] private string _compositePart; // vacio si la accion no es un composite (ej: "up" para Movement)
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _valueText;

    private InputActionRebindingExtensions.RebindingOperation _operation;
    private bool _waiting;

    private void OnEnable()
    {
        if (_button) _button.onClick.AddListener(StartRebind);
        Localizer.OnLanguageChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (_button) _button.onClick.RemoveListener(StartRebind);
        Localizer.OnLanguageChanged -= Refresh;
        CancelOperation();
    }

    public void Refresh()
    {
        if (!_valueText || _waiting) return;
        var action = GetAction();
        int index = FindBindingIndex(action);
        _valueText.text = index >= 0
            ? action.GetBindingDisplayString(index, InputBinding.DisplayStringOptions.DontIncludeInteractions)
            : "-";
    }

    private InputAction GetAction() => InputRebindStore.EditorAsset.FindAction("Character/" + _actionName);

    private int FindBindingIndex(InputAction action)
    {
        if (action == null) return -1;
        var bindings = action.bindings;
        for (int i = 0; i < bindings.Count; i++)
        {
            if (string.IsNullOrEmpty(_compositePart))
            {
                if (!bindings[i].isComposite && !bindings[i].isPartOfComposite) return i;
            }
            else if (bindings[i].isPartOfComposite && bindings[i].name == _compositePart)
            {
                return i;
            }
        }
        return -1;
    }

    private void StartRebind()
    {
        if (_waiting) return;
        var action = GetAction();
        int index = FindBindingIndex(action);
        if (index < 0) return;

        _waiting = true;
        _valueText.text = Localizer.Get("ctrl_press_key");
        if (EventSystem.current) EventSystem.current.sendNavigationEvents = false;

        _operation = action.PerformInteractiveRebinding(index)
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Mouse>/delta")
            .WithControlsExcluding("<Mouse>/scroll")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnComplete(op => FinishRebind(true))
            .OnCancel(op => FinishRebind(false))
            .Start();
    }

    private void FinishRebind(bool changed)
    {
        CancelOperation();
        _waiting = false;
        if (changed) InputRebindStore.Save();
        Refresh();
        StartCoroutine(RestoreNavigation());
    }

    // Un frame de espera para que la tecla elegida (ej. Enter/Espacio) no vuelva a activar el boton.
    private IEnumerator RestoreNavigation()
    {
        yield return null;
        if (EventSystem.current) EventSystem.current.sendNavigationEvents = true;
    }

    private void CancelOperation()
    {
        if (_operation == null) return;
        _operation.Dispose();
        _operation = null;
    }
}
