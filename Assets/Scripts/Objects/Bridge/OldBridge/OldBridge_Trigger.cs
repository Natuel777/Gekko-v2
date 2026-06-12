using UnityEngine;

public class OldBridge_Trigger : MonoBehaviour
{
    [SerializeField] private OldBridge _bridge;
    private void OnTriggerEnter(Collider other)
    {
        if(other.TryGetComponent(out OldBridge_Plank plank))
        {
            GameManager.Instance.Pj.PjTongue.ObjectLost();
            plank.Positioned();
            _bridge.PlankPositioned();
        }
    }
}
