using UnityEngine;

public class GekkoCollision
{
    private Player _owner;

    #region TriggerStay
    private float _damageTimer, _damageInterval = 0.5f;
    #endregion
    
    public GekkoCollision(Player owner)
    {
        _owner = owner;
    }

    public void ArtificialOnTriggerEnter(Collider other)
    {
        if(other.TryGetComponent(out Collectible collectible))
        {
            collectible.Grab();
            _owner.SlurpSound();
        }

        if(other.CompareTag("Nexus"))
        {
            Debug.Log("nexussss");
            _owner.PjController.SetSlipperySurface(true);
            return;
        }

        if(other.CompareTag("Lava"))
        {
            EventManager.Trigger<float>("OnPlayerDamaged", GameManager.Instance.lavaDamage);
            return;
        }
    }

    public void ArtificialOnTriggerExit(Collider other)
    {
        if(other.CompareTag("Nexus"))
        {
            _owner.PjController.SetSlipperySurface(false);
        }
    }

    public void ArtificialOnTriggerStay(Collider other)
    {
        if(other.CompareTag("Lava"))
        {
            _damageTimer += Time.deltaTime;

            if(_damageTimer >= _damageInterval)
            {
                _damageTimer = 0f;
                EventManager.Trigger<float>("OnPlayerDamaged", 1f);
            }

            return;
        }
    }
}
