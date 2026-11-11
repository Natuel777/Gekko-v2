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
}
