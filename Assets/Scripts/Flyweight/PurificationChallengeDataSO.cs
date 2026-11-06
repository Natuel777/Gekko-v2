using UnityEngine;

// Datos del desafío de núcleos. completionBeatSeconds (la duración de la cinemática de cierre) vive en ChallengeDataSO.
[CreateAssetMenu(fileName = "PurificationChallengeDataSO", menuName = "Scriptable Objects/PurificationChallengeDataSO")]
public class PurificationChallengeDataSO : ChallengeDataSO
{
    [Header("Beetle Re-corruption")]
    [Tooltip("Segundos mínimos hasta que un escarabajo purificado vuelve a corromperse (mientras la planta viva).")]
    public float recorruptDelayMin = 3f;
    [Tooltip("Segundos máximos hasta que un escarabajo purificado vuelve a corromperse.")]
    public float recorruptDelayMax = 5f;
}
