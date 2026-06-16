using UnityEngine;

namespace SplineTerrainTool
{
    /// <summary>
    /// Reusable terrain parameters profile. Stores a set of <see cref="SplineTerrainSettings"/>
    /// (and optionally the materials) so it can be applied to any terrain without configuring
    /// everything again. It does not store the spline shape or the meshes (that is done by the bake / SplineTerrainData).
    /// </summary>
    [CreateAssetMenu(fileName = "SplineTerrainProfile", menuName = "Spline Terrain/Profile")]
    public class SplineTerrainProfile : ScriptableObject
    {
        public SplineTerrainSettings settings = new SplineTerrainSettings();

        [Header("Materials (optional)")]
        public Material floorMaterial;
        public Material wallMaterial;
        public Material edgeMaterial;

        /// <summary>Copies the settings (and materials) of a terrain into this profile.</summary>
        public void CaptureFrom(SplineTerrain terrain)
        {
            if (terrain == null) return;
            settings = terrain.Settings.Clone();
            floorMaterial = terrain.FloorMaterial;
            wallMaterial = terrain.WallMaterial;
            edgeMaterial = terrain.EdgeMaterial;
        }
    }
}
