using System;
using UnityEngine;

namespace SplineTerrainTool
{
    /// <summary>
    /// Terrain generation mode.
    /// </summary>
    public enum SplineTerrainMode
    {
        /// <summary>Plateau: fills the interior of the closed spline with a raised floor and sloped walls.</summary>
        Internal,
        /// <summary>Road: flat ribbon that follows the spline with a configurable width.</summary>
        Road,
        /// <summary>Island: ring extending outward from the spline (the interior becomes a hole) with noise on the edge.</summary>
        External
    }

    /// <summary>
    /// How the dense floor is built when <see cref="SplineTerrainSettings.floorGrid"/> is on.
    /// </summary>
    public enum FloorGridStyle
    {
        /// <summary>Regular row/column grid clipped to the outline. Even quads inside, but the boundary
        /// cells are split into small irregular polygons.</summary>
        ClippedGrid,
        /// <summary>Triangulates the outline (ear clip) and subdivides each triangle uniformly. The
        /// boundary follows the outline exactly (no clipped slivers); interior is triangle-based.</summary>
        SubdividedContour
    }

    /// <summary>
    /// How the baked/live collider is split into separate MeshCollider pieces.
    /// The visual mesh is NOT affected by this; it only changes the collider geometry.
    /// Groups map to the submeshes: floor (0), wall (1), bevel/edge (2).
    /// </summary>
    public enum ColliderSplitMode
    {
        /// <summary>One single collider with floor + wall + bevel together (default).</summary>
        AllTogether,
        /// <summary>Three separate colliders: floor, wall and bevel each on their own.</summary>
        AllSeparate,
        /// <summary>Two colliders: floor+bevel together, wall on its own.</summary>
        FloorBevelTogether_WallSeparate,
        /// <summary>Two colliders: wall+bevel together, floor on its own.</summary>
        WallBevelTogether_FloorSeparate
    }

    /// <summary>
    /// All the editable terrain parameters in a single serializable object.
    /// Shared between the <see cref="SplineTerrain"/> component and the bake ScriptableObject,
    /// so they can be copied from one to the other without duplicating fields.
    /// </summary>
    [Serializable]
    public class SplineTerrainSettings
    {
        [Header("General")]
        public SplineTerrainMode mode = SplineTerrainMode.Internal;

        [Tooltip("Number of points sampled along the spline for the visual mesh. (affects performance)")]
        [Range(3, 400)] public int segmentsPerSpline = 96;

        [Tooltip("Whether the baked collider is identical to the visual mesh. Turn it off for an optimized collider " +
                 "that keeps the same shape (and bevel) but collapses redundant straight wall/bevel rows.")]
        public bool colliderMatchesVisual = true;

        [Tooltip("Optional EXTRA collider reduction (only if it does NOT match the visual): decimates the perimeter. " +
                 "0 = keep the full perimeter / footprint (recommended for faithful collision). Higher reduces the " +
                 "floor and wall columns at the cost of shape fidelity.")]
        [Range(0f, 1f)] public float colliderSimplify = 0f;

        [Tooltip("How the collider is split into separate MeshCollider pieces (does NOT affect the visual mesh). " +
                 "All together = one collider; the rest separate floor / wall / bevel into their own child colliders.")]
        public ColliderSplitMode colliderSplit = ColliderSplitMode.AllTogether;

        [Header("Visual mesh layout")]
        [Tooltip("Splits the VISUAL mesh into separate child GameObjects (Floor / Walls) under the terrain, " +
                 "so the floor can carry a denser grid for Polybrush painting independently from the walls. " +
                 "Everything stays parented under the terrain GameObject for tidiness.")]
        public bool separateVisualMeshes = false;

        [Tooltip("Replaces the floor triangulation (fan/ear-clip) with a dense grid so it has enough polygon " +
                 "density to be painted with Polybrush.")]
        public bool floorGrid = false;

        [Tooltip("Floor grid technique (only the Internal solid floor; Road/External always subdivide their quads). " +
                 "Clipped Grid = even quads but messy boundary cells. Subdivided Contour = clean outline-following " +
                 "boundary, triangle-based interior (usually fewer odd border polys).")]
        public FloorGridStyle floorGridStyle = FloorGridStyle.ClippedGrid;

        [Tooltip("Approximate world-space size of each floor grid cell. Smaller = denser (more polys). " +
                 "Drives the row/column subdivision of the floor in every mode.")]
        [Min(0.05f)] public float floorCellSize = 1f;

        [Tooltip("Smoothing of the outline relative to the spline (0 = none). Helps with very sharp splines.")]
        [Range(0f, 1f)] public float splineSmoothing = 0f;

        [Tooltip("Iterations of the outline smoothing. Many iterations shrink the shape.")]
        [Range(0, 50)] public int splineSmoothingIterations = 2;

        [Header("Top / Floor (Internal)")]
        [Tooltip("Offset of the top floor relative to the center (Y = height, XZ = offset). Mirrors the localPosition of the top transform.")]
        public Vector3 topOffset = new Vector3(0f, 1f, 0f);

        [Tooltip("Rotation (tilt) of the top floor in degrees. Mirrors the localRotation of the top transform.")]
        public Vector3 topEuler = Vector3.zero;

        [Tooltip("XZ scale of the top outline relative to the base. <1 makes the floor smaller than the base.")]
        [Min(0f)] public float topScale = 0.7f;

        [Header("Walls / Smoothing")]
        [Tooltip("Vertical subdivisions of the walls. More = smoother walls and better curvature. (affects performance)")]
        [Range(1, 64)] public int wallHeightSegments = 6;

        [Tooltip("Curvature (bulge) of the walls in world units. >0 convex outward, <0 concave.")]
        public float wallCurvature = 0f;

