using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gekko.PaintTools
{
    /// <summary>
    /// Una instancia dispersa. Es solo una transformada y un indice al prototipo: 40
    /// bytes, contra los cientos de bytes de YAML que ocupa un GameObject serializado en
    /// la escena con su Transform, su MeshFilter y su MeshRenderer.
    /// </summary>
    [Serializable]
    public struct ScatterInstance
    {
        public int PrototypeIndex;

        /// <summary>Posicion en espacio LOCAL del ScatterField.</summary>
        public Vector3 Position;

        public Quaternion Rotation;
        public Vector3 Scale;
    }

    /// <summary>
    /// Los props dispersos, guardados como datos y no como objetos de escena.
    ///
    /// Es la diferencia de fondo con Polybrush: Polybrush instancia GameObjects reales,
    /// asi que cada arbusto pintado se serializa entero en el .unity — con su Transform,
    /// su MeshFilter y su MeshRenderer. Mil arbustos son mil objetos que Unity carga,
    /// actualiza y guarda. Aca mil arbustos son 40 KB en un asset y un puñado de draw
    /// calls instanciados.
    ///
    /// El costo de esta decision: no son GameObjects, asi que no tienen collider ni
    /// scripts. Para los que necesiten, esta el boton "Materializar" del inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "ScatterData", menuName = "Gekko/Paint Tools/Scatter Data")]
    public class ScatterData : ScriptableObject
    {
        [Tooltip("Los prefabs que se pueden pintar. El pincel elige entre estos.")]
        [SerializeField] private List<GameObject> _prototypes = new List<GameObject>();

        [SerializeField] private List<ScatterInstance> _instances = new List<ScatterInstance>();

        public List<GameObject> Prototypes => _prototypes;
        public List<ScatterInstance> Instances => _instances;

        public int Count => _instances.Count;

        public void Clear()
        {
            _instances.Clear();
        }
    }
}
