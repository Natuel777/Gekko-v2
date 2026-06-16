using UnityEngine;

namespace SplineTerrainTool.Util
{
    /// <summary>
    /// Simple noise based on <see cref="Mathf.PerlinNoise"/>, readable and without dependencies.
    /// Returns values approximately in [-1, 1].
    /// </summary>
    public static class TerrainNoise
    {
        /// <summary>
        /// Samples noise at a world position (XZ).
        /// </summary>
        /// <param name="x">X coordinate.</param>
        /// <param name="z">Z coordinate.</param>
        /// <param name="frequency">Frequency (higher = more detail).</param>
        /// <param name="seed">Seed to offset the noise field.</param>
        public static float Sample(float x, float z, float frequency, int seed)
        {
            // PerlinNoise returns [0,1]; we center it to [-1,1].
            float offset = seed * 13.37f;
            float raw = Mathf.PerlinNoise(x * frequency + offset, z * frequency + offset);
            return raw * 2f - 1f;
        }
    }
}
