using UnityEngine;

public class SoundSettingsPanel : MonoBehaviour
{
    [SerializeField] private ArrowValueSelector _musicSelector;

    private void OnEnable()
    {
        Localizer.OnLanguageChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        Localizer.OnLanguageChanged -= Refresh;
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
}
