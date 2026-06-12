using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;

public class CameraStateManager : MonoBehaviour
{
    [SerializeField] private CinemachineCamera _currentCamera;
    [SerializeField] private CinemachineCamera[] _cameras;
    [SerializeField] private string[] _cameraTags;
    public string primaryCameraTag;
    private Dictionary<string, CinemachineCamera> _cameraWithTags = new Dictionary<string, CinemachineCamera>();

    #region Properties
    public CinemachineCamera[] Cameras { get { return _cameras; } }
    public static CameraStateManager Instance { get; private set; }
    public CinemachineCamera CurrentCamera { get { return _currentCamera; } }
    #endregion

    private void Awake() 
    {
        if(Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        
        Instance = this;

        for(int i = 0; i < _cameraTags.Length && i < _cameras.Length; i++)
            _cameraWithTags[_cameraTags[i]] = _cameras[i];
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        foreach(var item in _cameraWithTags)
            EventManager.Subscribe(item.Key, () => SwitchToCamera(item.Value));
    }

    private void SwitchToCamera(CinemachineCamera newCamera)
    {
        foreach(CinemachineCamera cam in CameraStateManager.Instance.Cameras)
            cam.enabled = cam == newCamera;
    }

    public string[] GetCameraTags() => _cameraTags;
}
