using UnityEngine;

[CreateAssetMenu(fileName = "CorruptedGeckoBossDataSO", menuName = "Scriptable Objects/CorruptedGeckoBossDataSO")]
public class CorruptedGeckoBossDataSO : ScriptableObject
{
    [Header("Detección")]
    [Tooltip("Distancia a la que Gekko arranca la pelea (entra a la fase 1).")]
    public float detectionRange = 10f;

    [System.Serializable]
    public class PhaseConfig
    {
        [Tooltip("Golpes de ataque en la tanda antes de abrir la ventana de purificación.")]
        public int attacksPerTanda = 3;
        [Tooltip("Segundos entre cada golpe de la tanda.")]
        public float attackInterval = 1.5f;
        [Tooltip("Segundos que dura la ventana de purificación expuesta.")]
        public float windowDuration = 4f;
        [Tooltip("Golpes de lengua necesarios para limpiar la ventana (antes de restar la reducción por arándanos).")]
        public int hitsRequired = 2;
        [Tooltip("Muérdagos que aparecen en la arena al arrancar cada tanda de esta fase (0 = ninguno).")]
        public int mistletoesPerTanda = 1;
    }

    [Header("Fases (suben de dificultad)")]
    public PhaseConfig[] phases = new PhaseConfig[]
    {
        new PhaseConfig { attacksPerTanda = 2, attackInterval = 1.5f, windowDuration = 5f, hitsRequired = 1 },
        new PhaseConfig { attacksPerTanda = 3, attackInterval = 1.2f, windowDuration = 4f, hitsRequired = 2 },
        new PhaseConfig { attacksPerTanda = 4, attackInterval = 1f, windowDuration = 3f, hitsRequired = 3 },
    };
}
