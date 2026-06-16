using System.Collections.Generic;
using UnityEngine;

namespace SplineTerrainTool.Util
{
    /// <summary>
    /// "Ear clipping" triangulation of simple polygons (without holes), robust for
    /// concave shapes. Works in 2D (XZ). Returns indices that reference the input polygon.
    /// O(n^2): enough for a few hundred vertices.
    /// </summary>
    public static class EarClippingTriangulator
    {
        /// <summary>
        /// Triangulates the polygon. Expects vertices in CCW order; if they come in CW it reverses them
        /// internally and adjusts the indices, so the result always references the original array.
        /// </summary>
        /// <param name="polygon">Vertices in XZ (x = .x, z = .y).</param>
        /// <returns>List of indices (triples) into <paramref name="polygon"/>.</returns>
        public static List<int> Triangulate(IReadOnlyList<Vector2> polygon)
        {
            var triangles = new List<int>();
            int n = polygon.Count;
            if (n < 3) return triangles;

            // Working list of indices in CCW order.
            var indices = new List<int>(n);
            if (SignedArea(polygon) >= 0f)
            {
                for (int i = 0; i < n; i++) indices.Add(i);
            }
            else
            {
                for (int i = n - 1; i >= 0; i--) indices.Add(i);
            }

            int guard = 0;
            int maxIterations = n * n + 1;
            while (indices.Count > 3 && guard++ < maxIterations)
            {
                bool earFound = false;
                int count = indices.Count;
                for (int i = 0; i < count; i++)
                {
                    int i0 = indices[(i - 1 + count) % count];
                    int i1 = indices[i];
                    int i2 = indices[(i + 1) % count];

                    Vector2 a = polygon[i0];
                    Vector2 b = polygon[i1];
                    Vector2 c = polygon[i2];

                    // Convex vertex? (CCW => cross > 0)
                    if (Cross(b - a, c - a) <= 0f) continue;

                    // No other vertex inside the triangle?
                    bool hasPointInside = false;
                    for (int k = 0; k < count; k++)
                    {
                        int idx = indices[k];
                        if (idx == i0 || idx == i1 || idx == i2) continue;
                        if (PointInTriangle(polygon[idx], a, b, c))
                        {
                            hasPointInside = true;
                            break;
                        }
                    }
                    if (hasPointInside) continue;

                    // It is an ear: we clip it.
                    triangles.Add(i0);
                    triangles.Add(i1);
                    triangles.Add(i2);
                    indices.RemoveAt(i);
                    earFound = true;
                    break;
                }

                if (!earFound)
                {
                    // Degenerate or self-intersecting polygon: we bail out to avoid hanging.
                    break;
                }
            }

            if (indices.Count == 3)
            {
                triangles.Add(indices[0]);
                triangles.Add(indices[1]);
                triangles.Add(indices[2]);
            }

            return triangles;
        }

        private static float SignedArea(IReadOnlyList<Vector2> p)
        {
            float area = 0f;
            int n = p.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = p[i];
                Vector2 b = p[(i + 1) % n];
                area += a.x * b.y - b.x * a.y;
            }
            return area * 0.5f;
        }

        private static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a);
            float d2 = Cross(c - b, p - b);
            float d3 = Cross(a - c, p - c);
            bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            // Inside if all signs match (without mixing pos and neg).
            return !(hasNeg && hasPos);
        }
    }
}
