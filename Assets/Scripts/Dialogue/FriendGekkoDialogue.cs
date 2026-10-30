using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

// Diálogo del gekko azul que aparece al purificar a CarniborousPlantBOSS (ver BossGekkoReveal). Al arrancar pone
// una cámara de plano doble (los dos gekkos en cuadro) vía CameraStateManager, el mismo sistema de cambio de
// cámara que usa el resto del juego. Al cerrar la conversación dispara la salida del nivel: fade a negro
// (ScreenFader) y, con la pantalla ya negra, pasa por la pantalla de carga hacia la escena siguiente.
public class FriendGekkoDialogue : DialogueNPC
{
    [Header("Siguiente escena")]
    [Tooltip("Escena a la que se va después de hablar con el gekko (pasa por SceneLoader).")]
    [SerializeField] private ScenesNames _nextScene = ScenesNames.Aldea;

    [Header("Cámara de diálogo")]
    [Tooltip("Cámara de plano doble, registrada en CameraStateManager, que encuadra a los dos gekkos mientras hablan. Vacío = no cambia de cámara.")]
    [SerializeField] private CinemachineCamera _twoShotCamera;
    [Tooltip("Tag con el que CameraStateManager registró esta cámara.")]
    [SerializeField] private string _twoShotCameraTag = "DialogueTwoShot";
    [Tooltip("Qué tan al costado se para la cámara respecto a la línea que une a los dos gekkos.")]
    [SerializeField] private float _sideDistance = 3f;
    [Tooltip("Altura de la cámara sobre el punto medio entre los dos.")]
    [SerializeField] private float _heightOffset = 0.6f;
    [Tooltip("Altura del punto al que apunta, sobre el punto medio entre los dos (en el piso).")]
    [SerializeField] private float _lookHeightOffset = 1f;

    private bool _leaving;

    // Arranca solo al aparecer (ver BossGekkoReveal), con el player donde haya quedado tras la pelea: si se
    // cerrara por distancia, terminaría en el primer frame y saltaría directo a la escena siguiente.
    public override bool CloseByDistance => false;

    public override void OnDialogueStart()
    {
        base.OnDialogueStart();
        FrameTwoShot();
    }

    public override void OnDialogueEnd()
    {
        base.OnDialogueEnd();

        if(_twoShotCamera != null && CameraStateManager.Instance != null)
            EventManager.Trigger(CameraStateManager.Instance.primaryCameraTag);

        if(_leaving) return;
        _leaving = true;

        if(ScreenFader.Instance == null)
        {
            LoadNextScene();
            return;
        }

        ScreenFader.Instance.OnFadeMiddle += OnScreenBlack;
        ScreenFader.Instance.StartFade();
    }

    // Pone la cámara de diálogo de perfil, encuadrando a los dos (plano de conversación clásico), y la activa vía
    // CameraStateManager: no la posicionamos en el Inspector porque el player puede acercarse desde cualquier lado.
    private void FrameTwoShot()
    {
        if(_twoShotCamera == null || GameManager.Instance == null || GameManager.Instance.Pj == null) return;

        Transform player = GameManager.Instance.Pj.transform;
        Transform self = transform;

        Vector3 line = self.position - player.position;
        line.y = 0f;
        if(line.sqrMagnitude < 0.0001f) line = Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, line.normalized);

        Vector3 mid = (player.position + self.position) * 0.5f;
        Vector3 lookPoint = mid + Vector3.up * _lookHeightOffset;
        Vector3 camPos = mid + side * _sideDistance + Vector3.up * _heightOffset;

        Transform camTransform = _twoShotCamera.transform;
        camTransform.position = camPos;
        camTransform.rotation = Quaternion.LookRotation(lookPoint - camPos, Vector3.up);

        EventManager.Trigger(_twoShotCameraTag);
    }

    // Se llama con la pantalla ya en negro: ahí no se nota el corte de escena.
    private void OnScreenBlack()
    {
        if(ScreenFader.Instance != null) ScreenFader.Instance.OnFadeMiddle -= OnScreenBlack;
        LoadNextScene();
    }

    private void LoadNextScene()
    {
        AsyncLoader.SetSceneToLoad(_nextScene);
        SceneManager.LoadScene(ScenesDictionary.SceneName[ScenesNames.LoadScene]);
    }
}
