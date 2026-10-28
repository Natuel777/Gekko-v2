using System.Collections.Generic;
using UnityEngine;

namespace Gekko.Purification.EditorTools
{
    /// <summary>
    /// Revisa la escena abierta y devuelve los problemas del sistema de purificación. No modifica nada.
    /// </summary>
    public static class PurifyValidator
    {
        public enum Gravedad { Error, Aviso, Info }

        public struct Problema
        {
            public Gravedad gravedad;
            public string mensaje;
            public Object objeto;   // para seleccionarlo desde la ventana
        }

        public static List<Problema> Validar()
        {
            var lista = new List<Problema>();
            void Add(Gravedad g, string m, Object o = null) => lista.Add(new Problema { gravedad = g, mensaje = m, objeto = o });

            var managers = Object.FindObjectsByType<PurifyManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var regiones = Object.FindObjectsByType<PurifyRegion>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var reactivos = Object.FindObjectsByType<PurifyReactive>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var triggers = Object.FindObjectsByType<PurifyTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            // Manager
            if(regiones.Length > 0 && managers.Length == 0)
                Add(Gravedad.Error, "Hay regiones pero no hay ningún PurifyManager: los shaders no van a ver ninguna región (todo se verá sano).");
            if(managers.Length > 1)
                foreach(var m in managers) Add(Gravedad.Error, $"Hay {managers.Length} PurifyManager en la escena; tiene que haber uno solo.", m);

            // Regiones: ids, nodos
            var ids = new Dictionary<string, PurifyRegion>();
            int nodosTotales = 0;
            foreach(var r in regiones)
            {
                if(string.IsNullOrWhiteSpace(r.RegionId)) Add(Gravedad.Error, $"La región '{r.name}' no tiene Region Id.", r);
                else if(ids.TryGetValue(r.RegionId, out var otra)) Add(Gravedad.Error, $"Region Id repetido '{r.RegionId}' ('{otra.name}' y '{r.name}').", r);
                else ids[r.RegionId] = r;

                if(r.CantidadNodos == 0) Add(Gravedad.Error, $"La región '{r.name}' no tiene nodos.", r);
                nodosTotales += r.CantidadNodos;
            }
            if(nodosTotales > PurifyManager.MaxEsferas)
                Add(Gravedad.Error, $"Hay {nodosTotales} nodos y el shader soporta {PurifyManager.MaxEsferas}: los que sobran no se dibujan.");

            // Superposición entre regiones distintas (permitido, pero esa zona necesita las dos purificadas).
            for(int a = 0; a < regiones.Length; a++)
            for(int b = a + 1; b < regiones.Length; b++)
            {
                float maxSolape = 0f;
                for(int i = 0; i < regiones[a].CantidadNodos; i++)
                for(int j = 0; j < regiones[b].CantidadNodos; j++)
                {
                    regiones[a].ObtenerNodo(i, out Vector3 ca, out _, out float ra);
                    regiones[b].ObtenerNodo(j, out Vector3 cb, out _, out float rb);
                    maxSolape = Mathf.Max(maxSolape, ra + rb - Vector3.Distance(ca, cb));
                }
                if(maxSolape > 0.5f)
                    Add(Gravedad.Info, $"'{regiones[a].NombreVisible}' y '{regiones[b].NombreVisible}' se superponen {maxSolape:0.#} m: esa zona queda corrupta hasta purificar las dos.", regiones[a]);
            }

            // Disparadores
            var conTrigger = new HashSet<PurifyRegion>();
            foreach(var t in triggers)
            {
                if(t.Region == null) { Add(Gravedad.Error, $"El PurifyTrigger de '{t.name}' no tiene región.", t); continue; }
                conTrigger.Add(t.Region);
                if(!t.TieneFuente) Add(Gravedad.Info, $"El PurifyTrigger de '{t.name}' no tiene núcleo ni desafío: solo dispara si un UnityEvent llama a Disparar().", t);
            }
            foreach(var r in regiones)
                if(!conTrigger.Contains(r)) Add(Gravedad.Aviso, $"La región '{r.NombreVisible}' no tiene PurifyTrigger: solo se purifica a mano (menú contextual).", r);

            // Reactivos fuera de toda región
            foreach(var re in reactivos)
                if(re.RegionOverride == null && PurifyRegion.Buscar(re.transform.position, regiones) == null)
                    Add(Gravedad.Aviso, $"'{re.name}' ({re.GetType().Name}) está fuera de toda región: no va a reaccionar.", re);

            // Atmósfera
            foreach(var atm in Object.FindObjectsByType<PurifyAtmosphere>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if(atm.GetComponent<UnityEngine.Rendering.Volume>() == null)
                    Add(Gravedad.Info, $"'{atm.name}' tiene PurifyAtmosphere: revisá que tenga asignado el Volume corrupto.", atm);

            if(lista.Count == 0) Add(Gravedad.Info, "Sin problemas.");
            return lista;
        }
    }
}
