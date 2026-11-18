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
        // Todo sale de LevelStats: es lo de ESTE nivel, no el acumulado de la partida.
        var stats = LevelStats.Instance;
        if(stats) stats.StopTimer();

        if(_blueberryText) _blueberryText.text = Count(stats, _blueberryKey);
        if(_strawberryText) _strawberryText.text = Count(stats, _strawberryKey);
        if(_timeText) _timeText.text = stats ? stats.FormattedTime : "00:00";
        if(_bugText) _bugText.text = Count(stats, "Bug");
    }

    private static string Count(LevelStats stats, string key) =>
        stats ? $"{stats.GetCollected(key)}/{stats.GetTotal(key)}" : "0/0";

    public void BTN_Restart()
    {
        GameManager.Instance.RestartLvl();
    }
    public void BTN_NextLvl()
    {
        GameManager.Instance.NextLevel();
    }
}
