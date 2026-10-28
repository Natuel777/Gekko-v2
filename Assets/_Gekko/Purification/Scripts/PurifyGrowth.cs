using System.Collections;
using UnityEngine;

namespace Gekko.Purification
{
    /// <summary>
    /// Vegetación nueva (flores, musgo, hongos) que brota detrás de la onda: arranca con escala 0 y crece hasta su
    /// escala original con un pequeño rebote. Para más de ~200 por región conviene hacerlo por shader en vez de un
    /// script por objeto.
    /// </summary>
    public class PurifyGrowth : PurifyReactive
    {
        [Header("Crecimiento")]
        [Tooltip("Segundos que tarda en crecer.")]
        [Min(0.01f)]
        [SerializeField] private float _duracion = 0.6f;
        [Tooltip("Cuánto se pasa de su tamaño antes de asentarse (0 = sin rebote).")]
        [Range(0f, 3f)]
        [SerializeField] private float _rebote = 1.7f;

        private Vector3 _escalaOriginal;

        private void Awake()
        {
            _escalaOriginal = transform.localScale;
        }

        protected override void Start()
        {
            base.Start();

            // Si la región ya estaba purificada (o el objeto quedó fuera de toda región) se queda como está.
            if(!Disparado && Region != null) transform.localScale = Vector3.zero;
        }

        protected override void OnReached()
        {
            if(gameObject.activeInHierarchy) StartCoroutine(Crecer());
            else transform.localScale = _escalaOriginal;
        }

        protected override void OnPurifiedInstant() => transform.localScale = _escalaOriginal;

        private IEnumerator Crecer()
        {
            float t = 0f;

            while(t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / _duracion);
                transform.localScale = _escalaOriginal * EaseOutBack(t);
                yield return null;
            }

            transform.localScale = _escalaOriginal;
        }

        private float EaseOutBack(float t)
        {
            float c3 = _rebote + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + _rebote * u * u;
        }
    }
}
