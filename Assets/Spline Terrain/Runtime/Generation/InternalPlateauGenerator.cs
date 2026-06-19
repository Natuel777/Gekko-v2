using UnityEngine;
using UnityEngine.Splines;
using SplineTerrainTool.Util;

namespace SplineTerrainTool.Generation
{
    /// <summary>
    /// Internal mode: plateau. Fills the interior of the closed spline with an upper floor
    /// (scaled relative to the base) and inclined walls that descend to the base contour.
    /// </summary>
    public class InternalPlateauGenerator : ISplineTerrainGenerator
    {
        public SampledOutline BuildOutline(Spline spline, SplineTerrainSettings s, int count)
        {
            // forceClosed: Internal mode works even if the spline is open (the shape is closed).
            SampledOutline outline = SplineSampler.SampleEvenly(spline, count, forceClosed: true);
            // We normalize to CCW so the winding corrections are consistent.
            if (SplineSampler.IsClockwiseXZ(outline.Points))
                SplineSampler.Reverse(outline);
            // Contour smoothing (for very pronounced splines).
            SplineSampler.SmoothOutline(outline, s.splineSmoothing, s.splineSmoothingIterations);
            return outline;
        }

        public MeshBuildResult Generate(Spline spline, SplineTerrainSettings s, int segmentOverride = -1, SampledOutline overrideOutline = null)
        {
            var result = new MeshBuildResult();

            int count = segmentOverride > 0 ? segmentOverride : s.segmentsPerSpline;
            SampledOutline outline = overrideOutline ?? BuildOutline(spline, s, count);
            int n = outline.Count;
            if (n < 3) return result;

            Vector3 centroid = SplineSampler.Centroid(outline.Points);

            // Floor matrix: scale + rotation (inclination) around the centroid + offset.
            Matrix4x4 topMatrix = s.BuildTopMatrix(centroid);

            // Base contour (bottom) and upper contour (transformed by the floor matrix).
            var bottom = new Vector3[n];
            var topRim = new Vector3[n];
            var inward = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                bottom[i] = outline.Points[i];
                topRim[i] = topMatrix.MultiplyPoint3x4(outline.Points[i]);
                inward[i] = -outline.Normals[i]; // toward the center
            }

            // Optional bevel between wall and floor (edge submesh).
            Vector3[] floorRing, wallTop;
            if (s.bevelFloorEdges && s.bevelSize > 0f)
                GeneratorUtils.BuildBevel(result, topRim, inward, outline.Normals, closed: true,
                    s.bevelSize, s.bevelSegments, s.bevelCurvature, s.wallUvScale, s.smoothWalls, out floorRing, out wallTop);
            else { floorRing = topRim; wallTop = topRim; }

            // Floor (upper cap): real normal (may be inclined), facing upward.
            // Grid mode: dense paintable topology. Otherwise: plain ear-clip fan.
            if (s.floorGrid && s.floorGridStyle == FloorGridStyle.ClippedGrid)
                GeneratorUtils.AddGridCap(result, floorRing, s.floorUvScale, faceUp: true, s.floorCellSize, MeshBuildResult.SubmeshFloor);
            else if (s.floorGrid)
                GeneratorUtils.AddSubdividedCap(result, floorRing, s.floorUvScale, faceUp: true, s.floorCellSize, MeshBuildResult.SubmeshFloor);
            else
                GeneratorUtils.AddFlatCap(result, floorRing, s.floorUvScale, faceUp: true, MeshBuildResult.SubmeshFloor);

            // Curved walls: from the base to the top (possibly lowered by the bevel), normal outward.
            GeneratorUtils.AddWall(result, bottom, wallTop, outline.Normals, outline.ArcLengths, outline.TotalLength,
                closed: true, MeshBuildResult.SubmeshWall, s.wallUvScale, faceOutward: true,
                s.wallHeightSegments, s.wallCurvature, s.smoothWalls);

            return result;
        }
    }
}
