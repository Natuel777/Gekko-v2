using System;
using UnityEngine;

// Relay de triggers: SplineRiver crea este componente (en Play) sobre el Rigidbody kinematic que
// agrupa los BoxCollider trigger del rio, y le reenvia las entradas/salidas.
[AddComponentMenu("")]
public class RiverVolume : MonoBehaviour
{
    [NonSerialized] public SplineRiver River;

    void OnTriggerEnter(Collider other)
    {
        if(River != null)
            River.HandleVolumeEnter(other);
    }

    void OnTriggerExit(Collider other)
    {
        if(River != null)
            River.HandleVolumeExit(other);
    }
}
