using System.Collections.Generic;
using UnityEngine;

public static class ScenesDictionary
{
    public static Dictionary<ScenesNames, string> SceneName = new Dictionary<ScenesNames, string>
    {
        {ScenesNames.LoadScene, "SceneLoader" },
        {ScenesNames.Menu, "Menu" },
        {ScenesNames.Level1, "LvlOne" },
        {ScenesNames.Level2, "LvlTwo" },
        {ScenesNames.Intentocin, "intentocin" },
    };
    public static Dictionary<int, ScenesNames> LevelIndex = new Dictionary<int, ScenesNames>
    {
        {1, ScenesNames.Level1},
        {2, ScenesNames.Level2},
    };

    public static bool TryGetNextLevel(string currentSceneName, out ScenesNames next)
    {
        next = default;

        foreach (var kvp in LevelIndex)
        {
            if (SceneName[kvp.Value] == currentSceneName)
                return LevelIndex.TryGetValue(kvp.Key + 1, out next);
        }

        return false;
    }
}

