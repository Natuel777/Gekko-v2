using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace SplineTerrainTool
{
    /// <summary>
    /// Baked asset of a terrain: stores all the parameters and a snapshot of the spline knots
    /// (so it can be edited again), plus references to the visual mesh and the low-poly
    /// collider mesh generated during the bake.
    /// </summary>
    [CreateAssetMenu(fileName = "SplineTerrainData", menuName = "Spline Terrain/Terrain Data")]
    public class SplineTerrainData : ScriptableObject
    {
        [Serializable]
        public struct KnotData
        {
            public Vector3 position;
            public Vector3 tangentIn;
            public Vector3 tangentOut;
            public Quaternion rotation;
        }

        public SplineTerrainSettings settings = new SplineTerrainSettings();

        public KnotData[] knots = Array.Empty<KnotData>();
        public bool closed;

        public Mesh visualMesh;
        public Mesh colliderMesh;

        public Material floorMaterial;
        public Material wallMaterial;
        public Material edgeMaterial;

        /// <summary>Captures the settings and the spline shape from a terrain.</summary>
        public void CaptureFrom(SplineTerrainComponentSnapshot snapshot)
        {
            settings = snapshot.Settings != null ? snapshot.Settings.Clone() : new SplineTerrainSettings();
            floorMaterial = snapshot.Floor;
            wallMaterial = snapshot.Wall;
            edgeMaterial = snapshot.Edge;
            CaptureSpline(snapshot.Spline);
        }

        /// <summary>Stores the spline knots in the serializable snapshot.</summary>
        public void CaptureSpline(Spline spline)
        {
            if (spline == null)
            {
                knots = Array.Empty<KnotData>();
                closed = false;
                return;
            }

            closed = spline.Closed;
            knots = new KnotData[spline.Count];
            for (int i = 0; i < spline.Count; i++)
            {
                BezierKnot k = spline[i];
                knots[i] = new KnotData
                {
                    position = (Vector3)k.Position,
                    tangentIn = (Vector3)k.TangentIn,
                    tangentOut = (Vector3)k.TangentOut,
                    rotation = k.Rotation
                };
            }
        }

        /// <summary>Rebuilds a spline from the stored snapshot.</summary>
        public void ApplyToSpline(Spline spline)
        {
            if (spline == null) return;
            spline.Clear();
            spline.Closed = closed;
            for (int i = 0; i < knots.Length; i++)
            {
                KnotData k = knots[i];
                var knot = new BezierKnot(
                    (float3)k.position,
                    (float3)k.tangentIn,
                    (float3)k.tangentOut,
                    k.rotation);
                spline.Add(knot);
            }
        }
    }

    /// <summary>
    /// Minimum data needed to bake, so the SO is not coupled to the MonoBehaviour.
    /// </summary>
    public struct SplineTerrainComponentSnapshot
    {
        public SplineTerrainSettings Settings;
        public Spline Spline;
        public Material Floor;
        public Material Wall;
        public Material Edge;
    }
}
