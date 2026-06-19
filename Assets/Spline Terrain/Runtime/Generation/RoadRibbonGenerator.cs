using UnityEngine;
using UnityEngine.Splines;
using SplineTerrainTool.Util;

namespace SplineTerrainTool.Generation
{
    /// <summary>
    /// Road mode: full terrain in the shape of a road. Generates an elevated floor that follows the
    /// spline with a configurable width, side walls that descend to y=0 and caps at the ends
    /// (if the spline is open). It is a solid terrain, same as Internal/External.
    /// The upper transform controls height (Y), offset (XZ) and width (scale).
    /// </summary>
    public class RoadRibbonGenerator : ISplineTerrainGenerator
    {
        public SampledOutline BuildOutline(Spline spline, SplineTerrainSettings s, int count)
        {
            // Road keeps the spline's open/closed state and does not reverse to CCW.
            SampledOutline outline = SplineSampler.SampleEvenly(spline, count);
            SplineSampler.SmoothOutline(outline, s.splineSmoothing, s.splineSmoothingIterations);
            return outline;
        }

        public MeshBuildResult Generate(Spline spline, SplineTerrainSettings s, int segmentOverride = -1, SampledOutline overrideOutline = null)
        {
            var result = new MeshBuildResult();

            int count = segmentOverride > 0 ? segmentOverride : s.segmentsPerSpline;
            SampledOutline outline = overrideOutline ?? BuildOutline(spline, s, count);
            int n = outline.Count;
            if (n < 2) return result;

            bool closed = outline.Closed;
            Vector3 offsetXZ = new Vector3(s.topOffset.x, 0f, s.topOffset.z);
            float floorY = s.topOffset.y;                       // floor height
            float half = s.roadWidth * 0.5f * Mathf.Max(0.01f, s.topScale); // width * scale

            var left = new Vector3[n];
            var right = new Vector3[n];
            var leftBottom = new Vector3[n];
            var rightBottom = new Vector3[n];
            var outwardLeft = new Vector3[n];
            var outwardRight = new Vector3[n];
            var inwardLeft = new Vector3[n];
            var inwardRight = new Vector3[n];

            for (int i = 0; i < n; i++)
            {
                Vector3 p = outline.Points[i] + offsetXZ;
                Vector3 nrm = outline.Normals[i];
                left[i] = new Vector3(p.x + nrm.x * half, floorY, p.z + nrm.z * half);
                right[i] = new Vector3(p.x - nrm.x * half, floorY, p.z - nrm.z * half);
                leftBottom[i] = new Vector3(left[i].x, 0f, left[i].z);
                rightBottom[i] = new Vector3(right[i].x, 0f, right[i].z);
                outwardLeft[i] = nrm; inwardLeft[i] = -nrm;
                outwardRight[i] = -nrm; inwardRight[i] = nrm;
            }

            // Optional bevel between each side wall and the floor.
            Vector3[] floorLeft = left, wallTopLeft = left;
            Vector3[] floorRight = right, wallTopRight = right;
            if (s.bevelFloorEdges && s.bevelSize > 0f)
            {
                GeneratorUtils.BuildBevel(result, left, inwardLeft, outwardLeft, closed,
                    s.bevelSize, s.bevelSegments, s.bevelCurvature, s.wallUvScale, s.smoothWalls, out floorLeft, out wallTopLeft);
                GeneratorUtils.BuildBevel(result, right, inwardRight, outwardRight, closed,
                    s.bevelSize, s.bevelSegments, s.bevelCurvature, s.wallUvScale, s.smoothWalls, out floorRight, out wallTopRight);
            }

            // Ribbon floor (normal upward). Fixed winding: we do NOT depend on the geometric
            // normal (in closed curves it flips); the order left->right->right->left always
            // gives an upward-facing face.
            int segments = closed ? n : n - 1;
            for (int i = 0; i < segments; i++)
            {
                int j = (i + 1) % n;

                // Grid mode: subdivide each ribbon quad into a dense paintable grid.
                if (s.floorGrid)
                {
                    GeneratorUtils.AddFloorQuadGrid(result, floorLeft[i], floorRight[i], floorRight[j], floorLeft[j],
                        s.floorUvScale, s.floorCellSize, MeshBuildResult.SubmeshFloor);
                    continue;
                }

                float vA = outline.ArcLengths[i] * s.floorUvScale;
                float vB = (j == 0) ? outline.TotalLength * s.floorUvScale : outline.ArcLengths[j] * s.floorUvScale;

                int lA = result.AddVertex(floorLeft[i], Vector3.up, new Vector2(0f, vA));
                int rA = result.AddVertex(floorRight[i], Vector3.up, new Vector2(1f, vA));
                int rB = result.AddVertex(floorRight[j], Vector3.up, new Vector2(1f, vB));
                int lB = result.AddVertex(floorLeft[j], Vector3.up, new Vector2(0f, vB));
                result.AddQuad(MeshBuildResult.SubmeshFloor, lA, rA, rB, lB);
            }

            // Side walls: descend from each border to y=0, facing outward from the road.
            GeneratorUtils.AddWall(result, leftBottom, wallTopLeft, outwardLeft, outline.ArcLengths, outline.TotalLength,
                closed, MeshBuildResult.SubmeshWall, s.wallUvScale, faceOutward: true,
                s.wallHeightSegments, s.wallCurvature, s.smoothWalls);

            GeneratorUtils.AddWall(result, rightBottom, wallTopRight, outwardRight, outline.ArcLengths, outline.TotalLength,
                closed, MeshBuildResult.SubmeshWall, s.wallUvScale, faceOutward: true,
                s.wallHeightSegments, s.wallCurvature, s.smoothWalls);

            // Caps at the ends (only if the road is open). They close the cross section,
            // accounting for the bevel.
            if (!closed)
            {
                AddCrossSectionCap(result, 0, -outline.Tangents[0], s.wallUvScale,
                    leftBottom, wallTopLeft, floorLeft, floorRight, wallTopRight, rightBottom);
                AddCrossSectionCap(result, n - 1, outline.Tangents[n - 1], s.wallUvScale,
                    leftBottom, wallTopLeft, floorLeft, floorRight, wallTopRight, rightBottom);
            }

            return result;
        }

