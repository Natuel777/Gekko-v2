using System.Collections.Generic;
using UnityEngine;
using SplineTerrainTool.Util;

namespace SplineTerrainTool.Generation
{
    /// <summary>
    /// Helpers shared by the generators: triangulated caps and walls (loft) with
    /// vertical subdivisions, curvature (bowing) and optional smooth shading.
    /// </summary>
    public static class GeneratorUtils
    {
        public static Vector3 TriNormal(Vector3 a, Vector3 b, Vector3 c)
        {
            return Vector3.Cross(b - a, c - a).normalized;
        }

        /// <summary>
        /// Adds a cap by triangulating the ring in XZ. The floor may be inclined
        /// (rotated): it is still planar, so we compute a single real normal for all
        /// the vertices. Orients the winding so it faces the requested side (<paramref name="faceUp"/>).
        /// Planar world UV: (x, z) * uvScale.
        /// </summary>
        public static void AddFlatCap(MeshBuildResult r, IReadOnlyList<Vector3> ring, float uvScale, bool faceUp, int submesh = MeshBuildResult.SubmeshFloor)
        {
            int n = ring.Count;
            if (n < 3) return;

            var poly2D = new List<Vector2>(n);
            for (int i = 0; i < n; i++) poly2D.Add(new Vector2(ring[i].x, ring[i].z));

            List<int> tris = EarClippingTriangulator.Triangulate(poly2D);
            if (tris.Count < 3) return;

            // Representative normal: average of the triangle normals (area-weighted),
            // oriented toward the requested side. This way an inclined floor has the correct normal.
            Vector3 accum = Vector3.zero;
            for (int t = 0; t < tris.Count; t += 3)
            {
                Vector3 a = ring[tris[t]], b = ring[tris[t + 1]], c = ring[tris[t + 2]];
                accum += Vector3.Cross(b - a, c - a); // unnormalized => weighted by area
            }
            Vector3 normal = accum.sqrMagnitude > 1e-10f ? accum.normalized : Vector3.up;
            if ((normal.y >= 0f) != faceUp) normal = -normal;

            int baseIndex = r.Vertices.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 uv = new Vector2(ring[i].x, ring[i].z) * uvScale;
                r.AddVertex(ring[i], normal, uv);
            }

            for (int t = 0; t < tris.Count; t += 3)
            {
                int a = baseIndex + tris[t];
                int b = baseIndex + tris[t + 1];
                int c = baseIndex + tris[t + 2];

                Vector3 triN = TriNormal(r.Vertices[a], r.Vertices[b], r.Vertices[c]);
                if (Vector3.Dot(triN, normal) >= 0f)
                    r.AddTriangle(submesh, a, b, c);
                else
                    r.AddTriangle(submesh, a, c, b);
            }
        }

        /// <summary>
        /// Like <see cref="AddFlatCap"/> but triangulates the (planar, possibly tilted) ring as a
        /// regular row/column grid clipped to the outline instead of an ear-clipped fan. Gives a dense,
        /// even, paintable topology (Polybrush). <paramref name="cellSize"/> drives the density.
        /// </summary>
        public static void AddGridCap(MeshBuildResult r, IReadOnlyList<Vector3> ring, float uvScale, bool faceUp,
            float cellSize, int submesh = MeshBuildResult.SubmeshFloor)
        {
            int n = ring.Count;
            if (n < 3) return;
            if (cellSize <= 0f) { AddFlatCap(r, ring, uvScale, faceUp, submesh); return; }

            // Plane fit: centroid + area-weighted normal (handles a tilted floor).
            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < n; i++) centroid += ring[i];
            centroid /= n;

