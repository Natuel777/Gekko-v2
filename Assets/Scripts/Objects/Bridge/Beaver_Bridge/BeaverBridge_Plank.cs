using System;
using UnityEngine;

public class BeaverBridge_Plank : CarriableObject, IRespawneable, IParticleSystemTarget
{
    private Vector3 _respawnPoint;
    private Quaternion _respawnRot;
    [SerializeField] private ParticleSystem _particle;
    public ParticleSystem Indicator => _particle;
    private bool _suscribed;
    public bool CanBeTargeted => _canInteract;

    private void Start()
    {
        _respawnPoint = transform.position;
        _respawnRot = transform.rotation;
        _suscribed = true;
        Suscribe();
        _canInteract = false;
    }

    private void Suscribe()
    {
        if (LevelOneManager.Instance == null) return;
        LevelOneManager.Instance.OnBeaverMission -= Activate;
        LevelOneManager.Instance.OnBeaverMission += Activate;
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
    private void OnEnable()
    {
        if (_suscribed) Suscribe();
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

