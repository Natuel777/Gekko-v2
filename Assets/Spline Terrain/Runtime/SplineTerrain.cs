using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using SplineTerrainTool.Generation;
using SplineTerrainTool.Util;

namespace SplineTerrainTool
{
    /// <summary>
    /// Main component of the tool. Generates, in real time within the editor,
    /// a terrain mesh from its <see cref="SplineContainer"/> and the <see cref="SplineTerrainSettings"/>.
    /// A child transform (<see cref="topTransform"/>) controls the top floor: its position
    /// is the offset (Y = height), its rotation tilts it and its XZ scale is the <c>topScale</c>.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [AddComponentMenu("Spline Terrain/Spline Terrain")]
    public class SplineTerrain : MonoBehaviour
    {
        [Tooltip("Source spline. If it is on the same GameObject, its local space matches that of the mesh.")]
        public SplineContainer splineContainer;

        [Tooltip("Child transform that controls the top floor (position = offset, rotation = tilt, XZ scale = topScale).")]
        public Transform topTransform;

        [SerializeField] private SplineTerrainSettings settings = new SplineTerrainSettings();

        [Header("Materials (floor / wall / edge)")]
        [SerializeField] private Material floorMaterial;
        [SerializeField] private Material wallMaterial;
        [SerializeField] private Material edgeMaterial;

        [Tooltip("ScriptableObject of the last bake, if it exists. Allows reloading parameters.")]
        public SplineTerrainData bakedData;

        [Header("Bake")]
        [Tooltip("Whether the bake also generates and assigns the MeshCollider.")]
        public bool bakeCollider = true;

        [Tooltip("Folder where the baked meshes (visual and collider) are saved.")]
        public string meshSaveFolder = "Assets/Spline Terrain/Baked";

        [Tooltip("Folder where the data ScriptableObject is saved. Can be the same as the meshes folder.")]
        public string dataSaveFolder = "Assets/Spline Terrain/Baked";

        [Tooltip("Assigned parameters profile. 'Apply profile' copies its settings to this terrain.")]
        public SplineTerrainProfile profile;

        // Internal state.
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _workingMesh;     // reusable working mesh (not an asset)
        private bool _dirty;
        private bool _subscribed;

        // Names of the managed child pieces (kept under this GameObject for tidiness).
        public const string FloorPieceName = "STT_Visual_Floor";
        public const string WallsPieceName = "STT_Visual_Walls";
        public const string ColliderPiecePrefix = "STT_Collider";

        public SplineTerrainSettings Settings => settings;
        public Material FloorMaterial { get => floorMaterial; set => floorMaterial = value; }
        public Material WallMaterial { get => wallMaterial; set => wallMaterial = value; }
        public Material EdgeMaterial { get => edgeMaterial; set => edgeMaterial = value; }

        private void OnEnable()
        {
            CacheComponents();
            if (splineContainer == null) splineContainer = GetComponent<SplineContainer>();
            // We do not create GameObjects in OnEnable (Unity advises against it): we only link if it already exists.
            if (topTransform == null)
            {
                Transform existing = transform.Find("TopTransform");
                if (existing != null) topTransform = existing;
            }
            Subscribe();
            MarkDirty();
        }

        private void OnDisable()
        {
            Unsubscribe();
            DestroyWorkingMesh();
        }

        private void CacheComponents()
        {
            if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
            if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            Spline.Changed += OnSplineChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            Spline.Changed -= OnSplineChanged;
            _subscribed = false;
        }

        private void OnSplineChanged(Spline spline, int knotIndex, SplineModification modification)
        {
            // We only care about our spline.
            if (splineContainer != null && spline == splineContainer.Spline)
                MarkDirty();
        }

        private void OnValidate()
        {
            if (settings == null) settings = new SplineTerrainSettings();
            settings.Validate();
            // Do not regenerate inside OnValidate (Unity advises against it): we only mark dirty.
            MarkDirty();
        }

