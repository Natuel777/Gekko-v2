using UnityEngine;
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
        else if(altHeld && kb.digit8Key.wasPressedThisFrame) PurifyNearestCarnivorousPlant();
        else if(kb.digit1Key.wasPressedThisFrame) GameManager.Instance.RespawnAt(0);
        else if(kb.digit2Key.wasPressedThisFrame) GameManager.Instance.RespawnAt(1);
        else if(kb.digit3Key.wasPressedThisFrame) GameManager.Instance.RespawnAt(2);
        else if (kb.digit4Key.wasPressedThisFrame) GameManager.Instance.RespawnAt(3);
        else if (kb.digit5Key.wasPressedThisFrame) GameManager.Instance.RespawnAt(4);
        else if (kb.digit6Key.wasPressedThisFrame) GameManager.Instance.RespawnAt(5);
        else if (kb.digit7Key.wasPressedThisFrame) GameManager.Instance.RespawnAt(6);
        else if (kb.digit8Key.wasPressedThisFrame) GameManager.Instance.RespawnAt(7);
        else if (kb.digit9Key.wasPressedThisFrame) BreakAllPurificationCores();
        else if (kb.digit0Key.wasPressedThisFrame) GameManager.Instance.RespawnAt(9);
    }

    // Debug (Alt+8): purifica la planta carnívora sin purificar más cercana a Gekko (en el nivel 1, la del jefe).
    private void PurifyNearestCarnivorousPlant()
    {
        Vector3 from = GameManager.Instance != null && GameManager.Instance.Pj != null
            ? GameManager.Instance.Pj.transform.position : Vector3.zero;

        CarnivorousPlant nearest = null;
        float best = float.MaxValue;
        foreach(CarnivorousPlant plant in Object.FindObjectsByType<CarnivorousPlant>(FindObjectsSortMode.None))
        {
            if(plant.IsPurified) continue;
            float d = (plant.transform.position - from).sqrMagnitude;
            if(d < best) { best = d; nearest = plant; }
        }

        if(nearest == null) { Debug.Log("[Debug] Alt+8: no hay plantas carnívoras sin purificar."); return; }

        Debug.Log($"[Debug] Alt+8: purificando {nearest.name}.");
        nearest.DebugPurify();
    }

    // Debug: rompe de una todos los nucleos de purificacion activos en la escena, para poder
    // probar directamente el paso de purificar la planta sin tener que pegarles a mano.
    private void BreakAllPurificationCores()
    {
        foreach(PurificationCore core in Object.FindObjectsByType<PurificationCore>(FindObjectsSortMode.None))
        {
            int guard = 0;
            while(!core.IsBroken && guard++ < 20) core.Damage(0f);
        }
    }
}
