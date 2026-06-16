using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace SplineTerrainTool.Util
{
    /// <summary>
    /// Sampled outline of a spline: points evenly spaced along arc length,
    /// with their tangents, planar normals (XZ) and accumulated length.
    /// Everything in the spline's local space.
    /// </summary>
    public class SampledOutline
    {
        public Vector3[] Points;        // positions (y = original spline position, normally 0)
        public Vector3[] Tangents;      // normalized forward direction
        public Vector3[] Normals;       // planar normal pointing "outward" (XZ), perpendicular to the tangent
        public float[] ArcLengths;      // accumulated length from point 0 to each point
        public float TotalLength;
        public bool Closed;

        public int Count => Points != null ? Points.Length : 0;
    }

    /// <summary>
    /// Spline sampling evenly spaced by arc length using a LUT.
    /// Stepping the parameter t uniformly does NOT give evenly spaced points,
    /// so we build a length->t table and invert it.
    /// </summary>
    public static class SplineSampler
    {
        /// <summary>
        /// Samples <paramref name="count"/> evenly spaced points along the spline.
        /// For closed splines the first point is not duplicated at the end (the loop wraps around).
        /// <paramref name="forceClosed"/> treats the outline as closed even if the spline is
        /// open (closes the shape without modifying the asset) — used by the Internal/External modes.
        /// </summary>
        public static SampledOutline SampleEvenly(Spline spline, int count, bool forceClosed = false)
        {
            count = Mathf.Max(2, count);
            bool closed = spline.Closed || forceClosed;

            // 1) Fine arc-length LUT.
            int fine = Mathf.Max(count * 4, 256);
            var lutT = new float[fine + 1];
            var lutLen = new float[fine + 1];
            float3 prev = spline.EvaluatePosition(0f);
            lutT[0] = 0f;
            lutLen[0] = 0f;
            float accum = 0f;
            for (int j = 1; j <= fine; j++)
            {
                float t = (float)j / fine;
                float3 p = spline.EvaluatePosition(t);
                accum += math.distance(prev, p);
                lutT[j] = t;
                lutLen[j] = accum;
                prev = p;
            }
            float total = accum;

            var outline = new SampledOutline
            {
                Points = new Vector3[count],
                Tangents = new Vector3[count],
                Normals = new Vector3[count],
                ArcLengths = new float[count],
                Closed = closed
            };

            // 2) For each output point, target distance -> t (inverting the LUT).
            // Closed: count points covering [0, total) without repeating the last one.
            // Open: count points covering [0, total] including both endpoints.
            int divisor = closed ? count : count - 1;
            for (int i = 0; i < count; i++)
            {
                float targetLen = total * i / divisor;
                float t = InvertLut(lutT, lutLen, targetLen);

                float3 pos = spline.EvaluatePosition(t);
                float3 tan = spline.EvaluateTangent(t);
                Vector3 tangent = ((Vector3)(float3)math.normalizesafe(tan, new float3(0, 0, 1))).normalized;

                outline.Points[i] = (Vector3)pos;
                outline.Tangents[i] = tangent;
                // Planar normal: perpendicular to the tangent on the XZ plane.
                outline.Normals[i] = Vector3.Cross(Vector3.up, tangent).normalized;
            }

            // 3) Accumulated length from the final points (consistent with the geometry).
            float running = 0f;
            outline.ArcLengths[0] = 0f;
            for (int i = 1; i < count; i++)
            {
                running += Vector3.Distance(outline.Points[i - 1], outline.Points[i]);
                outline.ArcLengths[i] = running;
            }
            if (closed)
                running += Vector3.Distance(outline.Points[count - 1], outline.Points[0]);
            outline.TotalLength = closed ? running : outline.ArcLengths[count - 1];

            return outline;
        }

        /// <summary>Given a target length, returns the interpolated t in the LUT.</summary>
        private static float InvertLut(float[] lutT, float[] lutLen, float targetLen)
        {
            int n = lutLen.Length;
            if (targetLen <= 0f) return lutT[0];
            if (targetLen >= lutLen[n - 1]) return lutT[n - 1];

            // Linear search (the LUT is small). It could be binary, but this is more readable.
            for (int j = 1; j < n; j++)
            {
                if (lutLen[j] >= targetLen)
                {
                    float segLen = lutLen[j] - lutLen[j - 1];
                    float f = segLen > 1e-6f ? (targetLen - lutLen[j - 1]) / segLen : 0f;
                    return Mathf.Lerp(lutT[j - 1], lutT[j], f);
                }
            }
            return lutT[n - 1];
        }

        /// <summary>
        /// Returns true if the outline (projected to XZ) is in clockwise order (CW).
        /// Useful to correct the winding so the normals point correctly.
        /// </summary>
        public static bool IsClockwiseXZ(IReadOnlyList<Vector3> points)
        {
            return SignedAreaXZ(points) < 0f;
        }

        /// <summary>Signed area of the polygon in XZ. Positive = CCW.</summary>
        public static float SignedAreaXZ(IReadOnlyList<Vector3> points)
        {
            int n = points.Count;
            float area = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = points[i];
                Vector3 b = points[(i + 1) % n];
                area += a.x * b.z - b.x * a.z;
            }
            return area * 0.5f;
        }

        /// <summary>Reverses the order of an outline (and its associated arrays) in-place.</summary>
        public static void Reverse(SampledOutline outline)
        {
            System.Array.Reverse(outline.Points);
            System.Array.Reverse(outline.Tangents);
            System.Array.Reverse(outline.Normals);
            // Tangents and normals end up pointing backward: we recompute them.
            int n = outline.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 next = outline.Points[(i + 1) % n];
                Vector3 dir = (next - outline.Points[i]);
                if (dir.sqrMagnitude > 1e-8f)
                {
                    outline.Tangents[i] = dir.normalized;
                    outline.Normals[i] = Vector3.Cross(Vector3.up, outline.Tangents[i]).normalized;
                }
            }
            // Recompute accumulated lengths.
            float running = 0f;
            outline.ArcLengths[0] = 0f;
            for (int i = 1; i < n; i++)
            {
                running += Vector3.Distance(outline.Points[i - 1], outline.Points[i]);
                outline.ArcLengths[i] = running;
            }
        }

        /// <summary>Centroid (average) of the outline.</summary>
        public static Vector3 Centroid(IReadOnlyList<Vector3> points)
        {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < points.Count; i++) sum += points[i];
            return points.Count > 0 ? sum / points.Count : Vector3.zero;
        }

        /// <summary>
        /// Smooths the outline (Laplacian) by moving each point toward the average of its neighbors.
        /// Reduces very sharp corners that break the offset/edge curve. Recomputes
        /// tangents, normals and lengths. <paramref name="strength"/> 0..1, several iterations.
        /// </summary>
        public static void SmoothOutline(SampledOutline outline, float strength, int iterations)
        {
            if (outline == null || strength <= 0f || iterations <= 0) return;
            int n = outline.Count;
            if (n < 3) return;
            strength = Mathf.Clamp01(strength);

            var pts = outline.Points;
            for (int it = 0; it < iterations; it++)
            {
                var copy = (Vector3[])pts.Clone();
                int start = outline.Closed ? 0 : 1;
                int end = outline.Closed ? n : n - 1;
                for (int i = start; i < end; i++)
                {
                    Vector3 prev = copy[(i - 1 + n) % n];
                    Vector3 next = copy[(i + 1) % n];
                    Vector3 avg = (prev + next) * 0.5f;
                    pts[i] = Vector3.Lerp(copy[i], avg, strength);
                }
            }

            RecomputeDerived(outline);
        }

        /// <summary>Recomputes tangents, planar normals and arc lengths from the points.</summary>
        private static void RecomputeDerived(SampledOutline outline)
        {
            int n = outline.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 next = outline.Points[(i + 1) % n];
                Vector3 prev = outline.Points[(i - 1 + n) % n];
                Vector3 dir = (next - prev);
                if (dir.sqrMagnitude > 1e-8f)
                {
                    outline.Tangents[i] = dir.normalized;
                    outline.Normals[i] = Vector3.Cross(Vector3.up, outline.Tangents[i]).normalized;
                }
            }
            float running = 0f;
            outline.ArcLengths[0] = 0f;
            for (int i = 1; i < n; i++)
            {
                running += Vector3.Distance(outline.Points[i - 1], outline.Points[i]);
                outline.ArcLengths[i] = running;
            }
            if (outline.Closed)
                running += Vector3.Distance(outline.Points[n - 1], outline.Points[0]);
            outline.TotalLength = outline.Closed ? running : outline.ArcLengths[n - 1];
        }

        /// <summary>Accumulated arc length of a ring of points.</summary>
        public static float[] CumulativeArc(IReadOnlyList<Vector3> ring)
        {
            int n = ring.Count;
            var arc = new float[n];
            float running = 0f;
            for (int i = 1; i < n; i++)
            {
                running += Vector3.Distance(ring[i - 1], ring[i]);
                arc[i] = running;
            }
            return arc;
        }

        /// <summary>
        /// Simplifies the outline by angle + distance decimation (in XZ): keeps a point whenever
        /// enough turning has accumulated since the last kept point, or a maximum chord length is
        /// reached. Unlike RDP, this DOES reduce smooth curves (a circle of 96 sides becomes ~20),
        /// while still keeping sharp corners. Deterministic, so it stays consistent with the shape.
        /// <paramref name="strength01"/> 0 = no change, 1 = very coarse. Returns a new outline.
        /// </summary>
        public static SampledOutline Simplify(SampledOutline outline, float strength01)
        {
            int n = outline.Count;
            if (strength01 <= 0f || n < 6) return outline;
            strength01 = Mathf.Clamp01(strength01);

            // Higher strength -> keep a point only after more turning, and allow longer chords.
            float thresholdDeg = Mathf.Lerp(3f, 45f, strength01);
            float maxChord = outline.TotalLength / Mathf.Lerp(40f, 6f, strength01);

            var pts = outline.Points;
            bool closed = outline.Closed;
            var keep = new bool[n];
            keep[0] = true;
            if (!closed) keep[n - 1] = true;

            float accAngle = 0f, accDist = 0f;
            int endExclusive = closed ? n : n - 1;
            for (int i = 1; i < endExclusive; i++)
            {
                accDist += Vector2.Distance(XZ(pts[i]), XZ(pts[i - 1]));

                Vector2 inDir = XZ(pts[i]) - XZ(pts[i - 1]);
                Vector2 outDir = XZ(pts[(i + 1) % n]) - XZ(pts[i]);
                accAngle += Vector2.Angle(inDir, outDir);

                if (accAngle >= thresholdDeg || accDist >= maxChord)
                {
                    keep[i] = true;
                    accAngle = 0f;
                    accDist = 0f;
                }
            }

            var kept = new List<Vector3>(n);
            for (int i = 0; i < n; i++) if (keep[i]) kept.Add(pts[i]);
            if (kept.Count < (closed ? 3 : 2)) return outline; // too aggressive: keep original

            return MakeOutline(kept, closed);
        }

        private static Vector2 XZ(Vector3 v) => new Vector2(v.x, v.z);

        /// <summary>Builds a fresh outline from a list of points and recomputes its derived data.</summary>
        public static SampledOutline MakeOutline(IReadOnlyList<Vector3> points, bool closed)
        {
            int n = points.Count;
            var outline = new SampledOutline
            {
                Points = new Vector3[n],
                Tangents = new Vector3[n],
                Normals = new Vector3[n],
                ArcLengths = new float[n],
                Closed = closed
            };
            for (int i = 0; i < n; i++) outline.Points[i] = points[i];
            RecomputeDerived(outline);
            return outline;
        }
    }
}
