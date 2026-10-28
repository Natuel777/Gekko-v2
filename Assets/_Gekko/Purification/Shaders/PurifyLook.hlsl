#ifndef GEKKO_PURIFY_LOOK_INCLUDED
#define GEKKO_PURIFY_LOOK_INCLUDED

// Arma el look corrupto a partir del color sano, sin texturas extra, y lo mezcla con la máscara de PurifyMask.
// Los parámetros de corrupción son de MATERIAL (hojas, tronco y piso necesitan valores distintos).
//
//  CorruptTint        color que tiñe lo corrupto (violáceo). Blanco = sin tinte.
//  CorruptSaturation  saturación que le queda a lo corrupto: 0 = gris, 1 = original (≈ 0.2).
//  CorruptDarken      multiplicador de brillo de lo corrupto: 0.45 = queda al 45 % (≈ 0.45).
//  EdgeColor/EdgeIntensity  brillo del frente de la onda (sumar a Emission).

float3 PurifyCorruptColor(float3 healthy, float3 tint, float saturation, float darken)
{
    float luminance = dot(healthy, float3(0.2126, 0.7152, 0.0722));
    float3 desaturated = lerp(luminance.xxx, healthy, saturation);
    return desaturated * tint * darken;
}

void PurifyLook_float(float3 HealthyColor, float Mask, float Edge,
                      float3 CorruptTint, float CorruptSaturation, float CorruptDarken,
                      float3 EdgeColor, float EdgeIntensity,
                      out float3 BaseColor, out float3 Emission)
{
    float3 corrupt = PurifyCorruptColor(HealthyColor, CorruptTint, CorruptSaturation, CorruptDarken);
    BaseColor = lerp(corrupt, HealthyColor, saturate(Mask));
    Emission = Edge * EdgeColor * EdgeIntensity;
}

// Todo en una sola función, para Shader Graph (un único nodo Custom Function):
//  RestoreMode = 0  material sano: genera el look corrupto dentro de las regiones sin purificar.
//  RestoreMode = 1  material YA corrupto (arte pintado): se ve tal cual y, donde pasó la onda, se multiplica por
//                   RestoreTint para volver al color sano. Fuera de las regiones queda corrupto como está.
//  Sin regiones en la escena (PurifyManager ausente) devuelve el color sin cambios en los dos modos.
#include "Assets/_Gekko/Purification/Shaders/PurifyMask.hlsl"

void PurifySurface_float(float3 Color, float3 WorldPos, float NoiseScale, float NoiseAmount,
                         float3 CorruptTint, float CorruptSaturation, float CorruptDarken,
                         float3 EdgeColor, float EdgeIntensity, float RestoreMode, float3 RestoreTint,
                         out float3 BaseColor, out float3 Emission)
{
    BaseColor = Color;
    Emission = 0;
    if (_PurifySphereCount < 0.5) return;

    float mask, edge, purified;
    PurifyMaskFull_float(WorldPos, PurifyNoise(WorldPos.xz, NoiseScale), NoiseAmount, mask, edge, purified);

    float3 looked, emission;
    PurifyLook_float(Color, mask, edge, CorruptTint, CorruptSaturation, CorruptDarken, EdgeColor, EdgeIntensity, looked, emission);
    float3 restored = Color * lerp(float3(1, 1, 1), RestoreTint, saturate(purified));

    BaseColor = RestoreMode > 0.5 ? restored : looked;
    Emission = emission;
}

void PurifySurface_half(half3 Color, half3 WorldPos, half NoiseScale, half NoiseAmount,
                        half3 CorruptTint, half CorruptSaturation, half CorruptDarken,
                        half3 EdgeColor, half EdgeIntensity, half RestoreMode, half3 RestoreTint,
                        out half3 BaseColor, out half3 Emission)
{
    float3 baseColor, emission;
    PurifySurface_float(Color, WorldPos, NoiseScale, NoiseAmount, CorruptTint, CorruptSaturation, CorruptDarken,
                        EdgeColor, EdgeIntensity, RestoreMode, RestoreTint, baseColor, emission);
    BaseColor = (half3)baseColor;
    Emission = (half3)emission;
}

void PurifyLook_half(half3 HealthyColor, half Mask, half Edge,
                     half3 CorruptTint, half CorruptSaturation, half CorruptDarken,
                     half3 EdgeColor, half EdgeIntensity,
                     out half3 BaseColor, out half3 Emission)
{
    float3 baseColor, emission;
    PurifyLook_float(HealthyColor, Mask, Edge, CorruptTint, CorruptSaturation, CorruptDarken,
                     EdgeColor, EdgeIntensity, baseColor, emission);
    BaseColor = (half3)baseColor;
    Emission = (half3)emission;
}

#endif // GEKKO_PURIFY_LOOK_INCLUDED
