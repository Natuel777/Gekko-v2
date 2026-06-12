using UnityEngine;

public class DefensiveBehaviour
{
    private Transform _target, _transform;
    private float _defenseThreshold, _speed, _rotationSpeed;
    private Bug _bug;
    private bool _isAttacking;

    #region Jump
    private bool _isJumping = false, _hasReachedTarget = false;
    private Vector3 _jumpStartPos, _jumpTargetPos;
    private float _jumpDuration = 0.5f, _jumpHeight = 2f, _jumpTimer, _initialYPosition;
    #endregion

    public DefensiveBehaviour(float threshold, Transform t, float speed, Bug bug, float rotationSpeed)
    {
        _defenseThreshold = threshold;
        _transform = t;
        _speed = speed;
        _bug = bug;
        _rotationSpeed = rotationSpeed;

        EventManager.Subscribe<float>("OnPlayerDamagedByBug", x => _hasReachedTarget = true);
    }

    public void StartDefense(Transform target)
    {
        _initialYPosition = _transform.position.y;
        _target = target;
        _isAttacking = false;
        _isJumping = false;
        _jumpTimer = 0f;
        _hasReachedTarget = false;
    }

    public void StopDefense()
    {
        _isJumping = false;
        _isAttacking = false;
        _jumpTimer = 0f;
        _transform.position = new Vector3(_transform.position.x, _initialYPosition, _transform.position.z);
    }

    public void ArtificialUpdate()
    {
        if(_target == null) return;

        if(_isJumping)
        {
            UpdateJump();
            return;
        }

        float distance = (_target.position - _transform.position).sqrMagnitude;

        if(!_isAttacking && distance < _defenseThreshold * _defenseThreshold)
        {
            _isAttacking = true;
            _bug.SetBugAttacking(true);
        }

        else if(_isAttacking) MoveTowardsTarget(distance);
    }

    private void MoveTowardsTarget(float distance)
    {
        Vector3 direction = _target.position - _transform.position;
        direction.y = 0f;
        direction.Normalize();

        if(distance < (_defenseThreshold * _defenseThreshold) * 0.5f)
        {
            if(!_hasReachedTarget) JumpToTarget(direction);

            else JumpFromTarget(direction);
            return;
        }

        if(_hasReachedTarget) _hasReachedTarget = false;

        RotateTowards(direction);
        _transform.position += direction * _speed * Time.deltaTime;
    }

    private void JumpToTarget(Vector3 direction)
    {
        if(_isJumping) return;

        _isJumping = true;
        _jumpTimer = 0f;
        _jumpStartPos = _transform.position;
        float jumpDistance = 2f;
        _jumpTargetPos = _transform.position + direction * jumpDistance;
    }

    private void JumpFromTarget(Vector3 direction)
    {
        if(_isJumping) return;

        _isJumping = true;
        _jumpTimer = 0f;
        _jumpStartPos = _transform.position;
        float jumpDistance = 2f;
        _jumpTargetPos = _transform.position - direction * jumpDistance;
    }

    private void UpdateJump()
    {
        _jumpTimer += Time.deltaTime;
        float t = _jumpTimer / _jumpDuration;

        if(t >= 1f)
        {
            _isJumping = false;
            _transform.position = new Vector3(
                            _jumpTargetPos.x,
                            _initialYPosition,
                            _jumpTargetPos.z);
            return;
        }

        Vector3 pos = Vector3.Lerp(_jumpStartPos, _jumpTargetPos, t);
        float height = 4f * _jumpHeight * t * (1f - t);
        pos.y += height;
        RotateTowards(_jumpTargetPos - _jumpStartPos);
        _transform.position = pos;
    }

    private void RotateTowards(Vector3 dir)
    {
        dir.y = 0f;
        if(dir.sqrMagnitude < 0.0001f) return;
        Quaternion target = Quaternion.LookRotation(dir);
        _transform.rotation = Quaternion.Slerp(_transform.rotation, target, Time.deltaTime * _rotationSpeed);
    }
}
