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

    /// <summary>
    /// Resuelve los huesos por JERARQUIA, no por nombre.
    ///
    /// El FBX tiene nombres duplicados: la rodilla delantera derecha se llama
    /// "Gecko_Knee_B_L" igual que la trasera izquierda, y no existe ningun
    /// "Gecko_Knee_F_R". Buscando por nombre, esta clase estiraba dos veces la misma
    /// pata y dejaba otras dos sin tocar, y el bicho quedaba asimetrico.
    ///
    /// Los muslos SI tienen nombre unico, asi que se arranca de ahi y se baja por el
    /// primer hijo: muslo -> rodilla -> tobillo.
    /// </summary>
    private void AutoFill()
    {
        ResolveLeg("Gecko_Thigh_F_L", ref _kneeFL, ref _ankleFL);
        ResolveLeg("Gecko_Thigh_F_R", ref _kneeFR, ref _ankleFR);
        ResolveLeg("Gecko_Thigh_B_L", ref _kneeBL, ref _ankleBL);
        ResolveLeg("Gecko_Thigh_B_R", ref _kneeBR, ref _ankleBR);
    }

    private void ResolveLeg(string thighName, ref Transform knee, ref Transform ankle)
    {
        if (knee != null && ankle != null) return;

        Transform thigh = Find(thighName);
        if (thigh == null || thigh.childCount == 0) return;

        Transform k = thigh.GetChild(0);
        if (knee == null) knee = k;
        if (ankle == null && k.childCount > 0) ankle = k.GetChild(0);
    }

    private Transform Find(string boneName)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == boneName) return t;
        return null;
    }
}
