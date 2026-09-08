#ifndef GEKKO_CLOUD_NOISE_INCLUDED
#define GEKKO_CLOUD_NOISE_INCLUDED

// Ruido procedural compartido por el shader de nubes.
// Todo se evalua en espacio LOCAL de la malla: asi el patron queda "pegado"
// a la nube cuando esta se mueve o rota, y solo rueda por el offset temporal.

float GekkoHash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

// Value noise trilineal con interpolacion quintica (derivadas continuas ->
// sin facetado visible al desplazar vertices).
float GekkoValueNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float3 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);

    float n000 = GekkoHash13(i + float3(0.0, 0.0, 0.0));
    float n100 = GekkoHash13(i + float3(1.0, 0.0, 0.0));
    float n010 = GekkoHash13(i + float3(0.0, 1.0, 0.0));
    float n110 = GekkoHash13(i + float3(1.0, 1.0, 0.0));
    float n001 = GekkoHash13(i + float3(0.0, 0.0, 1.0));
    float n101 = GekkoHash13(i + float3(1.0, 0.0, 1.0));
    float n011 = GekkoHash13(i + float3(0.0, 1.0, 1.0));
    float n111 = GekkoHash13(i + float3(1.0, 1.0, 1.0));

    float x00 = lerp(n000, n100, u.x);
    float x10 = lerp(n010, n110, u.x);
    float x01 = lerp(n001, n101, u.x);
    float x11 = lerp(n011, n111, u.x);

    return lerp(lerp(x00, x10, u.y), lerp(x01, x11, u.y), u.z);
}

// FBM de 3 octavas normalizado a 0..1.
float GekkoFbm(float3 p)
{
    float sum = 0.0;
    float amp = 0.5;
    float norm = 0.0;

    [unroll]
    for (int o = 0; o < 3; o++)
    {
        sum += GekkoValueNoise(p) * amp;
        norm += amp;
        p = p * 2.03 + 17.31;
        amp *= 0.5;
    }

    return sum / max(norm, 1e-5);
}

// Offset temporal: el "rolling". Se suma en espacio local, de modo que la
// direccion del viento es relativa a la nube, no al mundo.
float3 GekkoRollOffset(float3 rollDir, float rollSpeed, float time)
{
    return normalize(rollDir + 1e-5) * (time * rollSpeed);
}

#endif // GEKKO_CLOUD_NOISE_INCLUDED
