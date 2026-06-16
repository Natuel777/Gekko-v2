using System;
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
        /// </summary>
        public void Regenerate()
        {
            CacheComponents();
            if (splineContainer == null) splineContainer = GetComponent<SplineContainer>();
            if (splineContainer == null) return;

            Spline spline = splineContainer.Spline;
            if (spline == null || spline.Count < 2) return;

            // Internal/External close the shape internally (forceClosed) without modifying the spline,
            // so they also work with open splines.
            MeshBuildResult build = GetGenerator(settings.mode).Generate(spline, settings);
            if (build == null || build.IsEmpty) return;

            if (_workingMesh == null)
            {
                _workingMesh = new Mesh { name = "SplineTerrainMesh (working)" };
                _workingMesh.hideFlags = HideFlags.DontSave;
            }
            build.ToMesh(_workingMesh);

            _meshFilter.sharedMesh = _workingMesh;
            ApplyMaterials();
        }

        /// <summary>
        /// Builds a new, independent mesh (not the working mesh). The caller is
        /// responsible for saving it as an asset or destroying it. Used by the bake.
        /// </summary>
        /// <param name="segmentOverride">-1 = visual resolution; &gt;0 = forced resolution.</param>
        public Mesh BuildMesh(int segmentOverride, string meshName)
        {
            if (splineContainer == null) splineContainer = GetComponent<SplineContainer>();
            if (splineContainer == null) return null;
            Spline spline = splineContainer.Spline;
            if (spline == null || spline.Count < 2) return null;

            MeshBuildResult build = GetGenerator(settings.mode).Generate(spline, settings, segmentOverride);
            if (build == null || build.IsEmpty) return null;

            var mesh = new Mesh { name = meshName };
            return build.ToMesh(mesh);
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
            if (settings.colliderMatchesVisual)
                return BuildMesh(-1, "SplineTerrainCollider");

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

            if (build == null || build.IsEmpty) return null;
            return build.ToMesh(new Mesh { name = "SplineTerrainCollider" });
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
