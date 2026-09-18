using UnityEngine;

[CreateAssetMenu(fileName = "PurificationChallengeDataSO", menuName = "Scriptable Objects/PurificationChallengeDataSO")]
public class PurificationChallengeDataSO : ScriptableObject
{
    [Header("Re-corrupción de escarabajos")]
    [Tooltip("Segundos mínimos hasta que un escarabajo purificado vuelve a corromperse (mientras la planta viva).")]
    public float recorruptDelayMin = 3f;
    [Tooltip("Segundos máximos hasta que un escarabajo purificado vuelve a corromperse.")]
    public float recorruptDelayMax = 5f;

    [Header("Cierre")]
    [Tooltip("Duración de la cinemática de cierre (con el input bloqueado).")]
    public float completionBeatSeconds = 3f;
}
