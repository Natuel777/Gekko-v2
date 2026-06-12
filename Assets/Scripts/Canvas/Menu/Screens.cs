using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public abstract class Screens : MonoBehaviour, IScreen
{
    protected Button[] _buttons;
    protected void Awake()
    {
        _buttons = GetComponentsInChildren<Button>();

        foreach (var button in _buttons)
        {
            button.interactable = false;
        }
    }
    public virtual void BTN_Back()
    {
        ScreenManager.Instance.ButtonSound.Play();
        ScreenManager.Instance.Pop();
    }
    public virtual void Activate()
    {
        foreach (var button in _buttons)
        {
            button.interactable = true;
        }
    }

    public virtual void Deactivate()
    {
        foreach (var button in _buttons)
        {
            button.interactable = false;
        }
    }

    public virtual void Free()
    {
        Destroy(gameObject);
    }
}
