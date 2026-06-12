using UnityEngine;

public class BugBullet : ShooteableObject
{
    [SerializeField] private ParticleSystem _hitEffect;
    private Vector3 _direction;
    [SerializeField] private float _lifetime = 3.5f;
    private float _timer;

    public void SetDirection(Vector3 direction) {_direction = direction;}

    private void OnEnable() {_timer = 0f;}

    private void Update()
    {
        if(data == null)
            return;

        transform.position += _direction * data.speed * Time.deltaTime;
        _timer += Time.deltaTime;

        if(_timer >= _lifetime)
            ShooteableObjectFactory.Instance?.ReturnBullet(this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.TryGetComponent(out Player player))
        {
            EventManager.Trigger<float>("OnPlayerDamaged", data.damage);
            if(_hitEffect != null) _hitEffect.Play();
            ShooteableObjectFactory.Instance?.ReturnBullet(this);
        }
    }
}
