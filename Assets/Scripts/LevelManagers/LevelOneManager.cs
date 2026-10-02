using UnityEngine;

public class LevelOneManager : MonoBehaviour
{
    public static LevelOneManager Instance;

    public delegate void BeaverMission();
    public event BeaverMission OnBeaverMission;
    public delegate void BridgeConstructed();
    public event BridgeConstructed OnBridgeConstructed;

    public delegate void Challenge();
    public event Challenge OnChallengeStart;

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

    public void ChallengeStart() => OnChallengeStart?.Invoke();
    public void StartSong()
    {
        if (AudioManager.instance) AudioManager.instance.Play(SoundNames.LvlOne, true);
    }
}
