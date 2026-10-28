using System.Collections.Generic;
using UnityEngine;

namespace Gekko.Purification
{
    /// <summary>
    /// Arma el array de esferas con los nodos de todas las regiones y lo sube a los globales que lee
    /// PurifyMask.hlsl. Clase plana, sin MonoBehaviour: la maneja <see cref="PurifyManager"/> (y la ventana de debug en Edit Mode).
    ///
    /// Solo sube cuando algo cambió. Los arrays tienen SIEMPRE <see cref="PurifyManager.MaxEsferas"/> elementos:
    /// Shader.SetGlobalVectorArray fija el tamaño la primera vez que se llama y después no se puede agrandar.
    /// </summary>
    public class PurifyShaderGlobals
    {
        private static readonly int SpheresId = Shader.PropertyToID("_PurifySpheres");
        private static readonly int SphereParamsId = Shader.PropertyToID("_PurifySphereParams");
        private static readonly int SphereCountId = Shader.PropertyToID("_PurifySphereCount");
        private static readonly int EdgeWidthId = Shader.PropertyToID("_PurifyEdgeWidth");
        private static readonly int AreaFadeId = Shader.PropertyToID("_PurifyAreaFade");

        private readonly Vector4[] _esferas = new Vector4[PurifyManager.MaxEsferas];
        private readonly Vector4[] _parametros = new Vector4[PurifyManager.MaxEsferas];
        private readonly Object _contexto;
        private int _cantidadRegiones = -1;
        private bool _avisoExceso;

        public PurifyShaderGlobals(Object contexto)
        {
            _contexto = contexto;
        }

        public void SubirParametros(float anchoBorde, float fundidoArea)
        {
            Shader.SetGlobalFloat(EdgeWidthId, anchoBorde);
            Shader.SetGlobalFloat(AreaFadeId, fundidoArea);
        }

        /// <summary>Sube los nodos si alguna región cambió, o si se agregó/sacó una región.</summary>
        public void ArtificialLateUpdate(IReadOnlyList<PurifyRegion> regiones)
        {
            bool cambio = _cantidadRegiones != regiones.Count;

            // Se le pregunta a todas (sin cortar en la primera) para limpiar el aviso de cada una.
            for(int i = 0; i < regiones.Count; i++)
                cambio |= regiones[i].ConsumirCambio();

            if(cambio) Subir(regiones);
        }

        public void Subir(IReadOnlyList<PurifyRegion> regiones)
        {
            _cantidadRegiones = regiones.Count;

            int cantidad = 0;
            int nodosTotales = 0;

            for(int r = 0; r < regiones.Count; r++)
            {
                PurifyRegion region = regiones[r];

                for(int n = 0; n < region.CantidadNodos; n++)
                {
                    nodosTotales++;
                    if(cantidad >= PurifyManager.MaxEsferas) continue;

                    region.ObtenerNodo(n, out Vector3 centro, out float radioActual, out float radioMaximo);
                    _esferas[cantidad] = new Vector4(centro.x, centro.y, centro.z, radioActual);
                    _parametros[cantidad] = new Vector4(radioMaximo, 0f, 0f, 0f);
                    cantidad++;
                }
            }

            if(nodosTotales > PurifyManager.MaxEsferas && !_avisoExceso)
            {
                _avisoExceso = true;
                Debug.LogWarning($"[PurifyManager] Hay {nodosTotales} nodos de purificación y el shader soporta {PurifyManager.MaxEsferas}: los que sobran no se dibujan.", _contexto);
            }

            for(int i = cantidad; i < PurifyManager.MaxEsferas; i++)
            {
                _esferas[i] = Vector4.zero;
                _parametros[i] = Vector4.zero;
            }

            Shader.SetGlobalVectorArray(SpheresId, _esferas);
            Shader.SetGlobalVectorArray(SphereParamsId, _parametros);
            Shader.SetGlobalFloat(SphereCountId, cantidad);
        }

        /// <summary>Sin nodos el shader ve todo sano. Se llama al apagar el manager para no dejar globales viejos
        /// (en el Editor sobreviven a la salida de Play Mode y a los cambios de escena).</summary>
        public void Limpiar()
        {
            for(int i = 0; i < PurifyManager.MaxEsferas; i++)
            {
                _esferas[i] = Vector4.zero;
                _parametros[i] = Vector4.zero;
            }

            Shader.SetGlobalVectorArray(SpheresId, _esferas);
            Shader.SetGlobalVectorArray(SphereParamsId, _parametros);
            Shader.SetGlobalFloat(SphereCountId, 0f);
            _cantidadRegiones = -1;
        }
    }
}
