using System.Collections;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class LegsManager : MonoBehaviour
{
    #region Variables
    [Header("<color=green>Legs</color>")]
    [SerializeField] private Leg _RA;
    [SerializeField] private Leg _LA;
    [SerializeField] private Leg _RL;
    [SerializeField] private Leg _LL;

    [Header("<color=green>Body</color>")]
    [SerializeField] private Transform _bodyTransform;
    [SerializeField] private float _bodySmoothing = 5f;
    private float _targetWeight = 1f;
    private bool _isMoving = false;
    public bool IsOnSurface => _RA.IsOnSurface || _LA.IsOnSurface || _RL.IsOnSurface || _LL.IsOnSurface;
    public bool IsMoving { set { _isMoving = value; } }
    public Vector3 RATargetPos => _RA.TargetPos;
    public Vector3 LATargetPos => _LA.TargetPos;
    public Vector3 RLTargetPos => _RL.TargetPos;
    public Vector3 LLTargetPos => _LL.TargetPos;

    public Vector3 RASurfaceNormal => _RA.SurfaceNormal;
    public Vector3 LASurfaceNormal => _LA.SurfaceNormal;
    public Vector3 RLSurfaceNormal => _RL.SurfaceNormal;
    public Vector3 LLSurfaceNormal => _LL.SurfaceNormal;
    public Vector3 SurfaceNormal
    {
        get
        {
            Vector3 sum = Vector3.zero;
            int count = 0;

            // Solo incluimos patas que est�n en superficie
            if (_RA.IsOnSurface) { sum += _RA.SurfaceNormal * 2f; count++; }
            if (_LA.IsOnSurface) { sum += _LA.SurfaceNormal * 2f; count++; }
            if (_RL.IsOnSurface) { sum += _RL.SurfaceNormal; count++; }
            if (_LL.IsOnSurface) { sum += _LL.SurfaceNormal; count++; }

            if (count == 0) return Vector3.up;

            Vector3 avg = sum.normalized;

            // Si la normal promedio es muy horizontal (transici�n pared/suelo)
            // usamos solo las patas delanteras que son las que gu�an
            float avgAngle = Vector3.Angle(avg, Vector3.up);
            if (avgAngle > 45f && avgAngle < 135f)
            {
                Vector3 frontSum = Vector3.zero;
                if (_RA.IsOnSurface) frontSum += _RA.SurfaceNormal;
                if (_LA.IsOnSurface) frontSum += _LA.SurfaceNormal;
                if (frontSum != Vector3.zero) return frontSum.normalized;
            }

            return avg;
        }
    }
    #endregion
    private void Awake()
    {
        StartCoroutine(LegUpdateCoroutine());
    }
    private void Update()
    {
        AdjustBody();

        ChangeWeight();
    }
    private void AdjustBody()
    {
        Vector3 frontAvg = (_RA.TargetPos + _LA.TargetPos) / 2f;
        Vector3 backAvg = (_RL.TargetPos + _LL.TargetPos) / 2f;

        Vector3 leftAvg = (_LA.TargetPos + _LL.TargetPos) / 2f;
        Vector3 rightAvg = (_RA.TargetPos + _RL.TargetPos) / 2f;

        Vector3 forwardVec = (frontAvg - backAvg).normalized;
        Vector3 rightVec = (rightAvg - leftAvg).normalized;

        Vector3 surfaceNormal = Vector3.Cross(forwardVec, rightVec).normalized;

        if (Vector3.Dot(surfaceNormal, _bodyTransform.up) < 0)
        {
            surfaceNormal = -surfaceNormal;
        }

        Vector3 projectedForward = Vector3.ProjectOnPlane(forwardVec, surfaceNormal).normalized;
        if (projectedForward.sqrMagnitude < 0.01f) return;
        Quaternion targetRot = Quaternion.LookRotation(projectedForward, surfaceNormal);

        _bodyTransform.rotation = Quaternion.Slerp( _bodyTransform.rotation, targetRot, _bodySmoothing * Time.deltaTime);
    }
    private void ChangeWeight()
    {
        float weight = Mathf.Lerp(_RA.Weight, _targetWeight, 15f * Time.deltaTime);
        _RA.Weight = weight;
        _LA.Weight = weight;
        _RL.Weight = weight;
        _LL.Weight = weight;
    }
    public void Jumping(bool jump)
    {
        _targetWeight = jump ? 0f : 1f;
    }

    private IEnumerator LegUpdateCoroutine()
    {
        while (true)
        {
            if (_isMoving)
            {
                _LA.TryMove();
                _RL.TryMove();

                if (_RL.Moving || _LA.Moving)
                {
                    yield return new WaitWhile(() => _RL.Moving || _LA.Moving);
                }

                _RA.TryMove();
                _LL.TryMove();

                if (_LL.Moving || _RA.Moving)
                {
                    yield return new WaitWhile(() => _LL.Moving || _RA.Moving);
                }

            }
            else
            {
                _LA.TrySettle();
                _RL.TrySettle();
                if (_RL.Moving || _LA.Moving)
                    yield return new WaitWhile(() => _RL.Moving || _LA.Moving);
            
                _RA.TrySettle();
                _LL.TrySettle();
                if (_LL.Moving || _RA.Moving)
                    yield return new WaitWhile(() => _LL.Moving || _RA.Moving);
            }

            yield return null;
        }
    }

}
