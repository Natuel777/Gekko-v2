using UnityEngine;

public class Manager : MonoBehaviour
{
    [SerializeField] private EnergyBurstAnimation energyBurst;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            energyBurst.Play();
        }
    }
}
