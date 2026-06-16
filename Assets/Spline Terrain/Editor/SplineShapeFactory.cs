using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace SplineTerrainTool.EditorTools
{
    /// <summary>
    /// Builds initial splines (circle, square, line) using AutoSmooth tangents,
    /// which avoid having to compute local tangents by hand.
    /// </summary>
    public static class SplineShapeFactory
    {
        public enum StartShape { Circle, Square, Line }

        public static void Build(Spline spline, StartShape shape, float size, int circleKnots = 8)
        {
            spline.Clear();
            switch (shape)
            {
                case StartShape.Square: BuildSquare(spline, size); break;
                case StartShape.Line: BuildLine(spline, size); break;
                default: BuildCircle(spline, size, circleKnots); break;
            }
        }

        public static void BuildCircle(Spline spline, float radius, int knotCount)
        {
            knotCount = Mathf.Max(4, knotCount);
            spline.Closed = true;
            for (int i = 0; i < knotCount; i++)
            {
                float angle = (i / (float)knotCount) * Mathf.PI * 2f; // CCW
                var pos = new float3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                spline.Add(new BezierKnot(pos), TangentMode.AutoSmooth);
            }
        }

        public static void BuildSquare(Spline spline, float size)
        {
            spline.Closed = true;
            float h = size * 0.5f;
            float3[] corners =
            {
                new float3(-h, 0f, -h),
                new float3( h, 0f, -h),
                new float3( h, 0f,  h),
                new float3(-h, 0f,  h),
            };
            foreach (var c in corners)
                spline.Add(new BezierKnot(c), TangentMode.Linear);
        }

        public static void BuildLine(Spline spline, float length)
        {
            spline.Closed = false;
            int knots = 4;
            for (int i = 0; i < knots; i++)
            {
                float t = i / (float)(knots - 1);
                var pos = new float3(Mathf.Lerp(-length * 0.5f, length * 0.5f, t), 0f, 0f);
                spline.Add(new BezierKnot(pos), TangentMode.AutoSmooth);
            }
        }
    }
}
