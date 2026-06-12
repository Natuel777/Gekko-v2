using UnityEngine;

public class BeaverBridge_Plank : BringgableObject, IRespawneable
{
    [SerializeField] private Vector3 _respawnPoint;
    [SerializeField] private Quaternion _respawnRot;
    private MeshRenderer _mesh;
    private Collider _col;
    private void Start()
    {
        _mesh = GetComponentInChildren<MeshRenderer>();
        _col = GetComponent<Collider>();
        LevelOneManager.Instance.OnBeaverMission += Activate;
    }
    private void Activate()
    {
        _mesh.enabled = true;
        _col.enabled = true;
        _rb.useGravity = true;
    }
    public void Positioned()
    {
        Destroy(gameObject);
    }
    private void OnDisable()
    {
        LevelOneManager.Instance.OnBeaverMission -= Activate;
    }

    public void Respawn()
    {
        _rb.linearVelocity = Vector3.zero;
        transform.position = _respawnPoint;
        transform.rotation = _respawnRot;
    }
}

