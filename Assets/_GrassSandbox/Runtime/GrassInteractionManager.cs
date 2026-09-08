using System.Collections.Generic;
using UnityEngine;

namespace Gekko.GrassSandbox
{
    /// <summary>
    /// Junta los <see cref="GrassInteractor"/> activos y los manda al shader.
    /// Clase plana, sin MonoBehaviour: la maneja <see cref="GrassInteractionDriver"/>.
    ///
    /// Se mandan como propiedades GLOBALES (Shader.SetGlobalVectorArray) y no por
    /// material. Dos motivos: una sola escritura por frame alcanza para todos los
    /// materiales de pasto que haya en la escena, y no se toca ningun material, asi que
    /// el SRP Batcher los sigue agrupando.
    ///
    /// El tope de 8 es fijo porque el array del shader es de tamano fijo, y porque el
    /// vertex shader recorre esa lista por vertice: cada interactor extra se paga en
    /// cada brizna en pantalla. Si hay mas de 8 candidatos se quedan los mas cercanos a
    /// la camara, que son los unicos cuyo pasto se ve.
    /// </summary>
    public class GrassInteractionManager
    {
        /// <summary>Tiene que coincidir con MAX_GRASS_INTERACTORS del shader.</summary>
        public const int MaxInteractors = 8;

        private static readonly int InteractorsId = Shader.PropertyToID("_GrassInteractors");
        private static readonly int InteractorCountId = Shader.PropertyToID("_GrassInteractorCount");

        // El array se reusa: SetGlobalVectorArray exige longitud constante, y ademas
        // evita generar basura cada frame.
        private readonly Vector4[] _payload = new Vector4[MaxInteractors];
        private readonly List<GrassInteractor> _sorted = new List<GrassInteractor>(MaxInteractors * 2);

        private int _lastCount = -1;

        public void ArtificialLateUpdate()
        {
            IReadOnlyList<GrassInteractor> active = GrassInteractor.ActiveInteractors;
            int activeCount = active.Count;

            if (activeCount == 0)
            {
                // Solo se escribe cuando cambia: si no hay nadie, no hay que tocar nada
                // frame a frame.
                if (_lastCount != 0)
                {
                    Shader.SetGlobalFloat(InteractorCountId, 0f);
                    _lastCount = 0;
                }
                return;
            }

            _sorted.Clear();
            for (int i = 0; i < activeCount; i++)
            {
                if (active[i] != null)
                {
                    _sorted.Add(active[i]);
                }
            }

            if (_sorted.Count > MaxInteractors)
            {
                Camera camera = Camera.main;
                if (camera != null)
                {
                    Vector3 viewer = camera.transform.position;
                    _sorted.Sort((a, b) =>
                        (a.Center - viewer).sqrMagnitude.CompareTo((b.Center - viewer).sqrMagnitude));
                }
            }

            int count = Mathf.Min(_sorted.Count, MaxInteractors);
            for (int i = 0; i < count; i++)
            {
                Vector3 center = _sorted[i].Center;
                _payload[i] = new Vector4(center.x, center.y, center.z, _sorted[i].Radius);
            }

            // Las ranuras sobrantes se dejan con radio 0: el shader igual las descarta
            // por el contador, pero asi no quedan datos viejos si algo lee de mas.
            for (int i = count; i < MaxInteractors; i++)
            {
                _payload[i] = Vector4.zero;
            }

            Shader.SetGlobalVectorArray(InteractorsId, _payload);
            Shader.SetGlobalFloat(InteractorCountId, count);
            _lastCount = count;
        }
    }
}
