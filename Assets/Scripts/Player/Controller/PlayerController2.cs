using UnityEngine;
using System.Collections.Generic;

public class PlayerController2
{
    private Transform _pjTransform;
    private Transform _head;
    private Rigidbody _rb;
    private CapsuleCollider _collider;
    private Transform _camTransform;
    private float _rotationSpeed = 25f, _speed = 7f, _jumpForce = 6f, _smoothInputSpeed = 0.2f;
    private float _fallMultiplier = 2.5f;
    private float _lowJumpMultiplier = 3f;
    private Vector2 _rawInput, _smoothedInput, _smoothedVelocity;
    private bool _isGrounded = false, _jumpPressed = false;
    private LayerMask _groundRayMask;
    private PlayerViewer _pjViewer;

    #region Climbing
    private Vector3 _currentUp;
    private bool _isClimbing = false;
    private LayerMask _climbRayMask;
    #endregion 

    private Transform _target;

    #region Properties
    public Vector2 RawInput { get { return _rawInput; } set { _rawInput = value; } }
    public bool JumpPressed { set { _jumpPressed = value; } }
    public Transform Target { 
        get { return _target; } 
        set { 
            _target = value;
            if(currentMovement != null)
                currentMovement.Target = value;
        } 
    }
    public Vector3 CurrentUp { get { return _currentUp; } }
    #endregion

    #region Strategy
    public MovementBase currentMovement;
    private Dictionary<string, MovementBase> _movementDict;
    public MovementBase[] movementTypes;
    #endregion

    public PlayerController2(Rigidbody rb, 
                            Transform pjTransform, 
                            CapsuleCollider col, 
                            float rSpeed, 
                            LayerMask groundLayer,
                            Transform head,
                            PlayerViewer pjAnim)
    {
        _rb = rb;
        _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        _pjTransform = pjTransform;
        _rotationSpeed = rSpeed;
        _collider = col;
        _head = head;
        _groundRayMask = groundLayer;
        _pjViewer = pjAnim;
        movementTypes = new MovementBase[]
        {
            new NormalMovement(),
            new WallMovement()
        };
    }

    public void SetCameraTransform(Transform camTransform) {_camTransform = camTransform;}

    public void InitClimbVariables(LayerMask climbLayer)
    {
        _climbRayMask = climbLayer;
        _currentUp = Vector3.up;
        currentMovement = new NormalMovement();
        currentMovement.Initialize(_rb, _pjTransform, _collider, _camTransform, _groundRayMask, _climbRayMask, _head, _pjViewer);
    }

    public void InitEvents()
    {
        string[] cameraTags = CameraStateManager.Instance.GetCameraTags();
        _movementDict = new Dictionary<string, MovementBase>();

        foreach(MovementBase move in movementTypes)
        {
            foreach(string tag in cameraTags)
            {
                if(move.CameraTag == tag)
                {
                    _movementDict[tag] = move;
                    break;
                }
            }
        }

        foreach(var item in _movementDict)
            EventManager.Subscribe(item.Key, () => SwitchMovement(item.Key));
    }

    public void ArtificialUpdate(){currentMovement.ArtificialUpdate();}

    public void ArtificialFixedUpdate(){currentMovement.ArtificialFixedUpdate();}

    public void Jump(){currentMovement.Jump();}

    private void SwitchMovement(string tag)
    {
        if(currentMovement == _movementDict[tag]) return;

        currentMovement = _movementDict[tag];
        currentMovement.Initialize(_rb, _pjTransform, _collider, _camTransform, _groundRayMask, _climbRayMask, _head, _pjViewer);
        currentMovement.Target = _target;
        Debug.Log("Switched to " + _movementDict[tag]);
    }
    public void TargetLost()
    {
        _target = null;
        currentMovement.TargetLost();
        _head.localRotation = Quaternion.Euler(-90f, 0f, 0f);
    }
    public void TargetAquired(Transform target)
    {
        _target = target;
        currentMovement.TargetAquired(target);
        _head.LookAt(_target.position);
    }

    public void ChangeValues(float speed, float jump, float rotSpeed, float fallMulti, float lowmulti)
    {
        currentMovement.ChangeValues(speed, jump, rotSpeed, fallMulti, lowmulti);

    }

}
