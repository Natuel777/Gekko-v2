using UnityEngine;

public class ChallengeTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if(other.GetComponent<Player>())
        {
            LevelOneManager.Instance.ChallengeStart();
        }
    }
}
