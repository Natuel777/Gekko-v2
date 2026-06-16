using UnityEditor;
using UnityEngine;

namespace SplineTerrainTool.EditorTools
{
    /// <summary>
    /// Create / overwrite profiles (<see cref="SplineTerrainProfile"/>) from a terrain.
    /// </summary>
    public static class SplineTerrainProfileIO
    {
        private const string DefaultFolder = "Assets/Spline Terrain/Profiles";

        /// <summary>Creates a new profile (.asset) with the terrain's current settings.</summary>
        public static SplineTerrainProfile ExportNew(SplineTerrain terrain)
        {
            if (terrain == null) return null;

            if (!AssetDatabase.IsValidFolder("Assets/Spline Terrain"))
                AssetDatabase.CreateFolder("Assets", "Spline Terrain");
            if (!AssetDatabase.IsValidFolder(DefaultFolder))
                AssetDatabase.CreateFolder("Assets/Spline Terrain", "Profiles");

            string defaultName = $"{terrain.gameObject.name}_Profile.asset";
            string path = EditorUtility.SaveFilePanelInProject(
                "Export profile", defaultName, "asset", "Choose where to save the profile", DefaultFolder);
            if (string.IsNullOrEmpty(path)) return null;

            var profile = ScriptableObject.CreateInstance<SplineTerrainProfile>();
            profile.CaptureFrom(terrain);
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(terrain, "Assign profile");
            terrain.profile = profile;
            EditorUtility.SetDirty(terrain);

            EditorGUIUtility.PingObject(profile);
            return profile;
        }

        /// <summary>Overwrites an existing profile with the terrain's current settings.</summary>
        public static void SaveInto(SplineTerrainProfile profile, SplineTerrain terrain)
        {
            if (profile == null || terrain == null) return;
            Undo.RecordObject(profile, "Save into profile");
            profile.CaptureFrom(terrain);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }
    }
}
