using UnityEngine;
using TMPro;
using System.Collections;

public class BeaverBridge : MonoBehaviour
{
    [Header("<color=green>Values</color>")]
    [SerializeField] private bool _missionActive;
    [SerializeField] private int _planksQuantity = 3;
    private int _currentPlanks = 0;

    [Header("<color=green>Variables</color>")]
    [SerializeField] GameObject _brokenBridge;
    [SerializeField] GameObject _fixedBridge;
    [SerializeField] private Transform _canvas;
    [SerializeField] private TextMeshProUGUI _textCount;
    [SerializeField] private ParticleSystem _particleCompleted;
    [SerializeField] private AudioSource _plankSound;
    [SerializeField] private AudioSource _completedSound;
    private Transform _pjPosition;

    private void Start()
    {
        if (_missionActive) ActivateMission();

        _pjPosition = GameManager.Instance.Pj.transform;

        if(LevelOneManager.Instance != null)
            LevelOneManager.Instance.OnBeaverMission += ActivateMission;
    }
    private void Update()
    {
        if(_missionActive) FollowPlayer();
    }
    private void ActivateMission()
    {
        _missionActive = true;
        SetText();
        _canvas.gameObject.SetActive(true);
    }
    private void PlankPositioned()
    {
        _currentPlanks++;
        _plankSound.Play();
        SetText();
        if (_currentPlanks >= _planksQuantity)
        {
            _brokenBridge.SetActive(false);
            _fixedBridge.SetActive(true);
            LevelOneManager.Instance.BridgeFinish();
            StartCoroutine(Completed());
        }
    }
    private IEnumerator Completed()
    {
        _completedSound.Play();
        _particleCompleted.Play();
        while(_particleCompleted.isPlaying)
        {
            yield return null;
        }
        _canvas.gameObject.SetActive(false);
    }
    #region Canvas
    private void SetText()
    {
        _textCount.text = $"{_currentPlanks} / {_planksQuantity}";
    }
    private void FollowPlayer()
    {
        Vector3 forward = transform.position - _pjPosition.position;
        Vector3 newForward = new Vector3(forward.x, 0, forward.z);
        _canvas.forward = newForward;
    }
    #endregion
    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out BeaverBridge_Plank plank))
        {
            GameManager.Instance.Pj.PjTongue.ObjectLost();
            plank.Positioned();
            PlankPositioned();
        }
    }
    private void OnDisable()
    {
        LevelOneManager.Instance.OnBeaverMission -= ActivateMission;
    }
}
