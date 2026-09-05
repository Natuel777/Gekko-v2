using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gekko.GrassSandbox
{
    /// <summary>
    /// Una brizna pintada. Se guarda el minimo necesario para reconstruir la geometria:
    /// la malla no se guarda nunca, se rehace desde estos datos.
    /// </summary>
    [Serializable]
    public struct GrassBladeData
    {
        /// <summary>Posicion en espacio LOCAL del GrassField, no del mundo.</summary>
        public Vector3 Position;

        /// <summary>Normal del suelo donde se pinto. Da la orientacion y la iluminacion.</summary>
        public Vector3 Normal;

        public float Height;
        public float Width;

        /// <summary>Giro alrededor de la normal, para que no miren todas al mismo lado.</summary>
        public float Yaw;

        /// <summary>Aleatorio 0..1 por brizna: desfasa el viento y varia el color.</summary>
        public float Variation;
    }

    /// <summary>
    /// Las briznas pintadas viven en un asset aparte, no en la escena.
    ///
    /// Es a proposito: un campo de pasto son decenas de miles de briznas, y meterlas en
    /// el .unity haria la escena lentisima de abrir y volveria ilegible cualquier diff
    /// de git — con varias ramas en paralelo eso se paga caro. En un asset propio, la
    /// escena solo guarda una referencia.
    /// </summary>
    [CreateAssetMenu(fileName = "GrassData", menuName = "Gekko/Grass/Grass Data")]
    public class GrassData : ScriptableObject
    {
        [SerializeField] private List<GrassBladeData> _blades = new List<GrassBladeData>();

        public List<GrassBladeData> Blades => _blades;

        public int Count => _blades.Count;

        public void Clear()
        {
            _blades.Clear();
        }
    }
}
