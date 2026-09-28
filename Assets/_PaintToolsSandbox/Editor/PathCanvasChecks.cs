using System.Collections.Generic;
using SplineTerrainTool;
using SplineTerrainTool.Generation;
using UnityEditor;
using UnityEngine;

namespace Gekko.PaintTools.EditorTools
{
    /// <summary>
    /// Chequeos del piso de un <see cref="PathCanvas"/>, cada uno con su arreglo en un
    /// click. Existen porque los tres problemas de abajo rompen el pintado SIN tirar
    /// ningun error, y el sintoma (pintura corrida, rayas estiradas) no dice la causa:
    ///
    /// - Colliders viejos: el pincel raycastea contra COLLIDERS. Si el Spline Terrain se
    ///   regenero (por ejemplo, se le subio la altura) pero sus colliders horneados no, el
    ///   rayo atraviesa lo que se ve y pinta donde esta el collider viejo.
    /// - Triplanar apagado: la textura se proyecta solo desde arriba y en las paredes se
    ///   estira en rayas verticales.
    /// - Alto de la zona: las mascaras de costados solo cubren [Y - Height/2, Y + Height/2];
    ///   una pared fuera de ese rango no se puede pintar.
    /// </summary>
    public static class PathCanvasChecks
    {
        private const string PathShaderName = "Gekko/Path Blend";
        private const string TriplanarKeyword = "_TRIPLANAR_ON";
        private const string DefaultMeshFolder = "Assets/Spline Terrain/Baked";

        // Margen al ajustar el alto: que el borde de la pared no quede justo en el limite
        // de la mascara, donde el bilinear ya mezcla con afuera.
        private const float HeightMargin = 1f;

        /// <summary>Dibuja los avisos que correspondan, con su boton de arreglo.</summary>
        public static void DrawChecks(PathCanvas canvas)
        {
            DrawColliderCheck(canvas);
            DrawTriplanarCheck(canvas);
            DrawCoverageCheck(canvas);
        }

        /// <summary>
        /// Deja el canvas listo para pintar paredes: alto ajustado a los renderers y
        /// triplanar prendido. Se llama al crear las mascaras de costados.
        /// </summary>
        public static void PrepareForSides(PathCanvas canvas)
        {
            FitHeightToTargets(canvas);
            foreach (Material material in PathMaterialsWithoutTriplanar(canvas))
            {
                EnableTriplanar(material);
            }
        }

        // ------------------------------------------------------------------ colliders

        private static void DrawColliderCheck(PathCanvas canvas)
        {
            foreach (Renderer target in ValidTargets(canvas))
            {
                Transform root = ColliderRoot(target);
                List<Collider> colliders = SolidColliders(root);

                if (colliders.Count == 0)
                {
                    WarningWithButton(
                        $"'{root.name}' no tiene collider: el pincel pinta donde pega el rayo, así que no se va a poder pintar.",
                        root.GetComponent<SplineTerrain>() != null ? "Generar colliders" : null,
                        () => RebuildColliders(root));
                    continue;
                }

                var terrain = root.GetComponent<SplineTerrain>();
                bool stale = terrain != null
                    ? !SplineTerrainCollidersUpToDate(terrain)
                    : !CollidersMatchRenderers(root, colliders);

                if (stale)
                {
                    WarningWithButton(
                        $"Los colliders de '{root.name}' no coinciden con la malla que se ve (quedaron de una versión " +
                        "anterior). El pincel pega en el collider, así que la pintura sale corrida, sobre todo en las paredes.",
                        CanRebuild(root) ? "Regenerar colliders" : null,
                        () => RebuildColliders(root));
                }
            }
        }

        /// <summary>
        /// Donde viven los colliders de un renderer destino: la raiz del Spline Terrain si
        /// es uno (los colliders son hijos STT_Collider_*, y con mallas separadas el
        /// renderer tambien es un hijo), o el propio renderer si no.
        /// </summary>
        private static Transform ColliderRoot(Renderer target)
        {
            var terrain = target.GetComponentInParent<SplineTerrain>();
            return terrain != null ? terrain.transform : target.transform;
        }