        /// <summary>Marks the terrain to regenerate on the next tick.</summary>
        public void MarkDirty() => _dirty = true;

        private void Update()
        {
            if (_dirty)
            {
                _dirty = false;
                Regenerate();
            }
        }

        /// <summary>
        /// Regenerates the visual mesh according to the current mode and assigns it to the MeshFilter/Renderer.
        /// When <see cref="SplineTerrainSettings.separateVisualMeshes"/> is on, the floor and walls are split
        /// into child GameObjects (kept under this one) so the floor can carry its own dense grid.
        /// </summary>
        public void Regenerate()
        {
            CacheComponents();
            if (splineContainer == null) splineContainer = GetComponent<SplineContainer>();
            if (splineContainer == null) return;

            MeshBuildResult build = BuildVisualResult(-1);
            if (build == null || build.IsEmpty) return;

            if (settings.separateVisualMeshes)
                ApplySeparatedVisual(build);
            else
                ApplyCombinedVisual(build);
        }

        /// <summary>Single combined mesh on this GameObject (default). Removes any split pieces.</summary>
        private void ApplyCombinedVisual(MeshBuildResult build)
        {
            if (_workingMesh == null)
            {
                _workingMesh = new Mesh { name = "SplineTerrainMesh (working)" };
                _workingMesh.hideFlags = HideFlags.DontSave;
            }
            build.ToMesh(_workingMesh);
            _meshFilter.sharedMesh = _workingMesh;
            ApplyMaterials();
            DestroyPieceIfExists(FloorPieceName);
            DestroyPieceIfExists(WallsPieceName);
        }

        /// <summary>Floor and walls as separate child renderers, parented under this GameObject.</summary>
        private void ApplySeparatedVisual(MeshBuildResult build)
        {
            // The parent shows nothing; the pieces carry the geometry.
            _meshFilter.sharedMesh = null;

            // Floor piece (floor submesh only).
            MeshFilter floorMf = EnsureRendererPiece(FloorPieceName, out MeshRenderer floorMr);
            Mesh floorMesh = GetWorkingMesh(floorMf, "SplineTerrain Floor (working)");
            Mesh floorOut = build.ToMeshSubset(floorMesh, includeFloor: true, includeWall: false, includeEdge: false);
            floorMf.sharedMesh = floorOut;
            floorMr.sharedMaterials = new[] { floorMaterial };

            // Walls piece (wall + bevel/edge submeshes).
            MeshFilter wallMf = EnsureRendererPiece(WallsPieceName, out MeshRenderer wallMr);
            Mesh wallMesh = GetWorkingMesh(wallMf, "SplineTerrain Walls (working)");
            Mesh wallOut = build.ToMeshSubset(wallMesh, includeFloor: false, includeWall: true, includeEdge: true);
            wallMf.sharedMesh = wallOut;
            wallMr.sharedMaterials = build.HasEdge
                ? new[] { wallMaterial, edgeMaterial }
                : new[] { wallMaterial };
        }

        /// <summary>
        /// Builds the visual <see cref="MeshBuildResult"/> for the current mode/settings.
        /// Internal/External close the shape internally (forceClosed) so open splines also work.
        /// </summary>
        public MeshBuildResult BuildVisualResult(int segmentOverride)
        {
            if (splineContainer == null) splineContainer = GetComponent<SplineContainer>();
            if (splineContainer == null) return null;
            Spline spline = splineContainer.Spline;
            if (spline == null || spline.Count < 2) return null;

            MeshBuildResult build = GetGenerator(settings.mode).Generate(spline, settings, segmentOverride);
            return (build == null || build.IsEmpty) ? null : build;
        }

