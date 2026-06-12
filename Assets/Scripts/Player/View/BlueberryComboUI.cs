using UnityEngine;
using TMPro;

public class BlueberryComboUI : MonoBehaviour
{
    [Header("Multiplier Badge")]
    [SerializeField] private GameObject _badgeRoot;
    [SerializeField] private TMP_Text   _multiplierText;

    [Header("Lightning (x5 only)")]
    [SerializeField] private GameObject _lightning;

    public void UpdateCombo(int comboCount, bool boostActive)
    {
        if (boostActive)
        {
            SetBadge(true, "x5");
            if (_lightning != null) _lightning.SetActive(true);
        }
        else if (comboCount > 0)
        {
            SetBadge(true, $"x{comboCount}");
            if (_lightning != null) _lightning.SetActive(false);
        }
        else
        {
            SetBadge(false, string.Empty);
            if (_lightning != null) _lightning.SetActive(false);
        }
    }

    private void SetBadge(bool show, string label)
    {
        if (_badgeRoot != null) _badgeRoot.SetActive(show);
        if (_multiplierText != null) _multiplierText.text = label;
    }
}
