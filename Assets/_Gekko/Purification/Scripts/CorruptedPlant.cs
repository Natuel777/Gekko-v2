using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Gekko.Purification
{
    /// <summary>
    /// Vegetación corrupta (espinas, plantas muertas, enredaderas) que se quema cuando llega la onda: disolve con
    /// borde ardiente, brasas opcionales y al final se ocultan los renderers.
    ///
    /// El disolve anima una propiedad del shader (por defecto _Dissolve, la que ya usan S_Enredaderas y
    /// Dissolve.hlsl) en INSTANCIAS de material (r.materials): el GPU Resident Drawer de PC_RPAsset no es compatible
    /// con MaterialPropertyBlock, igual que en PurificationCore y PurificationBarrier. Si ningún material tiene la
    /// propiedad, se encoge hasta desaparecer, así funciona también con materiales comunes.
    /// </summary>
    public class CorruptedPlant : PurifyReactive
    {
        [Header("Quemado")]
        [Tooltip("Renderers que se queman. Si se deja vacío, usa los de este objeto y sus hijos.")]
        [SerializeField] private Renderer[] _renderers;
        [Tooltip("Propiedad del shader que controla el disolve (S_Enredaderas y Dissolve.hlsl usan _Dissolve).")]
        [SerializeField] private string _propiedadDisolve = "_Dissolve";
        [Tooltip("Valor del disolve entera (antes de quemarse).")]
        [SerializeField] private float _disolveDesde = 0f;
        [Tooltip("Valor del disolve quemada del todo.")]
        [SerializeField] private float _disolveHasta = 1f;
        [Tooltip("Segundos que tarda en quemarse.")]
        [Min(0.01f)]
        [SerializeField] private float _duracion = 1.2f;

        [Header("Feedback (opcional)")]
        [Tooltip("Brasas que se disparan al empezar a quemarse. No conviene que sean hijas de un renderer que se oculta (se siguen viendo igual: solo se apagan los renderers, no el objeto).")]
        [SerializeField] private ParticleSystem _brasas;

        private readonly List<Material> _instancias = new();
        private int _disolveId;
        private bool _inicializado;

        protected override void Start()
        {
            Inicializar();
            base.Start();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            // Las instancias creadas por r.materials no las libera Unity solas hasta cambiar de escena.
            foreach(Material material in _instancias)
                if(material != null) Destroy(material);
        }

        protected override void OnReached()
        {
            if(_brasas != null) _brasas.Play();

            if(gameObject.activeInHierarchy) StartCoroutine(Quemar());
            else Terminar();
        }

        protected override void OnPurifiedInstant() => Terminar();

        /// <summary>Se llama cuando terminó de quemarse (o directo, si se purifica sin animación).</summary>
        protected virtual void OnBurned() { }

        private void Inicializar()
        {
            if(_inicializado) return;
            _inicializado = true;

            if(_renderers == null || _renderers.Length == 0) _renderers = GetComponentsInChildren<Renderer>(true);
            _disolveId = Shader.PropertyToID(_propiedadDisolve);
        }

        private IEnumerator Quemar()
        {
            Inicializar();
            CrearInstancias();

            bool conDisolve = _instancias.Count > 0;
            Vector3 escalaInicial = transform.localScale;
            float t = 0f;

            while(t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / _duracion);

                if(conDisolve) SetDisolve(Mathf.Lerp(_disolveDesde, _disolveHasta, t));
                else transform.localScale = escalaInicial * (1f - t * t); // arranca lento y se cierra rápido

                yield return null;
            }

            Terminar();
        }

        private void CrearInstancias()
        {
            if(_instancias.Count > 0) return;

            foreach(Renderer r in _renderers)
            {
                if(r == null || !TieneDisolve(r)) continue;

                foreach(Material material in r.materials)
                    if(material != null && material.HasProperty(_disolveId)) _instancias.Add(material);
            }
        }

        private bool TieneDisolve(Renderer r)
        {
            foreach(Material material in r.sharedMaterials)
                if(material != null && material.HasProperty(_disolveId)) return true;

            return false;
        }

        private void SetDisolve(float valor)
        {
            foreach(Material material in _instancias)
                if(material != null) material.SetFloat(_disolveId, valor);
        }

        // Solo se ocultan los renderers: el objeto sigue activo para no cortar las brasas ni el sonido.
        private void Terminar()
        {
            Inicializar();

            foreach(Renderer r in _renderers)
                if(r != null) r.enabled = false;

            OnBurned();
        }
    }
}
