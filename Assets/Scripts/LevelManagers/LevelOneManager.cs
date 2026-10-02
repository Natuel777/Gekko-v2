using System;
using UnityEngine;

public class LevelOneManager : MonoBehaviour
{
    public static LevelOneManager Instance;

    public event Action OnBeaverMission;
    public event Action OnBridgeConstructed;
    public event Action OnMissionDuckStarted;
    public event Action OnDuckFound;
    public event Action OnMissionDuckFinish;
    public event Action OnChallengeStart;

    [SerializeField] private DuckPosition[] _positionsForDucks;

    private void Awake()
    {
        Instance = this;
    }
    private void Start()
    {
        StartSong();
    }
    public void BridgeFinish() => OnBridgeConstructed?.Invoke();
    public void BeaverMissionTaken() => OnBeaverMission?.Invoke();
    public void MissionDuck() => OnMissionDuckStarted?.Invoke();
    public Transform DuckFound()
    {
        OnDuckFound?.Invoke();
        return GetDuckPosition();
    }
    public void MissionDuckFinish() => OnMissionDuckFinish?.Invoke();
    public void ChallengeStart() => OnChallengeStart?.Invoke();

    public void StartSong()
    {
        if (AudioManager.instance) AudioManager.instance.Play(SoundNames.LvlOne, true);
    }
    private Transform GetDuckPosition()
    {
        foreach (var item in _positionsForDucks)
        {
            if (!item.Ocupied)
            {
                item.Ocupe();
                return item.transform;
            }
        }
        return null;
    }
}
