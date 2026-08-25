using UnityEngine;

[CreateAssetMenu(fileName = "HeavyBeetledataSO", menuName = "Scriptable Objects/HeavyBeetledataSO")]
public class HeavyBeetledataSO : ScriptableObject
{
    [Header("Movement")]
    public float patrolSpeed = 1.5f;
    public float chargeSpeed = 100f;
    public float chargeMaxDist = 10f;   // distancia max de fallo de embestida

    [Header("Timers")]
    public float recalibrateDelay = 1.2f;
    public float dazeDuration = 4f;

    [Header("Daze")]
    public float flipSpeed = 180f; // deg/seg; 180 => ~1s para darse vuelta, ~1s para enderezarse

    [Header("Detection")]
    public float detectionRange = 6f;
    public float groundCheckDistance = 1.5f; // largo del ray hacia abajo para detectar borde

    [Header("Combat")]
    public float attackDamage = 20f;
    public float knockbackForce = 12f;
    public float shakeForce = 1f;
    public float wanderSpeed = 1.5f;
    public float changeDirTime = 3f;
    public float rotationSpeed = 5f;
}
