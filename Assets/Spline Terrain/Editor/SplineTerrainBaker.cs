using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using SplineTerrainTool.Generation;

namespace SplineTerrainTool.EditorTools
{
    /// <summary>
    /// "Bake" logic: saves a <see cref="SplineTerrainData"/> with all the parameters and the
    /// spline shape, plus the visual mesh and the low-poly collider mesh as .asset assets.
    /// </summary>
    public static class SplineTerrainBaker
    {
        private const string DefaultFolder = "Assets/Spline Terrain/Baked";

        public static SplineTerrainData Bake(SplineTerrain terrain)
        {
            if (terrain == null) return null;
            var container = terrain.splineContainer != null
                ? terrain.splineContainer
                : terrain.GetComponent<SplineContainer>();
            if (container == null)
            {
                Debug.LogWarning("[SplineTerrain] There is no SplineContainer to bake.");
                return null;
            }

            // Configurable folders (meshes and SO can be different or the same).
            string meshFolder = EnsureFolder(terrain.meshSaveFolder);
            string dataFolder = EnsureFolder(terrain.dataSaveFolder);
            string safeName = MakeSafeName(terrain.gameObject.name);

            var settings = terrain.Settings;

            // 1) Build the visual result and turn it into one or more saved meshes.
            MeshBuildResult visualBuild = terrain.BuildVisualResult(-1);
            if (visualBuild == null || visualBuild.IsEmpty)
            {
                Debug.LogWarning("[SplineTerrain] The visual mesh came out empty; not baking.");
                return null;
            }

            string dataPath = AssetDatabase.GenerateUniqueAssetPath($"{dataFolder}/{safeName}_Data.asset");

            Undo.RegisterFullObjectHierarchyUndo(terrain.gameObject, "Bake Spline Terrain");

            Mesh primaryVisual;            // referenced by the SO for back-compat
            if (settings.separateVisualMeshes)
            {
                primaryVisual = BakeSeparatedVisual(terrain, visualBuild, meshFolder, safeName);
            }
            else
            {
                primaryVisual = visualBuild.ToMesh(new Mesh { name = $"{safeName}_Visual" });
                string visualPath = AssetDatabase.GenerateUniqueAssetPath($"{meshFolder}/{safeName}_Visual.asset");
                AssetDatabase.CreateAsset(primaryVisual, visualPath);

                // Single combined mesh stays on the terrain GameObject; drop any split pieces.
                terrain.DestroyVisualPieces();
                var mf = terrain.GetComponent<MeshFilter>();
                if (mf != null) mf.sharedMesh = primaryVisual;
            }

            // 2) Colliders: split into child pieces under the terrain according to the split mode.
            Mesh primaryCollider = terrain.bakeCollider
                ? BakeColliders(terrain, meshFolder, safeName)
                : null;
            if (!terrain.bakeCollider)
                terrain.ClearColliderPieces();

            // Remove the legacy single MeshCollider that used to live on the terrain GameObject:
            // colliders are now organized as child pieces.
            var legacyMc = terrain.GetComponent<MeshCollider>();
            if (legacyMc != null) Undo.DestroyObjectImmediate(legacyMc);

            // 3) Create the SO with settings + spline snapshot + mesh refs.
            var data = ScriptableObject.CreateInstance<SplineTerrainData>();
            data.CaptureFrom(new SplineTerrainComponentSnapshot
            {
                Settings = terrain.Settings,
                Spline = container.Spline,
                Floor = terrain.FloorMaterial,
                Wall = terrain.WallMaterial,
                Edge = terrain.EdgeMaterial
            });
            data.visualMesh = primaryVisual;
            data.colliderMesh = primaryCollider;

            AssetDatabase.CreateAsset(data, dataPath);
            AssetDatabase.SaveAssets();

            terrain.bakedData = data;
            EditorUtility.SetDirty(terrain);

            Selection.activeObject = data;
            EditorGUIUtility.PingObject(data);
            Debug.Log($"[SplineTerrain] Bake complete: {dataPath}");
            return data;
        }

