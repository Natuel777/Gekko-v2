using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Prende y apaga el FullScreenPassRendererFeature del viento (post proceso) en runtime.
/// Se usa cuando Gekko entra/sale del estado de velocidad aumentada (combo de arándanos
/// o frutilla). El feature se busca por tipo dentro del URP renderer activo, así no hace
/// falta arrastrar referencias en el Inspector.
/// </summary>
public static class WindEffectController
{
    private static ScriptableRendererFeature _windFeature;
    private static bool _searched;

    /// <summary>Activa o desactiva el post proceso de viento.</summary>
    public static void SetActive(bool active)
    {
        EnsureFeature();
        if (_windFeature == null) return;
        if (_windFeature.isActive == active) return;
        _windFeature.SetActive(active);
    }

    private static void EnsureFeature()
    {
        if (_searched && _windFeature != null) return;
        _searched = true;
        _windFeature = null;

        var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset == null) return;

        foreach (var data in urpAsset.rendererDataList)
        {
            if (data == null) continue;
            foreach (var feature in data.rendererFeatures)
            {
                if (feature is FullScreenPassRendererFeature)
                {
                    _windFeature = feature;
                    return;
                }
            }
        }
    }
}
