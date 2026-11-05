using UnityEngine;

public class BugBullet : ShooteableObject
{
    [SerializeField] private ParticleSystem _hitEffect;
    private Vector3 _direction;
    [SerializeField] private float _lifetime = 3.5f;
    private float _timer;

    [Header("Environment Hit")]
    [Tooltip("Con qué capas choca la bala. Los triggers se ignoran siempre y Gekko lo resuelve OnTriggerEnter (el daño).")]
    [SerializeField] private LayerMask _hitLayers = Physics.DefaultRaycastLayers;

    public void SetDirection(Vector3 direction) 
    {
        _direction = direction;
        transform.rotation = Quaternion.LookRotation(direction);
    }

    private void OnEnable() {_timer = 0f;}

    private void Update()
    {
        if(data == null)
            return;

        Vector3 step = _direction * data.speed * Time.deltaTime;

        if(TryHitEnvironment(step, out Vector3 impactPoint))
        {
            transform.position = impactPoint;
            Impact();
            return;
        }

        transform.position += step;
        _timer += Time.deltaTime;

        if(_timer >= _lifetime)
            ShooteableObjectFactory.Instance?.ReturnBullet(this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.TryGetComponent(out Player player))
        {
            Debug.Log("La yuta, la yuta...");
            EventManager.Trigger<float>("OnPlayerDamaged", data.damage);

            if(_hitEffect != null) _hitEffect.Play();
            
            ShooteableObjectFactory.Instance?.ReturnBullet(this);
        }
    }

    private void OnCollisionEnter(Collision other)
    {
        Impact();
    }

    private void Impact()
    {
        if(TryResolveService())
        {
            GenericPS ps = PSFactory.Create(PSType.VenomHit, transform.position, transform.rotation);
            ps?.Initialize();
        }

        ShooteableObjectFactory.Instance?.ReturnBullet(this);
    }

    private bool TryHitEnvironment(Vector3 step, out Vector3 impactPoint)
    {
        impactPoint = default;

        float distance = step.magnitude;

        if(distance < 0.0001f) return false;

        if(!Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, distance, _hitLayers, QueryTriggerInteraction.Ignore))
            return false;

        if(hit.collider.GetComponentInParent<Player>() != null) return false;

        impactPoint = hit.point;
        return true;
    }
}
