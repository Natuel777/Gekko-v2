using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SliderMusic : AudioSlider
{
    protected override void InitialiceValues()
    {
        _slider.onValueChanged.AddListener(AudioManager.Instance.SetMusicVolume);
        _slider.value = AudioManager.Instance.musicValue;
    }
}
