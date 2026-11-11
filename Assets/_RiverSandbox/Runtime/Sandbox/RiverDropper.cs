using System.Collections.Generic;
using UnityEngine;

// Deja caer objetos desde su posicion cada cierto tiempo (para mostrar salpicaduras). Solo sandbox.
[AddComponentMenu("Gekko/River/Sandbox/River Dropper")]
public class RiverDropper : MonoBehaviour
{
    [SerializeField] GameObject[] _prefabs;
    [SerializeField, Min(0.1f)] float _interval = 1.4f;
    [SerializeField, Min(0f)] float _radius = 3f;
    [SerializeField, Min(0.5f)] float _lifetime = 7f;

    readonly List<(GameObject go, float dieAt)> _alive = new List<(GameObject, float)>();
    float _timer = 0.5f;

    void Update()
    {
        for(int i = _alive.Count - 1; i >= 0; i--)
        {
            if(_alive[i].go == null || Time.time >= _alive[i].dieAt)
            {
                if(_alive[i].go != null)
                    Destroy(_alive[i].go);
                _alive.RemoveAt(i);
            }
        }

        _timer -= Time.deltaTime;
        if(_timer > 0f || _prefabs == null || _prefabs.Length == 0)
            return;

        _timer = _interval;
        GameObject prefab = _prefabs[Random.Range(0, _prefabs.Length)];
        if(prefab == null)
            return;

        Vector2 offset = Random.insideUnitCircle * _radius;
        Vector3 position = transform.position + new Vector3(offset.x, 0f, offset.y);
        GameObject go = Instantiate(prefab, position, Random.rotation);
        _alive.Add((go, Time.time + _lifetime));
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * 4f);
    }
}