        /// <summary>
        /// Saves the floor and walls as separate mesh assets and assigns them to the child renderer
        /// pieces (under the terrain). Clears the parent MeshFilter. Returns the floor mesh (or walls
        /// if there is no floor) as the SO's primary reference.
        /// </summary>
        private static Mesh BakeSeparatedVisual(SplineTerrain terrain, MeshBuildResult build, string meshFolder, string safeName)
        {
            var parentMf = terrain.GetComponent<MeshFilter>();
            if (parentMf != null) parentMf.sharedMesh = null;

            Mesh primary = null;

            Mesh floor = build.ToMeshSubset(null, includeFloor: true, includeWall: false, includeEdge: false);
            if (floor != null)
            {
                floor.name = $"{safeName}_Floor";
                AssetDatabase.CreateAsset(floor, AssetDatabase.GenerateUniqueAssetPath($"{meshFolder}/{safeName}_Floor.asset"));
                MeshFilter mf = terrain.EnsureRendererPiece(SplineTerrain.FloorPieceName, out MeshRenderer mr);
                mf.sharedMesh = floor;
                mr.sharedMaterials = new[] { terrain.FloorMaterial };
                primary = floor;
            }
            else terrain.DestroyPieceIfExists(SplineTerrain.FloorPieceName);

            Mesh walls = build.ToMeshSubset(null, includeFloor: false, includeWall: true, includeEdge: true);
            if (walls != null)
            {
                walls.name = $"{safeName}_Walls";
                AssetDatabase.CreateAsset(walls, AssetDatabase.GenerateUniqueAssetPath($"{meshFolder}/{safeName}_Walls.asset"));
                MeshFilter mf = terrain.EnsureRendererPiece(SplineTerrain.WallsPieceName, out MeshRenderer mr);
                mf.sharedMesh = walls;
                mr.sharedMaterials = build.HasEdge
                    ? new[] { terrain.WallMaterial, terrain.EdgeMaterial }
                    : new[] { terrain.WallMaterial };
                if (primary == null) primary = walls;
            }
            else terrain.DestroyPieceIfExists(SplineTerrain.WallsPieceName);

            return primary;
        }

        /// <summary>
        /// Builds the collider geometry, splits it per the chosen mode, saves each piece as an asset
        /// and assigns it to a child MeshCollider GameObject. Returns the first piece (SO reference).
        /// </summary>
        private static Mesh BakeColliders(SplineTerrain terrain, string meshFolder, string safeName)
        {
            MeshBuildResult build = terrain.BuildColliderResult();
            terrain.ClearColliderPieces();
            if (build == null) return null;

            Mesh primary = null;
            foreach (SplineTerrain.ColliderGroup g in SplineTerrain.GetColliderGroups(terrain.Settings.colliderSplit))
            {
                Mesh m = build.ToMeshSubset(null, g.floor, g.wall, g.edge);
                if (m == null) continue; // group with no geometry (e.g. bevel piece without bevel)
                m.name = $"{safeName}_Collider{g.suffix}";
                AssetDatabase.CreateAsset(m, AssetDatabase.GenerateUniqueAssetPath($"{meshFolder}/{safeName}_Collider{g.suffix}.asset"));
                MeshCollider mc = terrain.EnsureColliderPiece(SplineTerrain.ColliderPiecePrefix + g.suffix);
                mc.sharedMesh = m;
                if (primary == null) primary = m;
            }
            return primary;
        }

        /// <summary>Reloads settings + spline shape from an SO onto the terrain.</summary>
        public static void LoadInto(SplineTerrain terrain, SplineTerrainData data)
        {
            if (terrain == null || data == null) return;
            var container = terrain.splineContainer != null
                ? terrain.splineContainer
                : terrain.GetComponent<SplineContainer>();
            if (container == null) return;

            Undo.RecordObject(terrain, "Load Spline Terrain Data");
            data.ApplyToSpline(container.Spline);
            terrain.ApplySettings(data.settings);
            if (data.floorMaterial != null) terrain.FloorMaterial = data.floorMaterial;
            if (data.wallMaterial != null) terrain.WallMaterial = data.wallMaterial;
            if (data.edgeMaterial != null) terrain.EdgeMaterial = data.edgeMaterial;
            terrain.bakedData = data;
            terrain.Regenerate();
            EditorUtility.SetDirty(terrain);
        }

        /// <summary>Ensures the folder exists (creating it recursively) and returns a valid path.</summary>
        private static string EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path)) path = DefaultFolder;
            path = path.Replace('\\', '/').TrimEnd('/');
            if (!path.StartsWith("Assets")) path = DefaultFolder;
            if (AssetDatabase.IsValidFolder(path)) return path;

            string[] parts = path.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
            return path;
        }

        private static string MakeSafeName(string raw)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                raw = raw.Replace(c, '_');
            return raw.Replace(' ', '_');
        }
    }
}
