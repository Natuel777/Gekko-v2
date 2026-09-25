using UnityEngine;

[CreateAssetMenu(fileName = "CarnivorousPlantDataSO", menuName = "Scriptable Objects/CarnivorousPlantDataSO")]
public class CarnivorousPlantDataSO : ScriptableObject
{
    [Header("Config")]
    public float detectionRange = 6f;

    [Header("Attack")]
    public float shootInterval = 1.5f;

    [Header("Purify")]
    public float purifyHealthCost = 20f;

    [Header("Boss (purification challenge)")]
    [Tooltip("Golpes de lengua para purificarla. 1 = comportamiento original de las plantas chicas.")]
    public int hitsToPurify = 1;
    [Tooltip("Daño al player en los golpes NO finales (el golpe final cobra Purify Health Cost).")]
    public float hitHealthCost = 0f;
    [Tooltip("Segundos que la planta pausa su ataque tras un golpe no final.")]
    public float hurtStaggerSeconds = 0f;

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

    [Header("Purified")]
    public Material purifiedMaterial;
}
