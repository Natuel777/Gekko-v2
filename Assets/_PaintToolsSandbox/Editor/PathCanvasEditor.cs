using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gekko.PaintTools.EditorTools
{
    /// <summary>
    /// Pincel de caminos. Pinta sobre la textura de mascara del <see cref="PathCanvas"/>,
    /// no sobre los vertices de la malla — por eso funciona igual en una malla de 100
    /// triangulos que en una de 100.000, y no le importa como esten las UVs.
    ///
    /// La mascara guarda RGB = tinte (blanco = sin cambio) y A = cobertura del camino.
    /// </summary>
    [CustomEditor(typeof(PathCanvas))]
    public class PathCanvasEditor : UnityEditor.Editor
    {
        private const string DataFolder = "Assets/_PaintToolsSandbox/Data";

        private enum BrushMode
        {
            Pintar,
            Borrar,
            Desenfocar,
        }

        private static readonly SceneBrush Brush = new SceneBrush { Radius = 3f };

        private static BrushMode _mode = BrushMode.Pintar;
        private static float _strength = 0.5f;
        private static Color _tint = Color.white;
        private static int _blurRadius = 2;
        private static bool _randomRotation = true;
        private static int _selectedBrush;
        private static int _newMaskResolution = 1024;

        // Cache de pixeles: pintar leyendo y escribiendo la textura entera en cada
        // pincelada es inviable, asi que se mantiene una copia en RAM y solo se sube a
        // la GPU el rectangulo tocado.
        private Color32[] _pixels;
        private Texture2D _cachedMask;

        // Snapshot para deshacer el ultimo trazo. Undo.RecordObject no sirve bien con
        // datos de textura, asi que se guarda a mano al empezar cada trazo.
        private Color32[] _strokeBackup;

        private float[] _brushAlpha;
        private int _brushWidth;
        private int _brushHeight;
        private Texture2D _cachedBrushSource;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var canvas = (PathCanvas)target;

            EditorGUILayout.Space();

            if (canvas.Mask == null)
            {
                DrawMaskCreation(canvas);
                return;
            }

            DrawSummary(canvas);
            EditorGUILayout.Space();

            Brush.DrawToggleButton("Modo pintura: ACTIVO (Esc para salir)", "Activar modo pintura");

            if (Brush.Enabled)
            {
                EditorGUILayout.HelpBox(
                    "Click y arrastrar: aplicar.\nShift + click: borrar.\nCtrl + rueda: radio.",
                    MessageType.None);
            }

            EditorGUILayout.Space();
            DrawBrushSettings(canvas);

            EditorGUILayout.Space();
            DrawBrushPalette(canvas);

            EditorGUILayout.Space();
            DrawMaskButtons(canvas);
        }

        private void DrawSummary(PathCanvas canvas)
        {
            Vector2 texelsPerUnit = canvas.TexelsPerUnit;
            float worst = Mathf.Min(texelsPerUnit.x, texelsPerUnit.y);

            EditorGUILayout.HelpBox(
                $"Mascara {canvas.Mask.width} x {canvas.Mask.height} sobre {canvas.Size.x:0} x {canvas.Size.y:0} unidades\n" +
                $"{texelsPerUnit.x:0.00} texels por unidad en X, {texelsPerUnit.y:0.00} en Z",
                MessageType.Info);

            // La textura es cuadrada: si la zona no lo es, un eje pierde resolucion.
            float aspect = Mathf.Max(canvas.Size.x, canvas.Size.y)
                           / Mathf.Max(0.01f, Mathf.Min(canvas.Size.x, canvas.Size.y));
            if (aspect > 1.5f)
            {
                EditorGUILayout.HelpBox(
                    "La zona no es cuadrada, así que un eje tiene bastante menos resolución que el otro. " +
                    "El pincel sigue siendo circular en el mundo, pero si la diferencia molesta, usá varias zonas cuadradas.",
                    MessageType.None);
            }

            if (worst < 4f)
            {
                EditorGUILayout.HelpBox(
                    "Menos de 4 texels por unidad: el borde del camino va a depender casi por completo " +
                    "del ruido del shader. Subí la resolución de la máscara o achicá la zona.",
                    MessageType.Warning);
            }
        }

        private void DrawMaskCreation(PathCanvas canvas)
        {
            EditorGUILayout.HelpBox("Esta zona todavía no tiene máscara.", MessageType.Warning);

            _newMaskResolution = EditorGUILayout.IntPopup(
                "Resolución",
                _newMaskResolution,
                new[] { "256", "512", "1024", "2048", "4096" },
                new[] { 256, 512, 1024, 2048, 4096 });

            // Se muestra el peor eje: es el que manda para el detalle del borde.
            float texels = _newMaskResolution / Mathf.Max(canvas.Size.x, canvas.Size.y, 0.01f);
            EditorGUILayout.LabelField(" ", $"{texels:0.00} texels por unidad (eje más largo)");

            if (GUILayout.Button("Crear máscara", GUILayout.Height(28f)))
            {
                CreateMask(canvas, _newMaskResolution);
            }
        }

        private void DrawBrushSettings(PathCanvas canvas)
        {
            EditorGUILayout.LabelField("Pincel", EditorStyles.boldLabel);

            _mode = (BrushMode)EditorGUILayout.EnumPopup("Modo", _mode);
            Brush.DrawCommonSettings();
            _strength = EditorGUILayout.Slider("Fuerza", _strength, 0.01f, 1f);

            if (_mode == BrushMode.Desenfocar)
            {
                _blurRadius = EditorGUILayout.IntSlider("Radio del desenfoque (px)", _blurRadius, 1, 12);
            }
            else
            {
                _tint = EditorGUILayout.ColorField(
                    new GUIContent("Tinte", "Multiplica el color del material del camino. Blanco = sin cambio."),
                    _tint, true, false, false);
                _randomRotation = EditorGUILayout.Toggle(
                    new GUIContent("Rotación al azar", "Gira el stamp en cada aplicación para que no se note repetido."),
                    _randomRotation);
            }
        }

        private void DrawBrushPalette(PathCanvas canvas)
        {
            EditorGUILayout.LabelField("Pinceles cargados", EditorStyles.boldLabel);

            PathBrushSet set = canvas.BrushSet;

            if (set == null || set.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Sin set de pinceles: se usa un círculo suave por defecto.\n" +
                    "Para cargar los tuyos, creá un Path Brush Set (click derecho > Create > Gekko > Paint Tools) " +
                    "y asignalo arriba.",
                    MessageType.None);
                return;
            }

            const int columns = 6;
            int rows = Mathf.CeilToInt(set.Count / (float)columns);
            int index = 0;

            for (int row = 0; row < rows; row++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int column = 0; column < columns && index < set.Count; column++, index++)
                    {
                        Texture2D brush = set.Get(index);
                        Rect rect = GUILayoutUtility.GetRect(48f, 48f, GUILayout.ExpandWidth(false));

                        if (index == _selectedBrush)
                        {
                            EditorGUI.DrawRect(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4),
                                new Color(0.4f, 1f, 0.5f, 1f));
                        }

                        if (brush != null)
                        {
                            EditorGUI.DrawPreviewTexture(rect, brush);
                        }
                        else
                        {
                            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));
                        }

                        if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                        {
                            _selectedBrush = index;
                            _cachedBrushSource = null;
                        }
                    }
                }
            }

            Texture2D selected = set.Get(_selectedBrush);
            if (selected != null && !selected.isReadable)
            {
                EditorGUILayout.HelpBox(
                    $"'{selected.name}' no tiene Read/Write Enabled. El pincel lo lee por CPU y sin eso no funciona.",
                    MessageType.Error);

                if (GUILayout.Button("Arreglar el importador"))
                {
                    MakeReadable(selected);
                }
            }
        }

        private void DrawMaskButtons(PathCanvas canvas)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_strokeBackup == null))
                {
                    if (GUILayout.Button("Deshacer trazo"))
                    {
                        RestoreStrokeBackup(canvas);
                    }
                }

                if (GUILayout.Button("Guardar máscara"))
                {
                    SaveMask(canvas);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Desenfocar todo"))
                {
                    EnsurePixels(canvas);
                    SnapshotStroke();
                    BlurRegion(canvas.Mask.width, canvas.Mask.height, 0, 0, canvas.Mask.width, canvas.Mask.height, _blurRadius, 1f);
                    UploadAll(canvas.Mask);
                }

                if (GUILayout.Button("Limpiar máscara"))
                {
                    if (EditorUtility.DisplayDialog("Limpiar máscara",
                            "Se borra todo el camino pintado de esta zona.", "Limpiar", "Cancelar"))
                    {
                        EnsurePixels(canvas);
                        SnapshotStroke();
                        var neutral = new Color32(128, 128, 128, 0);
                        for (int i = 0; i < _pixels.Length; i++)
                        {
                            _pixels[i] = neutral;
                        }
                        UploadAll(canvas.Mask);
                    }
                }
            }
        }

        private void OnSceneGUI()
        {
            var canvas = (PathCanvas)target;
            if (canvas.Mask == null)
            {
                return;
            }

            Event e = Event.current;
            bool wasStroking = e.type == EventType.MouseDown && e.button == 0;

            SceneBrush.Action action = Brush.Update(e, out Vector3 point, out Vector3 normal, out bool cursorValid);

            if (Brush.Enabled && cursorValid)
            {
                Brush.DrawCursor(point, normal, _mode == BrushMode.Borrar || e.shift);
                SceneView.RepaintAll();
            }

            if (wasStroking && Brush.Enabled)
            {
                EnsurePixels(canvas);
                SnapshotStroke();
            }

            switch (action)
            {
                case SceneBrush.Action.Paint:
                    Apply(canvas, point, _mode == BrushMode.Borrar);
                    break;

                case SceneBrush.Action.Erase:
                    Apply(canvas, point, true);
                    break;

                case SceneBrush.Action.StrokeEnded:
                    if (_cachedMask != null)
                    {
                        EditorUtility.SetDirty(_cachedMask);
                    }
                    // Se reempuja al terminar el trazo: si el asset de la mascara se
                    // reimporto (cualquier Reimport, o un refresh forzado), la referencia
                    // que quedo dentro del MaterialPropertyBlock apunta a una textura
                    // destruida y el camino desaparece de golpe sin ningun error.
                    canvas.Apply();
                    Repaint();
                    break;
            }
        }

        private void Apply(PathCanvas canvas, Vector3 worldPoint, bool erase)
        {
            EnsurePixels(canvas);

            Texture2D mask = canvas.Mask;

            // Dos radios: en una zona no cuadrada, un circulo del mundo es una elipse en
            // pixeles. Con un solo radio el pincel sale deformado.
            Vector2 radiusPx = canvas.WorldRadiusToPixels(Brush.Radius);
            if (radiusPx.x < 0.5f || radiusPx.y < 0.5f)
            {
                return;
            }

            canvas.TryWorldToPixel(worldPoint, out Vector2 center);

            int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - radiusPx.x));
            int maxX = Mathf.Min(mask.width - 1, Mathf.CeilToInt(center.x + radiusPx.x));
            int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - radiusPx.y));
            int maxY = Mathf.Min(mask.height - 1, Mathf.CeilToInt(center.y + radiusPx.y));

            if (minX > maxX || minY > maxY)
            {
                return;
            }

            if (_mode == BrushMode.Desenfocar && !erase)
            {
                BlurRegion(mask.width, mask.height, minX, minY, maxX - minX + 1, maxY - minY + 1, _blurRadius, _strength);
            }
            else
            {
                StampRegion(canvas, center, radiusPx, minX, minY, maxX, maxY, erase);
            }

            UploadRegion(mask, minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private void StampRegion(
            PathCanvas canvas, Vector2 center, Vector2 radiusPx,
            int minX, int minY, int maxX, int maxY, bool erase)
        {
            LoadBrush(canvas);

            // Rotacion del stamp, constante dentro de una aplicacion.
            float angle = _randomRotation ? UnityEngine.Random.Range(0f, Mathf.PI * 2f) : 0f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            int width = canvas.Mask.width;

            // El tinte se guarda a la mitad: el shader lo multiplica por 2, asi blanco
            // vuelve a 1.0 y no cambia nada.
            var tint = new Color(_tint.r * 0.5f, _tint.g * 0.5f, _tint.b * 0.5f);
            var neutral = new Color(0.5f, 0.5f, 0.5f);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    // Se normaliza por eje: en pixeles el pincel es una elipse, pero en
                    // el mundo vuelve a ser el circulo que dibuja el cursor.
                    float dx = (x + 0.5f - center.x) / radiusPx.x;
                    float dy = (y + 0.5f - center.y) / radiusPx.y;

                    float rx = dx * cos - dy * sin;
                    float ry = dx * sin + dy * cos;

                    float amount = SampleBrush(rx * 0.5f + 0.5f, ry * 0.5f + 0.5f);
                    if (amount <= 0f)
                    {
                        continue;
                    }

                    amount *= _strength;

                    int index = y * width + x;
                    Color32 current = _pixels[index];

                    float coverage = current.a / 255f;
                    Color color = new Color(current.r / 255f, current.g / 255f, current.b / 255f);

                    if (erase)
                    {
                        coverage = Mathf.Max(0f, coverage - amount);
                        color = Color.Lerp(color, neutral, amount);
                    }
                    else
                    {
                        coverage = Mathf.Min(1f, coverage + amount);
                        color = Color.Lerp(color, tint, amount);
                    }

                    _pixels[index] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(color.r) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(color.g) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(color.b) * 255f),
                        (byte)Mathf.RoundToInt(coverage * 255f));
                }
            }
        }

        /// <summary>
        /// Box blur sobre una region. Se hace sobre una copia de la region para que el
        /// desenfoque no se retroalimente con los pixeles ya procesados de la misma
        /// pasada, que es lo que produce el arrastre direccional tipico.
        /// </summary>
        private void BlurRegion(int textureWidth, int textureHeight,
            int regionX, int regionY, int regionWidth, int regionHeight,
            int radius, float strength)
        {
            var source = new Color32[regionWidth * regionHeight];
            for (int y = 0; y < regionHeight; y++)
            {
                Array.Copy(_pixels, (regionY + y) * textureWidth + regionX,
                    source, y * regionWidth, regionWidth);
            }

            for (int y = 0; y < regionHeight; y++)
            {
                for (int x = 0; x < regionWidth; x++)
                {
                    int rSum = 0, gSum = 0, bSum = 0, aSum = 0, samples = 0;

                    for (int oy = -radius; oy <= radius; oy++)
                    {
                        int sy = y + oy;
                        if (sy < 0 || sy >= regionHeight)
                        {
                            continue;
                        }

                        for (int ox = -radius; ox <= radius; ox++)
                        {
                            int sx = x + ox;
                            if (sx < 0 || sx >= regionWidth)
                            {
                                continue;
                            }

                            Color32 sample = source[sy * regionWidth + sx];
                            rSum += sample.r;
                            gSum += sample.g;
                            bSum += sample.b;
                            aSum += sample.a;
                            samples++;
                        }
                    }

                    if (samples == 0)
                    {
                        continue;
                    }

                    int index = (regionY + y) * textureWidth + (regionX + x);
                    Color32 original = _pixels[index];

                    _pixels[index] = new Color32(
                        (byte)Mathf.Lerp(original.r, rSum / (float)samples, strength),
                        (byte)Mathf.Lerp(original.g, gSum / (float)samples, strength),
                        (byte)Mathf.Lerp(original.b, bSum / (float)samples, strength),
                        (byte)Mathf.Lerp(original.a, aSum / (float)samples, strength));
                }
            }
        }

        // -------------------------------------------------------------- pinceles

        private void LoadBrush(PathCanvas canvas)
        {
            Texture2D source = canvas.BrushSet != null ? canvas.BrushSet.Get(_selectedBrush) : null;

            if (source == _cachedBrushSource && _brushAlpha != null)
            {
                return;
            }

            _cachedBrushSource = source;

            if (source == null || !source.isReadable)
            {
                // Sin pincel cargado: circulo con caida suave, para que la herramienta
                // sirva desde el minuto cero.
                _brushAlpha = null;
                _brushWidth = 0;
                _brushHeight = 0;
                return;
            }

            Color32[] pixels = source.GetPixels32();
            _brushWidth = source.width;
            _brushHeight = source.height;
            _brushAlpha = new float[pixels.Length];

            for (int i = 0; i < pixels.Length; i++)
            {
                // Sirve tanto un pincel blanco sobre negro como uno con alpha.
                float luminance = pixels[i].r / 255f;
                float alpha = pixels[i].a / 255f;
                _brushAlpha[i] = luminance * alpha;
            }
        }

        private float SampleBrush(float u, float v)
        {
            if (_brushAlpha == null)
            {
                // Circulo por defecto: caida cuadratica desde el centro.
                float dx = u * 2f - 1f;
                float dy = v * 2f - 1f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float falloff = Mathf.Clamp01(1f - distance);
                return falloff * falloff;
            }

            if (u < 0f || u > 1f || v < 0f || v > 1f)
            {
                return 0f;
            }

            int x = Mathf.Clamp((int)(u * _brushWidth), 0, _brushWidth - 1);
            int y = Mathf.Clamp((int)(v * _brushHeight), 0, _brushHeight - 1);
            return _brushAlpha[y * _brushWidth + x];
        }

        // -------------------------------------------------------------- textura

        private void EnsurePixels(PathCanvas canvas)
        {
            if (_cachedMask == canvas.Mask && _pixels != null)
            {
                return;
            }

            _cachedMask = canvas.Mask;
            _pixels = _cachedMask.GetPixels32();
            _strokeBackup = null;
        }

        private void SnapshotStroke()
        {
            if (_pixels == null)
            {
                return;
            }

            _strokeBackup ??= new Color32[_pixels.Length];
            Array.Copy(_pixels, _strokeBackup, _pixels.Length);
        }

        private void RestoreStrokeBackup(PathCanvas canvas)
        {
            if (_strokeBackup == null || _pixels == null)
            {
                return;
            }

            Array.Copy(_strokeBackup, _pixels, _pixels.Length);
            UploadAll(canvas.Mask);
        }

        private void UploadRegion(Texture2D mask, int x, int y, int width, int height)
        {
            var block = new Color32[width * height];
            for (int row = 0; row < height; row++)
            {
                Array.Copy(_pixels, (y + row) * mask.width + x, block, row * width, width);
            }

            mask.SetPixels32(x, y, width, height, block);
            mask.Apply(false);
        }

        private void UploadAll(Texture2D mask)
        {
            mask.SetPixels32(_pixels);
            mask.Apply(false);
            EditorUtility.SetDirty(mask);
        }

        private void SaveMask(PathCanvas canvas)
        {
            if (canvas.Mask == null)
            {
                return;
            }

            EditorUtility.SetDirty(canvas.Mask);
            AssetDatabase.SaveAssets();
            Debug.Log("[PathCanvas] Máscara guardada.", canvas.Mask);
        }

        private void CreateMask(PathCanvas canvas, int resolution)
        {
            if (!Directory.Exists(DataFolder))
            {
                Directory.CreateDirectory(DataFolder);
                AssetDatabase.Refresh();
            }

            var mask = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true)
            {
                name = "PathMask",
                // Clamp: fuera de la zona no tiene que repetirse el camino.
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var neutral = new Color32(128, 128, 128, 0);
            var pixels = new Color32[resolution * resolution];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = neutral;
            }

            mask.SetPixels32(pixels);
            mask.Apply(false);

            Scene scene = canvas.gameObject.scene;
            string sceneName = string.IsNullOrEmpty(scene.name) ? "Untitled" : scene.name;
            string path = AssetDatabase.GenerateUniqueAssetPath($"{DataFolder}/{sceneName}_{canvas.name}_PathMask.asset");

            AssetDatabase.CreateAsset(mask, path);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(canvas, "Crear máscara de camino");
            canvas.SetMask(mask);
            EditorUtility.SetDirty(canvas);

            _cachedMask = null;
            _pixels = null;

            Debug.Log($"[PathCanvas] Máscara creada en {path}.", mask);
        }

        private static void MakeReadable(Texture2D texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                return;
            }

            importer.isReadable = true;
            importer.SaveAndReimport();
        }
    }
}
