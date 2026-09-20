using UnityEngine;

public class GameSettingsPanel : MonoBehaviour
{
    [SerializeField] private ArrowValueSelector _languageSelector;

    private void OnEnable()
    {
        if (!_languageSelector) return;

        _languageSelector.SetOptions(new[] { "Español", "English" }, (int)Localizer.Current);
        _languageSelector.OnIndexChanged.RemoveListener(SetLanguage);
        _languageSelector.OnIndexChanged.AddListener(SetLanguage);
    }

    private void SetLanguage(int index)
    {
        Localizer.SetLanguage((GameLanguage)index);
    }
}
