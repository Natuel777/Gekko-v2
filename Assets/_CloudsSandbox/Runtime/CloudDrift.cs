using UnityEngine;

namespace Gekko.CloudsSandbox
{
    /// <summary>
    /// Wrapper fino sobre <see cref="CloudDriftMotion"/>, siguiendo el patron del
    /// proyecto: el MonoBehaviour solo reenvia el ciclo de vida de Unity.
    /// </summary>
    public class CloudDrift : MonoBehaviour
    {
        [Header("Deriva")]
        [SerializeField] private Vector3 _driftVelocity = new Vector3(0.35f, 0f, 0f);
        [SerializeField] private float _wrapDistance = 60f;

        [Header("Cabeceo")]
        [SerializeField] private float _bobAmplitude = 0.4f;
        [SerializeField] private float _bobFrequency = 0.35f;

        [Header("Rotacion")]
        [Tooltip("Grados por segundo. Muy bajo: si gira rapido la nube se lee como un solido.")]
        [SerializeField] private float _spinSpeed = 1.5f;

        private CloudDriftMotion _motion;

        /// <summary>
        /// La usa <see cref="CloudField"/> al generar el campo, en tiempo de Editor.
        /// Tiene que llamarse antes de Awake: despues, la velocidad ya quedo capturada
        /// dentro de <see cref="CloudDriftMotion"/>.
        /// </summary>
        public void SetDriftVelocity(Vector3 driftVelocity)
        {
            _driftVelocity = driftVelocity;
        }

        private void Awake()
        {
            _motion = new CloudDriftMotion(
                transform,
                _driftVelocity,
                _bobAmplitude,
                _bobFrequency,
                _spinSpeed,
                _wrapDistance);
        }

        private void Update()
        {
            _motion.ArtificialUpdate();
        }
    }
}
