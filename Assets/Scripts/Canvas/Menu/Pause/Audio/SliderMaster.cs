using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SliderMaster : AudioSlider
{
    protected override void InitialiceValues()
    {
        _slider.onValueChanged.AddListener(AudioManager.instance.SetMasterVolume);
        _slider.value = AudioManager.instance.masterValue;
    }

}