        [Tooltip("Whether the walls use smooth shading (averaged normals) instead of faceted.")]
        public bool smoothWalls = true;

        [Tooltip("Applies a bevel (chamfer) between the wall and the floor to soften the edge. Uses the edge material.")]
        public bool bevelFloorEdges = false;

        [Tooltip("Size of the bevel between wall and floor.")]
        [Min(0f)] public float bevelSize = 0.1f;

        [Tooltip("Subdivisions of the bevel (more = more rounded). (affects performance)")]
        [Range(1, 32)] public int bevelSegments = 2;

        [Tooltip("Bevel curvature as a fraction of the bevel size (so it scales with it): " +
                 ">0 convex (outward, ~0.55 looks like a rounded quarter), <0 concave (inward), 0 = flat chamfer.")]
        [Range(-1f, 1f)] public float bevelCurvature = 0f;

        [Header("Road (Road mode)")]
        [Tooltip("Width of the road.")]
        [Min(0.001f)] public float roadWidth = 2f;

        [Header("Island (External mode)")]
        [Tooltip("Distance the outline is pushed outward to form the external edge.")]
        [Min(0.001f)] public float outwardDistance = 4f;

        [Tooltip("Amplitude of the noise applied to the external edge.")]
        public float noiseAmplitude = 1f;

        [Tooltip("Frequency of the external edge noise.")]
        public float noiseFrequency = 0.2f;

        [Tooltip("Noise seed to vary the shape of the island.")]
        public int noiseSeed = 0;

        [Tooltip("Whether the inner edge (rim of the hole) generates a vertical wall (edge submesh).")]
        public bool externalInnerWall = true;

        [Header("UVs / Tiling")]
        [Tooltip("Scale of the floor UVs (planar XZ).")]
        public float floorUvScale = 1f;

        [Tooltip("Scale of the wall UVs (arc-length x height).")]
        public float wallUvScale = 1f;

        /// <summary>Floor height (Y of the offset). Read shortcut.</summary>
        public float TopHeight => topOffset.y;

        /// <summary>
        /// Matrix that transforms the base outline (in terrain local space) to the top
        /// floor outline, applying scale + rotation (pivoting at the centroid) and the offset.
        /// top[i] = M * base[i].
        /// </summary>
        public Matrix4x4 BuildTopMatrix(Vector3 centroid)
        {
            Quaternion rot = Quaternion.Euler(topEuler);
            float s = Mathf.Max(0.0001f, topScale);
            Vector3 scale = new Vector3(s, 1f, s);
            // Pivot rotation/scale around the centroid and then add the offset.
            return Matrix4x4.Translate(centroid + topOffset)
                 * Matrix4x4.Rotate(rot)
                 * Matrix4x4.Scale(scale)
                 * Matrix4x4.Translate(-centroid);
        }

        /// <summary>Deep copy of the settings (for the bake and the undo).</summary>
        public SplineTerrainSettings Clone()
        {
            return (SplineTerrainSettings)MemberwiseClone();
        }

        /// <summary>Applies the values of <paramref name="other"/> onto this instance.</summary>
        public void CopyFrom(SplineTerrainSettings other)
        {
            if (other == null) return;
            mode = other.mode;
            segmentsPerSpline = other.segmentsPerSpline;
            colliderMatchesVisual = other.colliderMatchesVisual;
            colliderSimplify = other.colliderSimplify;
            colliderSplit = other.colliderSplit;
            separateVisualMeshes = other.separateVisualMeshes;
            floorGrid = other.floorGrid;
            floorGridStyle = other.floorGridStyle;
            floorCellSize = other.floorCellSize;
            splineSmoothing = other.splineSmoothing;
            splineSmoothingIterations = other.splineSmoothingIterations;
            topOffset = other.topOffset;
            topEuler = other.topEuler;
            topScale = other.topScale;
            wallHeightSegments = other.wallHeightSegments;
            wallCurvature = other.wallCurvature;
            smoothWalls = other.smoothWalls;
            roadWidth = other.roadWidth;
            outwardDistance = other.outwardDistance;
            noiseAmplitude = other.noiseAmplitude;
            noiseFrequency = other.noiseFrequency;
            noiseSeed = other.noiseSeed;
            externalInnerWall = other.externalInnerWall;
            floorUvScale = other.floorUvScale;
            wallUvScale = other.wallUvScale;
            bevelFloorEdges = other.bevelFloorEdges;
            bevelSize = other.bevelSize;
            bevelSegments = other.bevelSegments;
            bevelCurvature = other.bevelCurvature;
        }

        /// <summary>Clamps the values to valid ranges. Called from OnValidate.</summary>
        public void Validate()
        {
            // Clamps with a maximum to protect performance (vertices ~ segments * subdivisions).
            segmentsPerSpline = Mathf.Clamp(segmentsPerSpline, 3, 400);
            colliderSimplify = Mathf.Clamp01(colliderSimplify);
            splineSmoothing = Mathf.Clamp01(splineSmoothing);
            splineSmoothingIterations = Mathf.Clamp(splineSmoothingIterations, 0, 50);
            topScale = Mathf.Max(0f, topScale);
            wallHeightSegments = Mathf.Clamp(wallHeightSegments, 1, 64);
            roadWidth = Mathf.Max(0.001f, roadWidth);
            outwardDistance = Mathf.Max(0.001f, outwardDistance);
            bevelSize = Mathf.Max(0f, bevelSize);
            bevelSegments = Mathf.Clamp(bevelSegments, 1, 32);
            bevelCurvature = Mathf.Clamp(bevelCurvature, -1f, 1f);
            floorCellSize = Mathf.Max(0.05f, floorCellSize);
        }
    }
}
