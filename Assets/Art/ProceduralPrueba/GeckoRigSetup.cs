using UnityEngine;

/// <summary>
/// Correcciones de rig que se aplican POR CÓDIGO en Awake, sin tocar el .fbx.
///
/// El modelo base viene en pose T con las patas muy CORTAS: el hombro queda más
/// alto que el largo de la pata, así que el pie nunca llega bien al piso y el IK
/// se satura (el pie se da vuelta). Acá alargamos los huesos de las patas en
/// runtime — reposicionando Knee y Ankle respecto de su padre — para que la pata
/// alcance el piso con la rodilla flexionada y el pie apoye plano.
///
/// Se ejecuta ANTES que RigBuilder (ver DefaultExecutionOrder) para que el rig
/// tome la pose ya corregida como referencia.
/// </summary>
[DefaultExecutionOrder(-200)]
public class GeckoRigSetup : MonoBehaviour
{
    [Header("Alargado de patas (1 = sin cambio)")]
    [Tooltip("Factor por el que se aleja Knee/Ankle de su padre en las patas DELANTERAS.")]
    [SerializeField] private float _frontLegStretch = 1.35f;
    [Tooltip("Factor por el que se aleja Knee/Ankle de su padre en las patas TRASERAS.")]
    [SerializeField] private float _backLegStretch = 1.2f;

    [Header("Huesos (se autocompletan por nombre si quedan vacíos)")]
    [SerializeField] private Transform _kneeFL, _ankleFL;
    [SerializeField] private Transform _kneeFR, _ankleFR;
    [SerializeField] private Transform _kneeBL, _ankleBL;
    [SerializeField] private Transform _kneeBR, _ankleBR;

    private void Awake()
    {
        AutoFill();

        StretchSegment(_kneeFL, _frontLegStretch);
        StretchSegment(_ankleFL, _frontLegStretch);
        StretchSegment(_kneeFR, _frontLegStretch);
        StretchSegment(_ankleFR, _frontLegStretch);

        StretchSegment(_kneeBL, _backLegStretch);
        StretchSegment(_ankleBL, _backLegStretch);
        StretchSegment(_kneeBR, _backLegStretch);
        StretchSegment(_ankleBR, _backLegStretch);
    }

    private static void StretchSegment(Transform bone, float factor)
    {
        if (bone == null || Mathf.Approximately(factor, 1f)) return;
        bone.localPosition *= factor;
    }

    private void AutoFill()
    {
        if (_kneeFL == null) _kneeFL = Find("Gecko_Knee_F_L");
        if (_ankleFL == null) _ankleFL = Find("Gecko_Ankle_F_L");
        if (_kneeFR == null) _kneeFR = Find("Gecko_Knee_F_R");
        if (_ankleFR == null) _ankleFR = Find("Gecko_Ankle_F_R");
        if (_kneeBL == null) _kneeBL = Find("Gecko_Knee_B_L");
        if (_ankleBL == null) _ankleBL = Find("Gecko_Ankle_B_L");
        if (_kneeBR == null) _kneeBR = Find("Gecko_Knee_B_R");
        if (_ankleBR == null) _ankleBR = Find("Gecko_Ankle_B_R");
    }

    private Transform Find(string boneName)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == boneName) return t;
        return null;
    }
}
