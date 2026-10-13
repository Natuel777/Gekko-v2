using UnityEngine;

// Gate híbrido antes de la caída a la arena del jefe: SIEMPRE deja pasar, nunca bloquea. Al cruzarlo,
// avisa cuántos PurificationBerry tenés vs. el mínimo recomendado, y fija (una sola vez, acá) la
// reducción de golpes del jefe según cuántos lleves en ese momento (umbral pasivo, no se gastan).
[RequireComponent(typeof(Collider))]
public class PurificationGate : MonoBehaviour
{
    [Header("Jefe")]
    [SerializeField] private CorruptedGeckoBoss _boss;

    [Header("Arándano")]
    [Tooltip("Mismo NotificationSO asignado al prefab de PurificationBerry: su Name es la clave con la que se cuenta en CollectiblesRegister, y se reusa para mostrar el aviso.")]
    [SerializeField] private NotificationSO _berryData;

    [Header("Balance (placeholder)")]
    [Tooltip("Cantidad de arándanos recomendada antes de cruzar (solo informativo).")]
    [SerializeField] private int _recommendedCount = 3;
    [Tooltip("Cuántos arándanos hacen falta para restar 1 golpe necesario en cada ventana.")]
    [SerializeField] private int _berriesPerReduction = 1;
    [Tooltip("Reducción máxima de golpes (nunca deja una ventana en 0 golpes necesarios).")]
    [SerializeField] private int _maxReduction = 2;

    private bool _crossed;

    private void OnTriggerEnter(Collider other)
    {
        if(_crossed) return;
        if(!other.TryGetComponent(out Player _)) return;

        _crossed = true;

        int count = _berryData != null ? CollectiblesRegister.GetCollectibleCount(_berryData.Name) : 0;
        int reduction = Mathf.Min(_maxReduction, count / Mathf.Max(1, _berriesPerReduction));

        if(_boss != null) _boss.BlueberryHitReduction = reduction;

        if(_berryData != null && UIManager.Instance != null)
        {
            string msg = count >= _recommendedCount
                ? $"Arándanos de purificación: {count}/{_recommendedCount}. Listo para entrar."
                : $"Arándanos de purificación: {count}/{_recommendedCount} recomendados.";

            UIManager.Instance.notifications.ShowOrUpdate(_berryData, msg);
        }
    }
}
