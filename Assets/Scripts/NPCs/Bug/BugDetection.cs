using UnityEngine;

public class BugDetection
{
    private MonoBehaviour _owner;
    private Transform _target;
    private float _range;

    public BugDetection(MonoBehaviour owner, Transform target, float range)
    {
        _owner = owner;
        _target = target;
        _range = range;
    }

    public bool IsTargetInRange()
    {
        if(_target == null) return false;

        float sqrDistance =
            (_target.position - _owner.transform.position).sqrMagnitude;

        return sqrDistance <= _range * _range;
    }
}