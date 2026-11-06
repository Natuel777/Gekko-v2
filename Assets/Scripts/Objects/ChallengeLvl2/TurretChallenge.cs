using UnityEngine;

public class TurretChallenge : MonoBehaviour
{
    private Transform _tToShoot;
    private bool _purified;

    [SerializeField] private float _shootSpeed;
    [SerializeField] private float _shootRange;
    [SerializeField] private float _shootAngle;

    [SerializeField] private Bullet _bulletPrefab;


    private void Start()
    {
        _tToShoot = GameManager.Instance.Pj.transform;
    }
    private void Shoot()
    {

    }
}
