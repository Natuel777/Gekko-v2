using UnityEngine;

// Camara de la escena de prueba del rio (solo sandbox, no usar en niveles).
//   Click derecho + arrastrar: orbitar     Rueda: zoom
//   Click izquierdo: onda en el agua        E: soltar un objeto flotante donde apunta el mouse
[AddComponentMenu("Gekko/River/Sandbox/River Sandbox Camera")]
public class RiverSandboxCamera : MonoBehaviour
{
    [SerializeField] Transform _target;
    [SerializeField] float _distance = 14f;
    [SerializeField] Vector2 _distanceRange = new Vector2(3f, 45f);
    [SerializeField] float _yaw = 30f;
    [SerializeField] float _pitch = 38f;
    [SerializeField] float _orbitSpeed = 4f;
    [SerializeField] float _followSharpness = 6f;
    [SerializeField] GameObject[] _dropPrefabs;
    [SerializeField] float _dropHeight = 2.5f;

    Vector3 _focus;

    void Start()
    {
        _focus = _target != null ? _target.position : transform.position + transform.forward * _distance;
    }

    void LateUpdate()
    {
        if(Input.GetMouseButton(1))
        {
            _yaw += Input.GetAxis("Mouse X") * _orbitSpeed;
            _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * _orbitSpeed, 5f, 85f);
        }

        _distance = Mathf.Clamp(_distance * (1f - Input.mouseScrollDelta.y * 0.1f), _distanceRange.x, _distanceRange.y);

        if(_target != null)
            _focus = Vector3.Lerp(_focus, _target.position, 1f - Mathf.Exp(-_followSharpness * Time.deltaTime));

        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        transform.SetPositionAndRotation(_focus - rotation * Vector3.forward * _distance, rotation);

        if(Input.GetMouseButtonDown(0))
            TryRipple();
        if(Input.GetKeyDown(KeyCode.E))
            TryDrop();
    }

    bool TryGetRiverPoint(out SplineRiver river, out Vector3 point)
    {
        river = null;
        point = default;

        Camera cam = GetComponent<Camera>();
        if(cam == null)
            return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        // Incluye la capa Ignore Raycast, donde viven los volumenes del rio.
        if(!Physics.Raycast(ray, out RaycastHit hit, 300f, ~0, QueryTriggerInteraction.Collide))
            return false;

        foreach(SplineRiver candidate in SplineRiver.All)
        {
            if(candidate.Sample(hit.point, out SplineRiver.RiverSample sample) && sample.Inside)
            {
                river = candidate;
                // Interseccion del rayo con el plano de la superficie en ese punto.
                var plane = new Plane(Vector3.up, new Vector3(0f, sample.SurfaceY, 0f));
                point = plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : sample.SurfacePoint;
                return true;
            }
        }

        return false;
    }

    void TryRipple()
    {
        if(TryGetRiverPoint(out SplineRiver river, out Vector3 point))
            river.AddSplash(point, 1f);
    }

    void TryDrop()
    {
        if(_dropPrefabs == null || _dropPrefabs.Length == 0)
            return;
        if(!TryGetRiverPoint(out _, out Vector3 point))
            return;

        GameObject prefab = _dropPrefabs[Random.Range(0, _dropPrefabs.Length)];
        if(prefab != null)
            Instantiate(prefab, point + Vector3.up * _dropHeight, Random.rotation);
    }
}
