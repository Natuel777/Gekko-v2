#ifndef GEKKO_PURIFY_DISSOLVE_INCLUDED
#define GEKKO_PURIFY_DISSOLVE_INCLUDED

// Disolve con borde ardiente para la vegetación corrupta (lo anima CorruptedPlant por la propiedad _Dissolve).
// Uso en Shader Graph: Custom Function (File, Name: PurifyDissolve) → Alpha y EdgeGlow × color HDR → Emission,
// con Alpha Clipping activado (Alpha Clip Threshold ≈ 0.5).
//
//  Noise     ruido 0..1 (Simple Noise o Gradient Noise sobre UV o posición).
//  Dissolve  0 = entera, 1 = disuelta del todo (la propiedad _Dissolve del material).
//  EdgeWidth ancho del borde que arde, en unidades de ruido (≈ 0.08).
//  Alpha     1 = visible, 0 = recortado.
//  EdgeGlow  1 en el borde que se está quemando, 0 en el resto.

void PurifyDissolve_float(float Noise, float Dissolve, float EdgeWidth, out float Alpha, out float EdgeGlow)
{
    // Se estira el rango para que con Dissolve = 0 no quede nada recortado y con 1 no quede nada visible.
    float threshold = Dissolve * (1.0 + EdgeWidth) - EdgeWidth * 0.5;
    float width = max(EdgeWidth, 0.0001);

    Alpha = step(threshold, Noise);
    EdgeGlow = Alpha * (1.0 - saturate((Noise - threshold) / width));
    EdgeGlow *= step(0.0001, Dissolve); // sin quemar no hay borde
}

void PurifyDissolve_half(half Noise, half Dissolve, half EdgeWidth, out half Alpha, out half EdgeGlow)
{
    float alpha, glow;
    PurifyDissolve_float(Noise, Dissolve, EdgeWidth, alpha, glow);
    Alpha = (half)alpha;
    EdgeGlow = (half)glow;
}

#endif // GEKKO_PURIFY_DISSOLVE_INCLUDED
