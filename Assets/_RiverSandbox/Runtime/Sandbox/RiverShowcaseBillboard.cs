using UnityEngine;

// Hace que un cartel (texto 3D) mire siempre a la camara. Solo sandbox.
[AddComponentMenu("Gekko/River/Sandbox/River Showcase Billboard")]
public class RiverShowcaseBillboard : MonoBehaviour
{
    void LateUpdate()
    {
        Camera cam = Camera.main;
        if(cam != null)
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);
    }
}
