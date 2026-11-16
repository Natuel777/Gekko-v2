using UnityEngine;

// Mueve el rig de partículas ambientales (polen, luciérnagas, hojas) junto con la cámara, así hay ambiente en todo el
// nivel sin llenar el mapa de emisores. Las partículas simulan en World: una vez emitidas quedan quietas en el mundo y
// solo la caja de emisión acompaña a la cámara (no se "arrastran" con ella).
public class AmbientParticlesFollow : MonoBehaviour
{
    [Tooltip("Objeto a seguir. Vacío = Camera.main.")]
    [SerializeField] private Transform _target;
    [Tooltip("Desplazamiento respecto del objetivo (en mundo).")]
    [SerializeField] private Vector3 _offset = Vector3.zero;

    private void LateUpdate()
    {
        if(_target == null)
        {
            Camera cam = Camera.main;
            if(cam == null) return;
            _target = cam.transform;
        }

        transform.position = _target.position + _offset;
    }
}
