using System;
using UnityEngine;

public class LevelOneManager : MonoBehaviour
{
    public static LevelOneManager Instance;

    public event Action OnBeaverMission;
    public event Action OnBridgeConstructed;
    public event Action OnMissionDuckStarted;
    public event Action<BabyDuck> OnDuckFound;
    // Con el patito que volvió, para que MomDuck lo ubique en la fila y arme el plano de la familia.
    public event Action<BabyDuck> OnDuckReturned;
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
    public void DuckReturned(BabyDuck baby)
    {
        OnDuckReturned?.Invoke(baby);
    }
    public void DucksFinishedBefore(BabyDuck baby)
    {
        OnDuckFound?.Invoke(baby);
    }

    // Lugar armado a mano en la escena, para cuando MomDuck no encuentra piso para la fila.
    public Transform ReserveDuckPosition() => GetDuckPosition();
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
