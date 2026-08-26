using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SliderMaster : AudioSlider
{
    protected override void InitialiceValues()
    {
        _slider.onValueChanged.AddListener(VolumeManager.Instance.SetMasterVolume);
        _slider.value = VolumeManager.Instance.masterValue;
    }

}
