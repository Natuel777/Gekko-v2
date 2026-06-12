using UnityEngine;

public class LevelOneManager : MonoBehaviour
{
    public static LevelOneManager Instance;

    public delegate void BeaverMission();
    public event BeaverMission OnBeaverMission;
    public delegate void BridgeConstructed();
    public event BridgeConstructed OnBridgeConstructed;

    //[Header("Collectibles")]
    //[SerializeField] private Blueberry _blueberryPrefab;
    //[SerializeField] private Transform[] _blueberryPositions;

    private void Awake()
    {
        Instance = this;
    }

    /*private void Start()
    {
        FactoryStart();
    }
    private void FactoryStart()
    {
        var factory = GameManager.Instance.factory;
        factory.InitializePool(_blueberryPrefab.collectibleName, _blueberryPrefab.CreateCollectibleType);
        foreach (Transform t in _blueberryPositions)
            factory.SpawnFromPool(_blueberryPrefab.collectibleName, t);
    }*/
    public void BridgeFinish() => OnBridgeConstructed?.Invoke();
    public void BeaverMissionTaken() => OnBeaverMission?.Invoke();
}
