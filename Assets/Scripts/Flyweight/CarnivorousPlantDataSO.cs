using UnityEngine;

[CreateAssetMenu(fileName = "CarnivorousPlantDataSO", menuName = "Scriptable Objects/CarnivorousPlantDataSO")]
public class CarnivorousPlantDataSO : ScriptableObject
{
    [Header("Config")]
    public float reactionDelay = 0.25f;
    public float detectionRange = 6f;

    [Header("Attack")]
    public float shootInterval = 1.5f;

    [Header("Purify")]
    public float purifyHealthCost = 20f;

    [Header("Aim")]
    public bool rotateToPlayer = true;
    public float rotationSpeed = 5f;

    [Header("Bite")]
    public float biteRange = 2f;
    public float biteDamage = 8f;
    public float biteInterval = 1.2f;
    public float biteKnockbackForce = 8f;
    public float biteLungeDistance = 0.6f;
    public float biteLungeDuration = 0.2f;
}
