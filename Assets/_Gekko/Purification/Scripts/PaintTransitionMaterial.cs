using UnityEngine;

namespace Gekko.Purification
{
    /// <summary>
    /// Arma la instancia del shader "Gekko/Purification/Paint Transition" a partir de dos materiales URP/Lit: A (corrupto,
    /// "from") y B (sano, "to"). La usan TurretPurifiedMaterialSwap (torretas) y PlantPaintPurify (planta carnívora).
    /// Respeta los keywords del Lit: si un mapa está asignado pero su keyword está apagado, URP no lo usa, y acá tampoco.
    /// </summary>
    public static class PaintTransitionMaterial
    {
        public static readonly int PaintHeightId = Shader.PropertyToID("_PaintHeight");
        public static readonly int PulseId = Shader.PropertyToID("_Pulse");
        public static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
        public static readonly int EdgeIntensityId = Shader.PropertyToID("_EdgeIntensity");
        public static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
        public static readonly int NoiseAmountId = Shader.PropertyToID("_PaintNoiseAmount");
        public static readonly int PulseIntensityId = Shader.PropertyToID("_PulseIntensity");
        public static readonly int UseRestPositionId = Shader.PropertyToID("_UseRestPosition");

        /// <summary>Instancia nueva del shader con las texturas y valores de los dos Lit. Hay que destruirla al terminar.</summary>
        public static Material Create(Shader shader, Material from, Material to)
        {
            var m = new Material(shader) { name = $"{from.name}->{to.name} (pintado)" };

            CopySide(from, m, "A");
            CopySide(to, m, "B");

            bool detail = (to.IsKeywordEnabled("_DETAIL_MULX2") || to.IsKeywordEnabled("_DETAIL_SCALED")) &&
                          to.HasProperty("_DetailNormalMap") && to.GetTexture("_DetailNormalMap") != null;
            if(detail)
            {
                m.SetTexture("_DetailNormalMapB", to.GetTexture("_DetailNormalMap"));
                m.SetTextureScale("_DetailNormalMapB", to.GetTextureScale("_DetailAlbedoMap"));
                m.SetTextureOffset("_DetailNormalMapB", to.GetTextureOffset("_DetailAlbedoMap"));
                m.SetFloat("_DetailNormalScaleB", to.GetFloat("_DetailNormalMapScale"));
            }

            m.SetFloat(PulseId, 0f);
            return m;
        }

        /// <summary>Borde (franja que sube), irregularidad del frente y fuerza del pulso. Medidas en metros.</summary>
        public static void SetLook(Material m, Color edgeColor, float edgeIntensity, float edgeWidth, float noiseAmount, float pulseIntensity)
        {
            m.SetColor(EdgeColorId, edgeColor);
            m.SetFloat(EdgeIntensityId, edgeIntensity);
            m.SetFloat(EdgeWidthId, edgeWidth);
            m.SetFloat(NoiseAmountId, noiseAmount);
            m.SetFloat(PulseIntensityId, pulseIntensity);
        }

        private static void CopySide(Material lit, Material m, string side)
        {
            if(lit.HasProperty("_BaseMap"))
            {
                m.SetTexture("_BaseMap" + side, lit.GetTexture("_BaseMap"));
                m.SetTextureScale("_BaseMap" + side, lit.GetTextureScale("_BaseMap"));
                m.SetTextureOffset("_BaseMap" + side, lit.GetTextureOffset("_BaseMap"));
            }
            if(lit.HasProperty("_BaseColor")) m.SetColor("_BaseColor" + side, lit.GetColor("_BaseColor"));

            if(lit.IsKeywordEnabled("_NORMALMAP") && lit.HasProperty("_BumpMap"))
            {
                m.SetTexture("_BumpMap" + side, lit.GetTexture("_BumpMap"));
                m.SetFloat("_BumpScale" + side, lit.GetFloat("_BumpScale"));
            }

            bool metalMap = lit.IsKeywordEnabled("_METALLICSPECGLOSSMAP") && lit.HasProperty("_MetallicGlossMap") &&
                            lit.GetTexture("_MetallicGlossMap") != null;
            if(metalMap) m.SetTexture("_MetallicGlossMap" + side, lit.GetTexture("_MetallicGlossMap"));
            m.SetFloat("_UseMetalMap" + side, metalMap ? 1f : 0f);
            if(lit.HasProperty("_Metallic")) m.SetFloat("_Metallic" + side, lit.GetFloat("_Metallic"));
            if(lit.HasProperty("_Smoothness")) m.SetFloat("_Smoothness" + side, lit.GetFloat("_Smoothness"));

            bool emission = lit.IsKeywordEnabled("_EMISSION") && lit.HasProperty("_EmissionColor");
            if(emission)
            {
                if(lit.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap" + side, lit.GetTexture("_EmissionMap"));
                m.SetColor("_EmissionColor" + side, lit.GetColor("_EmissionColor"));
            }
            else m.SetColor("_EmissionColor" + side, Color.black);
        }
    }
}
