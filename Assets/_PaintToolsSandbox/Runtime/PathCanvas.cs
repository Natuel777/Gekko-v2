using UnityEngine;

namespace Gekko.PaintTools
{
    /// <summary>
    /// Una zona pintable de camino. Define un rectangulo del mundo (visto desde arriba)
    /// y la textura de mascara que lo cubre.
    ///
    /// Por que proyectada desde arriba y no en UV de la malla: asi el sistema no depende
    /// ni de la cantidad de vertices ni de que la malla tenga UVs limpias y sin
    /// solapamientos. Sobre las mallas del Spline Terrain, que no garantizan ninguna de
    /// las dos cosas, es la unica opcion que funciona sin retocar nada.
    ///
    /// El precio: la mascara es plana en XZ, asi que dos pisos apilados en la misma
    /// vertical comparten mascara. Para eso se usa UN CANVAS POR PISO, cada uno con su
    /// textura y sus renderers asignados.
    /// </summary>
    [ExecuteAlways]
    public class PathCanvas : MonoBehaviour
    {
        private static readonly int MaskId = Shader.PropertyToID("_PathMask");
        private static readonly int CanvasMinId = Shader.PropertyToID("_PathCanvasMin");
        private static readonly int CanvasSizeId = Shader.PropertyToID("_PathCanvasSize");

        [Header("Zona")]
        [Tooltip("Tamano de la zona pintable en X y Z, en unidades del mundo. El centro es este transform.")]
        [SerializeField] private Vector2 _size = new Vector2(60f, 60f);

        [Header("Mascara")]
        [Tooltip("Textura de mascara. La crea el editor si falta. RGB = tinte pintado, A = cobertura.")]
        [SerializeField] private Texture2D _mask;

        [Tooltip("Set de pinceles a usar al pintar. Es data de autoria: solo la lee el editor.")]
        [SerializeField] private PathBrushSet _brushSet;

        [Header("Destino")]
        [Tooltip("Los renderers del piso que tienen que mostrar el camino. Reciben la mascara por MaterialPropertyBlock.")]
        [SerializeField] private Renderer[] _targetRenderers;

        private MaterialPropertyBlock _propertyBlock;

        // Estado de lo ultimo que se empujo, para no reescribir el MPB cada frame.
        private bool _hasAppliedOnce;
        private int _appliedMaskId;
        private int _appliedTargetCount;
        private Vector2 _appliedSize;
        private Vector3 _appliedPosition;

        public Texture2D Mask => _mask;
        public Vector2 Size => _size;
        public PathBrushSet BrushSet => _brushSet;

        /// <summary>Esquina inferior del rectangulo en XZ del mundo.</summary>
        public Vector2 WorldMin
        {
            get
            {
                Vector3 center = transform.position;
                return new Vector2(center.x - _size.x * 0.5f, center.z - _size.y * 0.5f);
            }
        }

        /// <summary>
        /// Texels por unidad del mundo en cada eje. Si la zona no es cuadrada, los dos
        /// valores difieren: la textura siempre es cuadrada, asi que el eje mas largo
        /// tiene menos resolucion.
        /// </summary>
        public Vector2 TexelsPerUnit
        {
            get
            {
                if (_mask == null || _size.x <= 0f || _size.y <= 0f)
                {
                    return Vector2.zero;
                }
                return new Vector2(_mask.width / _size.x, _mask.height / _size.y);
            }
        }

