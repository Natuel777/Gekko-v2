using System.Collections.Generic;
using UnityEngine;

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

    public Transform CurrentTarget => _target;

    void Update()
    {
        if (_pjController.TongueOut) return;

        ToggleLock();
        if (_target != null)
        {

            _switchTimer -= Time.deltaTime;
            _pointToLook.position = _target.position;
            _tongue.LookAt(_target.position);
            // Gira el cuerpo hacia el target lockeado: sin esto, alejar a Gekko con el movimiento rompe el
            // FOV (chequeado contra transform.forward) y el ShootTongue (un Raycast recto, no un cono) termina
            // tirando para donde mira el cuerpo en vez de al núcleo. Solo para targets golpeables (núcleos,
            // barricadas, enemigos): un colectible (arándano, etc.) no lo necesita y no debería hacer girar a Gekko.
            if(_target.GetComponent<IDamageable>() != null)
                _pjController.RotateTowards(_target.position);
            if (!StaticMethods.InFOV(transform, _target.position, _viewRange + GetRangeBonus(_target), _viewAngle + GetAngleBonus(_target), GetObstacleMask(_target)))
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
                ShowIndicator(_target);
                _lockedIn = true;
            }
            else TargetNull();
        }
        //else TargetNull();
    }

    // Margen fijo para que el OverlapSphere (fase amplia) no descarte de entrada un núcleo con _extraCatchRange:
    // el corte preciso lo sigue haciendo InFOV más abajo, con el rango real de cada candidato.
    private const float MaxRangeBonusPadding = 10f;

    //Candidato a optimizar
    private List<Transform> GetTargets()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, _viewRange + MaxRangeBonusPadding, _aimableMask);
        List<Transform> targets = new List<Transform>();

        foreach (var hit in hits)
        {
            if (hit.TryGetComponent(out IParticleSystemTarget pst) && !pst.CanBeTargeted) continue;

            if (StaticMethods.InFOV(transform, hit.transform.position, _viewRange + GetRangeBonus(hit.transform), _viewAngle + GetAngleBonus(hit.transform), GetObstacleMask(hit.transform)))
            {
                targets.Add(hit.transform);
            }
        }
        return targets;
    }

    // Bonus de rango/ángulo/línea-de-visión por-objeto (ej. un núcleo en una posición o ángulo incómodo, o con el
    // propio piso tapándolo): valores neutros (0/_obstacle sin cambios) si no implementa PurificationCore.
    private float GetRangeBonus(Transform t) => t != null && t.TryGetComponent(out PurificationCore core) ? core.ExtraCatchRange : 0f;
    private float GetAngleBonus(Transform t) => t != null && t.TryGetComponent(out PurificationCore core) ? core.ExtraCatchAngle : 0f;

    private int _groundLayer = -2; // -2 = sin calcular todavía (NameToLayer puede devolver -1 válidamente si no existe la capa)

    private LayerMask GetObstacleMask(Transform t)
    {
        if (t == null || !t.TryGetComponent(out PurificationCore core) || !core.IgnoreGroundOcclusion)
            return _obstacle;

        if (_groundLayer == -2) _groundLayer = LayerMask.NameToLayer("Ground");
        if (_groundLayer < 0) return _obstacle;

        return _obstacle & ~(1 << _groundLayer);
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
            HideIndicator(_target);
        }

        _target = _orderedTargets[_currentIndex];
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