        /// <summary>
        /// Builds a new, independent mesh (not the working mesh). The caller is
        /// responsible for saving it as an asset or destroying it. Used by the bake.
        /// </summary>
        /// <param name="segmentOverride">-1 = visual resolution; &gt;0 = forced resolution.</param>
        public Mesh BuildMesh(int segmentOverride, string meshName)
        {
            MeshBuildResult build = BuildVisualResult(segmentOverride);
            if (build == null) return null;
            return build.ToMesh(new Mesh { name = meshName });
        }

        /// <summary>Independent visual mesh (resolution of settings.segmentsPerSpline).</summary>
        public Mesh BuildVisualMesh() => BuildMesh(-1, "SplineTerrainVisual");

        /// <summary>
        /// Mesh for the MeshCollider.
        /// - If colliderMatchesVisual: identical to the visual mesh.
        /// - Otherwise: an optimized collider with the SAME shape as the visual (same perimeter,
        ///   same bevel, same floor) that only trims what is redundant:
        ///     * Wall/bevel vertical rows are collapsed to 1 when there is no curvature (the rows
        ///       are collinear, so this is lossless). With curvature they are kept.
        ///     * Optionally (colliderSimplify > 0) the perimeter is decimated for extra reduction,
        ///       at the cost of shape fidelity.
        ///   The floor cap is already minimal (n-2 triangles for its perimeter), so it is not
        ///   reduced unless the perimeter is decimated.
        /// </summary>
        public Mesh BuildColliderMesh()
        {
            MeshBuildResult build = BuildColliderResult();
            return build == null ? null : build.ToMesh(new Mesh { name = "SplineTerrainCollider" });
        }

        /// <summary>
        /// Builds the collider <see cref="MeshBuildResult"/> (before any submesh split). Honors
        /// <see cref="SplineTerrainSettings.colliderMatchesVisual"/> and the optimized-collider logic.
        /// </summary>
        public MeshBuildResult BuildColliderResult()
        {
            if (settings.colliderMatchesVisual)
                return BuildVisualResult(-1);

            if (splineContainer == null) splineContainer = GetComponent<SplineContainer>();
            if (splineContainer == null) return null;
            Spline spline = splineContainer.Spline;
            if (spline == null || spline.Count < 2) return null;

            ISplineTerrainGenerator generator = GetGenerator(settings.mode);

            // Collider settings: keep the visual shape (bevel included), only collapse redundant
            // straight vertical rows. Collapsing is lossless when there is no curvature.
            SplineTerrainSettings cs = settings.Clone();
            const float curveEpsilon = 1e-4f;
            if (Mathf.Abs(cs.wallCurvature) < curveEpsilon) cs.wallHeightSegments = 1;
            if (Mathf.Abs(cs.bevelCurvature) < curveEpsilon) cs.bevelSegments = 1;
            // The dense paint grid is for the visual floor only; the optimized collider keeps the
            // minimal floor triangulation.
            cs.floorGrid = false;

            MeshBuildResult build;
            if (cs.colliderSimplify > 0f)
            {
                // Optional extra: decimate the perimeter (reduces floor tris + wall columns).
                SampledOutline outline = generator.BuildOutline(spline, cs, cs.segmentsPerSpline);
                SampledOutline simplified = SplineSampler.Simplify(outline, cs.colliderSimplify);
                build = generator.Generate(spline, cs, -1, simplified);
            }
            else
            {
                // Same perimeter as the visual (faithful footprint and bevel).
                build = generator.Generate(spline, cs, -1);
            }

            return (build == null || build.IsEmpty) ? null : build;
        }

        // ---- Collider split plan ----

        /// <summary>One collider piece: a child GameObject name suffix + which submesh groups it carries.</summary>
        public struct ColliderGroup
        {
            public string suffix;   // appended to ColliderPiecePrefix ("" = the single combined collider)
            public bool floor, wall, edge;
            public ColliderGroup(string suffix, bool floor, bool wall, bool edge)
            { this.suffix = suffix; this.floor = floor; this.wall = wall; this.edge = edge; }
        }

