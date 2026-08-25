using UnityEngine;

[CreateAssetMenu(fileName = "BugDataSO", menuName = "Scriptable Objects/BugDataSO")]
public class BugDataSO : ScriptableObject
{
    [Header("Config")]
    public float attackDamage = 10f;
    public float detectionRange = 5f;

    [Header("Flee")]
    public float fleeSpeed;
    public float fleeRandomness;

    [Header("Idle")]
    public float wanderSpeed;
    public float changeDirTime;

    [Header("Observe")]
    public float observeRotationSpeed;

    [Header("Defensive")]
    public float defenseThreshold;
    public float defensiveSpeed;
    public float defenseDuration = 7f;

    [Header("Reactions")]
    public float fleeChance = 0.4f;
    public float defensiveChance = 0.3f;
    public float observeChance = 0.3f;
    public float defensiveFlyingChance = 0.3f;
}
