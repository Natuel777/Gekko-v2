using UnityEngine;
using TMPro;
using System.Collections;
using Unity.Cinemachine;

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
    [SerializeField] private ParticleSystem _particlePlankSetted;
    [SerializeField] private GameObject _particleCompleted;
    [SerializeField] private AudioSource _plankSound;
    [SerializeField] private AudioSource _completedSound;
    [SerializeField] private GameObject _beaverAnimation;
    [SerializeField] private GameObject _hammerAnimation;
    [SerializeField] private CinemachineCamera _camFinish;
    private void Start()
    {
        if (_missionActive) ActivateMission();

        if(LevelOneManager.Instance != null)
            LevelOneManager.Instance.OnBeaverMission += ActivateMission;
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
        _particlePlankSetted.Play();
        _beaverAnimation.SetActive(true);
        _hammerAnimation.SetActive(true);
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
        _camFinish.Priority = 30;
        GameManager.Instance.Pj.Inputs(false);
        _completedSound.Play();
        _particleCompleted.SetActive(true);

        ParticleSystem[] particleSystems = _particleCompleted.GetComponentsInChildren<ParticleSystem>();

        foreach (ParticleSystem ps in particleSystems)
            ps.Play();
        
        bool anyPlaying;
        
        do
        {
            anyPlaying = false;

            foreach(ParticleSystem ps in particleSystems)
            {
                if(ps.IsAlive(true))
                {
                    anyPlaying = true;
                    break;
                }
            }

            yield return null;

        } while (anyPlaying);
        
        _canvas.gameObject.SetActive(false);
        GameManager.Instance.Pj.Inputs(true);
        _camFinish.Priority = 0;
    }
    #region Canvas
    private void SetText()
    {
        _textCount.text = $"{_currentPlanks} / {_planksQuantity}";
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
