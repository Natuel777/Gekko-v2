using UnityEngine;

public class TongueManager : MonoBehaviour
{
    [SerializeField] private Transform _pj;
    [SerializeField] private float _speed = 15f;
    [SerializeField] private float _maxDistance = 7f;
    [SerializeField] private Transform _mouthTransform;
    [SerializeField] private PlayerViewer _pjViewer;
    private SkinnedMeshRenderer _blend;

    private Vector3 _startPos;
    private Vector3 _finalPos;
    private Vector3 _currentPos;
    private Transform _object;
    private bool _attached = false;

    private bool _extending;
    private bool _retracting;
    private float _objectRadius = 0f;
    private float _stopDist;

    private PlayerController _pjController;

    [SerializeField] private AudioSource _tongueSound;
    [SerializeField] private AudioSource _slurpSound;

    private bool _canUseTongue = true;
    public bool IsAttached => _attached;
    public Vector3 MouthPos => transform.position + new Vector3(0,1,0);
    public Vector3 ObjectExtents => _object != null
    ? _object.GetComponent<Collider>().bounds.extents
    : Vector3.one * 0.5f;
    public Vector3 ObjectPosition => _object != null ? _object.position : Vector3.zero;
    public Collider HeldCollider => _object != null ? _object.GetComponent<Collider>() : null;
    public float ObjectRadius => _objectRadius;
    public int ObjectLayer => _object != null ? _object.gameObject.layer : 0;
    public bool CanUseTongue { set { _canUseTongue = value; } }

    private void Awake()
    {
        _blend = GetComponent<SkinnedMeshRenderer>();
        _blend.enabled = false;
    }
    private void FixedUpdate()
    {
        TongueBehaviour();
    }
    private void LateUpdate()
    {
        if (_extending || _retracting)
        {
            float currentDist = Vector3.Distance(transform.position, _currentPos);
            float blendValue = (currentDist / _maxDistance) * 100f;
            _blend.SetBlendShapeWeight(0, blendValue);
        }
        
    }
    public void MoveObject()
    {
        if (_attached && _object != null)
        {
            _object.position = transform.position + new Vector3(0, 1, 0) + _pj.forward * (_objectRadius + 0.5f);
            transform.LookAt(_object.position);
            _currentPos = _object.position;
            _startPos = transform.position;
        }
    }
    private void TongueBehaviour()
    {
        if (_extending || _retracting)
            _startPos = transform.position;
        if (_extending)
        {
            _currentPos = Vector3.MoveTowards(_currentPos, _finalPos, _speed * Time.deltaTime);

            if (Vector3.Distance(_currentPos, _finalPos) < 0.05f)
            {
                _extending = false;
                _retracting = true;

                if(_object != null)
                {
                    if (_object.TryGetComponent(out Bug buggy))
                    {
                        buggy.Absorbed = true;
                        CollectiblesRegister.RegisterCollectible("Bug");
                        _slurpSound.Play();
                    }
                    else if(_object.TryGetComponent(out Collectible col))
                    {
                        _object.GetComponent<Collider>().enabled = false;
                        _slurpSound.Play();
                    }
                    else if (_object.TryGetComponent(out IDamageable damageableObj))
                        damageableObj.Damage(1);
                }
            }
        }
        else if (_retracting)
        {
            _currentPos = Vector3.MoveTowards(_currentPos, _startPos, _speed * Time.deltaTime);

            if (Vector3.Distance(_currentPos, _startPos) > _stopDist + 0.2f)
                MoveGrabbedObject();

            if (Vector3.Distance(_currentPos, _startPos) < _stopDist)
            {
                _retracting = false;

                _pjController.CanRotate = true;

                if (_object != null)
                {
                    if (_object.TryGetComponent(out GrabbableObject grabObj))
                    {
                        grabObj.Grab();
                        Vector3 offset = _object.position - grabObj.GrabbedPoint.position;
                        _object.position = _mouthTransform.position + offset;
                        _object.rotation = _mouthTransform.rotation;
                        _object.SetParent(_mouthTransform, true);
                        _object.localPosition = Vector3.zero;
                    }
                    else if (_object.TryGetComponent(out BringgableObject bringgable))
                    {
                        bringgable.StartMoving();
                        Vector3 holdPos = transform.position + new Vector3(0,1,0) + _pj.forward * (_objectRadius + 0.5f);
                        _object.position = holdPos;
                        _currentPos = holdPos;
                        _attached = true;
                        //_pjController.CanRotate = false;
                        _pjController.HeadLocate();
                        return;
                    }
                    else if (_object.TryGetComponent(out IDamageable damaggeable))
                    {
                        damaggeable.Damage(1);
                        _object = null;
                    }
                    else if (_object.TryGetComponent(out Collectible coll))
                    {
                        coll.Grab();
                        _object = null;
                    }
                }
                _blend.SetBlendShapeWeight(0, 0f);
                _blend.enabled = false;
                _pjController.TongueOut = false;
                _pjController.HeadLocate();
            }
        }
    }
    public void ShootTongue()
    {
        if (!_canUseTongue) return;
        if (_extending || _retracting) return;
        _pjController.TongueOut = true;
        if (_object != null)
        {
            DropObject();
            _stopDist = 0.05f;
            _retracting = true;
            _pjViewer.Mouth(false);
            return;
        }
        _blend.enabled = true;
        _blend.SetBlendShapeWeight(0, 0f);
        _startPos = transform.position;
        _currentPos = _startPos;
        _tongueSound.Play();

        float stopDist = 0.05f;
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, _maxDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            if(hit.transform.GetComponent<BaseObject>() != null || hit.transform.GetComponent<IDamageable>() !=null)
            {
                _object = hit.transform;
                if (_object.TryGetComponent(out BringgableObject bringgable))
                {
                    if (bringgable.CanMove)
                    {
                        bringgable.Grab();

                    }
                    else _object = null;
                }
                    

                if (_object != null)
                {
                    Collider col = _object.GetComponentInChildren<Collider>();
                    if (col != null)
                    {
                        _objectRadius = col.bounds.extents.magnitude;
                        stopDist = _objectRadius + 0.1f;
                    }

                    if (_object.GetComponent<IDamageable>() != null) _pjViewer.Attack();
                    else if (_object.GetComponent<Collectible>() != null) _pjViewer.Attack();
                    else _pjViewer.Mouth(true);
                }     
            }
            else _pjViewer.Attack();
            _finalPos = hit.point;

        }
        else
        {
            _finalPos = transform.position + transform.forward * _maxDistance;
            _pjViewer.Attack();
        }