        /// <summary>Returns the collider pieces to generate for the given split mode.</summary>
        public static List<ColliderGroup> GetColliderGroups(ColliderSplitMode mode)
        {
            switch (mode)
            {
                case ColliderSplitMode.AllSeparate:
                    return new List<ColliderGroup>
                    {
                        new ColliderGroup("_Floor", true, false, false),
                        new ColliderGroup("_Wall",  false, true, false),
                        new ColliderGroup("_Bevel", false, false, true),
                    };
                case ColliderSplitMode.FloorBevelTogether_WallSeparate:
                    return new List<ColliderGroup>
                    {
                        new ColliderGroup("_FloorBevel", true, false, true),
                        new ColliderGroup("_Wall",       false, true, false),
                    };
                case ColliderSplitMode.WallBevelTogether_FloorSeparate:
                    return new List<ColliderGroup>
                    {
                        new ColliderGroup("_WallBevel", false, true, true),
                        new ColliderGroup("_Floor",     true, false, false),
                    };
                default: // AllTogether
                    return new List<ColliderGroup>
                    {
                        new ColliderGroup("", true, true, true),
                    };
            }
        }

        private ISplineTerrainGenerator GetGenerator(SplineTerrainMode mode)
        {
            switch (mode)
            {
                case SplineTerrainMode.Road: return new RoadRibbonGenerator();
                case SplineTerrainMode.External: return new ExternalIslandGenerator();
                default: return new InternalPlateauGenerator();
            }
        }

        private void ApplyMaterials()
        {
            CacheComponents();
            _meshRenderer.sharedMaterials = new[] { floorMaterial, wallMaterial, edgeMaterial };
        }

        // ---- Synchronization with the top transform ----

        /// <summary>Creates the top transform if it does not exist and positions it according to the settings.</summary>
        public void EnsureTopTransform()
        {
            if (topTransform == null)
            {
                Transform existing = transform.Find("TopTransform");
                if (existing != null)
                {
                    topTransform = existing;
                }
                else
                {
                    var go = new GameObject("TopTransform");
                    go.transform.SetParent(transform, false);
                    topTransform = go.transform;
                }
            }
            ApplyToTopTransform();
        }

        /// <summary>Reads offset, rotation (tilt) and scale (XZ) from the top transform into the settings.</summary>
        public void SyncFromTopTransform()
        {
            if (topTransform == null) return;
            settings.topOffset = topTransform.localPosition;
            settings.topEuler = topTransform.localEulerAngles;
            // We average X and Z in case they ended up different; the model uses uniform scale.
            float scale = (topTransform.localScale.x + topTransform.localScale.z) * 0.5f;
            settings.topScale = Mathf.Max(0f, scale);
            MarkDirty();
        }

        /// <summary>Writes offset/rotation/scale from the settings to the top transform.</summary>
        public void ApplyToTopTransform()
        {
            if (topTransform == null) return;
            topTransform.localPosition = settings.topOffset;
            topTransform.localEulerAngles = settings.topEuler;
            topTransform.localScale = new Vector3(settings.topScale, 1f, settings.topScale);
        }

        // ---- Managed child pieces (kept under this GameObject) ----

        /// <summary>Finds or creates a child GameObject with MeshFilter + MeshRenderer, parented here.</summary>
        public MeshFilter EnsureRendererPiece(string name, out MeshRenderer mr)
        {
            GameObject go = FindOrCreateChild(name);
            var mf = go.GetComponent<MeshFilter>();
            if (mf == null) mf = go.AddComponent<MeshFilter>();
            mr = go.GetComponent<MeshRenderer>();
            if (mr == null) mr = go.AddComponent<MeshRenderer>();
            return mf;
        }

        /// <summary>Finds or creates a child GameObject with a MeshCollider, parented here.</summary>
        public MeshCollider EnsureColliderPiece(string fullName)
        {
            GameObject go = FindOrCreateChild(fullName);
            var mc = go.GetComponent<MeshCollider>();
            if (mc == null) mc = go.AddComponent<MeshCollider>();
            return mc;
        }