        /// <summary>
        /// Closes the cross section at one end (a polygon: bottom left, wall top left,
        /// floor border left, floor border right, wall top right, bottom right) with a triangulated fan.
        /// </summary>
        private static void AddCrossSectionCap(MeshBuildResult r, int i, Vector3 facing, float uvScale,
            Vector3[] leftBottom, Vector3[] wallTopLeft, Vector3[] floorLeft,
            Vector3[] floorRight, Vector3[] wallTopRight, Vector3[] rightBottom)
        {
            facing.Normalize();
            Vector3 widthDir = Vector3.Cross(Vector3.up, facing);

            var poly = new Vector3[]
            {
                leftBottom[i], wallTopLeft[i], floorLeft[i],
                floorRight[i], wallTopRight[i], rightBottom[i]
            };

            int baseIndex = r.Vertices.Count;
            for (int k = 0; k < poly.Length; k++)
            {
                Vector2 uv = new Vector2(Vector3.Dot(poly[k], widthDir), poly[k].y) * uvScale;
                r.AddVertex(poly[k], facing, uv);
            }

            // Fan from the first vertex; we correct winding per triangle according to the requested normal.
            for (int k = 1; k < poly.Length - 1; k++)
            {
                int a = baseIndex;
                int b = baseIndex + k;
                int c = baseIndex + k + 1;
                Vector3 triN = GeneratorUtils.TriNormal(r.Vertices[a], r.Vertices[b], r.Vertices[c]);
                if (Vector3.Dot(triN, facing) >= 0f)
                    r.AddTriangle(MeshBuildResult.SubmeshEdge, a, b, c);
                else
                    r.AddTriangle(MeshBuildResult.SubmeshEdge, a, c, b);
            }
        }
    }
}
