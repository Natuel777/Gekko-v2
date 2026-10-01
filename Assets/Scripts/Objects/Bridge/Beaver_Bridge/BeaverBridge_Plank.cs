using UnityEngine;

public class BeaverBridge_Plank : CarriableObject, IRespawneable, IParticleSystemTarget
{
    private Vector3 _respawnPoint;
    private Quaternion _respawnRot;
    [SerializeField] private ParticleSystem _particle;
    public ParticleSystem Indicator => _particle;

    public bool CanBeTargeted => _canInteract;

    private void Start()
    {
        _respawnPoint = transform.position;
        _respawnRot = transform.rotation;
        LevelOneManager.Instance.OnBeaverMission += Activate;
        _canInteract = false;
    }
    private void Activate()
    {
        _canInteract = true;
        gameObject.layer = 9;
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

