using UnityEngine;
using UnityEngine.UI;

// Prompt de "mantene E para saltear" que PurificationChallenge prende mientras hay un timeline
// de la planta reproduciendose y el jugador mantiene apretado Interactuar.
public class SkipCinematicPrompt : MonoBehaviour
{
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private Image _fill;

    public void SetVisible(bool visible)
    {
        if(_group != null) _group.alpha = visible ? 1f : 0f;
    }

    public void SetProgress(float t01)
    {
        if(_fill != null) _fill.fillAmount = Mathf.Clamp01(t01);
    }
}
