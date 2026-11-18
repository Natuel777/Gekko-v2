using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ControlsSettingsPanel : MonoBehaviour
{
    [SerializeField] private RebindRow[] _rows;
    [SerializeField] private Button _resetButton;

    [Header("Botón Aplicar")]
    [Tooltip("Guarda las teclas cambiadas y las aplica al juego. Con cambios pendientes se ve en rojo; sin cambios, apagado.")]
    [SerializeField] private Button _applyButton;
    [SerializeField] private TMP_Text _applyLabel;
    [SerializeField] private OptionsTab _applyStyle;
    [SerializeField] private Sprite _pendingSprite;
    [SerializeField] private Sprite _pendingHighlightSprite;
    [SerializeField] private Sprite _appliedSprite;
    [SerializeField] private Sprite _appliedHighlightSprite;

    private void OnEnable()
    {
        if (_resetButton) _resetButton.onClick.AddListener(ResetControls);
        if (_applyButton) _applyButton.onClick.AddListener(ApplyControls);
        InputRebindStore.OnPendingChanged += Refresh;
        Localizer.OnLanguageChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (_resetButton) _resetButton.onClick.RemoveListener(ResetControls);
        if (_applyButton) _applyButton.onClick.RemoveListener(ApplyControls);
        InputRebindStore.OnPendingChanged -= Refresh;
        Localizer.OnLanguageChanged -= Refresh;
    }

    // Restablecer: vuelve a las teclas originales en pantalla; queda pendiente hasta Aplicar.
    private void ResetControls()
    {
        InputRebindStore.ResetAll();
        foreach (var row in _rows) row.Refresh();
    }

    private void ApplyControls()
    {
        if (!InputRebindStore.HasPending) return;
        InputRebindStore.Apply();
        if (AudioManager.instance) AudioManager.instance.Play(SoundNames.UiButton);
    }

    private void Refresh()
    {
        foreach (var row in _rows) if (row) row.Refresh();

        bool pending = InputRebindStore.HasPending;
        if (_applyButton) _applyButton.interactable = pending;
        if (_applyLabel) _applyLabel.text = Localizer.Get(pending ? "apply" : "applied");
        if (_applyStyle)
        {
            if (pending) _applyStyle.SetSprites(_pendingSprite, _pendingHighlightSprite);
            else _applyStyle.SetSprites(_appliedSprite, _appliedHighlightSprite);
        }
    }
}
