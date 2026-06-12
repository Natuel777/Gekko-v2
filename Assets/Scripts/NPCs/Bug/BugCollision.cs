using UnityEngine;

public class BugCollision
{
    private Bug _owner;

    public BugCollision(Bug owner) {_owner = owner;}

    public void ArtificialTriggerEnter(Collider other)
    {
        if(other.gameObject.TryGetComponent<Player>(out Player player) && _owner.IsAttacking)
        { 
            if(player != null)
            {
                EventManager.Trigger<float>("OnPlayerDamagedByBug", _owner.AttackDamage);
            }
        }
    }
}