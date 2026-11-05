using Unity.Cinemachine;
using UnityEngine;

// Cortes de cámara para la secuencia de cuando vuelve un patito (MomDuck): mientras la pantalla está negra, cualquier
// cambio de cámara es un corte. Un blend se vería al volver la imagen, y desde Gekko cruzaría medio mapa.
public static class DuckCameraTransition
{
    private static int _cutUntilFrame = -1;
    private static bool _registered;

    // Corta los cambios de cámara de este frame y el siguiente (el brain los toma en LateUpdate).
    public static void CutNow()
    {
        Register();
        _cutUntilFrame = Time.frameCount + 1;
    }

    // Después de un corte, la cámara que quedó en vivo (la de Gekko tiene damping) arranca en su lugar en vez de
    // deslizarse desde donde la dejó la última vez.
    public static void ResetLiveCamera()
    {
        CinemachineBrain brain = Object.FindFirstObjectByType<CinemachineBrain>();

        if (brain != null && brain.ActiveVirtualCamera is CinemachineVirtualCameraBase live)
            live.PreviousStateIsValid = false;
    }

    private static void Register()
    {
        if (_registered) return;
        _registered = true;
        CinemachineCore.GetBlendOverride += BlendOverride;
    }

    private static CinemachineBlendDefinition BlendOverride(ICinemachineCamera from, ICinemachineCamera to, CinemachineBlendDefinition defaultBlend, Object owner)
    {
        if (Time.frameCount <= _cutUntilFrame)
            return new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);

        return defaultBlend;
    }

    // CinemachineCore limpia GetBlendOverride al recargar el dominio: hay que volver a registrarse.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _registered = false;
        _cutUntilFrame = -1;
    }
}
