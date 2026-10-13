using UnityEngine;

// Utilidad estática: guarda qué escena hay que cargar al entrar a "SceneLoader", y
// sobrevive a un Domain Reload gracias a PlayerPrefs. La carga en sí la hace
// LoadingScreen.cs, que vive en la escena "SceneLoader".
public static class AsyncLoader
{
    private const string PrefKey = "SceneToLoad";
    public static ScenesNames sceneToLoad = default;

    public static void SetSceneToLoad(ScenesNames scene)
    {
        sceneToLoad = scene;
        PlayerPrefs.SetInt(PrefKey, (int)scene);
        PlayerPrefs.Save();
    }

    // Recupera el valor si el campo estático se perdió por un Domain Reload.
    public static ScenesNames GetSceneToLoad()
    {
        if (sceneToLoad == default && PlayerPrefs.HasKey(PrefKey))
            sceneToLoad = (ScenesNames)PlayerPrefs.GetInt(PrefKey);

        return sceneToLoad;
    }
}
