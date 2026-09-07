using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Gekko.Tools.EditorTools
{
    /// <summary>
    /// Panel central de las herramientas de Gekko.
    ///
    /// La idea es que cada herramienta se explique sola: al seleccionarla se ve qué
    /// hace, para qué sirve y los pasos concretos para usarla, con los botones al lado.
    /// Asi no hay que ir a buscar el README ni acordarse de en que menu estaba cada cosa.
    /// </summary>
    public class GekkoToolsWindow : EditorWindow
    {
        private const string SetFolder = "Assets/_GekkoTools/Sets";

        private class Page
        {
            public string Title;
            public string Tag;
            public string Summary;
            public string WhatFor;
            public string[] Steps;
            public string[] Notes;
            public Action DrawExtra;
            public (string label, Action action)[] Actions;
        }

        private readonly List<Page> _pages = new List<Page>();
        private int _selected;
        private Vector2 _navScroll;
        private Vector2 _bodyScroll;

        // Estado de la seccion de assets
        private PrefabSet _set;
        private readonly PrefabSetPreview _preview = new PrefabSetPreview();
        private Vector2 _entryScroll;
        private float _previewHeight = 300f;

        [MenuItem("Tools/Gekko/Panel de herramientas %#g", priority = 0)]
        public static void Open()
        {
            var window = GetWindow<GekkoToolsWindow>("Gekko Tools");
            window.minSize = new Vector2(760f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            BuildPages();
        }

        private void OnDisable()
        {
            _preview.Cleanup();
        }

        private void OnGUI()
        {
            DrawHeader();

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawNav();
                DrawBody();
            }
        }

        private void DrawHeader()
        {
            var rect = GUILayoutUtility.GetRect(0f, 42f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.16f, 0.20f, 0.17f));

            var title = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft,
            };
            title.normal.textColor = new Color(0.72f, 0.95f, 0.65f);

            GUI.Label(new Rect(rect.x + 12f, rect.y, 320f, rect.height), "Herramientas de Gekko", title);

            var sub = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
            sub.normal.textColor = new Color(1f, 1f, 1f, 0.5f);
            GUI.Label(new Rect(rect.x, rect.y, rect.width - 12f, rect.height),
                "Ctrl+Shift+G para abrir este panel", sub);
        }

        private void DrawNav()
        {
            using (var scope = new EditorGUILayout.VerticalScope(GUILayout.Width(190f)))
            {
                EditorGUI.DrawRect(scope.rect, new Color(0.19f, 0.19f, 0.21f));
                _navScroll = EditorGUILayout.BeginScrollView(_navScroll);

                for (int i = 0; i < _pages.Count; i++)
                {
                    Page page = _pages[i];
                    bool isSelected = i == _selected;

                    var style = new GUIStyle(EditorStyles.label)
                    {
                        padding = new RectOffset(10, 6, 7, 7),
                        fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal,
                    };

                    Rect row = GUILayoutUtility.GetRect(new GUIContent(page.Title), style, GUILayout.ExpandWidth(true));
                    if (isSelected)
                    {
                        EditorGUI.DrawRect(row, new Color(0.27f, 0.36f, 0.29f));
                    }

                    if (GUI.Button(row, page.Title, style))
                    {
                        _selected = i;
                        GUI.FocusControl(null);
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawBody()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                _bodyScroll = EditorGUILayout.BeginScrollView(_bodyScroll);

                Page page = _pages[_selected];

                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField(page.Title, new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 });
                if (!string.IsNullOrEmpty(page.Tag))
                {
                    var tag = new GUIStyle(EditorStyles.miniLabel);
                    tag.normal.textColor = new Color(0.6f, 0.8f, 1f);
                    EditorGUILayout.LabelField(page.Tag, tag);
                }

                EditorGUILayout.Space(4f);
                Paragraph("Qué hace", page.Summary);
                Paragraph("Para qué sirve", page.WhatFor);

                if (page.Steps != null && page.Steps.Length > 0)
                {
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField("Cómo se usa", EditorStyles.boldLabel);
                    for (int i = 0; i < page.Steps.Length; i++)
                    {
                        EditorGUILayout.LabelField($"{i + 1}.  {page.Steps[i]}", WrappedLabel());
                    }
                }

                if (page.Notes != null)
                {
                    EditorGUILayout.Space(4f);
                    foreach (string note in page.Notes)
                    {
                        EditorGUILayout.HelpBox(note, MessageType.Info);
                    }
                }

                if (page.Actions != null && page.Actions.Length > 0)
                {
                    EditorGUILayout.Space(6f);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        foreach ((string label, Action action) in page.Actions)
                        {
                            if (GUILayout.Button(label, GUILayout.Height(26f)))
                            {
                                action();
                            }
                        }
                    }
                }

                page.DrawExtra?.Invoke();

                EditorGUILayout.Space(10f);
                EditorGUILayout.EndScrollView();
            }
        }

        private static GUIStyle WrappedLabel()
        {
            return new GUIStyle(EditorStyles.label) { wordWrap = true };
        }

        private static void Paragraph(string header, string body)
        {
            if (string.IsNullOrEmpty(body))
            {
                return;
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(header, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(body, WrappedLabel());
        }

        // ==================================================================== assets

        private void DrawAssetsSection()
        {
            EditorGUILayout.Space(8f);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                _set = (PrefabSet)EditorGUILayout.ObjectField("Conjunto", _set, typeof(PrefabSet), false);
                if (EditorGUI.EndChangeCheck())
                {
                    _preview.IsolatedIndex = -1;
                    _preview.Invalidate();
                }

                if (GUILayout.Button("Nuevo", GUILayout.Width(60f)))
                {
                    CreateSet();
                }
            }

            if (_set == null)
            {
                EditorGUILayout.HelpBox(
                    "Elegí un conjunto o creá uno nuevo. Un conjunto es una paleta de prefabs reutilizable: " +
                    "la misma se puede usar en varias zonas y escenas, y se edita en un solo lugar.",
                    MessageType.Info);
                return;
            }

            // ---- Preview
            Rect previewRect = GUILayoutUtility.GetRect(0f, _previewHeight, GUILayout.ExpandWidth(true));
            _preview.Draw(previewRect, _set);

            // Manija para estirar la altura de la preview
            Rect handle = new Rect(previewRect.x, previewRect.yMax - 3f, previewRect.width, 6f);
            EditorGUIUtility.AddCursorRect(handle, MouseCursor.ResizeVertical);
            if (Event.current.type == EventType.MouseDrag && handle.Contains(Event.current.mousePosition))
            {
                _previewHeight = Mathf.Clamp(_previewHeight + Event.current.delta.y, 140f, 700f);
                Repaint();
            }

            // ---- Controles de la preview
            using (new EditorGUILayout.HorizontalScope())
            {
                _preview.LayoutMode = (PrefabSetPreview.Layout)EditorGUILayout.EnumPopup(
                    _preview.LayoutMode, GUILayout.Width(90f));

                EditorGUILayout.LabelField("Separación", GUILayout.Width(72f));
                _preview.Separation = EditorGUILayout.Slider(_preview.Separation, 0.5f, 6f);

                if (GUILayout.Button("Encuadrar", GUILayout.Width(80f)))
                {
                    _preview.ResetView();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                bool showAll = _preview.IsolatedIndex < 0;
                if (GUILayout.Toggle(showAll, "Ver todos", EditorStyles.miniButtonLeft) && !showAll)
                {
                    _preview.IsolatedIndex = -1;
                }

                GUILayout.Label(showAll
                    ? "  Tocá 'Aislar' en una fila para inspeccionarla sola."
                    : $"  Aislado: {_preview.NameOf(_preview.IsolatedIndex)}", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(6f);
            DrawDropArea();
            EditorGUILayout.Space(4f);
            DrawEntryList();
        }

        private void DrawDropArea()
        {
            Rect drop = GUILayoutUtility.GetRect(0f, 38f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(drop, new Color(0.22f, 0.24f, 0.22f));

            var style = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
            GUI.Label(drop, "Arrastrá prefabs acá para sumarlos al conjunto", style);

            Event e = Event.current;
            if ((e.type == EventType.DragUpdated || e.type == EventType.DragPerform) && drop.Contains(e.mousePosition))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (e.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    Undo.RecordObject(_set, "Agregar prefabs al conjunto");

                    int added = 0;
                    foreach (UnityEngine.Object dragged in DragAndDrop.objectReferences)
                    {
                        if (dragged is GameObject prefab && PrefabUtility.IsPartOfPrefabAsset(prefab))
                        {
                            _set.Entries.Add(new PrefabSetEntry { Prefab = prefab });
                            added++;
                        }
                    }

                    if (added > 0)
                    {
                        EditorUtility.SetDirty(_set);
                        _preview.Invalidate();
                    }

                    e.Use();
                }
            }
        }

        private void DrawEntryList()
        {
            EditorGUILayout.LabelField($"Prefabs del conjunto ({_set.EnabledCount} activos de {_set.Count})",
                EditorStyles.boldLabel);

            _entryScroll = EditorGUILayout.BeginScrollView(_entryScroll, GUILayout.MinHeight(120f));

            int removeAt = -1;
            int visibleIndex = 0;

            for (int i = 0; i < _set.Entries.Count; i++)
            {
                PrefabSetEntry entry = _set.Entries[i];
                if (entry == null)
                {
                    continue;
                }

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUI.BeginChangeCheck();
                    entry.Enabled = EditorGUILayout.Toggle(entry.Enabled, GUILayout.Width(16f));

                    Rect thumb = GUILayoutUtility.GetRect(42f, 42f, GUILayout.Width(42f));
                    Texture preview = entry.Prefab != null ? AssetPreview.GetAssetPreview(entry.Prefab) : null;
                    if (preview != null)
                    {
                        GUI.DrawTexture(thumb, preview, ScaleMode.ScaleToFit);
                    }
                    else
                    {
                        EditorGUI.DrawRect(thumb, new Color(0.25f, 0.25f, 0.27f));
                    }

                    using (new EditorGUILayout.VerticalScope())
                    {
                        entry.Prefab = (GameObject)EditorGUILayout.ObjectField(entry.Prefab, typeof(GameObject), false);

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField("Peso", GUILayout.Width(34f));
                            entry.Weight = EditorGUILayout.FloatField(entry.Weight, GUILayout.Width(42f));
                            EditorGUILayout.LabelField("Escala", GUILayout.Width(44f));
                            entry.ScaleMultiplier = EditorGUILayout.FloatField(entry.ScaleMultiplier, GUILayout.Width(42f));

                            // El indice de la preview solo cuenta las entradas activas
                            // con malla, por eso no coincide con el de la lista.
                            bool canIsolate = entry.Enabled && entry.Prefab != null && visibleIndex < _preview.ItemCount;
                            using (new EditorGUI.DisabledScope(!canIsolate))
                            {
                                bool isolated = canIsolate && _preview.IsolatedIndex == visibleIndex;
                                if (GUILayout.Toggle(isolated, "Aislar", EditorStyles.miniButton, GUILayout.Width(52f)) != isolated)
                                {
                                    _preview.IsolatedIndex = isolated ? -1 : visibleIndex;
                                }
                            }

                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("Quitar", EditorStyles.miniButton, GUILayout.Width(52f)))
                            {
                                removeAt = i;
                            }
                        }
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_set, "Editar conjunto");
                        EditorUtility.SetDirty(_set);
                        _preview.Invalidate();
                    }
                }

                if (entry.Enabled && entry.Prefab != null)
                {
                    visibleIndex++;
                }
            }

            EditorGUILayout.EndScrollView();

            if (removeAt >= 0)
            {
                Undo.RecordObject(_set, "Quitar del conjunto");
                _set.Entries.RemoveAt(removeAt);
                EditorUtility.SetDirty(_set);
                _preview.IsolatedIndex = -1;
                _preview.Invalidate();
            }
        }

        private void CreateSet()
        {
            if (!Directory.Exists(SetFolder))
            {
                Directory.CreateDirectory(SetFolder);
                AssetDatabase.Refresh();
            }

            string path = AssetDatabase.GenerateUniqueAssetPath($"{SetFolder}/PrefabSet.asset");
            var set = CreateInstance<PrefabSet>();
            AssetDatabase.CreateAsset(set, path);
            AssetDatabase.SaveAssets();

            _set = set;
            _preview.IsolatedIndex = -1;
            _preview.Invalidate();
            EditorGUIUtility.PingObject(set);
        }

        // ===================================================================== paginas

        private void BuildPages()
        {
            _pages.Clear();

            _pages.Add(new Page
            {
                Title = "Assets · Conjuntos",
                Tag = "Paleta de prefabs con preview 3D",
                Summary =
                    "Arma conjuntos de prefabs reutilizables y los muestra en una preview 3D que podés " +
                    "orbitar, separar y aislar pieza por pieza.",
                WhatFor =
                    "Para decidir qué prefabs van juntos antes de pintarlos en el nivel, y para revisar " +
                    "cada pieza sin tener que instanciarla en la escena. La preview corre en una escena " +
                    "aislada, así que nada de lo que hagas acá toca los prefabs originales ni la escena abierta.",
                Steps = new[]
                {
                    "Creá un conjunto con 'Nuevo', o elegí uno existente.",
                    "Arrastrá prefabs a la zona de drop.",
                    "Usá 'Separación' para abrirlos y el desplegable para cambiar la distribución (fila, grilla o círculo).",
                    "Tocá 'Aislar' en una fila para verla sola en la preview.",
                    "Destildá los que no quieras usar: quedan en el conjunto pero no se pintan ni se previsualizan.",
                    "'Peso' controla qué tan seguido sale cada uno al pintar al azar; 'Escala' es un multiplicador del conjunto, no del prefab.",
                },
                Notes = new[]
                {
                    "La separación es un multiplicador del tamaño real de las piezas, no una distancia fija: " +
                    "se comporta igual con un helecho de 20 cm que con un árbol de 8 m.",
                },
                DrawExtra = DrawAssetsSection,
            });

            _pages.Add(new Page
            {
                Title = "Caminos",
                Tag = "Pintura de suelo por máscara proyectada",
                Summary =
                    "Pinta caminos sobre el piso mezclando dos materiales con una máscara proyectada desde arriba.",
                WhatFor =
                    "Para marcar senderos, claros y zonas de tierra sin depender de la cantidad de vértices " +
                    "de la malla ni de cómo estén sus UVs. Probado sobre un Plane de 121 vértices y un Quad " +
                    "de 4: el resultado es idéntico.",
                Steps = new[]
                {
                    "Creá el material de piso y asignale tus texturas de base y de camino.",
                    "Poné ese material en los renderers del piso.",
                    "Creá una zona de camino, ubicala sobre el área y ajustale el tamaño.",
                    "Arrastrá los renderers del piso a 'Target Renderers' de la zona. SIN ESTO NO SE VE NADA.",
                    "Creá la máscara y activá el modo pintura.",
                    "Click y arrastrar pinta, shift borra, ctrl+rueda cambia el radio.",
                },
                Notes = new[]
                {
                    "La máscara es plana en XZ: dos pisos apilados en la misma vertical comparten máscara. " +
                    "Para eso se usa una zona por piso.",
                    "El pincel raycastea contra colliders, así que la superficie necesita uno.",
                },
                Actions = new (string, Action)[]
                {
                    ("Crear material de piso", () => EditorApplication.ExecuteMenuItem("Tools/Gekko/Paint Tools/Crear material de piso con camino")),
                    ("Crear zona de camino", () => EditorApplication.ExecuteMenuItem("Tools/Gekko/Paint Tools/Crear zona de camino")),
                },
            });

            _pages.Add(new Page
            {
                Title = "Props dispersos",
                Tag = "Scatter sin GameObjects",
                Summary =
                    "Pincel para desparramar vegetación y decoración sobre el piso, guardando cada instancia " +
                    "como datos y dibujándolas con GPU instancing.",
                WhatFor =
                    "Para poblar rápido sin engordar la escena. Polybrush instancia GameObjects reales y cada " +
                    "arbusto se serializa entero en el .unity; acá cada prop son 40 bytes en un asset aparte.",
                Steps = new[]
                {
                    "Creá un campo de props en la escena.",
                    "Activá el modo pintura: se crea solo el asset de datos.",
                    "Cargale los prefabs en la lista 'Prototypes' de ese asset.",
                    "Ajustá densidad, separación mínima y rango de escala, y pintá.",
                    "Para los que necesiten collider, usá 'Materializar' — solo para esos.",
                },
                Notes = new[]
                {
                    "Los materiales necesitan GPU Instancing activado o cada prop es su propio draw call. " +
                    "El inspector del campo los detecta y los arregla con un botón.",
                    "Todavía no está probado de punta a punta.",
                },
                Actions = new (string, Action)[]
                {
                    ("Crear campo de props", () => EditorApplication.ExecuteMenuItem("Tools/Gekko/Paint Tools/Crear campo de props")),
                },
            });

            _pages.Add(new Page
            {
                Title = "Pasto",
                Tag = "Pincel de césped interactivo",
                Summary =
                    "Pinta césped que se mueve con el viento y se aplasta cuando lo pisan los personajes.",
                WhatFor =
                    "Para vestir el suelo con césped denso sin matar el rendimiento: las briznas se hornean " +
                    "en mallas por chunk, así el frustum culling descarta lo que no se ve.",
                Steps = new[]
                {
                    "Creá el material de pasto y el campo de pasto en la escena.",
                    "Asignale el material al campo.",
                    "Activá el modo pintura y pintá sobre el piso.",
                    "Agregale el interactor al gecko y a los NPCs para que aplasten el pasto.",
                },
                Notes = new[]
                {
                    "El tope de interactores es 8 y es fijo: el vertex shader recorre esa lista por vértice.",
                    "Las briznas se guardan en un asset aparte para no engordar la escena.",
                },
                Actions = new (string, Action)[]
                {
                    ("Crear material de pasto", () => EditorApplication.ExecuteMenuItem("Tools/Gekko/Grass/Crear material de pasto")),
                    ("Crear campo de pasto", () => EditorApplication.ExecuteMenuItem("Tools/Gekko/Grass/Crear campo de pasto en la escena")),
                    ("Interactor a la selección", () => EditorApplication.ExecuteMenuItem("Tools/Gekko/Grass/Agregar interactor a la seleccion")),
                },
            });

            _pages.Add(new Page
            {
                Title = "Nubes",
                Tag = "Mar de nubes con deriva infinita",
                Summary =
                    "Genera mallas de nube y las reparte en un mar que deriva en un sentido sin desaparecer nunca.",
                WhatFor =
                    "Para el fondo de las zonas altas. Las nubes dan la vuelta por wrap toroidal, así que el " +
                    "movimiento es infinito y el costo constante.",
                Steps = new[]
                {
                    "Generá un set de mallas con el Cloud Mesh Builder.",
                    "Creá el material de nube.",
                    "Creá el campo de nubes en la escena y asignale mallas y material.",
                    "Elegí el modo: 'Por Cantidad' fija el número exacto de nubes, 'Por Separación' fija la distancia.",
                    "Apretá Rebuild y dale Play para ver la deriva.",
                },
                Notes = new[]
                {
                    "Para que se lea como mar continuo, la separación tiene que ser menor que el diámetro " +
                    "promedio de la nube. El inspector avisa si te pasás.",
                },
                Actions = new (string, Action)[]
                {
                    ("Cloud Mesh Builder", () => EditorApplication.ExecuteMenuItem("Tools/Gekko/Clouds/Cloud Mesh Builder")),
                    ("Crear material de nube", () => EditorApplication.ExecuteMenuItem("Tools/Gekko/Clouds/Crear material de nube")),
                    ("Crear campo de nubes", () => EditorApplication.ExecuteMenuItem("Tools/Gekko/Clouds/Crear campo de nubes en la escena")),
                },
            });
        }
    }
}
