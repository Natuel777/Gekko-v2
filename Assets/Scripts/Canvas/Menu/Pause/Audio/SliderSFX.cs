using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class SliderSFX : AudioSlider
{
    protected override void InitialiceValues()
    {
        _slider.onValueChanged.AddListener(AudioManager.Instance.SetSFXVolume);
        _slider.value = AudioManager.Instance.sfxValue;
    }
}
