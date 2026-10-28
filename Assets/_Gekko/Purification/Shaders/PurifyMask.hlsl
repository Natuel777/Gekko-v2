#ifndef GEKKO_PURIFY_MASK_INCLUDED
#define GEKKO_PURIFY_MASK_INCLUDED

// Máscara de purificación por regiones.
//
// Cada nodo de cada PurifyRegion es una esfera con:
//  - un ÁREA (radio máximo): adentro está corrupto, afuera no. Lo que no cae en ninguna región se ve SANO, porque
//    en el nivel hay zonas que nunca estuvieron corruptas (y así una escena sin PurifyManager no cambia nada).
//  - una ONDA (radio actual): avanza desde el centro y limpia el área. 0 = la región todavía no se purificó.
//
// Mask: 1 = sano, 0 = corrupto. Edge: banda brillante en el frente de la onda (0..1).
//
// Se usa de dos formas:
//  - Shader Graph: nodo Custom Function (modo File) con PurifyMaskAuto o PurifyMask.
//  - Shaders HLSL a mano (GekkoPathBlend, GekkoGrass): #include de este archivo y llamar PurifyMaskAuto_float.
// Conviene usar siempre PurifyMaskAuto: calcula el ruido acá adentro, así el borde de la onda coincide entre el
// piso, las hojas y el pasto aunque sean shaders distintos.

#define PURIFY_MAX_SPHERES 32 // igual que PurifyManager.MaxEsferas

// Globales: los sube PurifyManager con Shader.SetGlobal*. NO van en el Blackboard de Shader Graph ni en el
// CBUFFER del material.
float4 _PurifySpheres[PURIFY_MAX_SPHERES];      // xyz centro, w radio actual de la onda (0 = sin purificar)
float4 _PurifySphereParams[PURIFY_MAX_SPHERES]; // x radio máximo (área corrupta), yzw libres
float  _PurifySphereCount;
float  _PurifyEdgeWidth;                        // ancho del frente de la onda, en metros
float  _PurifyAreaFade;                         // ancho del fundido en el borde del área corrupta, en metros

// ---------- Ruido (en mundo, igual para todos los materiales) ----------

float PurifyHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float PurifyValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float a = PurifyHash(i);
    float b = PurifyHash(i + float2(1, 0));
    float c = PurifyHash(i + float2(0, 1));
    float d = PurifyHash(i + float2(1, 1));

    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

// 0..1, tres octavas (parecido al Simple Noise de Shader Graph).
float PurifyNoise(float2 xz, float scale)
{
    float2 p = xz * scale;
    float n = PurifyValueNoise(p) * 0.5
            + PurifyValueNoise(p * 2.0) * 0.25
            + PurifyValueNoise(p * 4.0) * 0.125;
    return n / 0.875;
}

// ---------- Máscara ----------

// NoiseValue (0..1) deforma el área y el frente de la onda hasta ±NoiseAmount metros.
// Purified: cuánto limpió la onda (1 solo dentro de una región ya alcanzada; 0 afuera de toda región). Lo usan
// los materiales que YA son corruptos (arte pintado a mano) para volver a su color sano sin tocar lo de afuera.
void PurifyMaskFull_float(float3 WorldPos, float NoiseValue, float NoiseAmount, out float Mask, out float Edge, out float Purified)
{
    float corruption = 0;
    Edge = 0;
    Purified = 0;

    int count = (int)_PurifySphereCount;
    float offset = (NoiseValue - 0.5) * 2.0 * NoiseAmount;
    float edgeWidth = max(_PurifyEdgeWidth, 0.001);
    float areaFade = max(_PurifyAreaFade, 0.001);

    [loop] for (int i = 0; i < PURIFY_MAX_SPHERES; i++)
    {
        if (i >= count) break;

        float4 s = _PurifySpheres[i];
        float maxRadius = _PurifySphereParams[i].x;
        if (maxRadius <= 0.001) continue;

        float d = distance(WorldPos, s.xyz);

        // Cuánto de esta posición pertenece al área corrupta del nodo (con borde suave e irregular).
        float area = saturate((maxRadius + offset - d) / areaFade);
        if (area <= 0) continue;

        // Cuánto ya limpió la onda. Con radio 0 la región no arrancó: sin esto el ruido dejaría una mancha sana
        // en el centro antes de purificar.
        float reached = 0;
        if (s.w > 0.001) reached = saturate((s.w + offset - d) / edgeWidth);

        corruption = max(corruption, area * (1.0 - reached));
        Edge = max(Edge, area * reached * (1.0 - reached) * 4.0);
        Purified = max(Purified, area * reached);
    }

    Mask = 1.0 - corruption;
}

void PurifyMask_float(float3 WorldPos, float NoiseValue, float NoiseAmount, out float Mask, out float Edge)
{
    float purified;
    PurifyMaskFull_float(WorldPos, NoiseValue, NoiseAmount, Mask, Edge, purified);
}

void PurifyMask_half(half3 WorldPos, half NoiseValue, half NoiseAmount, out half Mask, out half Edge)
{
    // Las posiciones de mundo necesitan precisión completa: se calcula en float y se baja al final.
    float mask, edge;
    PurifyMask_float(WorldPos, NoiseValue, NoiseAmount, mask, edge);
    Mask = (half)mask;
    Edge = (half)edge;
}

// Igual que PurifyMask, pero calcula el ruido acá. NoiseScale ≈ 0.3, NoiseAmount ≈ 2.5.
void PurifyMaskAuto_float(float3 WorldPos, float NoiseScale, float NoiseAmount, out float Mask, out float Edge)
{
    PurifyMask_float(WorldPos, PurifyNoise(WorldPos.xz, NoiseScale), NoiseAmount, Mask, Edge);
}

void PurifyMaskAuto_half(half3 WorldPos, half NoiseScale, half NoiseAmount, out half Mask, out half Edge)
{
    float mask, edge;
    PurifyMaskAuto_float(WorldPos, NoiseScale, NoiseAmount, mask, edge);
    Mask = (half)mask;
    Edge = (half)edge;
}

#endif // GEKKO_PURIFY_MASK_INCLUDED