        public void SetMask(Texture2D mask)
        {
            _mask = mask;
            Apply();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void OnValidate()
        {
            _size.x = Mathf.Max(1f, _size.x);
            _size.y = Mathf.Max(1f, _size.y);
            Apply();
        }

        private void Update()
        {
            // El MPB se queda viejo con demasiada facilidad: mover el transform no
            // dispara OnValidate, y editar los campos por SerializedObject (que es lo que
            // hacen el Undo y las herramientas) tampoco. Si eso pasa, el shader sigue
            // usando la zona anterior y el camino aparece corrido o directamente no
            // aparece. Se re-empuja cuando algo cambio, en editor y en runtime.
            if (HasChangedSinceLastApply())
            {
                Apply();
            }
        }

        private bool HasChangedSinceLastApply()
        {
            int maskId = _mask != null ? _mask.GetInstanceID() : 0;
            int targetCount = _targetRenderers != null ? _targetRenderers.Length : 0;

            if (!_hasAppliedOnce
                || maskId != _appliedMaskId
                || targetCount != _appliedTargetCount
                || _appliedSize != _size
                || _appliedPosition != transform.position)
            {
                return true;
            }

            return HasDanglingMask();
        }

        /// <summary>
        /// Detecta el caso que ningun contador de cambios ve: la mascara se reimporto,
        /// el Texture2D viejo se destruyo, y adentro del MaterialPropertyBlock quedo una
        /// referencia muerta. El bloque sigue diciendo que tiene la propiedad, pero la
        /// textura es null, asi que el shader cae al valor del material y el camino
        /// desaparece de golpe SIN ningun error en consola.
        ///
        /// Se revisa un solo renderer por frame: alcanza, porque Apply() los escribe a
        /// todos juntos, y GetPropertyBlock sobre un bloque cacheado no aloca.
        /// </summary>
        private bool HasDanglingMask()
        {
            if (_mask == null || _propertyBlock == null || _targetRenderers == null)
            {
                return false;
            }

            foreach (Renderer target in _targetRenderers)
            {
                if (target == null)
                {
                    continue;
                }

                target.GetPropertyBlock(_propertyBlock);
                return _propertyBlock.GetTexture(MaskId) == null;
            }

            return false;
        }

        /// <summary>
        /// Empuja la mascara y las coordenadas de la zona a los renderers asignados.
        ///
        /// Se usa MaterialPropertyBlock y no propiedades del material para que varios
        /// canvas puedan compartir el mismo material sin pisarse. El costo es que esos
        /// renderers se salen del SRP Batcher: son pocos (el piso), asi que conviene.
        /// </summary>
        public void Apply()
        {
            if (_targetRenderers == null || _targetRenderers.Length == 0 || _mask == null)
            {
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();

            _hasAppliedOnce = true;
            _appliedMaskId = _mask.GetInstanceID();
            _appliedTargetCount = _targetRenderers.Length;
            _appliedSize = _size;
            _appliedPosition = transform.position;

            Vector2 min = WorldMin;

            foreach (Renderer target in _targetRenderers)
            {
                if (target == null)
                {
                    continue;
                }

                target.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetTexture(MaskId, _mask);
                _propertyBlock.SetVector(CanvasMinId, new Vector4(min.x, min.y, 0f, 0f));
                _propertyBlock.SetVector(CanvasSizeId, new Vector4(_size.x, _size.y, 0f, 0f));
                target.SetPropertyBlock(_propertyBlock);
            }
        }

        /// <summary>Convierte una posicion del mundo a coordenada de pixel en la mascara.</summary>
        public bool TryWorldToPixel(Vector3 worldPosition, out Vector2 pixel)
        {
            pixel = Vector2.zero;
            if (_mask == null)
            {
                return false;
            }

            Vector2 min = WorldMin;
            float u = (worldPosition.x - min.x) / _size.x;
            float v = (worldPosition.z - min.y) / _size.y;

            pixel = new Vector2(u * _mask.width, v * _mask.height);
            return u >= 0f && u <= 1f && v >= 0f && v <= 1f;
        }

        /// <summary>
        /// Cuantos pixeles de la mascara ocupa un radio dado del mundo, por eje.
        ///
        /// Devuelve dos valores y no uno porque la textura es cuadrada pero la zona no
        /// tiene por que serlo: con una zona de 120x60, un pincel circular en el mundo
        /// es una ELIPSE en pixeles. Usar un solo radio deforma el pincel.
        /// </summary>
        public Vector2 WorldRadiusToPixels(float worldRadius)
        {
            return _mask == null ? Vector2.zero : worldRadius * TexelsPerUnit;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = transform.position;
            Gizmos.color = new Color(1f, 0.85f, 0.4f, 0.9f);
            Gizmos.DrawWireCube(center, new Vector3(_size.x, 0.05f, _size.y));
        }
    }
}
