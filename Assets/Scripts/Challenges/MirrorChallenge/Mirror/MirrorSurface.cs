using UnityEngine;

// Va en el GameObject del collider que refleja. Unity avisa los triggers al GameObject del collider y no a sus padres, y ese
// collider (la chapa) cuelga del pivot, no del GameObject que tiene el componente Mirror: este componente le pasa el aviso.
// Lo agrega Mirror solo, a partir de su campo _surface.
public class MirrorSurface : MonoBehaviour
{
    private Mirror _mirror;

    public void Initialize(Mirror mirror)
    {
        _mirror = mirror;
    }

    private void OnTriggerEnter(Collider other)
    {
        if(_mirror != null) _mirror.TryReflect(other);
    }
}
