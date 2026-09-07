using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gekko.Tools
{
    /// <summary>
    /// Una entrada del conjunto. Guarda ajustes de AUTORIA, no toca el prefab original:
    /// el prefab se referencia y nunca se modifica.
    /// </summary>
    [Serializable]
    public class PrefabSetEntry
    {
        public GameObject Prefab;

        [Tooltip("Si esta apagado, la entrada queda en el set pero no se usa ni se previsualiza.")]
        public bool Enabled = true;

        [Tooltip("Peso relativo al elegir al azar entre las entradas activas.")]
        [Min(0f)]
        public float Weight = 1f;

        [Tooltip("Multiplicador de escala aplicado solo al usar el set, no al prefab.")]
        [Min(0.01f)]
        public float ScaleMultiplier = 1f;
    }

    /// <summary>
    /// Un conjunto de prefabs reutilizable: la "paleta" con la que se pinta.
    ///
    /// Existe como asset propio y no como una lista dentro de cada herramienta para que
    /// el mismo conjunto (por ejemplo "vegetacion de cueva") se pueda usar en varios
    /// campos de props y en varias escenas, y editarse en un solo lugar.
    ///
    /// Nada de lo que hay aca modifica los prefabs referenciados.
    /// </summary>
    [CreateAssetMenu(fileName = "PrefabSet", menuName = "Gekko/Prefab Set")]
    public class PrefabSet : ScriptableObject
    {
        [TextArea(1, 3)]
        [SerializeField] private string _description;

        [SerializeField] private List<PrefabSetEntry> _entries = new List<PrefabSetEntry>();

        public string Description => _description;
        public List<PrefabSetEntry> Entries => _entries;
        public int Count => _entries.Count;

        public int EnabledCount
        {
            get
            {
                int count = 0;
                foreach (PrefabSetEntry entry in _entries)
                {
                    if (entry != null && entry.Enabled && entry.Prefab != null)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        /// <summary>Elige una entrada activa al azar, respetando los pesos.</summary>
        public PrefabSetEntry PickRandom()
        {
            float total = 0f;
            foreach (PrefabSetEntry entry in _entries)
            {
                if (entry != null && entry.Enabled && entry.Prefab != null)
                {
                    total += entry.Weight;
                }
            }

            if (total <= 0f)
            {
                return null;
            }

            float roll = UnityEngine.Random.value * total;
            foreach (PrefabSetEntry entry in _entries)
            {
                if (entry == null || !entry.Enabled || entry.Prefab == null)
                {
                    continue;
                }

                roll -= entry.Weight;
                if (roll <= 0f)
                {
                    return entry;
                }
            }

            return null;
        }
    }
}
