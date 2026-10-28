using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Gekko.Purification.EditorTools
{
    /// <summary>
    /// Ventana de debug de la purificación (Tools > Gekko > Purificación):
    ///  - Lista de regiones con un slider de progreso para previsualizar la onda en Edit Mode, sin dar Play.
    ///  - Purificar todo / Resetear todo (solo para debug).
    ///  - Cuántos reactivos y disparadores tiene cada región.
    ///  - Validador de la escena.
    /// La vista previa en Edit Mode no se guarda en la escena: al cerrar la ventana todo vuelve a verse sano.
    /// </summary>
    public class PurifyDebugWindow : EditorWindow
    {
        private PurifyShaderGlobals _globales;
        private readonly Dictionary<PurifyRegion, float> _preview = new();
        private List<PurifyValidator.Problema> _problemas;
        private bool _vistaPrevia;
        private Vector2 _scroll;

        [MenuItem("Tools/Gekko/Purificación")]
        public static void Abrir() => GetWindow<PurifyDebugWindow>("Purificación");

        private void OnEnable()
        {
            _globales = new PurifyShaderGlobals(null);
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            ApagarVistaPrevia();
        }

        // Al entrar a Play la vista previa se apaga: desde ahí manda el PurifyManager.
        private void OnPlayModeChanged(PlayModeStateChange cambio)
        {
            if(cambio == PlayModeStateChange.ExitingEditMode) ApagarVistaPrevia();
        }

        // En Play se redibuja seguido para ver el progreso en vivo.
        private void OnInspectorUpdate()
        {
            if(Application.isPlaying) Repaint();
        }

        private void OnGUI()
        {
            PurifyRegion[] regiones = Object.FindObjectsByType<PurifyRegion>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                            .OrderBy(r => r.RegionId).ToArray();
            PurifyReactive[] reactivos = Object.FindObjectsByType<PurifyReactive>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            PurifyTrigger[] triggers = Object.FindObjectsByType<PurifyTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField(Application.isPlaying ? "Play Mode: estado real de las regiones" : "Edit Mode: vista previa", EditorStyles.boldLabel);

            if(!Application.isPlaying)
            {
                bool nueva = EditorGUILayout.ToggleLeft(new GUIContent("Vista previa en la escena",
                    "Muestra la corrupción y la onda en la vista de escena sin dar Play. No se guarda nada."), _vistaPrevia);
                if(nueva != _vistaPrevia)
                {
                    _vistaPrevia = nueva;
                    if(_vistaPrevia) SubirVistaPrevia(regiones); else ApagarVistaPrevia();
                }
            }

            EditorGUILayout.Space();

            if(regiones.Length == 0) EditorGUILayout.HelpBox("No hay PurifyRegion en la escena abierta.", MessageType.Info);

            foreach(PurifyRegion region in regiones)
                DibujarRegion(region, regiones, reactivos, triggers);

            EditorGUILayout.Space();
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Purificar todo")) foreach(var r in regiones) Fijar(r, 1f, regiones, animar: true);
                if(GUILayout.Button("Resetear todo")) foreach(var r in regiones) Fijar(r, 0f, regiones, animar: false);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Validador", EditorStyles.boldLabel);
            if(GUILayout.Button("Validar escena")) _problemas = PurifyValidator.Validar();

            if(_problemas != null)
                foreach(var p in _problemas)
                {
                    MessageType tipo = p.gravedad == PurifyValidator.Gravedad.Error ? MessageType.Error
                                     : p.gravedad == PurifyValidator.Gravedad.Aviso ? MessageType.Warning : MessageType.Info;
                    using(new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.HelpBox(p.mensaje, tipo);
                        if(p.objeto != null && GUILayout.Button("Ver", GUILayout.Width(40), GUILayout.Height(38)))
                        {
                            Selection.activeObject = p.objeto;
                            EditorGUIUtility.PingObject(p.objeto);
                        }
                    }
                }

            EditorGUILayout.EndScrollView();
        }

        private void DibujarRegion(PurifyRegion region, PurifyRegion[] regiones, PurifyReactive[] reactivos, PurifyTrigger[] triggers)
        {
            int cantReactivos = reactivos.Count(re => (re.RegionOverride != null ? re.RegionOverride : PurifyRegion.Buscar(re.transform.position, regiones)) == region);
            int cantTriggers = triggers.Count(t => t.Region == region);

            using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using(new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"{region.NombreVisible}  ({region.RegionId})", EditorStyles.boldLabel);
                    if(GUILayout.Button("Seleccionar", GUILayout.Width(80))) { Selection.activeObject = region; SceneView.lastActiveSceneView?.FrameSelected(); }
                }

                EditorGUILayout.LabelField($"Nodos: {region.CantidadNodos}   Reactivos: {cantReactivos}   Disparadores: {cantTriggers}", EditorStyles.miniLabel);

                if(Application.isPlaying)
                {
                    string estado = region.Purificada ? "purificada" : region.Animando ? "purificando" : "corrupta";
                    Rect r = EditorGUILayout.GetControlRect();
                    EditorGUI.ProgressBar(r, region.Progreso, $"{estado}  {region.Progreso:P0}");
                    using(new EditorGUILayout.HorizontalScope())
                    {
                        if(GUILayout.Button("Purificar")) region.Purify();
                        if(GUILayout.Button("Resetear")) region.ResetToCorrupted();
                    }
                }
                else
                {
                    _preview.TryGetValue(region, out float valor);
                    using(new EditorGUI.DisabledScope(!_vistaPrevia))
                    {
                        float nuevo = EditorGUILayout.Slider("Progreso (vista previa)", valor, 0f, 1f);
                        if(!Mathf.Approximately(nuevo, valor)) Fijar(region, nuevo, regiones, animar: false);
                    }
                }
            }
        }

        private void Fijar(PurifyRegion region, float valor, PurifyRegion[] regiones, bool animar)
        {
            if(Application.isPlaying)
            {
                if(valor >= 1f) { if(animar) region.Purify(); else region.SetPurifiedInstant(); }
                else region.ResetToCorrupted();
                return;
            }

            _preview[region] = valor;
            if(_vistaPrevia) SubirVistaPrevia(regiones);
        }

        private void SubirVistaPrevia(PurifyRegion[] regiones)
        {
            // Mismos parámetros de borde que el manager de la escena, si hay uno.
            PurifyManager manager = Object.FindFirstObjectByType<PurifyManager>(FindObjectsInactive.Include);
            float ancho = 1.5f, fundido = 3f;
            if(manager != null)
            {
                var so = new SerializedObject(manager);
                ancho = so.FindProperty("_anchoBorde").floatValue;
                fundido = so.FindProperty("_fundidoArea").floatValue;
            }

            foreach(PurifyRegion r in regiones)
                r.PrevisualizarProgreso(_preview.TryGetValue(r, out float v) ? v : 0f);

            _globales.SubirParametros(ancho, fundido);
            _globales.Subir(regiones);
            SceneView.RepaintAll();
        }

        private void ApagarVistaPrevia()
        {
            if(Application.isPlaying || _globales == null) return;

            _vistaPrevia = false;
            foreach(PurifyRegion r in Object.FindObjectsByType<PurifyRegion>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                r.PrevisualizarProgreso(0f);

            _globales.Limpiar();
            SceneView.RepaintAll();
        }
    }
}
