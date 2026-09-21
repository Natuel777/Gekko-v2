using TMPro;
using UnityEngine;

public class OptionsTab : MonoBehaviour
{
    [SerializeField] private GameObject _underline;
    [SerializeField] private TextMeshProUGUI _label;
    [SerializeField] private Color _selectedColor = Color.white;
    [SerializeField] private Color _unselectedColor = new Color(1f, 1f, 1f, 0.5f);

    public void SetSelected(bool selected)
    {
        if (_underline) _underline.SetActive(selected);
        if (_label) _label.color = selected ? _selectedColor : _unselectedColor;
    }
}
