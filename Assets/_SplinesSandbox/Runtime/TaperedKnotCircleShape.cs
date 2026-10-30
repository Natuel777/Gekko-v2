using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

// Perfil circular para SplineMesh.Extrude (ver SplineExtrudeTapered.cs) que soporta:
// - un multiplicador de radio independiente por knot (agrandar un knot no mueve el radio
//   propio del knot siguiente, solo reinterpola el tramo de malla entre ambos).
// - un angostamiento hacia 0 (punta) cerca del inicio y/o el final de la spline.
// SetSegment se llama una vez por anillo de vertices (ver IExtrudeShape), asi que el calculo
// de "cuanto mide el radio en este punto" vive ahi; GetPosition solo aplica ese multiplicador
// al circulo unitario.
[Serializable]
internal class TaperedKnotCircleShape : IExtrudeShape
{
    int m_Sides = 8;
    float m_Rads;

    List<float> m_KnotRadiusMultipliers;
    int m_KnotSegmentCount;
    bool m_Closed;

    bool m_TaperStart;
    bool m_TaperEnd;
    float m_TaperLength = 0.12f;

    float m_CurrentMultiplier = 1f;

    public int SideCount
    {
        get => m_Sides;
        set => m_Sides = value;
    }

    public void Configure(List<float> knotRadiusMultipliers, bool taperStart, bool taperEnd, float taperLength)
    {
        m_KnotRadiusMultipliers = knotRadiusMultipliers;
        m_TaperStart = taperStart;
        m_TaperEnd = taperEnd;
        m_TaperLength = Mathf.Max(taperLength, 0.0001f);
    }

    public void Setup(ISpline path, int segmentCount)
    {
        m_Rads = math.radians(360f / SideCount);
        m_Closed = path.Closed;

        int knotCount = m_KnotRadiusMultipliers?.Count ?? 0;
        m_KnotSegmentCount = m_Closed ? knotCount : Mathf.Max(knotCount - 1, 1);
    }

    public void SetSegment(int index, float t, float3 position, float3 tangent, float3 up)
    {
        m_CurrentMultiplier = EvaluateKnotRadius(t) * EvaluateTaper(t);
    }

    float EvaluateKnotRadius(float t)
    {
        if(m_KnotRadiusMultipliers == null || m_KnotRadiusMultipliers.Count == 0)
            return 1f;

        if(m_KnotRadiusMultipliers.Count == 1)
            return m_KnotRadiusMultipliers[0];

        float knotT = Mathf.Clamp01(t) * m_KnotSegmentCount;
        int i0 = Mathf.Clamp(Mathf.FloorToInt(knotT), 0, m_KnotRadiusMultipliers.Count - 1);
        int i1 = m_Closed
            ? (i0 + 1) % m_KnotRadiusMultipliers.Count
            : Mathf.Min(i0 + 1, m_KnotRadiusMultipliers.Count - 1);
        float frac = knotT - Mathf.Floor(knotT);

        return Mathf.Lerp(m_KnotRadiusMultipliers[i0], m_KnotRadiusMultipliers[i1], frac);
    }

    float EvaluateTaper(float t)
    {
        float factor = 1f;

        if(m_TaperStart)
            factor *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / m_TaperLength));

        if(m_TaperEnd)
            factor *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - t) / m_TaperLength));

        return factor;
    }

    public float2 GetPosition(float t, int index)
    {
        return new float2(math.cos(index * m_Rads), math.sin(index * m_Rads)) * m_CurrentMultiplier;
    }
}
