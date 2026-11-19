using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class ScreenFinishLevel : Screens
{
    [Header("Resultados")]
    [SerializeField] private TMP_Text _blueberryText;
    [SerializeField] private TMP_Text _strawberryText;
    [SerializeField] private TMP_Text _timeText;
    [SerializeField] private TMP_Text _bugText;

    [Header("Purificación de la zona (coleccionables juntados)")]
    [SerializeField] private Slider _purificationSlider;
    [SerializeField] private TMP_Text _purificationPercentText;

    [Header("Animación")]
    [Tooltip("Espera antes de que empiecen a contar los números (deja ver el pop de la tarjeta).")]
    [SerializeField] private float _countDelay = 0.35f;
    [SerializeField] private float _countDuration = 0.8f;
    [SerializeField] private float _barDuration = 0.8f;

    [Header("Foco inicial (teclado/joystick)")]
    [SerializeField] private Selectable _focusOnOpen;

    [Header("Keys del registro (= _name del NotificationSO)")]
    [SerializeField] private string _blueberryKey = "BlueBerry";  // arándano
    [SerializeField] private string _strawberryKey = "RaspBerry"; // frutilla

    private int _blueGot, _blueTotal, _strawGot, _strawTotal;
    private float _seconds;
    private float _purification; // 0..1
    private Coroutine _reveal;

    private new void Awake()
    {
        base.Awake();
        // Todo sale de LevelStats: es lo de ESTE nivel, no el acumulado de la partida.
        var stats = LevelStats.Instance;
        if(stats) stats.StopTimer();

        _blueGot = stats ? stats.GetCollected(_blueberryKey) : 0;
        _blueTotal = stats ? stats.GetTotal(_blueberryKey) : 0;
        _strawGot = stats ? stats.GetCollected(_strawberryKey) : 0;
        _strawTotal = stats ? stats.GetTotal(_strawberryKey) : 0;
        _seconds = stats ? stats.ElapsedTime : 0f;
        _purification = ComputePurification();

        // Valores finales de entrada (por si no hay animación); Activate los anima desde 0.
        ShowValues(1f, 1f);
        if(_bugText) _bugText.text = stats ? $"{stats.GetCollected("Bug")}/{stats.GetTotal("Bug")}" : "0/0";
    }

    public override void Activate()
    {
        base.Activate();
        if(_focusOnOpen && EventSystem.current) EventSystem.current.SetSelectedGameObject(_focusOnOpen.gameObject);
        if(_reveal != null) StopCoroutine(_reveal);
        _reveal = StartCoroutine(Reveal());
    }

    // Promedio de lo juntado sobre el total de arándanos y frutillas (solo cuenta los que el nivel tiene).
    private float ComputePurification()
    {
        float sum = 0f; int parts = 0;
        if(_blueTotal > 0) { sum += Mathf.Clamp01((float)_blueGot / _blueTotal); parts++; }
        if(_strawTotal > 0) { sum += Mathf.Clamp01((float)_strawGot / _strawTotal); parts++; }
        return parts > 0 ? sum / parts : 1f;
    }

    // Primero cuentan los números, después se llena la barra. Tiempo sin escala por si el juego quedó pausado.
    private IEnumerator Reveal()
    {
        ShowValues(0f, 0f);
        float t = 0f;
        while(t < _countDelay) { t += Time.unscaledDeltaTime; yield return null; }

        t = 0f;
        while(t < _countDuration)
        {
            t += Time.unscaledDeltaTime;
            ShowValues(Ease(t / _countDuration), 0f);
            yield return null;
        }

        t = 0f;
        while(t < _barDuration)
        {
            t += Time.unscaledDeltaTime;
            ShowValues(1f, Ease(t / _barDuration));
            yield return null;
        }
        ShowValues(1f, 1f);
        _reveal = null;
    }

    private void ShowValues(float counts, float bar)
    {
        if(_blueberryText) _blueberryText.text = $"{Mathf.RoundToInt(_blueGot * counts)}/{_blueTotal}";
        if(_strawberryText) _strawberryText.text = $"{Mathf.RoundToInt(_strawGot * counts)}/{_strawTotal}";
        if(_timeText)
        {
            int s = Mathf.FloorToInt(_seconds * counts);
            _timeText.text = $"{s / 60:00}:{s % 60:00}";
        }
        float p = _purification * bar;
        if(_purificationSlider) _purificationSlider.SetValueWithoutNotify(p);
        if(_purificationPercentText) _purificationPercentText.text = $"{Mathf.RoundToInt(p * 100f)}%";
    }

    private static float Ease(float x)
    {
        x = Mathf.Clamp01(x);
        return 1f - Mathf.Pow(1f - x, 3f);
    }

    public void BTN_Restart()
    {
        GameManager.Instance.RestartLvl();
    }
    public void BTN_NextLvl()
    {
        GameManager.Instance.NextLevel();
    }
}
