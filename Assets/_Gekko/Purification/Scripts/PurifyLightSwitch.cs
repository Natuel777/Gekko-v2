using System.Collections;
using UnityEngine;

namespace Gekko.Purification
{
    /// <summary>
    /// Luces que se prenden cuando la onda de su región las alcanza (faroles, ventanas, el brillo del árbol).
    /// Arrancan apagadas mientras la región está corrupta y suben de a poco hasta la intensidad que tienen en la
    /// escena. Las luces "corruptas" (opcional) hacen lo contrario.
    /// </summary>
    public class PurifyLightSwitch : PurifyReactive
    {
        [Header("Luces")]
        [Tooltip("Se prenden con la onda, hasta la intensidad que tienen en la escena.")]
        [SerializeField] private Light[] _sanas;
        [Tooltip("Se apagan con la onda.")]
        [SerializeField] private Light[] _corruptas;
        [Tooltip("Segundos que tarda el fundido.")]
        [Min(0.01f)]
        [SerializeField] private float _duracion = 1.2f;

        private float[] _intensidadSanas;
        private float[] _intensidadCorruptas;

        private void Awake()
        {
            _intensidadSanas = Guardar(_sanas);
            _intensidadCorruptas = Guardar(_corruptas);
        }

        protected override void Start()
        {
            base.Start();

            if(Disparado || Region == null) return;

            Aplicar(_sanas, _intensidadSanas, 0f);
        }

        protected override void OnReached() => StartCoroutine(Fundido());

        protected override void OnPurifiedInstant()
        {
            Aplicar(_sanas, _intensidadSanas, 1f);
            Aplicar(_corruptas, _intensidadCorruptas, 0f);
        }

        private IEnumerator Fundido()
        {
            float t = 0f;
            while(t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / _duracion);
                float k = Mathf.SmoothStep(0f, 1f, t);
                Aplicar(_sanas, _intensidadSanas, k);
                Aplicar(_corruptas, _intensidadCorruptas, 1f - k);
                yield return null;
            }
        }

        private static float[] Guardar(Light[] luces)
        {
            if(luces == null) return System.Array.Empty<float>();

            var intensidades = new float[luces.Length];
            for(int i = 0; i < luces.Length; i++)
                intensidades[i] = luces[i] != null ? luces[i].intensity : 0f;
            return intensidades;
        }

        private static void Aplicar(Light[] luces, float[] intensidades, float factor)
        {
            if(luces == null) return;

            for(int i = 0; i < luces.Length; i++)
            {
                if(luces[i] == null) continue;
                luces[i].intensity = intensidades[i] * factor;
                luces[i].enabled = factor > 0.001f;
            }
        }
    }
}
