using UnityEngine;

// Datos que comparten todos los desafíos de la planta (ver Challenge). Cada tipo de desafío puede heredar de este SO para
// sumar los suyos (ej. PurificationChallengeDataSO agrega la re-corrupción de escarabajos).
[CreateAssetMenu(fileName = "ChallengeDataSO", menuName = "Scriptable Objects/ChallengeDataSO")]
public class ChallengeDataSO : ScriptableObject
{
    [Header("Finish")]
    [Tooltip("Duración de la cinemática de cierre (con el input bloqueado).")]
    public float completionBeatSeconds = 3f;
}
