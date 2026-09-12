using UnityEngine;

public static class StaticMethods
{
    public static bool InFOV(Transform startPos, Vector3 endPos, float viewRange, float viewAngle, LayerMask obstacle)
    {
        Vector3 dir = endPos - startPos.position;
        if (!InLOS(startPos.position, endPos, obstacle)) return false;
        if (dir.magnitude > viewRange) return false;
        if (Vector3.Angle(startPos.forward, dir) > viewAngle / 2) return false;
        return true;
    }

     static bool InLOS(Vector3 start, Vector3 end, LayerMask obstacle)
    {
        Vector3 dir = end - start;

        return !Physics.Raycast(start, dir.normalized, dir.magnitude, obstacle, QueryTriggerInteraction.Ignore);
    }

}
