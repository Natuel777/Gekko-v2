using System.Collections.Generic;
using UnityEngine;

namespace SplineTerrainTool.Util
{
    /// <summary>
    /// Triangulates a planar polygon (in 2D) as a regular row/column grid clipped to the outline.
    /// Unlike ear clipping (which fans the contour), this produces an even, terrain-like topology
    /// with a configurable density, ideal for vertex painting (Polybrush).
    ///
    /// Method: overlay an axis-aligned grid over the polygon's bounding box and, for every cell,
    /// clip the polygon against the cell square (Sutherland–Hodgman, the cell is convex). Interior
    /// cells yield a full quad; boundary cells yield the polygon's clipped corner, so the outline is
    /// preserved exactly. Shared grid/boundary vertices are welded so the mesh stays watertight.
    /// </summary>
    public static class GridCapTriangulator
    {
        // Safety cap on grid dimensions so a tiny cell size cannot explode the vertex count.
        private const int MaxCellsPerAxis = 256;

        /// <summary>
        /// Fills <paramref name="outVerts"/> / <paramref name="outTris"/> with a clipped grid of the
        /// polygon. Vertices come out in CCW-wound triangles (front face up in the source 2D plane).
        /// </summary>
        public static void Triangulate(IReadOnlyList<Vector2> polygon, float cellSize,
            List<Vector2> outVerts, List<int> outTris)
        {
            outVerts.Clear();
            outTris.Clear();

            int pn = polygon.Count;
            if (pn < 3 || cellSize <= 0f) return;

            // Bounding box.
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < pn; i++)
            {
                Vector2 p = polygon[i];
                if (p.x < minX) minX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.x > maxX) maxX = p.x;
                if (p.y > maxY) maxY = p.y;
            }
            float width = maxX - minX, height = maxY - minY;
            if (width <= 1e-6f || height <= 1e-6f) return;

            int nx = Mathf.Clamp(Mathf.CeilToInt(width / cellSize), 1, MaxCellsPerAxis);
            int ny = Mathf.Clamp(Mathf.CeilToInt(height / cellSize), 1, MaxCellsPerAxis);
            float stepX = width / nx;
            float stepY = height / ny;

            // Vertex welding: quantize positions to a fraction of the smaller step so coincident
            // grid corners and (identically computed) boundary intersections collapse to one vertex.
            float q = Mathf.Min(stepX, stepY) * 1e-3f;
            if (q <= 0f) q = 1e-5f;
            var weld = new Dictionary<long, int>();

            int VertexAt(Vector2 v)
            {
                int ix = Mathf.RoundToInt((v.x - minX) / q);
                int iy = Mathf.RoundToInt((v.y - minY) / q);
                long key = ((long)ix << 32) | (uint)iy;
                if (weld.TryGetValue(key, out int idx)) return idx;
                idx = outVerts.Count;
                outVerts.Add(v);
                weld[key] = idx;
                return idx;
            }

            // Reusable buffers for the per-cell clipping.
            var clip = new List<Vector2>(12);
            var tmp = new List<Vector2>(12);

            for (int cy = 0; cy < ny; cy++)
            {
                float y0 = minY + cy * stepY;
                float y1 = (cy == ny - 1) ? maxY : y0 + stepY;
                for (int cx = 0; cx < nx; cx++)
                {
                    float x0 = minX + cx * stepX;
                    float x1 = (cx == nx - 1) ? maxX : x0 + stepX;

                    ClipPolygonToCell(polygon, x0, y0, x1, y1, clip, tmp);
                    if (clip.Count < 3) continue;

                    // Fan-triangulate the clipped (convex-ish, CCW) piece.
                    int a = VertexAt(clip[0]);
                    for (int k = 1; k < clip.Count - 1; k++)
                    {
                        int b = VertexAt(clip[k]);
                        int c = VertexAt(clip[k + 1]);
                        if (a == b || b == c || a == c) continue; // skip degenerate slivers
                        outTris.Add(a);
                        outTris.Add(b);
                        outTris.Add(c);
                    }
                }
            }
        }

        /// <summary>
        /// Sutherland–Hodgman clip of <paramref name="subject"/> against the axis-aligned cell
        /// [x0,x1]x[y0,y1]. Result (CCW preserved) is written to <paramref name="result"/>.
        /// </summary>
        private static void ClipPolygonToCell(IReadOnlyList<Vector2> subject,
            float x0, float y0, float x1, float y1, List<Vector2> result, List<Vector2> tmp)
        {
            // Seed with the subject.
            result.Clear();
            for (int i = 0; i < subject.Count; i++) result.Add(subject[i]);

            // Clip successively against the 4 edges. edge: 0=left(x>=x0),1=right(x<=x1),2=bottom(y>=y0),3=top(y<=y1).
            for (int e = 0; e < 4 && result.Count > 0; e++)
            {
                tmp.Clear();
                int cnt = result.Count;
                for (int i = 0; i < cnt; i++)
                {
                    Vector2 cur = result[i];
                    Vector2 prev = result[(i - 1 + cnt) % cnt];
                    bool curIn = Inside(cur, e, x0, y0, x1, y1);
                    bool prevIn = Inside(prev, e, x0, y0, x1, y1);

                    if (curIn)
                    {
                        if (!prevIn) tmp.Add(Intersect(prev, cur, e, x0, y0, x1, y1));
                        tmp.Add(cur);
                    }
                    else if (prevIn)
                    {
                        tmp.Add(Intersect(prev, cur, e, x0, y0, x1, y1));
                    }
                }
                // Swap result <- tmp.
                result.Clear();
                for (int i = 0; i < tmp.Count; i++) result.Add(tmp[i]);
            }
        }

        private static bool Inside(Vector2 p, int edge, float x0, float y0, float x1, float y1)
        {
            switch (edge)
            {
                case 0: return p.x >= x0;
                case 1: return p.x <= x1;
                case 2: return p.y >= y0;
                default: return p.y <= y1;
            }
        }

        private static Vector2 Intersect(Vector2 a, Vector2 b, int edge, float x0, float y0, float x1, float y1)
        {
            Vector2 d = b - a;
            float t;
            switch (edge)
            {
                case 0: t = Mathf.Abs(d.x) > 1e-12f ? (x0 - a.x) / d.x : 0f; break;
                case 1: t = Mathf.Abs(d.x) > 1e-12f ? (x1 - a.x) / d.x : 0f; break;
                case 2: t = Mathf.Abs(d.y) > 1e-12f ? (y0 - a.y) / d.y : 0f; break;
                default: t = Mathf.Abs(d.y) > 1e-12f ? (y1 - a.y) / d.y : 0f; break;
            }
            return a + d * t;
        }
    }
}
