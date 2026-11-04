using UnityEngine;

// Lo agrega LittleBarricade en Awake a los colliders de sus hijos: sin Rigidbody en la barricada, los choques
// contra un hijo solo le llegan a ese hijo. Este relay se los pasa a la barricada.
public class LittleBarricadeTouchRelay : MonoBehaviour
{
    public LittleBarricade Owner { get; set; }

    private void OnCollisionEnter(Collision collision) { if(Owner != null) Owner.HandleTouch(collision); }
    private void OnCollisionStay(Collision collision) { if(Owner != null) Owner.HandleTouch(collision); }
}
