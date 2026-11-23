using UnityEngine;

// Ajustes del haz de luz de GrappleHighlight por tipo de gancho (uno para las bellotas/GrapplePoint, otro para las
// mariquitas/Gancho). Se tocan acá, no en cada prefab.
[CreateAssetMenu(fileName = "GrappleHighlightDataSO", menuName = "Scriptable Objects/GrappleHighlightDataSO")]
public class GrappleHighlightDataSO : ScriptableObject
{
    [Tooltip("Color del haz.")]
    [ColorUsage(false, true)]
    public Color color = new Color(1f, 0.75f, 0.25f, 1f);

    [Header("Haz de luz")]
    [Tooltip("Shader Gekko/FX/Light Beam. Vacío = sin haz.")]
    public Shader beamShader;
    [Min(0f)] public float beamHeight = 7f;
    [Min(0f)] public float beamWidth = 0.5f;
    [Tooltip("Qué tan fuerte se ve el haz (es aditivo: más bajo = más transparente).")]
    [Min(0f)] public float beamIntensity = 0.3f;
    [Tooltip("Distancias a la cámara en las que el haz se desvanece (para no tapar la vista al engancharse).")]
    [Min(0f)] public float beamFadeStart = 7f;
    [Min(0f)] public float beamFadeEnd = 3f;
}
