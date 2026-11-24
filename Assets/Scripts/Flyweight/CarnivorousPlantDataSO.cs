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
    [Tooltip("Segundos que la planta pausa su ataque tras un golpe no final. Si la planta tiene PlantHurtReaction, la pausa dura como mínimo lo que dura esa animación.")]
    public float hurtStaggerSeconds = 0f;

    [Header("Aim")]
    public bool rotateToPlayer = true;
    public float rotationSpeed = 5f;

    [Header("Bite")]
    [Tooltip("Rango en el que el player queda a tiro del ataque cuerpo a cuerpo: es lo que dispara la preparación del mordisco. Lo que pega es el BoxCollider del GameObject HitBox del prefab (si la planta tiene uno).")]
    public float biteRange = 2f;
    public float biteDamage = 8f;
    public float biteInterval = 1.2f;
    public float biteKnockbackForce = 8f;
    public float biteLungeDistance = 0.6f;
    public float biteLungeDuration = 0.2f;
    [Tooltip("Si está activo, a la planta solo se la puede dañar y purificar durante el Bite Interval (entre mordiscos): no mientras se prepara, no en plena embestida, ni escupiendo o fuera del rango de mordisco. Apagado = se la puede golpear siempre, salvo en plena embestida.")]
    public bool vulnerableOnlyInBiteInterval = false;

    [Header("Purified")]
    public Material purifiedMaterial;

    [Header("Damaged")]
    [Tooltip("Material que se le pone a la planta cuando Gekko la daña sin purificarla (golpe que cuenta, no el bloqueado), durante Damaged Material Seconds. Después vuelve al material que tenía. Vacío = la planta no cambia de material (hoy, solo lo usa el boss).")]
    public Material damagedMaterial;
    [Tooltip("Segundos que dura el material de daño. 0 = no se aplica.")]
    [Min(0f)]
    public float damagedMaterialSeconds = 0.5f;

    [Header("Painted purification (hit by hit)")]
    [Tooltip("Cada golpe que cuenta pinta la planta de abajo hacia arriba con el material purificado (1/3 por golpe con 3 golpes), con el mismo shader que las torretas. El golpe final termina de pintarla, hace un destello y deja Purified Material. Reemplaza al material de daño (Damaged Material).")]
    public bool paintOnHit = false;
    [Tooltip("Shader Gekko/Purification/Paint Transition.")]
    public Shader paintShader;
    [Tooltip("Segundos que tarda en subir cada tramo del pintado.")]
    [Min(0.05f)]
    public float paintStepSeconds = 0.6f;
    [Tooltip("Color de la franja que sube (el dorado de la onda de purificación).")]
    [ColorUsage(false, true)]
    public Color paintEdgeColor = new Color(1f, 0.8f, 0.3f, 1f);
    [Min(0f)]
    public float paintEdgeIntensity = 2f;
    [Tooltip("Brillo del destello de cada golpe (toda la planta, más fuerte en la silueta).")]
    [Min(0f)]
    public float paintHitFlashIntensity = 1.2f;
    [Tooltip("Brillo del destello final, cuando termina de pintarse.")]
    [Min(0f)]
    public float paintFinalPulseIntensity = 1.6f;
    [Tooltip("Partícula que se crea donde pega la lengua en cada golpe que cuenta (se destruye sola).")]
    public GameObject paintImpactSparks;
    [Tooltip("Partícula que acompaña a la franja mientras sube (se crea una vez, como hija de la planta).")]
    public GameObject paintEdgeSparks;
    [Tooltip("Segundos que tarda el piso alrededor (la PurifyRegion que contiene a la planta) en limpiarse cada tramo. Sin región, no hace nada.")]
    [Min(0.05f)]
    public float paintGroundStepSeconds = 1.2f;
    [Tooltip("Escala de tiempo de la cámara lenta del golpe final (1 = sin cámara lenta).")]
    [Range(0.05f, 1f)]
    public float paintFinalSlowMoScale = 0.35f;
    [Tooltip("Segundos reales que dura la cámara lenta del golpe final.")]
    [Min(0f)]
    public float paintFinalSlowMoSeconds = 0.5f;

    [Header("Hit sound")]
    [Tooltip("Cuánto sube el tono del sonido de golpe con cada golpe (0.15 = +15% por golpe). 0 = siempre igual.")]
    [Min(0f)]
    public float hurtPitchStep = 0f;
}
