using System.Collections.Generic;
using UnityEngine;

public static class ScenesDictionary
{
    public static Dictionary<ScenesNames, string> SceneName = new Dictionary<ScenesNames, string>
    {
        {ScenesNames.LoadScene, "SceneLoader" },
        {ScenesNames.Menu, "Menu" },
        {ScenesNames.Level1, "LvlParcialBlocking 2" },
        {ScenesNames.Intentocin, "intentocin" },
    };
    public static Dictionary<int, ScenesNames> LevelIndex = new Dictionary<int, ScenesNames>
    {
        {1, ScenesNames.Level1},
    };
}

