using UnityEngine;

public class HeavyBeetleCollision
{
    private readonly HeavyBeetle _owner;

    public HeavyBeetleCollision(HeavyBeetle owner)
    {
        _owner = owner;
    }
    
    public void ArtificialTriggerEnter(Collider other)
    {
        if(!_owner.IsCharging) return;

        if(other.TryGetComponent<Player>(out Player player))
        {
            player.GetComponent<Rigidbody>().AddForce(_owner.chargeMovement.ChargeDirection * _owner.data.knockbackForce, ForceMode.Impulse);
            EventManager.Trigger<float>("OnPlayerDamaged", _owner.data.attackDamage);
            EventManager.Trigger<float>("OnCameraShake", _owner.data.shakeForce);
            _owner.SendEvent(CreatureEvent.ChargeHitPlayer);
            return;
        }

        if(((1 << other.gameObject.layer) & LayerMask.GetMask("Wall")) != 0)
            _owner.SendEvent(CreatureEvent.ChargeHitWall);
    }
}
