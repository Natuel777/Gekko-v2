using System.Collections.Generic;
using System.IO.Pipes;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class AimManager : MonoBehaviour
{
    [SerializeField] private LayerMask _aimableMask;
    private Transform _camTransform;
    private PlayerController _pjController;
    private float _switchTimer;
    private float _switchCooldown = 0.2f;
    [SerializeField] private LayerMask _obstacle;
    [SerializeField] private float _viewRange, _viewAngle;

    [SerializeField] private Transform _pointToLook;
    [SerializeField] private Transform _target;
    [SerializeField] private Transform _tongue;

    private List<Transform> _orderedTargets;
    private int _currentIndex = -1;

    private bool _lockedIn;

    void Update()
    {
        if (_pjController.TongueOut) return;

        ToggleLock();
        if (_target != null)
        {

            _switchTimer -= Time.deltaTime;
            _pointToLook.position = _target.position;
            _tongue.LookAt(_target.position);
            if (!InFOV(transform, _target.position, _viewRange, _viewAngle))
                TargetNull();
        }
        else
        {
            if (_target == null)
                TargetNull();
        }

    }
    public void SetControllerAndCamera(PlayerController pjC, Transform camera)
    {
        _pjController = pjC;
        _camTransform = camera;
    }
    public bool InFOV(Transform startPos, Vector3 endPos, float viewRange, float viewAngle)
    {
        Vector3 dir = endPos - startPos.position;
        if (!InLOS(startPos.position, endPos)) return false;
        if (dir.magnitude > viewRange) return false;
        if (Vector3.Angle(startPos.forward, dir) > viewAngle / 2) return false;
        return true;
    }

    bool InLOS(Vector3 start, Vector3 end)
    {
        Vector3 dir = end - start;

        return !Physics.Raycast(start, dir.normalized, dir.magnitude, _obstacle, QueryTriggerInteraction.Ignore);
    }
    public void ToggleLock()
    {
        if (_pjController.TongueOut) return;

        if (_target == null)
        {
            _orderedTargets = GetOrderedTargets();

            _currentIndex = 0;
            if(_orderedTargets.Count>0)
            {
                _target = _orderedTargets[_currentIndex];
            }

            if (_target != null)
            {
                var renderer = _target.GetComponentInChildren<MeshRenderer>();
                if (renderer != null) renderer.material.color = Color.red;
                ShowIndicator(_target);
                _lockedIn = true;
            }
            else TargetNull();
        }
        //else TargetNull();
    }

    //Candidato a optimizar
    private List<Transform> GetTargets()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, _viewRange, _aimableMask);

        List<Transform> targets = new List<Transform>();

        foreach (var hit in hits)
        {
            if (hit.GetComponentInChildren<MeshRenderer>() == null) continue;

            if (hit.TryGetComponent(out IParticleSystemTarget pst) && !pst.CanBeTargeted) continue;

            if(InFOV(transform, hit.transform.position, _viewRange, _viewAngle))
                targets.Add(hit.transform);
        }

        return targets;
    }
    public void SwitchTarget(Vector2 scroll)
    {
        if (_pjController.TongueOut) return;
        if (!_lockedIn) return;

        int changeDir = 0;

        if (_switchTimer > 0f) return;

        if (scroll.y > 0)
        {
            changeDir = +1;
            _switchTimer = _switchCooldown;
        }
        else if (scroll.y < 0)
        {
            changeDir = -1;
            _switchTimer = _switchCooldown;
        }
        _orderedTargets = GetOrderedTargets();
        _currentIndex = _orderedTargets.IndexOf(_target);

        if (_orderedTargets == null || _orderedTargets.Count == 0) return;

        _currentIndex += changeDir;

        if (_currentIndex >= _orderedTargets.Count)
            _currentIndex = 0;
        else if (_currentIndex < 0)
        {
            _currentIndex = _orderedTargets.Count - 1;

        }
        if (_target != null)
        {
            _target.GetComponentInChildren<MeshRenderer>().material.color = Color.white;
            HideIndicator(_target);
        }

        _target = _orderedTargets[_currentIndex];
        _target.GetComponentInChildren<MeshRenderer>().material.color = Color.red;
        ShowIndicator(_target);
    }
    private List<Transform> GetOrderedTargets()
    {
        List<Transform> targets = GetTargets();

        Vector3 camForward = Vector3.ProjectOnPlane(_camTransform.forward, _pjController.CurrentUp);

        targets.Sort((a, b) =>
        {
            Vector3 dirA = Vector3.ProjectOnPlane(a.position - _camTransform.position, _pjController.CurrentUp);
            Vector3 dirB = Vector3.ProjectOnPlane(b.position - _camTransform.position, _pjController.CurrentUp);

            float angleA = Vector3.SignedAngle(camForward, dirA, _pjController.CurrentUp);
            float angleB = Vector3.SignedAngle(camForward, dirB, _pjController.CurrentUp);

            return angleA.CompareTo(angleB);
        });

        return targets;
    }
    private void TargetNull()
    {
        if (_target != null)
        {
            _target.GetComponentInChildren<MeshRenderer>().material.color = Color.white;
            HideIndicator(_target);
        }
        _target = null;
        _pointToLook.localPosition = new Vector3(0, 0.2f,1.12f);
        if(!_pjController.TongueOut)
        _tongue.localRotation = Quaternion.identity;
        _lockedIn = false;
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, _viewRange);

        Gizmos.color = Color.cyan;
        Vector3 dirA = GetAngleFromDir(_viewAngle / 2 + transform.eulerAngles.y);
        Vector3 dirB = GetAngleFromDir(-_viewAngle / 2 + transform.eulerAngles.y);
        Gizmos.DrawLine(transform.position, transform.position + dirA.normalized * _viewRange);
        Gizmos.DrawLine(transform.position, transform.position + dirB.normalized * _viewRange);
    }

    Vector3 GetAngleFromDir(float angleInDegrees) => new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));

    private void ShowIndicator(Transform target)
    {
        if(target != null && target.TryGetComponent(out IParticleSystemTarget pst) && pst.Indicator != null)
            pst.Indicator.Play();     
    }

    private void HideIndicator(Transform target)
    {
        if(target != null && target.TryGetComponent(out IParticleSystemTarget pst) && pst.Indicator != null)
            pst.Indicator.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
