using System.Collections.Generic;
using UnityEngine;

// Larga muérdagos en la arena al arrancar cada tanda de ataque del jefe (CorruptedGeckoBoss.TandaStarted),
// en puntos al azar entre _spawnPoints (colocados a mano en el Inspector, deben estar sobre piso válido
// para que el HeavyBeetle que sueltan pueda caminar). No repite un punto ya ocupado por un muérdago activo.
public class MistletoeSpawner : MonoBehaviour
{
    [Header("Jefe")]
    [SerializeField] private CorruptedGeckoBoss _boss;

    [Header("Prefabs")]
    [SerializeField] private Mistletoe _mistletoePrefab;
    [SerializeField] private HeavyBeetle _beetlePrefab;

    [Header("Puntos de aparición")]
    [Tooltip("Posiciones de la arena donde puede aparecer un muérdago. Colocar sobre piso válido (el HeavyBeetle necesita piso debajo para caminar).")]
    [SerializeField] private Transform[] _spawnPoints;

    private readonly HashSet<Transform> _occupied = new();

    private void OnEnable()
    {
        if(_boss != null) _boss.TandaStarted += SpawnForTanda;
    }

    private void OnDisable()
    {
        if(_boss != null) _boss.TandaStarted -= SpawnForTanda;
    }

    private void SpawnForTanda()
    {
        if(_mistletoePrefab == null || _spawnPoints == null) return;

        int count = Mathf.Min(_boss.CurrentPhase.mistletoesPerTanda, _spawnPoints.Length);

        for(int i = 0; i < count; i++)
        {
            Transform point = PickFreePoint();
            if(point == null) break;

            SpawnAt(point);
        }
    }

    private Transform PickFreePoint()
    {
        List<Transform> free = new();

        foreach(Transform t in _spawnPoints)
            if(t != null && !_occupied.Contains(t)) free.Add(t);

        if(free.Count == 0) return null;

        return free[Random.Range(0, free.Count)];
    }

    private void SpawnAt(Transform point)
    {
        _occupied.Add(point);

        Mistletoe instance = Instantiate(_mistletoePrefab, point.position, point.rotation);
        instance.Init(_beetlePrefab, _ => _occupied.Remove(point));
    }
}
