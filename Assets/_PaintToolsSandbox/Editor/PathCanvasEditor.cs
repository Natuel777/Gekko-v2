using System;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gekko.PaintTools.EditorTools
{
    /// <summary>
    /// Pincel de caminos. Pinta sobre DOS mascaras del <see cref="PathCanvas"/>, no sobre
    /// los vertices de la malla — por eso funciona igual en una malla de 100 triangulos
    /// que en una de 100.000, y no le importa como esten las UVs.
    ///
    /// Splat mask: RGBA = peso de Textura 1/2/3/4 (que textura se ve, no un tinte).
    /// Tint mask: RGB = tinte (blanco = sin cambio), A = fuerza de ese tinte. Es una
    /// segunda pasada, independiente de que textura haya debajo.
    /// </summary>
    [CustomEditor(typeof(PathCanvas))]
    public class PathCanvasEditor : UnityEditor.Editor
    {
        private const string DataFolder = "Assets/_PaintToolsSandbox/Data";
        private const int SlotCount = 4;

        private enum BrushMode
        {
            Pintar,
            Borrar,
            Desenfocar,
        }

        private enum PaintTarget
        {
            Textura,
            Tinte,
        }

        // Interpolate: un movimiento rapido del mouse rellena el tramo en vez de dejar un
        // trazo punteado. StepFraction mas chico = trazo mas parejo.
        private static readonly SceneBrush Brush = new SceneBrush { Radius = 3f, Interpolate = true, StepFraction = 0.25f };

        private static BrushMode _mode = BrushMode.Pintar;
        private static PaintTarget _paintTarget = PaintTarget.Textura;
        private static int _activeSlot = 1;
        private static float _strength = 1f;

        // Dureza del pincel por defecto: 0 = caida suave desde el centro, 1 = disco duro.
        // Antes la caida era cuadratica desde el mismo centro, asi que aun con fuerza al
        // maximo el pincel pintaba poco y habia que repasar cada zona varias veces.
        private static float _hardness = 0.5f;

        // Solo golpea los renderers destino del PathCanvas. Con arboles, rocas y props con
        // collider en la escena, el rayo pegaba en la copa y pintaba lejos de donde se veia.
        private static bool _onlyTargets = true;
        private static Color _tint = Color.white;
        private static int _blurRadius = 2;
        private static bool _randomRotation = true;
        private static int _selectedBrush;
        private static int _newMaskResolution = 1024;

        // Con que inclinacion de la normal un golpe pinta tambien en una proyeccion. Con
        // 0.3, una pared vertical solo pinta su costado, el piso solo arriba, y un bisel
        // a 45 grados pinta los dos, que es justo donde el shader los mezcla.
        private const float ProjectionThreshold = 0.3f;

        /// <summary>
        /// Cache de pixeles de UNA mascara: pintar leyendo y escribiendo la textura entera
        /// en cada pincelada es inviable, asi que se mantiene una copia en RAM y solo se
        /// sube a la GPU el rectangulo tocado.
        /// </summary>
        private sealed class MaskBuffer
        {
            public Texture2D Texture;
            public Color32[] Pixels;

            // Snapshot para deshacer el ultimo trazo. Undo.RecordObject no sirve bien con
            // datos de textura, asi que se guarda a mano al empezar cada trazo.
            public Color32[] Backup;

            // Rectangulo tocado en los stamps del evento actual. Se sube a la GPU UNA vez
            // por evento (no una por stamp): con interpolacion un evento puede traer varios.
            public bool HasDirty;
            public int DirtyMinX, DirtyMinY, DirtyMaxX, DirtyMaxY;

            public int Width => Texture.width;
        }

        // Una para la mascara de arriba y otra para la de costados, las dos de la tab
        // ACTIVA (splat o tinte, segun _paintTarget) — cambiar de tab invalida las dos.
        private readonly MaskBuffer _topBuffer = new MaskBuffer();
        private readonly MaskBuffer _sideBuffer = new MaskBuffer();

        private float[] _brushAlpha;
        private int _brushWidth;
        private int _brushHeight;
        private Texture2D _cachedBrushSource;

        private PathCanvas _filterCanvas;
        private GUIStyle _hudStyle;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var canvas = (PathCanvas)target;

            EditorGUILayout.Space();

            if (canvas.Mask == null || canvas.TintMask == null)
            {
                DrawMaskCreation(canvas);
                return;
            }

            DrawSummary(canvas);
            DrawSideMasks(canvas);
            PathCanvasChecks.DrawChecks(canvas);
            EditorGUILayout.Space();

            Brush.DrawToggleButton("Modo pintura: ACTIVO (Esc para salir)", "Activar modo pintura");

            if (Brush.Enabled)
            {
                EditorGUILayout.HelpBox(
                    "Click y arrastrar: aplicar.\nShift + click: borrar.\nCtrl + rueda o [ ]: radio.\n" +
                    "1-4: cambiar de textura activa (pintando Textura).",
                    MessageType.None);
            }

            EditorGUILayout.Space();
            DrawActiveIndicator(canvas);

            EditorGUILayout.Space();
            DrawPaintTargetTabs(canvas);

            EditorGUILayout.Space();
            DrawBrushSettings(canvas);

            EditorGUILayout.Space();
            DrawBrushPalette(canvas);

            EditorGUILayout.Space();
            DrawMaskButtons(canvas);
        }

        // -------------------------------------------------------- indicador activo

        /// <summary>
        /// Muestra bien grande QUE se esta pintando ahora mismo. Es la respuesta directa
        /// a "no se con que textura estoy cargando": se ve arriba de todo, sin tener que
        /// interpretar el resto del inspector.
        /// </summary>
        private void DrawActiveIndicator(PathCanvas canvas)
        {
            string label = _paintTarget == PaintTarget.Textura
                ? $"Pintando: Textura {_activeSlot} ({SlotDisplayName(canvas, _activeSlot)})"
                : "Pintando: Tinte";

            var style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            var color = _paintTarget == PaintTarget.Textura ? new Color(0.55f, 0.85f, 1f) : new Color(1f, 0.85f, 0.55f);

            Material material = GetActiveMaterial(canvas);
            float buttonWidth = material != null ? 90f : 0f;

            Rect rect = GUILayoutUtility.GetRect(0f, 24f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));
            var prevColor = style.normal.textColor;
            style.normal.textColor = color;
            GUI.Label(new Rect(rect.x + 8f, rect.y, rect.width - 16f - buttonWidth, rect.height), label, style);
            style.normal.textColor = prevColor;

            // Ir directo al material desde acá: es el lugar donde se ven "Textura N (sin
            // asignar)" y demas, asi que de ahi mismo se puede saltar a arreglarlo en vez
            // de andar buscando a mano que renderer/material tiene el PathCanvas.
            if (material != null)
            {
                var buttonRect = new Rect(rect.xMax - buttonWidth - 4f, rect.y + 2f, buttonWidth, rect.height - 4f);
                if (GUI.Button(buttonRect, "Ver material"))
                {
                    PingMaterial(material);
                }
            }

            if (_paintTarget == PaintTarget.Textura)
            {
                bool enabled = IsSlotEnabled(material, _activeSlot);
                bool assigned = material != null && material.GetTexture($"_Tex{_activeSlot}") != null;

                if (!enabled)
                {
                    DrawMaterialWarning(
                        $"La Textura {_activeSlot} no esta activa en el material (toggle 'Textura {_activeSlot} " +
                        "activa' destildado) — vas a pintar en un canal que el shader ignora y no se va a ver nada.",
                        material);
                }
                else if (!assigned)
                {
                    // Justo lo que pasaba en la captura: slot activo pero sin textura
                    // asignada = el shader cae al blanco por defecto y "pinta blanco".
                    DrawMaterialWarning(
                        $"La Textura {_activeSlot} esta activa pero no tiene textura asignada — el shader cae " +
                        "al blanco por defecto del slot. Asignale una textura en el material.",
                        material);
                }
            }
        }

        /// <summary>HelpBox + boton para ir directo al material, para los casos donde el problema se arregla ahi.</summary>
        private static void DrawMaterialWarning(string message, Material material)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    new GUIContent(message, EditorGUIUtility.IconContent("console.warnicon").image),
                    new GUIStyle(EditorStyles.wordWrappedLabel));

                if (GUILayout.Button("Abrir material", GUILayout.Width(100f), GUILayout.ExpandHeight(true)))
                {
                    PingMaterial(material);
                }
            }
        }

        private static void PingMaterial(Material material)
        {
            Selection.activeObject = material;
            EditorGUIUtility.PingObject(material);
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

        /// <summary>
        /// Estado de las mascaras de costados. Sin ellas, pintar una pared escribe en la
        /// mascara de arriba, que en vertical es una sola fila de pixeles para toda la
        /// altura: sale una raya estirada de arriba a abajo.
        /// </summary>
        private void DrawSideMasks(PathCanvas canvas)
        {
            if (canvas.HasSideMasks)
            {
                RectInt quadrant = canvas.SideQuadrantPixels(PathCanvas.SideFace.PositiveX);
                float horizontal = quadrant.width / Mathf.Max(canvas.Size.x, canvas.Size.y, 0.01f);
                float vertical = quadrant.height / Mathf.Max(canvas.Height, 0.01f);
                EditorGUILayout.HelpBox(
                    $"Costados: {canvas.SideMask.width} x {canvas.SideMask.height} (4 caras), " +
                    $"{horizontal:0.00} texels por unidad en horizontal y {vertical:0.00} en vertical.\n" +
                    $"Cubren de Y {canvas.WorldMinY:0.0} a {canvas.WorldMinY + canvas.Height:0.0} (ajustalo con 'Height').",
                    MessageType.None);
                return;
            }

            EditorGUILayout.HelpBox(
                "Sin máscaras de costados: pintar una pared la estira de arriba a abajo. " +
                "Creálas para poder pintar los costados de la plataforma.",
                MessageType.Warning);

            if (GUILayout.Button("Crear máscaras de costados"))
            {
                // El doble que la de arriba: el atlas tiene 4 caras, cada una a un cuarto.
                int resolution = Mathf.Min(4096, canvas.Mask.width * 2);
                CreateSideMasks(canvas, resolution);
                GUIUtility.ExitGUI();
            }
        }

        private void DrawMaskCreation(PathCanvas canvas)
        {
            EditorGUILayout.HelpBox("Esta zona todavía no tiene sus máscaras (splat + tinte).", MessageType.Warning);

            _newMaskResolution = EditorGUILayout.IntPopup(
                "Resolución",
                _newMaskResolution,
                new[] { "256", "512", "1024", "2048", "4096" },
                new[] { 256, 512, 1024, 2048, 4096 });

            // Se muestra el peor eje: es el que manda para el detalle del borde.
            float texels = _newMaskResolution / Mathf.Max(canvas.Size.x, canvas.Size.y, 0.01f);
            EditorGUILayout.LabelField(" ", $"{texels:0.00} texels por unidad (eje más largo)");

            if (GUILayout.Button("Crear máscaras", GUILayout.Height(28f)))
            {
                CreateMasks(canvas, _newMaskResolution);
            }
        }

        // -------------------------------------------------------- tabs textura/tinte

        private void DrawPaintTargetTabs(PathCanvas canvas)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawTab(PaintTarget.Textura, "Pintar textura");
                DrawTab(PaintTarget.Tinte, "Pintar tinte");
            }

            EditorGUILayout.Space(4f);

            if (_paintTarget == PaintTarget.Textura)
            {
                DrawTexturePalette(canvas);
            }
            else
            {
                DrawTintSettings();
            }
        }

        private void DrawTab(PaintTarget tab, string label)
        {
            bool isActive = _paintTarget == tab;
            var style = new GUIStyle(GUI.skin.button) { fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal };
            var prevColor = GUI.backgroundColor;
            GUI.backgroundColor = isActive ? new Color(0.55f, 0.85f, 1f) : prevColor;

            if (GUILayout.Toggle(isActive, label, style, GUILayout.Height(24f)) && !isActive)
            {
                _paintTarget = tab;
                InvalidateBuffers();
            }

            GUI.backgroundColor = prevColor;
        }

        /// <summary>
        /// Paleta de las hasta 4 texturas pintables del material asignado, con thumbnail
        /// y nombre — asi elegis "con que textura estas cargando" viendola, no
        /// adivinando un indice. Los shortcuts 1-4 (activos con el pincel prendido) hacen
        /// lo mismo sin soltar el mouse.
        /// </summary>
        private void DrawTexturePalette(PathCanvas canvas)
        {
            Material material = GetActiveMaterial(canvas);

            if (material == null)
            {
                EditorGUILayout.HelpBox(
                    "El PathCanvas todavia no tiene 'Target Renderers' asignados, asi que no puedo leer que " +
                    "texturas tiene el material para armar la paleta.",
                    MessageType.Warning);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                for (int slot = 1; slot <= SlotCount; slot++)
                {
                    DrawTextureSlotButton(material, slot);
                }
            }
        }

        private void DrawTextureSlotButton(Material material, int slot)
        {
            bool enabled = IsSlotEnabled(material, slot);
            bool isActive = _activeSlot == slot;
            Texture thumbnail = material != null ? material.GetTexture($"_Tex{slot}") : null;

            using (new EditorGUILayout.VerticalScope(GUILayout.Width(64f)))
            {
                Rect rect = GUILayoutUtility.GetRect(60f, 48f, GUILayout.Width(60f));

                if (isActive)
                {
                    EditorGUI.DrawRect(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4),
                        new Color(0.4f, 0.75f, 1f, 1f));
                }

                if (thumbnail != null)
                {
                    GUI.DrawTexture(rect, thumbnail, ScaleMode.ScaleAndCrop);
                }
                else
                {
                    EditorGUI.DrawRect(rect, enabled ? new Color(0.25f, 0.25f, 0.27f) : new Color(0.12f, 0.12f, 0.12f));
                }

                if (!enabled)
                {
                    GUI.Label(rect, "off", EditorStyles.centeredGreyMiniLabel);
                }

                if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                {
                    _activeSlot = slot;
                }

                var numberStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
                GUILayout.Label($"({slot})", numberStyle);
            }
        }

        private void DrawTintSettings()
        {
            _tint = EditorGUILayout.ColorField(
                new GUIContent("Tinte", "Multiplica el color final. Blanco = sin cambio."),
                _tint, true, false, false);
        }

        private void DrawBrushSettings(PathCanvas canvas)
        {
            EditorGUILayout.LabelField("Pincel", EditorStyles.boldLabel);

            _mode = (BrushMode)EditorGUILayout.EnumPopup("Modo", _mode);
            Brush.DrawCommonSettings();
            _strength = EditorGUILayout.Slider("Fuerza", _strength, 0.01f, 1f);
            _hardness = EditorGUILayout.Slider(
                new GUIContent("Dureza", "0 = borde muy suave, 1 = disco duro. Solo afecta al pincel circular por defecto."),
                _hardness, 0f, 0.95f);
            _onlyTargets = EditorGUILayout.Toggle(
                new GUIContent("Solo sobre el piso destino",
                    "El pincel atraviesa arboles, rocas y demas colliders que no sean los Target Renderers del PathCanvas."),
                _onlyTargets);

            if (_mode == BrushMode.Desenfocar)
            {
                _blurRadius = EditorGUILayout.IntSlider("Radio del desenfoque (px)", _blurRadius, 1, 12);
            }
            else
            {
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
                using (new EditorGUI.DisabledScope(_topBuffer.Backup == null && _sideBuffer.Backup == null))
                {
                    if (GUILayout.Button("Deshacer trazo"))
                    {
                        RestoreStrokeBackup();
                    }
                }

                if (GUILayout.Button("Guardar máscaras"))
                {
                    SaveMask(canvas.Mask);
                    SaveMask(canvas.TintMask);
                    SaveMask(canvas.SideMask);
                    SaveMask(canvas.SideTintMask);
                    Debug.Log("[PathCanvas] Máscaras (arriba y costados) guardadas.", canvas);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Desenfocar todo (activa)"))
                {
                    EnsureBuffers(canvas);
                    SnapshotStroke();

                    MaskBuffer top = _topBuffer;
                    BlurRegion(top, 0, 0, top.Width, top.Texture.height, _blurRadius, 1f);
                    UploadAll(top);

                    // Los costados, cuadrante por cuadrante: un blur del atlas entero
                    // mezclaria en los bordes caras que no tienen nada que ver.
                    if (_sideBuffer.Pixels != null)
                    {
                        for (int face = 0; face < 4; face++)
                        {
                            RectInt quadrant = canvas.SideQuadrantPixels((PathCanvas.SideFace)face);
                            BlurRegion(_sideBuffer, quadrant.x, quadrant.y, quadrant.width, quadrant.height, _blurRadius, 1f);
                        }
                        UploadAll(_sideBuffer);
                    }
                }

                string clearLabel = _paintTarget == PaintTarget.Textura ? "Limpiar textura" : "Limpiar tinte";
                if (GUILayout.Button(clearLabel))
                {
                    string what = _paintTarget == PaintTarget.Textura ? "toda la textura pintada" : "todo el tinte pintado";
                    if (EditorUtility.DisplayDialog(clearLabel, $"Se borra {what} de esta zona (arriba y costados).", "Limpiar", "Cancelar"))
                    {
                        EnsureBuffers(canvas);
                        SnapshotStroke();
                        // Splat neutro = 0,0,0,0 (todo base). Tinte neutro = 128,128,128,0 (sin cambio).
                        Color32 neutral = _paintTarget == PaintTarget.Textura
                            ? new Color32(0, 0, 0, 0)
                            : new Color32(128, 128, 128, 0);
                        foreach (MaskBuffer buffer in new[] { _topBuffer, _sideBuffer })
                        {
                            if (buffer.Pixels == null)
                            {
                                continue;
                            }
                            for (int i = 0; i < buffer.Pixels.Length; i++)
                            {
                                buffer.Pixels[i] = neutral;
                            }
                            UploadAll(buffer);
                        }
                    }
                }
            }
        }

        private void OnSceneGUI()
        {
            var canvas = (PathCanvas)target;
            if (canvas.Mask == null || canvas.TintMask == null)
            {
                return;
            }

            Event e = Event.current;

            HandleShortcuts(canvas, e);

            // Filtro de raycast: solo los renderers destino cuentan como "piso".
            _filterCanvas = canvas;
            Brush.HitFilter = _onlyTargets ? (Func<RaycastHit, bool>)IsTargetHit : null;

            bool wasStroking = e.type == EventType.MouseDown && e.button == 0;

            SceneBrush.Action action = Brush.Update(e, out Vector3 point, out Vector3 normal, out bool cursorValid);

            if (Brush.Enabled && cursorValid && e.type == EventType.Repaint)
            {
                Brush.DrawCursor(point, normal, _mode == BrushMode.Borrar || e.shift);
            }

            // Antes se llamaba RepaintAll() en CADA evento, incluido el Repaint: cada
            // repintado disparaba otro y el editor quedaba repintando sin parar, robandole
            // tiempo al pintado. Ahora solo se repinta cuando el cursor realmente se movio.
            if (Brush.Enabled && (e.type == EventType.MouseMove || e.type == EventType.MouseDrag))
            {
                SceneView.currentDrawingSceneView?.Repaint();
            }

            if (wasStroking && Brush.Enabled)
            {
                EnsureBuffers(canvas);
                SnapshotStroke();
            }

            switch (action)
            {
                case SceneBrush.Action.Paint:
                    ApplyStroke(canvas, point, normal, _mode == BrushMode.Borrar);
                    break;

                case SceneBrush.Action.Erase:
                    ApplyStroke(canvas, point, normal, true);
                    break;

                case SceneBrush.Action.StrokeEnded:
                    if (_topBuffer.Texture != null)
                    {
                        EditorUtility.SetDirty(_topBuffer.Texture);
                    }
                    if (_sideBuffer.Texture != null)
                    {
                        EditorUtility.SetDirty(_sideBuffer.Texture);
                    }
                    // Se reempuja al terminar el trazo: si el asset de alguna mascara se
                    // reimporto (cualquier Reimport, o un refresh forzado), la referencia
                    // que quedo dentro del MaterialPropertyBlock apunta a una textura
                    // destruida y el camino desaparece de golpe sin ningun error.
                    canvas.Apply();
                    Repaint();
                    break;
            }

            DrawHud(e);
        }

        /// <summary>Un golpe de rayo cuenta si es collider de (o hijo/padre de) alguno de los Target Renderers.</summary>
        private bool IsTargetHit(RaycastHit hit)
        {
            Renderer[] targets = _filterCanvas != null ? _filterCanvas.TargetRenderers : null;
            if (targets == null || hit.collider == null)
            {
                return false;
            }

            Transform hitTransform = hit.collider.transform;
            foreach (Renderer renderer in targets)
            {
                if (renderer == null)
                {
                    continue;
                }

                Transform target = renderer.transform;
                if (hitTransform == target || hitTransform.IsChildOf(target) || target.IsChildOf(hitTransform))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 1-4 cambian la textura activa (solo tiene sentido pintando Textura). [ y ] cambian
        /// el radio. Solo responde con el pincel prendido, para no robarle las teclas al
        /// resto del editor cuando el PathCanvas esta seleccionado nomas de paso.
        /// </summary>
        private void HandleShortcuts(PathCanvas canvas, Event e)
        {
            if (!Brush.Enabled || e.type != EventType.KeyDown)
            {
                return;
            }

            // [ y ] por caracter y no por keyCode: en teclados con otra distribucion
            // (es-AR, es-ES) los corchetes salen con AltGr y el keyCode no es confiable.
            if (e.character == '[' || e.character == ']' || e.keyCode == KeyCode.LeftBracket || e.keyCode == KeyCode.RightBracket)
            {
                bool shrink = e.character == '[' || e.keyCode == KeyCode.LeftBracket;
                Brush.Radius = Mathf.Clamp(Brush.Radius * (shrink ? 0.85f : 1.15f), 0.1f, 50f);
                e.Use();
                Repaint();
                SceneView.RepaintAll();
                return;
            }

            int slot = e.keyCode switch
            {
                KeyCode.Alpha1 or KeyCode.Keypad1 => 1,
                KeyCode.Alpha2 or KeyCode.Keypad2 => 2,
                KeyCode.Alpha3 or KeyCode.Keypad3 => 3,
                KeyCode.Alpha4 or KeyCode.Keypad4 => 4,
                _ => 0,
            };

            if (slot == 0)
            {
                return;
            }

            _paintTarget = PaintTarget.Textura;
            _activeSlot = slot;
            InvalidateBuffers();

            e.Use();
            Repaint();
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Indicador permanente en la esquina de la Scene View: la respuesta a "no se
        /// con que textura estoy cargando" sin tener que mirar el Inspector.
        /// </summary>
        private void DrawHud(Event e)
        {
            // Solo se dibuja en Repaint (el resto de eventos no pinta nada) y el estilo se
            // cachea: antes se creaba un GUIStyle nuevo en cada evento del mouse.
            if (!Brush.Enabled || e.type != EventType.Repaint)
            {
                return;
            }

            _hudStyle ??= new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 6, 6),
            };

            Handles.BeginGUI();

            string label = _paintTarget == PaintTarget.Textura
                ? $"Pintando: Textura {_activeSlot}"
                : "Pintando: Tinte";

            var rect = new Rect(10f, 10f, 220f, 28f);
            GUI.Label(rect, label, _hudStyle);
            GUI.Label(new Rect(10f, rect.yMax + 2f, 260f, 18f), "1-4 textura   [ ] radio   Shift borra", EditorStyles.miniLabel);

            Handles.EndGUI();
        }

        /// <summary>
        /// Aplica todos los puntos del evento (con interpolacion pueden ser varios) y
        /// recien despues sube el rectangulo tocado a la GPU, una sola vez.
        /// </summary>
        private void ApplyStroke(PathCanvas canvas, Vector3 fallbackPoint, Vector3 normal, bool erase)
        {
            EnsureBuffers(canvas);
            _topBuffer.HasDirty = false;
            _sideBuffer.HasDirty = false;

            // Los puntos interpolados usan la normal del evento: son el tramo entre dos
            // posiciones del cursor, casi siempre sobre la misma cara.
            if (Brush.StrokePoints.Count == 0)
            {
                StampAt(canvas, fallbackPoint, normal, erase);
            }
            else
            {
                foreach (Vector3 stampPoint in Brush.StrokePoints)
                {
                    StampAt(canvas, stampPoint, normal, erase);
                }
            }

            FlushUpload(_topBuffer);
            FlushUpload(_sideBuffer);
        }

        /// <summary>
        /// Un stamp en cada proyeccion que "ve" la superficie golpeada, segun su normal:
        /// arriba para el piso, la cara lateral que corresponda para una pared, y las
        /// dos en un bisel. Sin mascaras de costados todo va a la de arriba, como antes.
        /// </summary>
        private void StampAt(PathCanvas canvas, Vector3 worldPoint, Vector3 normal, bool erase)
        {
            bool hasSide = _sideBuffer.Pixels != null;

            if (!hasSide || Mathf.Abs(normal.y) >= ProjectionThreshold)
            {
                // Dos radios: en una zona no cuadrada, un circulo del mundo es una elipse
                // en pixeles. Con un solo radio el pincel sale deformado.
                canvas.TryWorldToPixel(worldPoint, out Vector2 center);
                Vector2 radiusPx = canvas.WorldRadiusToPixels(Brush.Radius);
                var whole = new RectInt(0, 0, _topBuffer.Width, _topBuffer.Texture.height);
                StampProjection(canvas, _topBuffer, center, radiusPx, whole, erase);
            }

            if (!hasSide)
            {
                return;
            }

            if (Mathf.Abs(normal.x) >= ProjectionThreshold)
            {
                StampSide(canvas, worldPoint, normal.x >= 0f ? PathCanvas.SideFace.PositiveX : PathCanvas.SideFace.NegativeX, erase);
            }

            if (Mathf.Abs(normal.z) >= ProjectionThreshold)
            {
                StampSide(canvas, worldPoint, normal.z >= 0f ? PathCanvas.SideFace.PositiveZ : PathCanvas.SideFace.NegativeZ, erase);
            }
        }

        private void StampSide(PathCanvas canvas, Vector3 worldPoint, PathCanvas.SideFace face, bool erase)
        {
            canvas.TryWorldToSidePixel(worldPoint, face, out Vector2 center);
            Vector2 radiusPx = canvas.WorldRadiusToSidePixels(Brush.Radius, face);

            // Recortado al cuadrante de la cara: un pincel cerca del borde no tiene que
            // derramar pintura sobre la cara vecina del atlas.
            StampProjection(canvas, _sideBuffer, center, radiusPx, canvas.SideQuadrantPixels(face), erase);
        }

        private void StampProjection(PathCanvas canvas, MaskBuffer buffer, Vector2 center, Vector2 radiusPx, RectInt clip, bool erase)
        {
            if (radiusPx.x < 0.5f || radiusPx.y < 0.5f)
            {
                return;
            }

            int minX = Mathf.Max(clip.xMin, Mathf.FloorToInt(center.x - radiusPx.x));
            int maxX = Mathf.Min(clip.xMax - 1, Mathf.CeilToInt(center.x + radiusPx.x));
            int minY = Mathf.Max(clip.yMin, Mathf.FloorToInt(center.y - radiusPx.y));
            int maxY = Mathf.Min(clip.yMax - 1, Mathf.CeilToInt(center.y + radiusPx.y));

            if (minX > maxX || minY > maxY)
            {
                return;
            }

            if (_mode == BrushMode.Desenfocar && !erase)
            {
                BlurRegion(buffer, minX, minY, maxX - minX + 1, maxY - minY + 1, _blurRadius, _strength);
            }
            else if (_paintTarget == PaintTarget.Textura)
            {
                StampTextureRegion(canvas, buffer, center, radiusPx, minX, minY, maxX, maxY, erase);
            }
            else
            {
                StampTintRegion(canvas, buffer, center, radiusPx, minX, minY, maxX, maxY, erase);
            }

            MarkDirty(buffer, minX, minY, maxX, maxY);
        }

        private static void MarkDirty(MaskBuffer buffer, int minX, int minY, int maxX, int maxY)
        {
            if (!buffer.HasDirty)
            {
                buffer.DirtyMinX = minX;
                buffer.DirtyMinY = minY;
                buffer.DirtyMaxX = maxX;
                buffer.DirtyMaxY = maxY;
                buffer.HasDirty = true;
                return;
            }

            buffer.DirtyMinX = Mathf.Min(buffer.DirtyMinX, minX);
            buffer.DirtyMinY = Mathf.Min(buffer.DirtyMinY, minY);
            buffer.DirtyMaxX = Mathf.Max(buffer.DirtyMaxX, maxX);
            buffer.DirtyMaxY = Mathf.Max(buffer.DirtyMaxY, maxY);
        }

        private void FlushUpload(MaskBuffer buffer)
        {
            if (!buffer.HasDirty)
            {
                return;
            }

            UploadRegion(buffer, buffer.DirtyMinX, buffer.DirtyMinY,
                buffer.DirtyMaxX - buffer.DirtyMinX + 1, buffer.DirtyMaxY - buffer.DirtyMinY + 1);
            buffer.HasDirty = false;
        }

        /// <summary>
        /// Pinta "cuanto de la textura activa hay" en el canal correspondiente del splat
        /// (R/G/B/A = Textura 1/2/3/4), y les resta proporcionalmente a los otros 3 para
        /// que la suma nunca pase de 1 — el mismo criterio que un pincel de splatmap de
        /// terreno. Borrar solo baja el canal activo: lo que libera vuelve a la base.
        /// </summary>
        private void StampTextureRegion(
            PathCanvas canvas, MaskBuffer buffer, Vector2 center, Vector2 radiusPx,
            int minX, int minY, int maxX, int maxY, bool erase)
        {
            LoadBrush(canvas);

            float angle = _randomRotation ? UnityEngine.Random.Range(0f, Mathf.PI * 2f) : 0f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            Color32[] pixels = buffer.Pixels;
            int width = buffer.Width;
            int activeChannel = _activeSlot - 1;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
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
                    Color32 current = pixels[index];

                    // Vector4 (struct en stack) y no un float[4] por pixel: con radios
                    // grandes eran decenas de miles de arrays por stamp, y el GC
                    // provocaba los tirones al pintar.
                    var weights = new Vector4(current.r, current.g, current.b, current.a) * (1f / 255f);

                    if (erase)
                    {
                        weights[activeChannel] = Mathf.Max(0f, weights[activeChannel] - amount);
                    }
                    else
                    {
                        weights[activeChannel] = Mathf.Min(1f, weights[activeChannel] + amount);

                        // A los demas canales les toca lo que quede libre, repartido en
                        // proporcion a lo que ya tenian (no de a uno): asi pintar Textura
                        // 2 sobre una zona con Textura 1 al 100% la va reemplazando de a
                        // poco, en vez de dejarlas superpuestas sumando mas de 1.
                        float othersSum = weights.x + weights.y + weights.z + weights.w - weights[activeChannel];

                        if (othersSum > 1e-4f)
                        {
                            float allowed = Mathf.Max(0f, 1f - weights[activeChannel]);
                            float scale = allowed / othersSum;
                            for (int c = 0; c < 4; c++)
                            {
                                if (c != activeChannel)
                                {
                                    weights[c] *= scale;
                                }
                            }
                        }
                    }

                    pixels[index] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(weights.x) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(weights.y) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(weights.z) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(weights.w) * 255f));
                }
            }
        }

        /// <summary>Pinta color + fuerza en la tint mask. Logica identica a la version anterior de un solo mask.</summary>
        private void StampTintRegion(
            PathCanvas canvas, MaskBuffer buffer, Vector2 center, Vector2 radiusPx,
            int minX, int minY, int maxX, int maxY, bool erase)
        {
            LoadBrush(canvas);

            float angle = _randomRotation ? UnityEngine.Random.Range(0f, Mathf.PI * 2f) : 0f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            Color32[] pixels = buffer.Pixels;
            int width = buffer.Width;

            // El tinte se guarda a la mitad: el shader lo multiplica por 2, asi blanco
            // vuelve a 1.0 y no cambia nada.
            var tint = new Color(_tint.r * 0.5f, _tint.g * 0.5f, _tint.b * 0.5f);
            var neutral = new Color(0.5f, 0.5f, 0.5f);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
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
                    Color32 current = pixels[index];

                    float strength01 = current.a / 255f;
                    Color color = new Color(current.r / 255f, current.g / 255f, current.b / 255f);

                    if (erase)
                    {
                        strength01 = Mathf.Max(0f, strength01 - amount);
                        color = Color.Lerp(color, neutral, amount);
                    }
                    else
                    {
                        strength01 = Mathf.Min(1f, strength01 + amount);
                        color = Color.Lerp(color, tint, amount);
                    }

                    pixels[index] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(color.r) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(color.g) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(color.b) * 255f),
                        (byte)Mathf.RoundToInt(strength01 * 255f));
                }
            }
        }

        /// <summary>
        /// Box blur sobre una region. Se hace sobre una copia de la region para que el
        /// desenfoque no se retroalimente con los pixeles ya procesados de la misma
        /// pasada, que es lo que produce el arrastre direccional tipico.
        /// </summary>
        private static void BlurRegion(MaskBuffer buffer,
            int regionX, int regionY, int regionWidth, int regionHeight,
            int radius, float strength)
        {
            Color32[] pixels = buffer.Pixels;
            int textureWidth = buffer.Width;

            var source = new Color32[regionWidth * regionHeight];
            for (int y = 0; y < regionHeight; y++)
            {
                Array.Copy(pixels, (regionY + y) * textureWidth + regionX,
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
                    Color32 original = pixels[index];

                    pixels[index] = new Color32(
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

            if (source == null || !source.isReadable || source.width <= 0 || source.height <= 0)
            {
                // Sin pincel cargado (o con dimensiones invalidas): circulo con caida
                // suave, para que la herramienta sirva desde el minuto cero.
                _brushAlpha = null;
                _brushWidth = 0;
                _brushHeight = 0;
                return;
            }

            Color32[] pixels = source.GetPixels32();

            // Defensivo: si la textura se reimporto justo antes (por ejemplo al tocar
            // "Arreglar el importador", que dispara un SaveAndReimport), GetPixels32
            // puede devolver un array que todavia no coincide con width*height de ESTE
            // frame. Sin este chequeo, SampleBrush indexaba fuera de rango y tiraba
            // abajo la Scene View entera con un IndexOutOfRangeException.
            if (pixels.Length != source.width * source.height)
            {
                _brushAlpha = null;
                _brushWidth = 0;
                _brushHeight = 0;
                return;
            }

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
            if (_brushAlpha == null || _brushWidth <= 0 || _brushHeight <= 0)
            {
                // Circulo por defecto con dureza: pleno hasta el radio "_hardness" y
                // caida suave (smoothstep) hasta el borde. Con dureza 0 es una caida
                // continua desde el centro; con dureza alta, un disco casi pleno.
                float dx = u * 2f - 1f;
                float dy = v * 2f - 1f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float t = Mathf.Clamp01((1f - distance) / Mathf.Max(1f - _hardness, 0.05f));
                return t * t * (3f - 2f * t);
            }

            if (u < 0f || u > 1f || v < 0f || v > 1f)
            {
                return 0f;
            }

            int x = Mathf.Clamp((int)(u * _brushWidth), 0, _brushWidth - 1);
            int y = Mathf.Clamp((int)(v * _brushHeight), 0, _brushHeight - 1);

            // Ultima red de seguridad: si por lo que sea el array no mide width*height,
            // no crashear la Scene View por un pincel — simplemente no pintar ese texel.
            int index = y * _brushWidth + x;
            return index >= 0 && index < _brushAlpha.Length ? _brushAlpha[index] : 0f;
        }

        // -------------------------------------------------------------- textura

        /// <summary>Cual de las dos mascaras corresponde pintar segun la tab activa.</summary>
        private Texture2D ActiveMaskTexture(PathCanvas canvas)
        {
            return _paintTarget == PaintTarget.Textura ? canvas.Mask : canvas.TintMask;
        }

        /// <summary>La mascara de costados que corresponde a la tab activa, o null si el canvas no tiene.</summary>
        private Texture2D ActiveSideMaskTexture(PathCanvas canvas)
        {
            if (!canvas.HasSideMasks)
            {
                return null;
            }
            return _paintTarget == PaintTarget.Textura ? canvas.SideMask : canvas.SideTintMask;
        }

        private void EnsureBuffers(PathCanvas canvas)
        {
            EnsurePixels(_topBuffer, ActiveMaskTexture(canvas));
            EnsurePixels(_sideBuffer, ActiveSideMaskTexture(canvas));
        }

        private static void EnsurePixels(MaskBuffer buffer, Texture2D mask)
        {
            if (buffer.Texture == mask && (mask == null || buffer.Pixels != null))
            {
                return;
            }

            buffer.Texture = mask;
            buffer.Pixels = mask != null ? mask.GetPixels32() : null;
            buffer.Backup = null;
            buffer.HasDirty = false;
        }

        private void InvalidateBuffers()
        {
            foreach (MaskBuffer buffer in new[] { _topBuffer, _sideBuffer })
            {
                buffer.Texture = null;
                buffer.Pixels = null;
                buffer.Backup = null;
                buffer.HasDirty = false;
            }
        }

        private void SnapshotStroke()
        {
            foreach (MaskBuffer buffer in new[] { _topBuffer, _sideBuffer })
            {
                if (buffer.Pixels == null)
                {
                    continue;
                }

                if (buffer.Backup == null || buffer.Backup.Length != buffer.Pixels.Length)
                {
                    buffer.Backup = new Color32[buffer.Pixels.Length];
                }
                Array.Copy(buffer.Pixels, buffer.Backup, buffer.Pixels.Length);
            }
        }

        private void RestoreStrokeBackup()
        {
            foreach (MaskBuffer buffer in new[] { _topBuffer, _sideBuffer })
            {
                if (buffer.Backup == null || buffer.Pixels == null || buffer.Texture == null)
                {
                    continue;
                }

                Array.Copy(buffer.Backup, buffer.Pixels, buffer.Pixels.Length);
                UploadAll(buffer);
            }
        }

        private static void UploadRegion(MaskBuffer buffer, int x, int y, int width, int height)
        {
            Texture2D mask = buffer.Texture;
            Color32[] pixels = buffer.Pixels;

            if (mask.format == TextureFormat.RGBA32)
            {
                // Escribe directo en la memoria CPU de la textura, fila por fila, sin el
                // array temporal ni la conversion de SetPixels32 (que alocaba un bloque
                // nuevo en cada stamp).
                NativeArray<Color32> raw = mask.GetRawTextureData<Color32>();
                for (int row = 0; row < height; row++)
                {
                    int offset = (y + row) * mask.width + x;
                    NativeArray<Color32>.Copy(pixels, offset, raw, offset, width);
                }
            }
            else
            {
                var block = new Color32[width * height];
                for (int row = 0; row < height; row++)
                {
                    Array.Copy(pixels, (y + row) * mask.width + x, block, row * width, width);
                }

                mask.SetPixels32(x, y, width, height, block);
            }

            mask.Apply(false);
        }

        private static void UploadAll(MaskBuffer buffer)
        {
            buffer.Texture.SetPixels32(buffer.Pixels);
            buffer.Texture.Apply(false);
            EditorUtility.SetDirty(buffer.Texture);
        }

        // Se guarda como PNG y no como Texture2D nativo (AssetDatabase.CreateAsset)
        // porque ese formato demostro perder datos (a veces el archivo entero, guid
        // incluido) en un reimport. Ver AUDITORIA.md, seccion "Persistencia". Un PNG es
        // un archivo comun que el importer de Unity maneja de forma robusta.
        private static void SaveMask(Texture2D mask)
        {
            if (mask == null)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(mask);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[PathCanvas] Una máscara no tiene un archivo en disco asociado; no se pudo guardar.", mask);
                return;
            }

            WritePng(mask, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>Crea la splat mask (neutro = todo 0, es decir toda base) y la tint mask (neutro = gris medio, sin cobertura) juntas.</summary>
        private void CreateMasks(PathCanvas canvas, int resolution)
        {
            if (!Directory.Exists(DataFolder))
            {
                Directory.CreateDirectory(DataFolder);
                AssetDatabase.Refresh();
            }

            Scene scene = canvas.gameObject.scene;
            string sceneName = string.IsNullOrEmpty(scene.name) ? "Untitled" : scene.name;

            Texture2D splat = GetOrCreateMask(
                canvas, $"{DataFolder}/{sceneName}_{canvas.name}_SplatMask.png", resolution,
                new Color32(0, 0, 0, 0), m => m == canvas.Mask);

            Texture2D tint = GetOrCreateMask(
                canvas, $"{DataFolder}/{sceneName}_{canvas.name}_TintMask.png", resolution,
                new Color32(128, 128, 128, 0), m => m == canvas.TintMask);

            Undo.RecordObject(canvas, "Crear máscaras de camino");
            canvas.SetMask(splat);
            canvas.SetTintMask(tint);
            EditorUtility.SetDirty(canvas);

            // Las de costados salen de una: sin ellas, pintar una pared la estira de arriba
            // a abajo, y es facil no darse cuenta de que hay que crearlas aparte.
            if (!canvas.HasSideMasks)
            {
                CreateSideMasks(canvas, Mathf.Min(4096, resolution * 2));
            }

            InvalidateBuffers();

            Debug.Log($"[PathCanvas] Máscaras creadas: splat en {AssetDatabase.GetAssetPath(splat)}, " +
                      $"tinte en {AssetDatabase.GetAssetPath(tint)}.", canvas);
        }

        /// <summary>Crea el atlas de costados (splat + tinte), con los mismos neutros que las de arriba.</summary>
        private void CreateSideMasks(PathCanvas canvas, int resolution)
        {
            if (!Directory.Exists(DataFolder))
            {
                Directory.CreateDirectory(DataFolder);
                AssetDatabase.Refresh();
            }

            Scene scene = canvas.gameObject.scene;
            string sceneName = string.IsNullOrEmpty(scene.name) ? "Untitled" : scene.name;

            Texture2D splat = GetOrCreateMask(
                canvas, $"{DataFolder}/{sceneName}_{canvas.name}_SideSplatMask.png", resolution,
                new Color32(0, 0, 0, 0), m => m == canvas.SideMask);

            Texture2D tint = GetOrCreateMask(
                canvas, $"{DataFolder}/{sceneName}_{canvas.name}_SideTintMask.png", resolution,
                new Color32(128, 128, 128, 0), m => m == canvas.SideTintMask);

            Undo.RecordObject(canvas, "Crear máscaras de costados");
            canvas.SetSideMasks(splat, tint);
            EditorUtility.SetDirty(canvas);

            // Sin esto, recien creadas ya estarian "rotas": el alto por defecto no tiene
            // por que cubrir las paredes, y con triplanar apagado se ven estiradas igual.
            PathCanvasChecks.PrepareForSides(canvas);

            InvalidateBuffers();

            Debug.Log($"[PathCanvas] Máscaras de costados creadas: splat en {AssetDatabase.GetAssetPath(splat)}, " +
                      $"tinte en {AssetDatabase.GetAssetPath(tint)}.", canvas);
        }

        private Texture2D GetOrCreateMask(PathCanvas canvas, string path, int resolution, Color32 neutral, Func<Texture2D, bool> isOwnedByThis)
        {
            if (File.Exists(path))
            {
                var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                // Huerfana (nadie la tiene asignada) = reusable. Asignada a este mismo
                // canvas = tambien reusable (recrear mascaras no debe perder la pintada).
                if (existing != null && (isOwnedByThis(existing) || !IsMaskOwnedByAnyCanvas(existing, canvas)))
                {
                    ConfigureMaskImporter(path);
                    return existing;
                }

                if (existing != null)
                {
                    path = AssetDatabase.GenerateUniqueAssetPath(path);
                }
            }

            var pixels = new Color32[resolution * resolution];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = neutral;
            }

            var scratch = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true);
            scratch.SetPixels32(pixels);
            scratch.Apply(false);

            WritePng(scratch, path);
            UnityEngine.Object.DestroyImmediate(scratch);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            ConfigureMaskImporter(path);

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void WritePng(Texture2D texture, string assetPath)
        {
            File.WriteAllBytes(assetPath, texture.EncodeToPNG());
        }

        // Distingue un archivo huerfano (reusable) de uno que es una mascara real de otra
        // zona con el mismo nombre de GameObject (NO reusable). Sin este chequeo, dos
        // canvases rotos con el mismo nombre terminan apuntando los dos al mismo archivo
        // la segunda vez que se les da a "Crear mascaras" — las dos zonas quedan
        // compartiendo pintura sin que nadie lo pida.
        private static bool IsMaskOwnedByAnyCanvas(Texture2D mask, PathCanvas self)
        {
            var canvases = UnityEngine.Object.FindObjectsByType<PathCanvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (PathCanvas other in canvases)
            {
                if (other == self)
                {
                    continue;
                }

                if (other.Mask == mask || other.TintMask == mask || other.SideMask == mask || other.SideTintMask == mask)
                {
                    return true;
                }
            }

            return false;
        }

        // RGBA32 sin mips, sin sRGB (es una mascara de datos, no color) y readable para
        // que el pincel la pueda leer por CPU.
        private static void ConfigureMaskImporter(string assetPath)
        {
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
            {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.SaveAndReimport();
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

        // -------------------------------------------------------------- material

        /// <summary>El material del primer renderer asignado, que es de donde se lee la paleta de texturas.</summary>
        private static Material GetActiveMaterial(PathCanvas canvas)
        {
            Renderer[] renderers = canvas.TargetRenderers;
            if (renderers == null)
            {
                return null;
            }

            foreach (Renderer renderer in renderers)
            {
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    return renderer.sharedMaterial;
                }
            }

            return null;
        }

        private static bool IsSlotEnabled(Material material, int slot)
        {
            if (material == null)
            {
                return slot == 1; // Sin material para consultar, se asume el minimo (Textura 1).
            }

            if (slot == 1)
            {
                return true; // Textura 1 no tiene toggle: siempre es la capa pintable minima.
            }

            string property = $"_Tex{slot}Enabled";
            return material.HasProperty(property) && material.GetFloat(property) > 0.5f;
        }

        private static string SlotDisplayName(PathCanvas canvas, int slot)
        {
            Material material = GetActiveMaterial(canvas);
            if (material == null)
            {
                return "sin material";
            }

            Texture tex = material.GetTexture($"_Tex{slot}");
            return tex != null ? tex.name : "sin asignar";
        }
    }
}
