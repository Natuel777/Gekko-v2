using System;
using System.Collections.Generic;
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

        // Buffer compartido para el raycast filtrado: RaycastAll alocaria un array por evento.
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[256];

        public float Radius = 2.5f;
        public LayerMask Layers = ~0;
        public bool Enabled;

        /// <summary>Fraccion del radio que hay que avanzar para que se pinte de nuevo.</summary>
        public float StepFraction = 0.35f;

        /// <summary>
        /// Si se prende, cuando el cursor salta lejos entre dos eventos (movimiento rapido
        /// del mouse) el pincel rellena el tramo con aplicaciones intermedias, en
        /// <see cref="StrokePoints"/>. Sin esto, arrastrar rapido deja un trazo punteado.
        /// Apagado por defecto: el dispersor de props depende de una aplicacion por evento.
        /// </summary>
        public bool Interpolate;

        /// <summary>
        /// Si esta, un golpe de rayo solo cuenta cuando pasa este filtro. Permite que el pincel
        /// atraviese arboles, rocas y demas colliders que no son el piso que se esta pintando.
        /// </summary>
        public Func<RaycastHit, bool> HitFilter;

        /// <summary>Puntos a aplicar para la accion devuelta por Update (solo con Interpolate).</summary>
        public readonly List<Vector3> StrokePoints = new List<Vector3>(64);

        private Vector3 _lastPoint;
        private bool _hasLastPoint;

        // El raycast solo se repite si el mouse se movio: Layout/Repaint llegan varias
        // veces por frame con el cursor quieto y cada rayo contra la escena cuesta.
        private Vector2 _lastMouse = new Vector2(float.NaN, float.NaN);
        private bool _cachedValid;
        private Vector3 _cachedPoint;
        private Vector3 _cachedNormal = Vector3.up;

        public Vector3 LastPoint => _lastPoint;

        /// <summary>Distancia del mundo entre dos aplicaciones del pincel.</summary>
        public float StepDistance => Radius * StepFraction;

        /// <summary>
        /// Procesa el evento actual. Devuelve que hay que hacer y donde.
        /// Llamar desde OnSceneGUI.
        /// </summary>
        public Action Update(Event e, out Vector3 point, out Vector3 normal, out bool cursorValid)
        {
            StrokePoints.Clear();
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
                // Proporcional: un paso fijo era lentisimo con radios grandes y brusco con chicos.
                Radius = Mathf.Clamp(Radius * (1f - e.delta.y * 0.05f), 0.1f, 50f);
                e.Use();
                return Action.None;
            }

            bool mouseChanged = e.mousePosition != _lastMouse
                                || e.type == EventType.MouseDown
                                || e.type == EventType.MouseUp
                                || e.type == EventType.ScrollWheel;
            if (mouseChanged)
            {
                _cachedValid = TryRaycast(e.mousePosition, out _cachedPoint, out _cachedNormal);
                _lastMouse = e.mousePosition;
            }

            point = _cachedPoint;
            normal = _cachedNormal;
            cursorValid = _cachedValid;

            if (e.type == EventType.MouseUp && e.button == 0)
            {
                _hasLastPoint = false;
                return Action.StrokeEnded;
            }

            if (!cursorValid)
            {
                // Si el cursor sale del piso a mitad de trazo, no unir con una linea el
                // ultimo punto valido con el proximo: se corta el trazo.
                if (e.type == EventType.MouseDrag)
                {
                    _hasLastPoint = false;
                }
                return Action.None;
            }

            bool isStroke = (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
                            && e.button == 0
                            && !e.alt;

            if (!isStroke)
            {
                return Action.None;
            }

            if (e.type == EventType.MouseDown)
            {
                _hasLastPoint = false;
            }

            // Se aplica cada cierto avance del cursor y no en cada evento del mouse: si
            // no, arrastrar despacio apila decenas de aplicaciones en el mismo punto.
            float step = StepDistance;
            bool farEnough = !_hasLastPoint || (point - _lastPoint).sqrMagnitude > step * step;

            e.Use();

            if (!farEnough)
            {
                return Action.None;
            }

            if (Interpolate && _hasLastPoint)
            {
                AddInterpolated(_lastPoint, point, step);
            }
            else
            {
                StrokePoints.Add(point);
            }

            _lastPoint = point;
            _hasLastPoint = true;
            return e.shift ? Action.Erase : Action.Paint;
        }

        private void AddInterpolated(Vector3 from, Vector3 to, float step)
        {
            float distance = Vector3.Distance(from, to);
            // Tope: un salto enorme (mouse que cruza la pantalla) no debe generar miles de aplicaciones.
            int count = Mathf.Clamp(Mathf.CeilToInt(distance / Mathf.Max(step, 1e-4f)), 1, 128);

            for (int i = 1; i <= count; i++)
            {
                StrokePoints.Add(Vector3.Lerp(from, to, i / (float)count));
            }
        }

        public bool TryRaycast(Vector2 guiPosition, out Vector3 point, out Vector3 normal)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);

            if (HitFilter == null)
            {
                if (Physics.Raycast(ray, out RaycastHit hit, 5000f, Layers, QueryTriggerInteraction.Ignore))
                {
                    point = hit.point;
                    normal = hit.normal;
                    return true;
                }
            }
            else
            {
                // Con filtro se toma el golpe MAS CERCANO que lo pase, no el primero: un
                // arbol entre la camara y el piso no tiene que bloquear la pintura.
                int count = Physics.RaycastNonAlloc(ray, HitBuffer, 5000f, Layers, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue;
                int bestIndex = -1;

                for (int i = 0; i < count; i++)
                {
                    if (HitBuffer[i].distance < best && HitFilter(HitBuffer[i]))
                    {
                        best = HitBuffer[i].distance;
                        bestIndex = i;
                    }
                }

                if (bestIndex >= 0)
                {
                    point = HitBuffer[bestIndex].point;
                    normal = HitBuffer[bestIndex].normal;
                    return true;
                }
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
                _lastMouse = new Vector2(float.NaN, float.NaN);
                SceneView.RepaintAll();
            }

            return clicked;
        }
    }
}
