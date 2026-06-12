using UnityEngine;

public class BeaverTrigger : MonoBehaviour
{
    [SerializeField] private PuzzleBeaver _beaver;

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<Player>()) _beaver.StartAnim();
    }
}
