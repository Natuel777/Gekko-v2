using System.Collections.Generic;
using UnityEngine;

namespace SplineTerrainTool.Util
{
    /// <summary>
    /// Offsets a closed outline outward or inward along the bisector
    /// of each vertex. Works in XZ and preserves the original height (y) of each point.
    /// </summary>
    public static class PolygonOffset
    {
        /// <summary>
        /// Generates a new offset outline.
        /// </summary>
        /// <param name="points">Original outline (closed).</param>
        /// <param name="normals">Planar normals per vertex (outward).</param>
        /// <param name="distance">Offset distance.</param>
        /// <param name="outward">true = outward, false = inward.</param>
        public static Vector3[] Offset(IReadOnlyList<Vector3> points, IReadOnlyList<Vector3> normals, float distance, bool outward)
        {
            int n = points.Count;
            var result = new Vector3[n];
            float sign = outward ? 1f : -1f;

            // Note: we do not clamp the distance. It used to be limited to a fraction of the
            // shortest edge, but with high resolution the edges are tiny and the distance got
            // capped (the external mode would not grow). Self-intersections at very
            // concave corners are a known limitation; they are reduced by smoothing the outline.
            for (int i = 0; i < n; i++)
            {
                // Bisector: average of the normals of the two adjacent edges.
                Vector3 nPrev = normals[(i - 1 + n) % n];
                Vector3 nCurr = normals[i];
                Vector3 bisector = (nPrev + nCurr);
                if (bisector.sqrMagnitude < 1e-8f) bisector = nCurr;
                bisector.Normalize();

                // Compensate the shortening of the bisector at corners (1/cos of the half-angle).
                float cos = Mathf.Clamp(Vector3.Dot(bisector, nCurr), 0.2f, 1f);
                float offsetLen = distance / cos;

                result[i] = points[i] + bisector * (offsetLen * sign);
            }

            return result;
        }

        /// <summary>
        /// Indicates whether the proposed offset exceeds the safe distance (corners too close together).
        /// Used by the generator to warn/clamp.
        /// </summary>
        public static bool WouldSelfIntersect(IReadOnlyList<Vector3> points, float distance)
        {
            int n = points.Count;
            float minEdge = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                float e = Vector3.Distance(points[i], points[(i + 1) % n]);
                if (e > 1e-5f) minEdge = Mathf.Min(minEdge, e);
            }
            return Mathf.Abs(distance) > minEdge * 2f;
        }
    }
}