        private GameObject FindOrCreateChild(string name)
        {
            Transform t = transform.Find(name);
            if (t != null) return t.gameObject;
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go;
        }

        /// <summary>Removes a managed child piece if it exists.</summary>
        public void DestroyPieceIfExists(string name)
        {
            Transform t = transform.Find(name);
            if (t == null) return;
            if (Application.isPlaying) Destroy(t.gameObject);
            else DestroyImmediate(t.gameObject);
        }

        /// <summary>Removes the separated visual pieces (floor / walls).</summary>
        public void DestroyVisualPieces()
        {
            DestroyPieceIfExists(FloorPieceName);
            DestroyPieceIfExists(WallsPieceName);
        }

        /// <summary>Removes every collider piece (all children whose name starts with the collider prefix).</summary>
        public void ClearColliderPieces()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform c = transform.GetChild(i);
                if (c != null && c.name.StartsWith(ColliderPiecePrefix))
                {
                    if (Application.isPlaying) Destroy(c.gameObject);
                    else DestroyImmediate(c.gameObject);
                }
            }
        }

        /// <summary>
        /// Rebuilds the collider child pieces in the editor (live preview) using working meshes,
        /// according to <see cref="SplineTerrainSettings.colliderSplit"/>. The bake produces the same
        /// hierarchy but with saved mesh assets instead.
        /// </summary>
        public void RebuildCollidersPreview()
        {
            ClearColliderPieces();

            MeshBuildResult build = BuildColliderResult();
            if (build == null) return;

            foreach (ColliderGroup g in GetColliderGroups(settings.colliderSplit))
            {
                Mesh m = build.ToMeshSubset(null, g.floor, g.wall, g.edge);
                if (m == null) continue; // this group had no geometry (e.g. no bevel)
                m.name = ColliderPiecePrefix + g.suffix + " (working)";
                m.hideFlags = HideFlags.DontSave;
                MeshCollider mc = EnsureColliderPiece(ColliderPiecePrefix + g.suffix);
                mc.sharedMesh = m;
            }
        }

        /// <summary>Reusable working mesh for a piece: reuses a non-asset mesh, otherwise makes one.</summary>
        private Mesh GetWorkingMesh(MeshFilter mf, string name)
        {
            Mesh m = mf.sharedMesh;
            if (m == null || (m.hideFlags & HideFlags.DontSave) == 0)
            {
                m = new Mesh { name = name };
                m.hideFlags = HideFlags.DontSave;
            }
            return m;
        }

        private void DestroyWorkingMesh()
        {
            if (_workingMesh == null) return;
            // We never destroy meshes that are assets (baked); the working mesh never is.
            if (Application.isPlaying) Destroy(_workingMesh);
            else DestroyImmediate(_workingMesh);
            _workingMesh = null;
        }

        /// <summary>Replaces the settings (e.g. when loading from an SO) and regenerates.</summary>
        public void ApplySettings(SplineTerrainSettings newSettings)
        {
            if (newSettings == null) return;
            settings.CopyFrom(newSettings);
            settings.Validate();
            ApplyToTopTransform();
            MarkDirty();
        }

        /// <summary>Applies the assigned profile (its settings and, if any, its materials) and regenerates.</summary>
        public void ApplyProfile(SplineTerrainProfile fromProfile = null)
        {
            var p = fromProfile != null ? fromProfile : profile;
            if (p == null) return;
            profile = p;
            ApplySettings(p.settings);
            if (p.floorMaterial != null) floorMaterial = p.floorMaterial;
            if (p.wallMaterial != null) wallMaterial = p.wallMaterial;
            if (p.edgeMaterial != null) edgeMaterial = p.edgeMaterial;
        }
    }
}