        _stopDist = stopDist;
        _extending = true;
    }
    public void ResetTongue()
    {
        if (_object)
        {
            if(_object.TryGetComponent(out InteractableObject a)) a.Drop();
            _object = null;
            _attached = false;
        }
        _currentPos = _startPos;
        _blend.SetBlendShapeWeight(0, 0f);
        _blend.enabled = false;
        _pjController.TongueOut = false;
        _pjController.HeadLocate();
        _pjController.CanRotate = true;
        _pjViewer.Mouth(false);
        _retracting = false;
        _extending = false;
    }
    public void ObjectLost()
    {
        if (_object == null) return;
        _pjController.TongueOut = true;
        _object.GetComponent<InteractableObject>().Drop();
        _currentPos = _object.position;
        _object = null;
        _attached = false;
        _stopDist = 0.05f;
        _startPos = transform.position;
        _pjController.HeadLocate();
        _pjController.CanRotate = true;
        _retracting = true;
        _pjViewer.Mouth(false);
    }
    private void DropObject()
    {
        _currentPos = _object.position;
        if (_object.TryGetComponent(out GrabbableObject grabObj))
        {
            grabObj.Drop();
            _object = null;
        }
        else if (_object.TryGetComponent(out BringgableObject bringgable))
        {
            bringgable.Drop();

            _attached = false;
            _object = null;
        }
        _startPos = transform.position;
        _pjController.CanRotate = true;
        _pjController.HeadLocate();
    }
    private void MoveGrabbedObject()
    {
        if (_object == null) return;
        if (!_object.GetComponent<InteractableObject>()) return;
        if(TryGetComponent(out Rigidbody rb))
        {
            Collider col = _object.GetComponentInChildren<Collider>();
            float objectRadius = col.bounds.extents.magnitude;

            rb.MovePosition(Vector3.MoveTowards(rb.position, _currentPos, _speed * Time.fixedDeltaTime));

            Vector3 toMouth = rb.position - transform.position;
            float dist = toMouth.magnitude;

            if (dist < objectRadius)
            {
                rb.position = transform.position + toMouth.normalized * objectRadius;
            }
        }
        else
        {
            _object.position = _currentPos;
        }
    }
    public void GetPlayerController(PlayerController pjC)
    {
        _pjController = pjC;
    }
    public void SlurpSound()
    {
        _slurpSound.Play();
        _pjViewer.Attack();
    }
}
