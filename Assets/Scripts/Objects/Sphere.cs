using UnityEngine;

public class Sphere : BaseObject, IDamageable
{
    public void Damage(float dmg)
    {
        GameManager.Instance.Pj.ChangeAim();
        Destroy(gameObject);
    }
}
