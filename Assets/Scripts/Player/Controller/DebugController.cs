using UnityEngine.InputSystem;

public class DebugController
{
    public void ArtificialUpdate()
    {
        var kb = Keyboard.current;
        if(kb == null) return;

        bool altHeld = kb.leftAltKey.isPressed;

        if(altHeld && kb.digit1Key.wasPressedThisFrame) GameManager.Instance.LoadDebugLevel(1);
        else if(altHeld && kb.digit2Key.wasPressedThisFrame) GameManager.Instance.LoadDebugLevel(2);
        else if(altHeld && kb.digit3Key.wasPressedThisFrame) GameManager.Instance.LoadDebugLevel(3);
        else if(kb.digit1Key.wasPressedThisFrame) GameManager.Instance.RespawnAt(0);
        else if(kb.digit2Key.wasPressedThisFrame) GameManager.Instance.RespawnAt(1);
        else if(kb.digit3Key.wasPressedThisFrame) GameManager.Instance.RespawnAt(2);
        else if (kb.digit4Key.wasPressedThisFrame) GameManager.Instance.RespawnAt(3);
        else if (kb.digit5Key.wasPressedThisFrame) GameManager.Instance.RespawnAt(4);
        else if (kb.digit6Key.wasPressedThisFrame) GameManager.Instance.RespawnAt(5);
        else if (kb.digit7Key.wasPressedThisFrame) GameManager.Instance.RespawnAt(6);
        else if (kb.digit8Key.wasPressedThisFrame) GameManager.Instance.RespawnAt(7);
        else if (kb.digit9Key.wasPressedThisFrame) GameManager.Instance.RespawnAt(8);
        else if (kb.digit0Key.wasPressedThisFrame) GameManager.Instance.RespawnAt(9);
    }
}
