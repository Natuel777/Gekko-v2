using UnityEngine;
using TMPro;

public class ScreenFinishLevel : Screens
{
    [Header("Resultados")]
    [SerializeField] private TMP_Text _blueberryText;
    [SerializeField] private TMP_Text _strawberryText;
    [SerializeField] private TMP_Text _timeText;

    [Header("Keys del registro (ojo: prefabs cruzados)")]
    [SerializeField] private string _blueberryKey = "PF_CollectableRasberry";   // arándano
    [SerializeField] private string _strawberryKey = "PF_CollectableBlueberry"; // frutilla

    private new void Awake()
    {
        base.Awake();
        var stats = LevelStats.Instance;

        int bbGot = CollectiblesRegister.GetCollectibleCount(_blueberryKey);
        int swGot = CollectiblesRegister.GetCollectibleCount(_strawberryKey);

        if (_blueberryText) _blueberryText.text = $"x {bbGot} / {(stats ? stats.GetTotal(_blueberryKey) : 0)}";
        if (_strawberryText) _strawberryText.text = $"x {swGot} / {(stats ? stats.GetTotal(_strawberryKey) : 0)}";
        if (_timeText) _timeText.text = stats ? stats.FormattedTime : "00:00";
    }

    public void BTN_Restart()
    {
        GameManager.Instance.RestartLvl();
    }
}
