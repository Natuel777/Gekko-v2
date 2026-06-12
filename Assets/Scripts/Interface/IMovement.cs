using UnityEngine;
using System;
using UnityEngine.Rendering;

[Serializable]
public abstract class MovementBase
{
    protected string _cameraTag = "Default";
    public string CameraTag => _cameraTag;

    protected Rigidbody _rb;
    protected Transform _pjTransform;
    protected CapsuleCollider _collider;
    protected Transform _camTransform;
    protected LayerMask _groundRayMask;
    protected LayerMask _climbRayMask;
    protected Transform _target;
    protected Vector2 _rawInput;
    protected bool _jumpPressed = false;
    protected Transform _head;
    protected PlayerViewer _pjViewer;
    protected bool _tongueOut = true;
    protected float _tongueSlowness = 0.05f;

    public Transform Target { get { return _target; } set { _target = value; } }
    public Vector2 RawInput { get { return _rawInput; } set { _rawInput = value; } }
    public bool JumpPressed { get { return _jumpPressed; } set { _jumpPressed = value; } }
    public bool TongueOut { set { _tongueOut = value; } }

    public virtual void Initialize(Rigidbody rb, 
                                Transform pjTransform, 
                                CapsuleCollider collider, 
                                Transform camTransform, 
                                LayerMask groundLayer, 
                                LayerMask climbLayer,
                                Transform head,
                                PlayerViewer pjviewer)
    {
        _rb = rb;
        _pjTransform = pjTransform;
        _collider = collider;
        _camTransform = camTransform;
        _groundRayMask = groundLayer;
        _climbRayMask = climbLayer;
        _head = head;
        _pjViewer = pjviewer;
    }

    public abstract void ArtificialUpdate();
    public abstract void ArtificialFixedUpdate();
    public abstract void Jump();
    public abstract void TargetAquired(Transform target);
    public abstract void TargetLost();
    public virtual void SetCameraTransform(Transform camTransform) {_camTransform = camTransform;}
    public abstract void ChangeValues(float speed, float jump, float rotSpeed, float fallMulti, float lowmulti);
    public void CancelJump() { _pjViewer.Jump(false); }
    public void CancelMovement() { _pjViewer.Move(false); }

}