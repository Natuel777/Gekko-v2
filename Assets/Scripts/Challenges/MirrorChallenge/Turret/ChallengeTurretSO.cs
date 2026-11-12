using UnityEngine;

[CreateAssetMenu(fileName = "ChallengeTurretSO", menuName = "Scriptable Objects/ChallengeTurretSO")]
public class ChallengeTurretSO : ScriptableObject
{
    [Header("Rotation")]
    public float yRotationSpeed;
    //public float xRotationSpeed;

    [Header("Shooting Behaviour")]
    public float fireRate;
    public float detectionRange;
}
