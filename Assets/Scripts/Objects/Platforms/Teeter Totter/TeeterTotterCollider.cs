using UnityEngine;

public class TeeterTotterCollider : MonoBehaviour
{
    private TeeterTotterPlatform _platform;

    public void SetParent(TeeterTotterPlatform parent) { _platform = parent; }
    private void OnCollisionEnter(Collision col)
    {
        if (col.gameObject.TryGetComponent(out Player player))
            _platform.PlayerOnPlatform = player.transform;
    }

    private void OnCollisionExit(Collision col)
    {
        if (col.gameObject.TryGetComponent(out Player player))
            _platform.PlayerOnPlatform = null;
    }
}
