using UnityEngine;

namespace Gekko.CloudsSandbox
{
    /// <summary>
    /// Movimiento de una nube: deriva lineal, cabeceo vertical y rotacion lenta.
    /// Clase plana, sin MonoBehaviour: la maneja <see cref="CloudDrift"/>.
    ///
    /// Ojo con la rotacion: el ruido del shader se evalua en espacio LOCAL, asi que
    /// al rotar el transform el patron rota con la nube en vez de quedarse quieto en
    /// el mundo. Eso es justamente lo que se busca (nada de "swimming"), pero implica
    /// que la velocidad de giro tiene que ser baja para que no se lea como un objeto
    /// solido girando.
    /// </summary>
    public class CloudDriftMotion
    {
        private readonly Transform _transform;
        private readonly Vector3 _driftVelocity;
        private readonly float _bobAmplitude;
        private readonly float _bobFrequency;
        private readonly float _spinSpeed;
        private readonly float _wrapDistance;
        private readonly Vector3 _origin;
        private readonly float _phase;

        private readonly Vector3 _driftAxis;
        private float _traveled;

        public CloudDriftMotion(
            Transform transform,
            Vector3 driftVelocity,
            float bobAmplitude,
            float bobFrequency,
            float spinSpeed,
            float wrapDistance)
        {
            _transform = transform;
            _driftVelocity = driftVelocity;
            _bobAmplitude = bobAmplitude;
            _bobFrequency = bobFrequency;
            _spinSpeed = spinSpeed;
            _wrapDistance = wrapDistance;

            _origin = transform.position;
            _driftAxis = driftVelocity.sqrMagnitude > 1e-6f ? driftVelocity.normalized : Vector3.right;

            // Desfase por instancia para que un campo de nubes no cabecee al unisono.
            _phase = Random.Range(0f, Mathf.PI * 2f);
        }

        public void ArtificialUpdate()
        {
            float deltaTime = Time.deltaTime;

            _traveled += _driftVelocity.magnitude * deltaTime;
            if (_wrapDistance > 0f && _traveled > _wrapDistance)
            {
                // Se reengancha por el otro lado en vez de acumular distancia infinita.
                _traveled -= _wrapDistance * 2f;
            }

            float bob = _bobAmplitude * Mathf.Sin(_phase + Time.time * _bobFrequency);

            _transform.position = _origin + _driftAxis * _traveled + Vector3.up * bob;

            if (!Mathf.Approximately(_spinSpeed, 0f))
            {
                _transform.Rotate(Vector3.up, _spinSpeed * deltaTime, Space.World);
            }
        }
    }
}
