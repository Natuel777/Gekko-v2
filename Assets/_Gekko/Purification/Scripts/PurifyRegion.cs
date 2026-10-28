using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Gekko.Purification
{
    /// <summary>
    /// Una parte de la zona que se purifica sola. Define un área corrupta (uno o más nodos esféricos) y, al llamar
    /// <see cref="Purify"/>, anima una onda desde el centro de cada nodo que la limpia. No toca nada fuera de sus
    /// nodos: purificar esta región no cambia otras.
    ///
    /// No sube nada al shader por su cuenta: avisa que cambió y <see cref="PurifyManager"/> junta todas las regiones
    /// y sube los globales una vez por frame.
    /// </summary>
    public class PurifyRegion : MonoBehaviour
    {
        [Serializable]
        public class Nodo
        {
            [Tooltip("Centro de la onda y del área. Si se deja vacío usa la posición de esta región.")]
            public Transform centro;

            [Tooltip("Radio del área de este nodo, en metros. Adentro arranca corrupto y hasta acá avanza la onda. Ajustarlo con el gizmo para que no invada la región vecina.")]
            [Min(0.1f)]
            public float radioMaximo = 10f;
        }

        // Regiones habilitadas. PurifyManager las lee de acá, así no depende del orden de Awake/OnEnable.
        private static readonly List<PurifyRegion> _activas = new();
        public static IReadOnlyList<PurifyRegion> Activas => _activas;

        [Header("Identidad")]
        [Tooltip("Identificador único de la región (sin espacios). Se usa para guardar y para el validador.")]
        [SerializeField] private string _regionId = "region";
        [Tooltip("Nombre para mostrar en los gizmos y en las herramientas.")]
        [SerializeField] private string _nombreVisible = "Región";

        [Header("Área")]
        [Tooltip("Uno o más nodos esféricos. Una región con forma irregular usa varios para cubrir su área sin invadir la de al lado.")]
        [SerializeField] private List<Nodo> _nodos = new() { new Nodo() };

        [Header("Onda")]
        [Tooltip("Segundos que tarda la onda en cubrir toda la región.")]
        [Min(0.01f)]
        [SerializeField] private float _duracion = 3f;
        [Tooltip("Avance de la onda en el tiempo (0..1 en los dos ejes). Por defecto arranca lenta y acelera.")]
        [SerializeField] private AnimationCurve _curva = new(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2f, 0f));
        [Tooltip("Segundos de espera entre la llamada a Purify() y el arranque de la onda.")]
        [Min(0f)]
        [SerializeField] private float _retrasoInicio;

        [Header("Gizmos")]
        [Tooltip("Color de esta región en la vista de escena.")]
        [SerializeField] private Color _colorGizmo = new(0.6f, 0.3f, 0.9f, 1f);

        [Header("Eventos")]
        [Tooltip("Arranca la onda (después del retraso).")]
        public UnityEvent OnPurifyStarted = new();
        [Tooltip("Cada frame mientras avanza la onda, con el progreso de 0 a 1.")]
        public UnityEvent<float> OnProgress = new();
        [Tooltip("La onda terminó: la región quedó purificada.")]
        public UnityEvent OnPurifyCompleted = new();

        private Coroutine _rutina;
        private bool _animando;
        private bool _cambio = true;

        public string RegionId => _regionId;
        public string NombreVisible => _nombreVisible;
        public bool Purificada { get; private set; }
        /// <summary>Avance en el tiempo, de 0 a 1 (lineal). El radio de la onda sale de pasarlo por la curva.</summary>
        public float Progreso { get; private set; }
        public bool Animando => _animando;
        public int CantidadNodos => _nodos.Count;

        private void OnEnable()
        {
            if(!_activas.Contains(this)) _activas.Add(this);
            _cambio = true;

            // Si se desactivó a mitad de la onda Unity cortó la corrutina: se retoma desde donde quedó.
            if(_animando) _rutina = StartCoroutine(RutinaOnda(false));
        }

        private void OnDisable()
        {
            _activas.Remove(this);

            if(_rutina == null) return;

            StopCoroutine(_rutina);
            _rutina = null;
        }

        private void OnValidate()
        {
            if(_regionId != null) _regionId = _regionId.Trim();
            _cambio = true;
        }

        #region API
        /// <summary>Purifica la región con la onda. Si ya está purificada o animando, no hace nada.</summary>
        public void Purify()
        {
            if(Purificada || _animando) return;

            // Desactivada no puede correr la corrutina: queda purificada directo.
            if(!isActiveAndEnabled)
            {
                SetPurifiedInstant();
                return;
            }

            _animando = true;
            _rutina = StartCoroutine(RutinaOnda(true));
        }

        /// <summary>Deja la región purificada sin animación ni eventos (para cargar partidas o saltear).</summary>
        public void SetPurifiedInstant()
        {
            DetenerRutina();
            Progreso = 1f;
            Purificada = true;
            _cambio = true;
        }

        /// <summary>Vuelve a corrupta, sin animación. Solo para pruebas.</summary>
        public void ResetToCorrupted()
        {
            DetenerRutina();
            Progreso = 0f;
            Purificada = false;
            _cambio = true;
        }

        /// <summary>Si la posición cae dentro del área de algún nodo. Sirve para asignar objetos a regiones.</summary>
        public bool Contiene(Vector3 pos)
        {
            for(int i = 0; i < _nodos.Count; i++)
            {
                Nodo nodo = _nodos[i];
                if(nodo == null) continue;

                float r = nodo.radioMaximo;
                if((CentroDe(nodo) - pos).sqrMagnitude <= r * r) return true;
            }

            return false;
        }

        /// <summary>
        /// Región habilitada que contiene la posición. Si caen varias, la que tiene el nodo más cercano en proporción
        /// a su radio. Null si la posición no está en ninguna región (zona sana).
        /// </summary>
        public static PurifyRegion Buscar(Vector3 pos) => Buscar(pos, _activas);

        /// <summary>Igual que <see cref="Buscar(Vector3)"/> pero entre las regiones dadas (las herramientas del
        /// Editor lo usan en Edit Mode, donde las regiones no se registran solas).</summary>
        public static PurifyRegion Buscar(Vector3 pos, IEnumerable<PurifyRegion> regiones)
        {
            PurifyRegion mejor = null;
            float mejorProporcion = float.MaxValue;

            foreach(PurifyRegion region in regiones)
            {
                if(region == null) continue;

                foreach(Nodo nodo in region._nodos)
                {
                    if(nodo == null || nodo.radioMaximo <= 0f) continue;

                    float proporcion = Vector3.Distance(region.CentroDe(nodo), pos) / nodo.radioMaximo;
                    if(proporcion > 1f || proporcion >= mejorProporcion) continue;

                    mejor = region;
                    mejorProporcion = proporcion;
                }
            }

            return mejor;
        }

        /// <summary>Si la onda de algún nodo ya llegó a la posición (y la posición está dentro de ese nodo).</summary>
        public bool OndaAlcanzo(Vector3 pos)
        {
            if(Purificada) return Contiene(pos);
            if(Progreso <= 0f) return false;

            for(int i = 0; i < _nodos.Count; i++)
            {
                Nodo nodo = _nodos[i];
                if(nodo == null) continue;

                float dist = Vector3.Distance(CentroDe(nodo), pos);
                if(dist <= nodo.radioMaximo && dist <= RadioActual(nodo)) return true;
            }

            return false;
        }
        #endregion

        #region Para PurifyManager
        /// <summary>Devuelve true si algo cambió desde la última vez que se preguntó, y limpia el aviso.</summary>
        internal bool ConsumirCambio()
        {
            bool cambio = _cambio;
            _cambio = false;
            return cambio;
        }

        /// <summary>Solo para herramientas del Editor: muestra la región con ese progreso sin animar ni disparar
        /// eventos. No se guarda en la escena.</summary>
        public void PrevisualizarProgreso(float progreso)
        {
            DetenerRutina();
            Progreso = Mathf.Clamp01(progreso);
            Purificada = Progreso >= 1f;
            _cambio = true;
        }

        public void ObtenerNodo(int indice, out Vector3 centro, out float radioActual, out float radioMaximo)
        {
            Nodo nodo = _nodos[indice];

            if(nodo == null)
            {
                centro = transform.position;
                radioActual = radioMaximo = 0f;
                return;
            }

            centro = CentroDe(nodo);
            radioActual = RadioActual(nodo);
            radioMaximo = nodo.radioMaximo;
        }
        #endregion

        private Vector3 CentroDe(Nodo nodo) => nodo.centro != null ? nodo.centro.position : transform.position;

        // La onda termina un poco más allá del área (margen del manager) para que el ruido del borde no deje
        // manchas corruptas en el límite.
        private float RadioActual(Nodo nodo)
        {
            if(Purificada) return nodo.radioMaximo + PurifyManager.MargenOnda;
            if(Progreso <= 0f) return 0f;

            return Mathf.Max(0f, _curva.Evaluate(Progreso)) * (nodo.radioMaximo + PurifyManager.MargenOnda);
        }

        private IEnumerator RutinaOnda(bool desdeElPrincipio)
        {
            if(desdeElPrincipio)
            {
                if(_retrasoInicio > 0f) yield return new WaitForSeconds(_retrasoInicio);
                OnPurifyStarted.Invoke();
            }

            while(Progreso < 1f)
            {
                Progreso = Mathf.Min(1f, Progreso + Time.deltaTime / _duracion);
                _cambio = true;
                OnProgress.Invoke(Progreso);
                yield return null;
            }

            _rutina = null;
            _animando = false;
            Purificada = true;
            _cambio = true;
            OnPurifyCompleted.Invoke();
        }

        private void DetenerRutina()
        {
            if(_rutina != null) StopCoroutine(_rutina);
            _rutina = null;
            _animando = false;
        }

        #region Context Menu
        [ContextMenu("Probar purificación")]
        private void ProbarPurificacion()
        {
            if(!Application.isPlaying)
            {
                Debug.LogWarning($"[{name}] La purificación se prueba en Play Mode.", this);
                return;
            }

            Purify();
        }

        [ContextMenu("Resetear a corrupta")]
        private void ResetearACorrupta() => ResetToCorrupted();
        #endregion

        #if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if(_nodos == null) return;

            Color relleno = _colorGizmo;
            relleno.a = 0.2f;

            foreach(Nodo nodo in _nodos)
            {
                if(nodo == null) continue;

                Vector3 centro = CentroDe(nodo);

                Gizmos.color = _colorGizmo;
                Gizmos.DrawWireSphere(centro, nodo.radioMaximo);

                float radio = Mathf.Min(RadioActual(nodo), nodo.radioMaximo);
                if(radio <= 0f) continue;

                Gizmos.color = relleno;
                Gizmos.DrawSphere(centro, radio);
            }

            string estado = Purificada ? "purificada" : _animando ? $"purificando {Progreso:P0}" : "corrupta";
            Handles.color = _colorGizmo;
            Handles.Label(transform.position + Vector3.up * 2f, $"{_nombreVisible} ({_regionId})\n{estado}");
        }
        #endif
    }
}
