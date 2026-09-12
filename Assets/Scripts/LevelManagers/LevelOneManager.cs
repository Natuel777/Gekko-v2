using UnityEngine;

public class LevelOneManager : MonoBehaviour
{
    public static LevelOneManager Instance;

    public delegate void BeaverMission();
    public event BeaverMission OnBeaverMission;
    public delegate void BridgeConstructed();
    public event BridgeConstructed OnBridgeConstructed;

    private void Awake()
    {
        Instance = this;
    }
    private void Start()
    {
        if (AudioManager.instance) AudioManager.instance.Play(SoundNames.LvlOne,true);
    }
    public void BridgeFinish() => OnBridgeConstructed?.Invoke();
    public void BeaverMissionTaken() => OnBeaverMission?.Invoke();
}
