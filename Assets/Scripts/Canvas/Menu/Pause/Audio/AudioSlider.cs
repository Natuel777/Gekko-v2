using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public abstract class AudioSlider : MonoBehaviour
{
    [SerializeField] protected ScreenAudio _screen;
    protected Slider _slider;
    void Start()
    {
         _slider = GetComponent<Slider>();
        InitialiceValues();
    }
    protected abstract void InitialiceValues();

}
