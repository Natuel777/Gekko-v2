using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SliderMaster : AudioSlider
{
    protected override void InitialiceValues()
    {
        _slider.onValueChanged.AddListener(AudioManager.Instance.SetMasterVolume);
        _slider.value = AudioManager.Instance.masterValue;
    }

}
