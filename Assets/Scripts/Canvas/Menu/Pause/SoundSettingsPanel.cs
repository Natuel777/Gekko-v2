using UnityEngine;
using UnityEngine.UI;

public class SoundSettingsPanel : MonoBehaviour
{
    [SerializeField] private ArrowValueSelector _musicSelector;
    [Tooltip("Restablecer sonido: vuelve los volúmenes y la música a los valores del prefab AudioManager.")]
    [SerializeField] private Button _resetButton;

    private void OnEnable()
    {
        Localizer.OnLanguageChanged += Refresh;
        if (_resetButton) _resetButton.onClick.AddListener(ResetSound);
        Refresh();
    }

    private void OnDisable()
    {
        Localizer.OnLanguageChanged -= Refresh;
        if (_resetButton) _resetButton.onClick.RemoveListener(ResetSound);
    }

    private void Refresh()
    {
        if (!_musicSelector || !AudioManager.instance) return;

        _musicSelector.SetOptions(new[] { Localizer.Get("on"), Localizer.Get("off") }, AudioManager.instance.musicEnabled ? 0 : 1);
        _musicSelector.OnIndexChanged.RemoveListener(SetMusic);
        _musicSelector.OnIndexChanged.AddListener(SetMusic);
    }

    private void SetMusic(int index)
    {
        AudioManager.instance.SetMusicEnabled(index == 0);
    }

    private void ResetSound()
    {
        var audio = AudioManager.instance;
        if (!audio) return;

        audio.ResetToDefaults();

        // Los sliders se cargan solos en su Start (AudioSlider): acá solo se actualiza lo que muestran.
        foreach (var slider in GetComponentsInChildren<AudioSlider>(true))
        {
            var ui = slider.GetComponent<Slider>();
            if (!ui) continue;
            if (slider is SliderMaster) ui.SetValueWithoutNotify(audio.masterValue);
            else if (slider is SliderMusic) ui.SetValueWithoutNotify(audio.musicValue);
            else if (slider is SliderSFX) ui.SetValueWithoutNotify(audio.sfxValue);
        }

        Refresh();
        audio.Play(SoundNames.UiButton);
    }
}
