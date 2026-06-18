using System.Collections.Generic;
using UnityEngine;

public class LevelStats : MonoBehaviour
{
    public static LevelStats Instance { get; private set; }

    private float _elapsed;
    private bool _running = true;
    private readonly Dictionary<string, int> _totals = new();

    public float ElapsedTime => _elapsed;
    public string FormattedTime
    {
        get { int t = Mathf.FloorToInt(_elapsed); return $"{t / 60:00}:{t % 60:00}"; }
    }

    private void Awake()
    {
        Instance = this;
        CollectiblesRegister.Clear();
    }

    private void Start()
    {
        foreach (var c in FindObjectsByType<Collectible>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!_totals.ContainsKey(c.CollectibleName)) _totals[c.CollectibleName] = 0;
            _totals[c.CollectibleName]++;
        }
    }

    private void Update()
    {
        if (_running) _elapsed += Time.deltaTime;
    }

    public void StopTimer() => _running = false;
    public int GetTotal(string key) => _totals.TryGetValue(key, out var n) ? n : 0;
}
