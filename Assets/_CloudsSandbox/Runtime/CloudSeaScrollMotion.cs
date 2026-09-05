using UnityEngine;

namespace Gekko.CloudsSandbox
{
    /// <summary>
    /// Desplaza todo el mar de nubes en un sentido y recicla las que se pasan del
    /// borde. Clase plana, sin MonoBehaviour: la maneja <see cref="CloudSeaScroll"/>.
    ///
    /// El wrap es toroidal en el espacio LOCAL del padre: cuando una nube cruza
    /// +extent/2 en X reaparece en -extent/2, y lo mismo en Z. Ninguna nube se
    /// destruye ni se instancia: son siempre las mismas mallas dando la vuelta, asi
    /// que el movimiento es infinito y el costo constante.
    ///
    /// La costura no se nota porque <see cref="CloudField"/> distribuye en grilla:
    /// la celda que sale por un lado es equivalente a la que entra por el otro. Con
    /// una distribucion random pura, en cambio, el borde se leeria al dar la vuelta.
    /// </summary>
    public class CloudSeaScrollMotion
    {
        private readonly Transform _root;

        private Vector3 _velocity;
        private Vector2 _extent;
        private float _bobAmplitude;
        private float _bobFrequency;

        private Transform[] _clouds;
        private float[] _phases;
        private float[] _baseHeights;

        public CloudSeaScrollMotion(Transform root)
        {
            _root = root;
        }

        public void Configure(Vector3 direction, float speed, Vector2 extent, float bobAmplitude, float bobFrequency)
        {
            // Solo XZ: la altura la maneja el cabeceo, y dejar componente Y en la
            // deriva romperia el wrap (no hay periodo vertical sobre el que envolver).
            direction.y = 0f;
            _velocity = direction.sqrMagnitude > 1e-6f ? direction.normalized * speed : Vector3.zero;

            _extent = extent;
            _bobAmplitude = bobAmplitude;
            _bobFrequency = bobFrequency;
        }

        /// <summary>
        /// Cachea los hijos actuales. Hay que llamarla despues de regenerar el campo.
        /// </summary>
        public void CacheClouds()
        {
            int count = _root.childCount;
            _clouds = new Transform[count];
            _phases = new float[count];
            _baseHeights = new float[count];

            for (int i = 0; i < count; i++)
            {
                Transform child = _root.GetChild(i);
                _clouds[i] = child;
                _baseHeights[i] = child.localPosition.y;

                // Desfase determinista por indice: el mar no cabecea al unisono y
                // ademas el resultado es reproducible entre corridas.
                _phases[i] = (i * 0.6180339887f) % 1f * Mathf.PI * 2f;
            }
        }

        public void ArtificialUpdate()
        {
            if (_clouds == null || _clouds.Length == 0)
            {
                return;
            }

            Vector3 delta = _velocity * Time.deltaTime;
            float time = Time.time;

            for (int i = 0; i < _clouds.Length; i++)
            {
                Transform cloud = _clouds[i];
                if (cloud == null)
                {
                    continue;
                }

                Vector3 position = cloud.localPosition;
                position.x = Wrap(position.x + delta.x, _extent.x);
                position.z = Wrap(position.z + delta.z, _extent.y);

                // La altura no se integra: se reescribe siempre desde la base, asi el
                // cabeceo no acumula deriva vertical a lo largo de la sesion.
                position.y = _baseHeights[i] + _bobAmplitude * Mathf.Sin(_phases[i] + time * _bobFrequency);

                cloud.localPosition = position;
            }
        }

        /// <summary>
        /// Envuelve un valor dentro de [-size/2, +size/2]. Un solo paso alcanza porque
        /// el desplazamiento por frame es siempre mucho menor que el periodo.
        /// </summary>
        private static float Wrap(float value, float size)
        {
            if (size <= 0f)
            {
                return value;
            }

            float half = size * 0.5f;
            if (value > half)
            {
                return value - size;
            }
            if (value < -half)
            {
                return value + size;
            }
            return value;
        }
    }
}
