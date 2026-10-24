using UnityEngine;

public class PressurePlate : MonoBehaviour
{
    [SerializeField] GameObject _object;
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.GetComponent<Player>()) _object.GetComponent<IActivable>().Activated();
    }
}
