using UnityEngine;
using UnityEngine.UI;

public class ControlsSettingsPanel : MonoBehaviour
{
    [SerializeField] private RebindRow[] _rows;
    [SerializeField] private Button _resetButton;

    private void OnEnable()
    {
        if (_resetButton) _resetButton.onClick.AddListener(ResetControls);
    }

    private void OnDisable()
    {
        if (_resetButton) _resetButton.onClick.RemoveListener(ResetControls);
    }

    private void ResetControls()
    {
        InputRebindStore.ResetAll();
        foreach (var row in _rows) row.Refresh();
    }
}
