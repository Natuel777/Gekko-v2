using System.Collections.Generic;

public static class CollectiblesRegister
{
    private static Dictionary<string, int> _counts = new Dictionary<string, int>();

    public static void RegisterCollectible(string collectibleName)
    {
        if (!_counts.ContainsKey(collectibleName))
            _counts[collectibleName] = 0;
        _counts[collectibleName]++;
    }

    public static int GetCollectibleCount(string collectibleName) =>
        _counts.TryGetValue(collectibleName, out int c) ? c : 0;

    public static void Clear() => _counts.Clear();
}
