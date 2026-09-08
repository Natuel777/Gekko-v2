using UnityEngine;

namespace Gekko.GrassSandbox
{
    /// <summary>
    /// Wrapper fino sobre <see cref="GrassInteractionManager"/>, siguiendo el patron del
    /// proyecto: el MonoBehaviour solo reenvia el ciclo de vida de Unity.
    ///
    /// Se actualiza en LateUpdate a proposito: para entonces los personajes ya se
    /// movieron en su Update/FixedUpdate, asi que el pasto no va un frame atrasado.
    ///
    /// Lo crea solo el primer <see cref="GrassInteractor"/> que se habilita. Es
    /// ExecuteAlways para que el aplastado tambien se vea al pintar, sin darle Play.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public class GrassInteractionDriver : MonoBehaviour
    {
        private static GrassInteractionDriver _instance;

        private GrassInteractionManager _manager;

        /// <summary>Crea el driver si todavia no hay uno en la escena.</summary>
        public static void EnsureExists()
        {
            if (_instance != null)
            {
                return;
            }

            _instance = FindAnyObjectByType<GrassInteractionDriver>();
            if (_instance != null)
            {
                return;
            }

            var host = new GameObject("GrassInteractionDriver")
            {
                // No se guarda en la escena: se recrea solo cuando hace falta.
                hideFlags = HideFlags.DontSave
            };

            _instance = host.AddComponent<GrassInteractionDriver>();
        }

        private void OnEnable()
        {
            if (_instance != null && _instance != this)
            {
                // Alguien puso un segundo driver a mano: uno solo alcanza.
                enabled = false;
                return;
            }

            _instance = this;
            _manager ??= new GrassInteractionManager();
        }

        private void OnDisable()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void LateUpdate()
        {
            _manager?.ArtificialLateUpdate();
        }
    }
}
