using UnityEditor;
using UnityEngine;

namespace Gekko.PaintTools.EditorTools
{
    /// <summary>
    /// Pincel de la vista de escena, compartido por el pintor de caminos y el
    /// dispersor de props. Se encarga de lo aburrido y facil de arruinar: robarle el
    /// click a la seleccion, tirar el rayo, espaciar el trazo y dibujar el cursor.
    ///
    /// El raycast va contra COLLIDERS. Es a proposito: lo que pintan estas herramientas
    /// (mascara de camino, props dispersos) no tiene collider, asi que el pincel nunca
    /// se engancha con su propio resultado.
    /// </summary>
    public class SceneBrush
    {
        public enum Action
        {
            None,
            Paint,
            Erase,
            StrokeEnded,
        }

        public float Radius = 2.5f;
        public LayerMask Layers = ~0;
        public bool Enabled;

        /// <summary>Fraccion del radio que hay que avanzar para que se pinte de nuevo.</summary>
        public float StepFraction = 0.35f;

        private Vector3 _lastPoint;
        private bool _strokeActive;

        public Vector3 LastPoint => _lastPoint;

        /// <summary>Distancia del mundo entre dos aplicaciones del pincel.</summary>
        public float StepDistance => Radius * StepFraction;

        /// <summary>
        /// Procesa el evento actual. Devuelve que hay que hacer y donde.
        /// Llamar desde OnSceneGUI.
        /// </summary>
        public Action Update(Event e, out Vector3 point, out Vector3 normal, out bool cursorValid)
        {
            point = Vector3.zero;
            normal = Vector3.up;
            cursorValid = false;

            if (!Enabled)
            {
                return Action.None;
            }

            // Sin esto el click selecciona objetos en vez de pintar.
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlId);

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                Enabled = false;
                e.Use();
                return Action.None;
            }

            if (e.type == EventType.ScrollWheel && e.control)
            {
                Radius = Mathf.Clamp(Radius - e.delta.y * 0.1f, 0.1f, 50f);
                e.Use();
                return Action.None;
            }

            cursorValid = TryRaycast(e.mousePosition, out point, out normal);

            if (e.type == EventType.MouseUp && e.button == 0)
            {
                _strokeActive = false;
                return Action.StrokeEnded;
            }

            if (!cursorValid)
            {
                return Action.None;
            }

            bool isStroke = (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
                            && e.button == 0
                            && !e.alt;

            if (!isStroke)
            {
                return Action.None;
            }

            // Se aplica cada cierto avance del cursor y no en cada evento del mouse: si
            // no, arrastrar despacio apila decenas de aplicaciones en el mismo punto.
            float step = StepDistance;
            bool farEnough = !_strokeActive || (point - _lastPoint).sqrMagnitude > step * step;

            if (e.type == EventType.MouseDown)
            {
                _strokeActive = true;
                farEnough = true;
            }

            e.Use();

            if (!farEnough)
            {
                return Action.None;
            }

            _lastPoint = point;
            return e.shift ? Action.Erase : Action.Paint;
        }

        public bool TryRaycast(Vector2 guiPosition, out Vector3 point, out Vector3 normal)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);

            if (Physics.Raycast(ray, out RaycastHit hit, 5000f, Layers, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                normal = hit.normal;
                return true;
            }

            point = Vector3.zero;
            normal = Vector3.up;
            return false;
        }

        public void DrawCursor(Vector3 point, Vector3 normal, bool erasing)
        {
            Handles.color = erasing
                ? new Color(1f, 0.4f, 0.35f, 1f)
                : new Color(0.45f, 1f, 0.6f, 1f);

            Handles.DrawWireDisc(point, normal, Radius);
            Handles.DrawWireDisc(point, normal, Radius * 0.5f);
        }

        /// <summary>Campos comunes del pincel para el inspector.</summary>
        public void DrawCommonSettings()
        {
            Radius = EditorGUILayout.Slider("Radio", Radius, 0.1f, 50f);

            int displayed = UnityEditorInternal.InternalEditorUtility.LayerMaskToConcatenatedLayersMask(Layers);
            displayed = EditorGUILayout.MaskField("Capas pintables", displayed,
                UnityEditorInternal.InternalEditorUtility.layers);
            Layers = UnityEditorInternal.InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(displayed);
        }

        /// <summary>Boton de encendido del modo pintura, con el color de estado.</summary>
        public bool DrawToggleButton(string labelOn, string labelOff)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = Enabled ? new Color(0.5f, 1f, 0.5f) : previous;

            bool clicked = GUILayout.Button(Enabled ? labelOn : labelOff, GUILayout.Height(28f));

            GUI.backgroundColor = previous;

            if (clicked)
            {
                Enabled = !Enabled;
                SceneView.RepaintAll();
            }

            return clicked;
        }
    }
}
