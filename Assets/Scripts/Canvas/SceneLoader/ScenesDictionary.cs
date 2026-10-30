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
        {ScenesNames.BossLvlOne, "BossLvlOne" },
        {ScenesNames.Aldea, "Aldea" },
    };
    public static Dictionary<int, ScenesNames> LevelIndex = new Dictionary<int, ScenesNames>
    {
        {1, ScenesNames.Level1},
        {2, ScenesNames.Level2},
    };

    // Escenas que no están en LevelIndex pero cierran un nivel: su "siguiente nivel" va directo acá.
    // El nivel 1 sigue LvlOne -> BossLvlOne -> Aldea, y el Level Finish de la Aldea manda a LvlTwo.
    public static Dictionary<ScenesNames, ScenesNames> NextSceneOverride = new Dictionary<ScenesNames, ScenesNames>
    {
        {ScenesNames.Aldea, ScenesNames.Level2},
    };

    public static bool TryGetNextLevel(string currentSceneName, out ScenesNames next)
    {
        next = default;

        foreach (var kvp in NextSceneOverride)
        {
            if (SceneName[kvp.Key] == currentSceneName)
            {
                next = kvp.Value;
                return true;
            }
        }

        foreach (var kvp in LevelIndex)
        {
            if (SceneName[kvp.Value] == currentSceneName)
                return LevelIndex.TryGetValue(kvp.Key + 1, out next);
        }

        return false;
    }
}

