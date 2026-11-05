using UnityEngine;

public enum SRType
{
    Cross
}

public class GenericSR : MonoBehaviour
{
    [SerializeField] private SRType _type;
    [SerializeField] private bool _autoReturnToFactory = false;
    [SerializeField] private float _returnTime;

    private float _timer;
    private SpriteRendererFactory _factory;

    public SRType SRType => _type;

    private void Start() 
    {
        if(_factory == null)
            ServiceLocator.Instance.TryGet(out _factory);
    }

    private void Update()
    {
        if(_factory == null)
        {
            Debug.LogWarning("[GenericPS] Factory is null.");
            return;
        }

        if(!_autoReturnToFactory) return;

        if(_timer >= _returnTime)
        {
            _factory.Return(this);
            _timer = 0f;
            return;
        }

        _timer += Time.deltaTime;
    }
}
