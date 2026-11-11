using System.Collections.Generic;
using UnityEngine;

// Suelta objetos flotantes rio arriba cada tanto y los recicla al llegar al final (solo sandbox).
[AddComponentMenu("Gekko/River/Sandbox/River Sandbox Spawner")]
public class RiverSandboxSpawner : MonoBehaviour
{
    [SerializeField] SplineRiver _river;
    [SerializeField] GameObject[] _prefabs;
    [SerializeField, Min(0.1f)] float _interval = 2.5f;
    [SerializeField, Min(1)] int _maxAlive = 14;
    [SerializeField] float _spawnDistance = 1.5f;
    [SerializeField] float _endMargin = 2.5f;
    [SerializeField] float _dropHeight = 0.6f;
    [SerializeField, Tooltip("Para rios con corriente invertida: aparecen al final y se reciclan al inicio.")]
    bool _spawnAtEnd;
    [SerializeField, Tooltip("Rio cerrado (loop): nunca se reciclan por llegar al final.")]
    bool _neverRecycle;

    readonly List<GameObject> _alive = new List<GameObject>();
    float _timer;

    void Update()
    {
        if(_river == null || _prefabs == null || _prefabs.Length == 0)
            return;

        for(int i = _alive.Count - 1; i >= 0; i--)
        {
            GameObject go = _alive[i];
            if(go == null)
            {
                _alive.RemoveAt(i);
                continue;
            }

            bool reachedEnd = !_neverRecycle
                && _river.Sample(go.transform.position, out SplineRiver.RiverSample sample)
                && (_spawnAtEnd
                    ? sample.DistanceAlong < _endMargin
                    : sample.DistanceAlong > _river.TotalLength - _endMargin);
            if(reachedEnd || go.transform.position.y < -30f)
            {
                Destroy(go);
                _alive.RemoveAt(i);
            }
        }

        _timer -= Time.deltaTime;
        if(_timer > 0f || _alive.Count >= _maxAlive)
            return;

        _timer = _interval;
        float distance = _spawnAtEnd ? _river.TotalLength - _spawnDistance : _spawnDistance;
        Vector3 point = _river.PointAt(distance, Random.Range(0.25f, 0.75f));
        GameObject prefab = _prefabs[Random.Range(0, _prefabs.Length)];
        if(prefab != null)
            _alive.Add(Instantiate(prefab, point + Vector3.up * _dropHeight, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)));
    }
}