            Vector3 accum = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = ring[i] - centroid;
                Vector3 b = ring[(i + 1) % n] - centroid;
                accum += Vector3.Cross(a, b);
            }
            Vector3 normal = accum.sqrMagnitude > 1e-12f ? accum.normalized : Vector3.up;
            if ((normal.y >= 0f) != faceUp) normal = -normal;

            // In-plane orthonormal basis with u x v = normal (so CCW in (u,v) faces 'normal').
            Vector3 helper = Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right;
            Vector3 u = Vector3.Cross(helper, normal);
            if (u.sqrMagnitude < 1e-10f) u = Vector3.Cross(Vector3.right, normal);
            u.Normalize();
            Vector3 v = Vector3.Cross(normal, u); // already unit

            // Project the ring to 2D (relative to the centroid).
            var poly2D = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                Vector3 d = ring[i] - centroid;
                poly2D.Add(new Vector2(Vector3.Dot(d, u), Vector3.Dot(d, v)));
            }
            // Ensure CCW so the clipped triangles face 'normal'.
            if (SignedArea2D(poly2D) < 0f) poly2D.Reverse();

            var verts2D = new List<Vector2>();
            var tris = new List<int>();
            GridCapTriangulator.Triangulate(poly2D, cellSize, verts2D, tris);
            if (tris.Count < 3) { AddFlatCap(r, ring, uvScale, faceUp, submesh); return; }

            int baseIndex = r.Vertices.Count;
            for (int i = 0; i < verts2D.Count; i++)
            {
                Vector3 pos = centroid + u * verts2D[i].x + v * verts2D[i].y;
                Vector2 uv = new Vector2(pos.x, pos.z) * uvScale;
                r.AddVertex(pos, normal, uv);
            }
            for (int t = 0; t < tris.Count; t += 3)
                r.AddTriangle(submesh, baseIndex + tris[t], baseIndex + tris[t + 1], baseIndex + tris[t + 2]);
        }

        /// <summary>
        /// Like <see cref="AddFlatCap"/> but, after ear-clipping the ring, uniformly subdivides each
        /// triangle so the floor gets paint-ready density while the boundary follows the outline exactly
        /// (no clipped slivers). Crack-free: every triangle uses the same subdivision level, so shared
        /// edges match and coincident vertices are welded.
        /// </summary>
        public static void AddSubdividedCap(MeshBuildResult r, IReadOnlyList<Vector3> ring, float uvScale, bool faceUp,
            float cellSize, int submesh = MeshBuildResult.SubmeshFloor)
        {
            int n = ring.Count;
            if (n < 3) return;
            if (cellSize <= 0f) { AddFlatCap(r, ring, uvScale, faceUp, submesh); return; }

            // Triangulate the outline (XZ projection, like AddFlatCap).
            var poly2D = new List<Vector2>(n);
            for (int i = 0; i < n; i++) poly2D.Add(new Vector2(ring[i].x, ring[i].z));
            List<int> tris = EarClippingTriangulator.Triangulate(poly2D);
            if (tris.Count < 3) { AddFlatCap(r, ring, uvScale, faceUp, submesh); return; }

            // Representative normal (area-weighted), oriented to the requested side.
            Vector3 accum = Vector3.zero;
            for (int t = 0; t < tris.Count; t += 3)
            {
                Vector3 a = ring[tris[t]], b = ring[tris[t + 1]], c = ring[tris[t + 2]];
                accum += Vector3.Cross(b - a, c - a);
            }
            Vector3 normal = accum.sqrMagnitude > 1e-10f ? accum.normalized : Vector3.up;
            if ((normal.y >= 0f) != faceUp) normal = -normal;

            // One global subdivision level from the average triangle edge, so all triangles match.
            float avgEdge = 0f; int edgeCount = 0;
            for (int t = 0; t < tris.Count; t += 3)
            {
                Vector3 a = ring[tris[t]], b = ring[tris[t + 1]], c = ring[tris[t + 2]];
                avgEdge += Vector3.Distance(a, b) + Vector3.Distance(b, c) + Vector3.Distance(c, a);
                edgeCount += 3;
            }
            avgEdge = edgeCount > 0 ? avgEdge / edgeCount : cellSize;
            int L = Mathf.Clamp(Mathf.CeilToInt(avgEdge / cellSize), 1, 64);

            // Vertex welding shared across triangles (quantized position).
            float q = cellSize * 1e-3f;
            if (q <= 0f) q = 1e-5f;
            var weld = new Dictionary<long, int>();

            int VertexAt(Vector3 p)
            {
                int ix = Mathf.RoundToInt(p.x / q);
                int iy = Mathf.RoundToInt(p.y / q);
                int iz = Mathf.RoundToInt(p.z / q);
                long key = ((long)(ix & 0x1FFFFF) << 42) ^ ((long)(iy & 0x1FFFFF) << 21) ^ (long)(iz & 0x1FFFFF);
                if (weld.TryGetValue(key, out int idx)) return idx;
                idx = r.AddVertex(p, normal, new Vector2(p.x, p.z) * uvScale);
                weld[key] = idx;
                return idx;
            }

            for (int t = 0; t < tris.Count; t += 3)
            {
                Vector3 A = ring[tris[t]], B = ring[tris[t + 1]], C = ring[tris[t + 2]];
                // Keep each sub-triangle facing 'normal' (the ear-clip winding may not).
                bool flip = Vector3.Dot(Vector3.Cross(B - A, C - A), normal) < 0f;

                Vector3 P(int i, int j) => A + (B - A) * (i / (float)L) + (C - A) * (j / (float)L);

                for (int j = 0; j < L; j++)
                {
                    for (int i = 0; i < L - j; i++)
                    {
                        int v00 = VertexAt(P(i, j));
                        int v10 = VertexAt(P(i + 1, j));
                        int v01 = VertexAt(P(i, j + 1));
                        EmitTri(r, submesh, v00, v10, v01, flip);

                        if (i < L - j - 1)
                        {
                            int v11 = VertexAt(P(i + 1, j + 1));
                            EmitTri(r, submesh, v10, v11, v01, flip);
                        }
                    }
                }
            }
        }

        private static void EmitTri(MeshBuildResult r, int submesh, int a, int b, int c, bool flip)
        {
            if (a == b || b == c || a == c) return;
            if (flip) r.AddTriangle(submesh, a, c, b);
            else r.AddTriangle(submesh, a, b, c);
        }

        /// <summary>
        /// Adds a planar quad (corners a,b,c,d in order) subdivided into a grid so the floor has enough
        /// density to paint. Density follows <paramref name="cellSize"/> on each edge. Triangles face up.
        /// Planar XZ UVs. Used by the Road and External floors (which are already quad strips/rings).
        /// </summary>
        public static void AddFloorQuadGrid(MeshBuildResult r, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            float uvScale, float cellSize, int submesh = MeshBuildResult.SubmeshFloor)
        {
            // P(s,t) = lerp( lerp(a,b,s), lerp(d,c,s), t ). s along a->b, t along a->d.
            int su = cellSize > 0f ? Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(a, b) / cellSize), 1, 256) : 1;
            int sv = cellSize > 0f ? Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(a, d) / cellSize), 1, 256) : 1;

            // Orient so the face points up regardless of the input corner order.
            Vector3 rawN = TriNormal(a, b, c);
            bool flip = rawN.y < 0f;
            Vector3 nrm = flip ? -rawN : rawN;

            int cols = su + 1, rows = sv + 1;
            int baseIndex = r.Vertices.Count;
            for (int j = 0; j < rows; j++)
            {
                float t = j / (float)sv;
                for (int i = 0; i < cols; i++)
                {
                    float s = i / (float)su;
                    Vector3 ab = Vector3.Lerp(a, b, s);
                    Vector3 dc = Vector3.Lerp(d, c, s);
                    Vector3 p = Vector3.Lerp(ab, dc, t);
                    r.AddVertex(p, nrm, new Vector2(p.x, p.z) * uvScale);
                }
            }
            for (int j = 0; j < sv; j++)
            {
                for (int i = 0; i < su; i++)
                {
                    int i00 = baseIndex + j * cols + i;
                    int i10 = i00 + 1;
                    int i01 = i00 + cols;
                    int i11 = i01 + 1;
                    // a(i00) -> b(i10) -> c(i11) -> d(i01); reverse if the natural normal faced down.
                    if (flip) r.AddQuad(submesh, i00, i01, i11, i10);
                    else r.AddQuad(submesh, i00, i10, i11, i01);
                }
            }
        }

        private static float SignedArea2D(IReadOnlyList<Vector2> p)
        {
            int n = p.Count;
            float area = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = p[i];
                Vector2 b = p[(i + 1) % n];
                area += a.x * b.y - b.x * a.y;
            }
            return area * 0.5f;
        }

        /// <summary>
        /// Builds a wall between two contours (bottom and top), with vertical subdivisions and
        /// curvature. The profile goes from the base to the top; the curvature bows the wall along
        /// the horizontal direction of each column. If the top is inclined, the wall curves
        /// smoothly to follow it.
        /// </summary>
        /// <param name="outwardDirs">Horizontal "outward" direction per column (contour normal).
        /// It also defines the side the wall normals face.</param>
        /// <param name="faceOutward">true = the wall faces toward <paramref name="outwardDirs"/>; false = the opposite side.</param>
        /// <param name="heightSegments">Vertical subdivisions (>=1).</param>
        /// <param name="curvature">Bowing in world units (0 = straight).</param>
        /// <param name="smooth">true = averaged smooth normals; false = faceted.</param>
        public static void AddWall(MeshBuildResult r, Vector3[] bottom, Vector3[] top, Vector3[] outwardDirs,
            float[] arcLengths, float totalLength, bool closed, int submesh, float uvScale,
            bool faceOutward, int heightSegments, float curvature, bool smooth)
        {
            int n = bottom.Length;
            if (n < 2) return;
            int H = Mathf.Max(1, heightSegments);
            float faceSign = faceOutward ? 1f : -1f;

            // Mean height to scale the UVs' V coordinate consistently.
            float meanHeight = 0f;
            for (int i = 0; i < n; i++) meanHeight += Mathf.Abs(top[i].y - bottom[i].y);
            meanHeight = n > 0 ? meanHeight / n : 1f;

            // Columns: when closed we duplicate the first at the end (UV continuity at the seam).
            int cols = closed ? n + 1 : n;
            int rows = H + 1;

            int baseIndex = r.Vertices.Count;
            var gridIndex = new int[cols * rows];
            var positions = new Vector3[cols * rows];

            for (int c = 0; c < cols; c++)
            {
                int src = c % n;
                float u = (closed && c == n ? totalLength : arcLengths[src]) * uvScale;
                Vector3 dir = outwardDirs[src]; dir.y = 0f;
                if (dir.sqrMagnitude > 1e-8f) dir.Normalize();

                for (int row = 0; row < rows; row++)
                {
                    float v = row / (float)H;
                    Vector3 p = Vector3.Lerp(bottom[src], top[src], v);
                    float bow = Mathf.Sin(v * Mathf.PI) * curvature;
                    p += dir * (bow * faceSign);

                    int gi = c * rows + row;
                    positions[gi] = p;
                    gridIndex[gi] = r.AddVertex(p, Vector3.zero, new Vector2(u, v * meanHeight * uvScale));
                }
            }

            // Triangles.
            for (int c = 0; c < cols - 1; c++)
            {
                for (int row = 0; row < H; row++)
                {
                    int i00 = gridIndex[c * rows + row];
                    int i10 = gridIndex[(c + 1) * rows + row];
                    int i11 = gridIndex[(c + 1) * rows + row + 1];
                    int i01 = gridIndex[c * rows + row + 1];

                    Vector3 p00 = positions[c * rows + row];
                    Vector3 p10 = positions[(c + 1) * rows + row];
                    Vector3 p11 = positions[(c + 1) * rows + row + 1];

                    // Quad orientation according to the column's "outward" direction.
                    Vector3 quadN = TriNormal(p00, p10, p11);
                    Vector3 outward = outwardDirs[c % n]; outward.y = 0f;
                    bool quadIsOutward = Vector3.Dot(quadN, outward) >= 0f;
                    if (quadIsOutward == faceOutward)
                        r.AddQuad(submesh, i00, i10, i11, i01);
                    else
                        r.AddQuad(submesh, i00, i01, i11, i10);
                }
            }

            // Normals.
            if (smooth)
                AccumulateSmoothNormals(r, gridIndex, positions, cols, rows, n, outwardDirs, faceOutward);
            else
                AssignFlatNormals(r, gridIndex, positions, cols, rows, n, outwardDirs, faceOutward);
        }

        /// <summary>
        /// Computes the rings for a bevel (chamfer) between the wall and the floor, and adds the
        /// bevel face to the edge submesh. Returns:
        ///  - floorRing: the floor border inset inward (to triangulate the floor/ring).
        ///  - wallTopRing: the lowered wall top (so the wall ends earlier and leaves room for the bevel).
        /// </summary>
        /// <param name="inwardDirs">Horizontal direction toward the interior of the floor per column.</param>
        /// <param name="outwardDirs">Horizontal outward direction (to orient the bevel normals).</param>
        public static void BuildBevel(MeshBuildResult r, Vector3[] topRim, Vector3[] inwardDirs, Vector3[] outwardDirs,
            bool closed, float bevelSize, int bevelSegments, float bevelCurvature, float uvScale, bool smooth,
            out Vector3[] floorRing, out Vector3[] wallTopRing)
        {
            int n = topRim.Length;
            floorRing = new Vector3[n];
            wallTopRing = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 inward = inwardDirs[i]; inward.y = 0f;
                if (inward.sqrMagnitude > 1e-8f) inward.Normalize();
                floorRing[i] = topRim[i] + inward * bevelSize;
                wallTopRing[i] = topRim[i] - Vector3.up * bevelSize;
            }

            // The chamfer is a mini-wall from the lowered-top (bottom) to the inset-border (top).
            // bevelCurvature bows that chamfer: >0 convex (outward), <0 concave (inward).
            // The bow is scaled by bevelSize so the curvature stays consistent at any bevel size
            // (so lowering the size also shrinks the bulge instead of looking concave/odd).
            float bow = bevelCurvature * bevelSize;
            float[] arc = SplineSampler.CumulativeArc(wallTopRing);
            float total = arc[n - 1] + Vector3.Distance(wallTopRing[n - 1], wallTopRing[0]);
            AddWall(r, wallTopRing, floorRing, outwardDirs, arc, total, closed,
                MeshBuildResult.SubmeshEdge, uvScale, faceOutward: true, Mathf.Max(1, bevelSegments), bow, smooth);
        }

        /// <summary>Smooth normals: average of the neighboring faces in the wall grid.</summary>
        private static void AccumulateSmoothNormals(MeshBuildResult r, int[] gridIndex, Vector3[] pos, int cols, int rows,
            int n, Vector3[] outwardDirs, bool faceOutward)
        {
            var accum = new Vector3[gridIndex.Length];
            for (int c = 0; c < cols - 1; c++)
            {
                for (int row = 0; row < rows - 1; row++)
                {
                    int a = c * rows + row;
                    int b = (c + 1) * rows + row;
                    int d = c * rows + row + 1;
                    Vector3 fn = Vector3.Cross(pos[b] - pos[a], pos[d] - pos[a]);
                    accum[a] += fn; accum[b] += fn; accum[d] += fn;
                    accum[(c + 1) * rows + row + 1] += fn;
                }
            }
            for (int c = 0; c < cols; c++)
            {
                for (int row = 0; row < rows; row++)
                {
                    int i = c * rows + row;
                    Vector3 nrm = accum[i].sqrMagnitude > 1e-10f ? accum[i].normalized : Vector3.up;
                    nrm = OrientOutward(nrm, outwardDirs[c % n], faceOutward);
                    r.Normals[gridIndex[i]] = nrm;
                }
            }
        }

        /// <summary>Faceted normals: each vertex takes the normal of its cell.</summary>
        private static void AssignFlatNormals(MeshBuildResult r, int[] gridIndex, Vector3[] pos, int cols, int rows,
            int n, Vector3[] outwardDirs, bool faceOutward)
        {
            for (int c = 0; c < cols; c++)
            {
                int cc = Mathf.Min(c, cols - 2);
                for (int row = 0; row < rows; row++)
                {
                    int rr = Mathf.Min(row, rows - 2);
                    int a = cc * rows + rr;
                    int b = (cc + 1) * rows + rr;
                    int d = cc * rows + rr + 1;
                    Vector3 fn = Vector3.Cross(pos[b] - pos[a], pos[d] - pos[a]).normalized;
                    fn = OrientOutward(fn, outwardDirs[c % n], faceOutward);
                    r.Normals[gridIndex[c * rows + row]] = fn;
                }
            }
        }

        private static Vector3 OrientOutward(Vector3 normal, Vector3 outwardDir, bool faceOutward)
        {
            Vector3 outward = outwardDir; outward.y = 0f;
            bool isOutward = Vector3.Dot(normal, outward) >= 0f;
            return (isOutward == faceOutward) ? normal : -normal;
        }
    }
}
