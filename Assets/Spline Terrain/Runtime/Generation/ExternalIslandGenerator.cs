using UnityEngine;
using UnityEngine.Splines;
using SplineTerrainTool.Util;

namespace SplineTerrainTool.Generation
{
    /// <summary>
    /// External mode: island with a hole. The interior of the spline remains as a hollow; the geometry
    /// is generated outward (ring) between the spline contour (inner border) and an
    /// external contour = outward offset + noise. Requires a closed spline.
    /// </summary>
    public class ExternalIslandGenerator : ISplineTerrainGenerator
    {
        public SampledOutline BuildOutline(Spline spline, SplineTerrainSettings s, int count)
        {
            // forceClosed: External mode works even if the spline is open.
            SampledOutline outline = SplineSampler.SampleEvenly(spline, count, forceClosed: true);
            if (SplineSampler.IsClockwiseXZ(outline.Points))
                SplineSampler.Reverse(outline);
            // Contour smoothing before computing the external offset.
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

            // External contour: outward offset + noise along the normal.
            Vector3[] outer = PolygonOffset.Offset(outline.Points, outline.Normals, s.outwardDistance, outward: true);

            // Floor matrix (offset + inclination + scale) applied to the upper ring.
            Matrix4x4 topMatrix = s.BuildTopMatrix(centroid);

            // Upper ring (transformed by the floor matrix) and projections to y=0 for the walls.
            var innerTopRim = new Vector3[n];
            var innerBottom = new Vector3[n];
            var outerTopRim = new Vector3[n];
            var outerBottom = new Vector3[n];

            for (int i = 0; i < n; i++)
            {
                Vector3 b = outline.Points[i];
                Vector3 innerFlat = new Vector3(b.x, 0f, b.z);
                innerBottom[i] = innerFlat;
                innerTopRim[i] = topMatrix.MultiplyPoint3x4(innerFlat);

                Vector3 o = outer[i];
                float noise = TerrainNoise.Sample(o.x, o.z, s.noiseFrequency, s.noiseSeed) * s.noiseAmplitude;
                Vector3 pushed = o + outline.Normals[i] * noise;
                Vector3 outerFlat = new Vector3(pushed.x, 0f, pushed.z);
                outerBottom[i] = outerFlat;
                outerTopRim[i] = topMatrix.MultiplyPoint3x4(outerFlat);
            }

            // Directions "toward the interior of the ring floor":
            //  - on the external border, the interior is toward the center  (-normal).
            //  - on the internal border (hole rim), the interior is outward (+normal).
            var outerInward = new Vector3[n];
            var innerInward = new Vector3[n];
            var negNormals = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                outerInward[i] = -outline.Normals[i];
                innerInward[i] = outline.Normals[i];
                negNormals[i] = -outline.Normals[i];
            }

            // Optional bevel on both borders (edge submesh). Returns the floor rings
            // (inset) and the wall tops (lowered).
            Vector3[] floorOuter = outerTopRim, wallTopOuter = outerTopRim;
            Vector3[] floorInner = innerTopRim, wallTopInner = innerTopRim;
            bool bevel = s.bevelFloorEdges && s.bevelSize > 0f;
            if (bevel)
            {
                GeneratorUtils.BuildBevel(result, outerTopRim, outerInward, outline.Normals, closed: true,
                    s.bevelSize, s.bevelSegments, s.bevelCurvature, s.wallUvScale, s.smoothWalls, out floorOuter, out wallTopOuter);

                // The internal bevel only makes sense if there is an internal wall.
                if (s.externalInnerWall)
                    GeneratorUtils.BuildBevel(result, innerTopRim, innerInward, negNormals, closed: true,
                        s.bevelSize, s.bevelSegments, s.bevelCurvature, s.wallUvScale, s.smoothWalls, out floorInner, out wallTopInner);
            }

            // Floor = ring between the internal and external borders (facing upward).
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                int it = result.AddVertex(floorInner[i], Vector3.up, new Vector2(floorInner[i].x, floorInner[i].z) * s.floorUvScale);
                int ot = result.AddVertex(floorOuter[i], Vector3.up, new Vector2(floorOuter[i].x, floorOuter[i].z) * s.floorUvScale);
                int oj = result.AddVertex(floorOuter[j], Vector3.up, new Vector2(floorOuter[j].x, floorOuter[j].z) * s.floorUvScale);
                int ij = result.AddVertex(floorInner[j], Vector3.up, new Vector2(floorInner[j].x, floorInner[j].z) * s.floorUvScale);

                Vector3 normal = GeneratorUtils.TriNormal(floorInner[i], floorOuter[i], floorOuter[j]);
                if (normal.y >= 0f)
                    result.AddQuad(MeshBuildResult.SubmeshFloor, it, ot, oj, ij);
                else
                    result.AddQuad(MeshBuildResult.SubmeshFloor, it, ij, oj, ot);
            }

            // External wall: from the external border downward, facing outward. Wall submesh.
            float[] outerArc = SplineSampler.CumulativeArc(outerBottom);
            float outerTotal = outerArc[n - 1] + Vector3.Distance(outerBottom[n - 1], outerBottom[0]);
            GeneratorUtils.AddWall(result, outerBottom, wallTopOuter, outline.Normals, outerArc, outerTotal,
                closed: true, MeshBuildResult.SubmeshWall, s.wallUvScale, faceOutward: true,
                s.wallHeightSegments, s.wallCurvature, s.smoothWalls);

            // Internal wall (hole rim): faces the center. Wall submesh.
            if (s.externalInnerWall)
            {
                GeneratorUtils.AddWall(result, innerBottom, wallTopInner, outline.Normals, outline.ArcLengths, outline.TotalLength,
                    closed: true, MeshBuildResult.SubmeshWall, s.wallUvScale, faceOutward: false,
                    s.wallHeightSegments, s.wallCurvature, s.smoothWalls);
            }

            return result;
        }
    }
}
