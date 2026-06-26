using UnityEngine;
using TMPro;

public class ScreenFinishLevel : Screens
{
    [Header("Resultados")]
    [SerializeField] private TMP_Text _blueberryText;
    [SerializeField] private TMP_Text _strawberryText;
    [SerializeField] private TMP_Text _timeText;
    [SerializeField] private TMP_Text _bugText;

    [Header("Keys del registro (= _name del NotificationSO)")]
    [SerializeField] private string _blueberryKey = "BlueBerry";  // arándano
    [SerializeField] private string _strawberryKey = "RaspBerry"; // frutilla

    private new void Awake()
    {
        base.Awake();
        var stats = LevelStats.Instance;

        int bbGot = CollectiblesRegister.GetCollectibleCount(_blueberryKey);
        int swGot = CollectiblesRegister.GetCollectibleCount(_strawberryKey);
        int bugsEaten = CollectiblesRegister.GetCollectibleCount("Bug");

        if (_blueberryText) _blueberryText.text = $"x {bbGot} / {(stats ? stats.GetTotal(_blueberryKey) : 0)}";
        if (_strawberryText) _strawberryText.text = $"x {swGot} / {(stats ? stats.GetTotal(_strawberryKey) : 0)}";
        if (_timeText) _timeText.text = stats ? stats.FormattedTime : "00:00";
        if (_bugText) _bugText.text = $"x {bugsEaten} / {(stats ? stats.GetTotal("Bug") : 0)}";
    }

    public void BTN_Restart()
    {
        GameManager.Instance.RestartLvl();
    }
}