        private static List<Collider> SolidColliders(Transform root)
        {
            var result = new List<Collider>();
            foreach (Collider collider in root.GetComponentsInChildren<Collider>())
            {
                if (collider.enabled && !collider.isTrigger)
                {
                    result.Add(collider);
                }
            }
            return result;
        }

        /// <summary>
        /// Compara la caja de todos los colliders con la de todos los renderers. Tolerancia
        /// generosa (5% del tamano): un collider simplificado difiere un poco y esta bien;
        /// uno viejo suele estar corrido varias unidades.
        /// </summary>
        private static bool CollidersMatchRenderers(Transform root, List<Collider> colliders)
        {
            Bounds colliderBounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Count; i++)
            {
                colliderBounds.Encapsulate(colliders[i].bounds);
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return true;
            }

            Bounds rendererBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                rendererBounds.Encapsulate(renderers[i].bounds);
            }

            float tolerance = Mathf.Max(0.5f, rendererBounds.size.magnitude * 0.05f);
            return Vector3.Distance(colliderBounds.min, rendererBounds.min) <= tolerance
                   && Vector3.Distance(colliderBounds.max, rendererBounds.max) <= tolerance;
        }

        // Resultado del chequeo por terreno. Generar la geometria del collider en cada
        // repintado del inspector es caro, y el terreno no cambia tan seguido.
        private static readonly Dictionary<int, (double time, bool upToDate)> TerrainCheckCache =
            new Dictionary<int, (double, bool)>();

        private const double TerrainCheckInterval = 1.0;

        /// <summary>
        /// Compara cada pieza STT_Collider_* contra lo que generaria el terreno AHORA, en
        /// espacio local. Pieza por pieza y no la caja total: si solo el piso quedo viejo,
        /// la pared (que ocupa toda la altura) tapa la diferencia en la caja total.
        /// </summary>
        private static bool SplineTerrainCollidersUpToDate(SplineTerrain terrain)
        {
            int id = terrain.GetInstanceID();
            double now = EditorApplication.timeSinceStartup;
            if (TerrainCheckCache.TryGetValue(id, out var cached) && now - cached.time < TerrainCheckInterval)
            {
                return cached.upToDate;
            }

            bool upToDate = ComputeSplineTerrainCollidersUpToDate(terrain);
            TerrainCheckCache[id] = (now, upToDate);
            return upToDate;
        }

        private static bool ComputeSplineTerrainCollidersUpToDate(SplineTerrain terrain)
        {
            MeshBuildResult build = terrain.BuildColliderResult();
            if (build == null)
            {
                return true; // Nada que comparar.
            }

            foreach (SplineTerrain.ColliderGroup group in SplineTerrain.GetColliderGroups(terrain.Settings.colliderSplit))
            {
                Mesh expected = build.ToMeshSubset(null, group.floor, group.wall, group.edge);
                Transform piece = terrain.transform.Find(SplineTerrain.ColliderPiecePrefix + group.suffix);
                Mesh actual = piece != null && piece.TryGetComponent(out MeshCollider collider) ? collider.sharedMesh : null;

                bool matches;
                if (expected == null)
                {
                    matches = actual == null;
                }
                else if (actual == null)
                {
                    matches = false;
                }
                else
                {
                    float tolerance = Mathf.Max(0.05f, expected.bounds.size.magnitude * 0.02f);
                    matches = Vector3.Distance(expected.bounds.min, actual.bounds.min) <= tolerance
                              && Vector3.Distance(expected.bounds.max, actual.bounds.max) <= tolerance;
                }

                if (expected != null)
                {
                    Object.DestroyImmediate(expected);
                }

                if (!matches)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool CanRebuild(Transform root)
        {
            if (root.GetComponent<SplineTerrain>() != null)
            {
                return true;
            }

            var meshCollider = root.GetComponent<MeshCollider>();
            var meshFilter = root.GetComponent<MeshFilter>();
            return meshCollider != null && meshFilter != null && meshFilter.sharedMesh != null;
        }

        private static void RebuildColliders(Transform root)
        {
            var terrain = root.GetComponent<SplineTerrain>();
            if (terrain != null)
            {
                RebuildSplineTerrainColliders(terrain);
                return;
            }

            // Malla comun: el MeshCollider pasa a usar la misma malla que se ve.
            var meshCollider = root.GetComponent<MeshCollider>();
            var meshFilter = root.GetComponent<MeshFilter>();
            if (meshCollider != null && meshFilter != null)
            {
                Undo.RecordObject(meshCollider, "Regenerar collider");
                meshCollider.sharedMesh = meshFilter.sharedMesh;
                EditorUtility.SetDirty(meshCollider);
            }
        }

        /// <summary>
        /// Rehace los colliders del Spline Terrain con su forma ACTUAL. Si ya habia un mesh
        /// horneado (.asset) se rellena ese mismo asset, asi la escena no cambia de
        /// referencias y no se acumulan copias "_1", "_2"... en la carpeta de bakes. Si no
        /// habia, se crea uno nuevo en la carpeta de bakes del terreno.
        /// </summary>
        private static void RebuildSplineTerrainColliders(SplineTerrain terrain)
        {
            MeshBuildResult build = terrain.BuildColliderResult();
            if (build == null)
            {
                Debug.LogWarning($"[PathCanvas] '{terrain.name}' no genera geometria de collider.", terrain);
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(terrain.gameObject, "Regenerar colliders");

            foreach (SplineTerrain.ColliderGroup group in SplineTerrain.GetColliderGroups(terrain.Settings.colliderSplit))
            {
                string pieceName = SplineTerrain.ColliderPiecePrefix + group.suffix;
                Transform existing = terrain.transform.Find(pieceName);
                Mesh current = existing != null && existing.TryGetComponent(out MeshCollider old) ? old.sharedMesh : null;
                bool currentIsAsset = current != null && AssetDatabase.Contains(current);

                Mesh mesh = build.ToMeshSubset(currentIsAsset ? current : null, group.floor, group.wall, group.edge);
                if (mesh == null)
                {
                    // El grupo ya no tiene geometria (por ejemplo, se saco el bisel).
                    if (existing != null)
                    {
                        Undo.DestroyObjectImmediate(existing.gameObject);
                    }
                    continue;
                }

                if (currentIsAsset)
                {
                    EditorUtility.SetDirty(mesh);
                }
                else
                {
                    string folder = EnsureFolder(terrain.meshSaveFolder);
                    string safeName = terrain.gameObject.name.Replace(' ', '_');
                    mesh.name = $"{safeName}_Collider{group.suffix}";
                    AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{mesh.name}.asset"));
                }

                MeshCollider collider = terrain.EnsureColliderPiece(pieceName);
                // Reasignar aunque sea el mismo asset: el MeshCollider no se entera de que
                // la malla cambio y seguiria con la fisica vieja cocinada.
                collider.sharedMesh = null;
                collider.sharedMesh = mesh;
                EditorUtility.SetDirty(collider);
            }

            AssetDatabase.SaveAssets();
            TerrainCheckCache.Remove(terrain.GetInstanceID());
            Debug.Log($"[PathCanvas] Colliders de '{terrain.name}' regenerados con la forma actual.", terrain);
        }

        private static string EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.Replace('\\', '/').StartsWith("Assets"))
            {
                path = DefaultMeshFolder;
            }
            path = path.Replace('\\', '/').TrimEnd('/');

            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
            return path;
        }

        // ------------------------------------------------------------------ triplanar

        private static void DrawTriplanarCheck(PathCanvas canvas)
        {
            // Solo importa si se van a pintar paredes: en un piso plano, triplanar y
            // cenital se ven igual y la cenital es mas barata.
            if (!canvas.HasSideMasks)
            {
                return;
            }

            foreach (Material material in PathMaterialsWithoutTriplanar(canvas))
            {
                WarningWithButton(
                    $"El material '{material.name}' tiene Triplanar apagado: en las paredes la textura se ve estirada en rayas.",
                    "Activar triplanar",
                    () => EnableTriplanar(material));
            }
        }

        private static IEnumerable<Material> PathMaterialsWithoutTriplanar(PathCanvas canvas)
        {
            var seen = new HashSet<Material>();
            foreach (Renderer target in ValidTargets(canvas))
            {
                foreach (Material material in target.sharedMaterials)
                {
                    if (material != null
                        && material.shader != null
                        && material.shader.name == PathShaderName
                        && !material.IsKeywordEnabled(TriplanarKeyword)
                        && seen.Add(material))
                    {
                        yield return material;
                    }
                }
            }
        }

        private static void EnableTriplanar(Material material)
        {
            Undo.RecordObject(material, "Activar triplanar");
            material.SetFloat("_Triplanar", 1f);
            material.EnableKeyword(TriplanarKeyword);
            EditorUtility.SetDirty(material);
        }

        // ------------------------------------------------------------------ cobertura

        private static void DrawCoverageCheck(PathCanvas canvas)
        {
            if (!TryGetTargetsBounds(canvas, out Bounds bounds))
            {
                return;
            }

            if (canvas.HasSideMasks)
            {
                float minY = canvas.WorldMinY;
                float maxY = minY + canvas.Height;
                if (bounds.min.y < minY - 0.01f || bounds.max.y > maxY + 0.01f)
                {
                    WarningWithButton(
                        $"Las paredes van de Y {bounds.min.y:0.0} a {bounds.max.y:0.0}, pero los costados cubren de " +
                        $"{minY:0.0} a {maxY:0.0}: lo que queda afuera no se puede pintar.",
                        "Ajustar alto",
                        () => FitHeightToTargets(canvas));
                }
            }

            // Sin boton a proposito: cambiar el tamano o la posicion en XZ corre todo lo
            // que ya se pinto arriba. Conviene arreglarlo ANTES de pintar.
            Vector2 min = canvas.WorldMin;
            Vector2 max = min + canvas.Size;
            if (bounds.min.x < min.x - 0.01f || bounds.max.x > max.x + 0.01f
                || bounds.min.z < min.y - 0.01f || bounds.max.z > max.y + 0.01f)
            {
                EditorGUILayout.HelpBox(
                    $"El piso (X {bounds.min.x:0.0}..{bounds.max.x:0.0}, Z {bounds.min.z:0.0}..{bounds.max.z:0.0}) se sale " +
                    $"de la zona (X {min.x:0.0}..{max.x:0.0}, Z {min.y:0.0}..{max.y:0.0}). Agrandá 'Size' o mové el canvas; " +
                    "ojo que eso corre lo que ya esté pintado.",
                    MessageType.Info);
            }
        }

        /// <summary>
        /// Centra el canvas en Y sobre los renderers destino y ajusta el alto para
        /// cubrirlos. Mover en Y no toca la mascara de arriba: esa solo usa X y Z.
        /// </summary>
        public static void FitHeightToTargets(PathCanvas canvas)
        {
            if (!TryGetTargetsBounds(canvas, out Bounds bounds))
            {
                return;
            }

            Undo.RecordObject(canvas.transform, "Ajustar alto del camino");
            Vector3 position = canvas.transform.position;
            position.y = bounds.center.y;
            canvas.transform.position = position;

            var serialized = new SerializedObject(canvas);
            serialized.FindProperty("_height").floatValue = bounds.size.y + HeightMargin * 2f;
            serialized.ApplyModifiedProperties();

            canvas.Apply();
        }

        private static bool TryGetTargetsBounds(PathCanvas canvas, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Renderer target in ValidTargets(canvas))
            {
                if (!any)
                {
                    bounds = target.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(target.bounds);
                }
            }
            return any;
        }

        // ------------------------------------------------------------------ utilidades

        private static IEnumerable<Renderer> ValidTargets(PathCanvas canvas)
        {
            if (canvas.TargetRenderers == null)
            {
                yield break;
            }

            foreach (Renderer target in canvas.TargetRenderers)
            {
                if (target != null)
                {
                    yield return target;
                }
            }
        }

        /// <summary>HelpBox de warning con un boton de arreglo al costado (o sin boton si label es null).</summary>
        private static void WarningWithButton(string message, string buttonLabel, System.Action fix)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    new GUIContent(message, EditorGUIUtility.IconContent("console.warnicon").image),
                    EditorStyles.wordWrappedLabel);

                if (buttonLabel != null && GUILayout.Button(buttonLabel, GUILayout.Width(130f), GUILayout.ExpandHeight(true)))
                {
                    fix();
                    SceneView.RepaintAll();

                    // El arreglo puede cambiar que avisos se dibujan: cortar este pase de
                    // GUI evita el error de "GUI Layout mismatch".
                    GUIUtility.ExitGUI();
                }
            }
        }
    }
}
