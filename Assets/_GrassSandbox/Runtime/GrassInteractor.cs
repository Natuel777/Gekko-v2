using System.Collections.Generic;
using UnityEngine;

namespace Gekko.GrassSandbox
{
    /// <summary>
    /// Marca un objeto como "aplasta pasto": el gecko, los NPCs, una roca que rueda.
    /// Se registra solo al habilitarse y se da de baja al deshabilitarse.
    ///
    /// El registro es una lista estatica en vez de un FindObjectsOfType por frame:
    /// son pocos objetos y cambian poco, no tiene sentido re-escanear la escena.
    /// </summary>
    [DisallowMultipleComponent]
    public class GrassInteractor : MonoBehaviour
    {
        private static readonly List<GrassInteractor> Active = new List<GrassInteractor>();

        [Tooltip("Radio de aplastado en unidades del mundo.")]
        [Min(0.01f)]
        [SerializeField] private float _radius = 1.2f;

        [Tooltip("Desplazamiento del centro respecto del transform. Util para bajarlo a los pies.")]
        [SerializeField] private Vector3 _offset = Vector3.zero;

        public static IReadOnlyList<GrassInteractor> ActiveInteractors => Active;

        public float Radius => _radius;

        public Vector3 Center => transform.position + transform.TransformVector(_offset);

        private void OnEnable()
        {
            if (!Active.Contains(this))
            {
                Active.Add(this);
            }

            // El primer interactor de la escena levanta el driver: asi alcanza con poner
            // este componente en el personaje, sin acordarse de agregar nada mas.
            GrassInteractionDriver.EnsureExists();
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.5f);
            Gizmos.DrawWireSphere(Center, _radius);
        }
    }
}
