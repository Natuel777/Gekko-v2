using UnityEngine;

namespace Gekko.CloudsSandbox
{
    /// <summary>
    /// Wrapper fino sobre <see cref="CloudSeaScrollMotion"/>, siguiendo el patron del
    /// proyecto: el MonoBehaviour solo reenvia el ciclo de vida de Unity.
    ///
    /// A proposito NO es [ExecuteAlways]: en modo edicion moveria los hijos generados
    /// y ensuciaria la escena en cada repaint. El movimiento se ve dandole Play.
    /// </summary>
    [DisallowMultipleComponent]
    public class CloudSeaScroll : MonoBehaviour
    {
        [Header("Deriva")]
        [Tooltip("Solo XZ. La componente Y se ignora: no hay periodo vertical sobre el que envolver.")]
        [SerializeField] private Vector3 _direction = new Vector3(1f, 0f, 0f);
        [SerializeField] private float _speed = 0.6f;

        [Header("Periodo del wrap")]
        [Tooltip("Medida total del campo en X y Z. La rellena CloudField al hacer Rebuild.")]
        [SerializeField] private Vector2 _extent = new Vector2(60f, 48f);

        [Header("Cabeceo")]
        [SerializeField] private float _bobAmplitude = 0.35f;
        [SerializeField] private float _bobFrequency = 0.3f;

        private CloudSeaScrollMotion _motion;

        /// <summary>
        /// La usa <see cref="CloudField"/> al regenerar el campo, en tiempo de Editor.
        /// </summary>
        public void Configure(Vector3 direction, float speed, Vector2 extent, float bobAmplitude, float bobFrequency)
        {
            _direction = direction;
            _speed = speed;
            _extent = extent;
            _bobAmplitude = bobAmplitude;
            _bobFrequency = bobFrequency;
        }

        private void Awake()
        {
            _motion = new CloudSeaScrollMotion(transform);
            _motion.Configure(_direction, _speed, _extent, _bobAmplitude, _bobFrequency);
            _motion.CacheClouds();
        }

        private void Update()
        {
            _motion.ArtificialUpdate();
        }

        private void OnValidate()
        {
            // Permite tocar velocidad y direccion en vivo con el juego corriendo.
            _motion?.Configure(_direction, _speed, _extent, _bobAmplitude, _bobFrequency);
        }
    }
}
