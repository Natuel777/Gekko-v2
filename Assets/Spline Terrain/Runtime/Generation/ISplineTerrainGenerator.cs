using UnityEngine.Splines;
using SplineTerrainTool.Util;

namespace SplineTerrainTool.Generation
{
    /// <summary>
    /// Generates a terrain mesh from a spline and the settings.
    /// Each mode (internal, road, external) implements this interface.
    /// </summary>
    public interface ISplineTerrainGenerator
    {
        /// <summary>
        /// Builds the working outline (sampled, mode-prepared and smoothed). Exposed so callers
        /// (e.g. the optimized collider) can simplify it and feed it back via <c>overrideOutline</c>.
        /// </summary>
        SampledOutline BuildOutline(Spline spline, SplineTerrainSettings settings, int count);

        /// <summary>
        /// Builds the mesh.
        /// </summary>
        /// <param name="spline">Source spline (in the terrain's local space).</param>
        /// <param name="settings">Generation parameters.</param>
        /// <param name="segmentOverride">If &gt; 0, forces that number of segments. If -1 it uses settings.segmentsPerSpline.</param>
        /// <param name="overrideOutline">If not null, uses this prepared outline instead of sampling (used by the optimized collider).</param>
        MeshBuildResult Generate(Spline spline, SplineTerrainSettings settings, int segmentOverride = -1, SampledOutline overrideOutline = null);
    }
}
