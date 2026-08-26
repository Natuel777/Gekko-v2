using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class SliderSFX : AudioSlider
{
    protected override void InitialiceValues()
    {
        _slider.onValueChanged.AddListener(VolumeManager.Instance.SetSFXVolume);
        _slider.value = VolumeManager.Instance.sfxValue;
    }
}
