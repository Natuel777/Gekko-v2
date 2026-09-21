using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [SerializeField] private string _key;
    [SerializeField] private bool _uppercase;

    private TMP_Text _text;

    private void OnEnable()
    {
        Localizer.OnLanguageChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        Localizer.OnLanguageChanged -= Refresh;
    }

    public void Refresh()
    {
        if (!_text) _text = GetComponent<TMP_Text>();
        string value = Localizer.Get(_key);
        _text.text = _uppercase ? value.ToUpperInvariant() : value;
    }
}
