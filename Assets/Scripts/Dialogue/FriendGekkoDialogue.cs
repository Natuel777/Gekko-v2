using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

// Dialogue of the friendly gekko that appears when the boss plant is purified (see BossGekkoReveal): Celeste (blue) in LvlOne,
// Moki (green) in LvlTwo. Same component in both: the dialogue and the object's materials change.
// Staging of the conversation (if _stageConversation is on):
//   1. Short fade to black (only ScreenFader's fade, without touching inputs: the dialogue already blocks Gekko).
//   2. In the dark: Gekko is placed in front of Celeste at a fixed distance and the two face each other.
//   3. Own dialogue camera (high priority, direct cut): the first line is a shot of both and from the
//      second one it cuts to a close-up of whoever is speaking, over the shoulder of the other (according to UIManager.OnDialogueLine).
// The shot sizes are computed from the characters' real height, so it works at any scale.
// When the conversation closes it triggers the level exit: fade to black (ScreenFader) and loading screen.
// Every line that is shown also plays one Gekko voice sound (see Talk sounds), never the same one twice in a row.
public class FriendGekkoDialogue : DialogueNPC
{
    [Header("Next scene")]
    [Tooltip("Escena a la que se va después de hablar con el gekko (pasa por SceneLoader).")]
    [SerializeField] private ScenesNames _nextScene = ScenesNames.Aldea;
    [Tooltip("Nombre con el que se registra el rescate (ver AldeaRescates): la Aldea muestra en su casa al AldeaHabitante " +
             "con el mismo nombre. Vacío = usa el speakerName del diálogo (Celeste, Moki...).")]
    [SerializeField] private string _rescateId;

    private string RescateId => string.IsNullOrWhiteSpace(_rescateId) ? Dialogue.speakerName : _rescateId;

    [Header("Talk sounds")]
    [Tooltip("Sonidos de Gekko (SoundNames) entre los que se sortea uno por cada línea del diálogo, la diga quien la diga (Gekko o el gekko rescatado). Un sonido no puede sonar en dos líneas seguidas: tiene que esperar un turno para poder sonar de nuevo. Vacío = sin sonido.")]
    [SerializeField] private SoundNames[] _talkSounds =
    {
        SoundNames.PlayerHmmh,
        SoundNames.PlayerWaah,
        SoundNames.PlayerOwowo
    };

    [Header("Conversation staging")]
    [Tooltip("Ubica a Gekko frente a Celeste y usa planos fijos (plano doble + primer plano del que habla).")]
    [SerializeField] private bool _stageConversation = true;
    [Tooltip("Fundido corto a negro para que no se note el acomodo de los personajes.")]
    [SerializeField] private bool _fadeToStage = true;
    [Tooltip("Distancia entre los dos, en alturas de Gekko (2.2 = poco más de dos Gekkos de distancia).")]
    [Min(0.5f)]
    [SerializeField] private float _conversationDistance = 2.2f;
    [Tooltip("Nombre de hablante (en speakerNames del diálogo) que corresponde a Gekko. Cualquier otro = el gekko amigo (Celeste, Moki...).")]
    [SerializeField] private string _playerSpeakerName = "Gekko";
    [Tooltip("Cuántas líneas se ven en plano doble antes de pasar a primeros planos.")]
    [Min(0)]
    [SerializeField] private int _twoShotLines = 1;

    [Header("Close-up (over the shoulder)")]
    [Tooltip("Qué tan atrás del que escucha va la cámara, en alturas de personaje.")]
    [SerializeField] private float _closeUpBack = 1.1f;
    [Tooltip("Corrimiento al costado (hombro), en alturas de personaje.")]
    [SerializeField] private float _closeUpSide = 0.8f;
    [Tooltip("Altura de la cámara sobre la cara del que habla, en alturas de personaje.")]
    [SerializeField] private float _closeUpHeight = 0.2f;
    [Tooltip("Baja el punto al que mira la cámara (en alturas de personaje) para que la cara quede en el tercio de arriba del cuadro.")]
    [SerializeField] private float _closeUpFaceRaise = 0.3f;
    [Range(15f, 80f)]
    [SerializeField] private float _closeUpFov = 28f;
    [Range(15f, 80f)]
    [SerializeField] private float _twoShotFov = 45f;

    [Header("Dialogue camera (without staging)")]
    [Tooltip("Cámara de plano doble, registrada en CameraStateManager, que se usa si _stageConversation está apagado. Vacío = no cambia de cámara.")]
    [SerializeField] private CinemachineCamera _twoShotCamera;
    [Tooltip("Tag con el que CameraStateManager registró esta cámara.")]
    [SerializeField] private string _twoShotCameraTag = "DialogueTwoShot";
    [Tooltip("Distancia mínima al costado de la línea que une a los dos gekkos. Si están muy separados se aleja más sola para que entren los dos (ver _framingPadding).")]
    [SerializeField] private float _sideDistance = 5f;
    [Tooltip("Espacio extra a cada lado de los gekkos al calcular cuánto alejarse para que entren los dos en cuadro.")]
    [Min(0f)]
    [SerializeField] private float _framingPadding = 1.5f;
    [Tooltip("Altura extra de la cámara sobre el punto medio entre los dos, sumada a la que da _downAngle.")]
    [SerializeField] private float _heightOffset = 0.6f;
    [Tooltip("Grados que mira hacia abajo: la cámara sube con la distancia para mantener este ángulo (y no quedar tapada por el pasto).")]
    [Range(0f, 80f)]
    [SerializeField] private float _downAngle = 30f;
    [Tooltip("Altura del punto al que apunta, sobre el punto medio entre los dos (en el piso).")]
    [SerializeField] private float _lookHeightOffset = 1f;

    private const int DialogueCameraPriority = 100;

    private bool _leaving;
    private bool _staged;          // they are already placed and the dialogue camera is active
    private int _lineIndex = -1;
    private string _currentSpeaker;
    private CinemachineCamera _dialogueCam;
    private CinemachineBrain _brain;
    private CinemachineBlendDefinition _savedBlend;
    private bool _blendSaved;
    private NoRepeatSoundPicker _talkPicker; // lives as long as this gekko, so the "no repeat" rule holds across all its lines

    // It starts by itself when it appears (see BossGekkoReveal), with the player wherever the fight left them: if it
    // closed by distance, it would end on the first frame and jump straight to the next scene.
    public override bool CloseByDistance => false;

    public override void OnDialogueStart()
    {
        base.OnDialogueStart();

        // One Gekko voice sound per line, whoever says it. UIManager raises OnDialogueLine when it starts each line, the first one
        // included (this hook runs right before it), and the voice does not wait for the staging fade. The -= avoids a double subscription.
        if(UIManager.Instance != null)
        {
            UIManager.Instance.OnDialogueLine -= PlayTalkSound;
            UIManager.Instance.OnDialogueLine += PlayTalkSound;
        }

        if(!_stageConversation)
        {
            FrameTwoShot();
            return;
        }

        _lineIndex = -1;
        _staged = false;
        if(UIManager.Instance != null) UIManager.Instance.OnDialogueLine += OnDialogueLine;
        StartCoroutine(StageRoutine());
    }

    public override void OnDialogueEnd()
    {
        base.OnDialogueEnd();

        if(UIManager.Instance != null) UIManager.Instance.OnDialogueLine -= OnDialogueLine;
        if(UIManager.Instance != null) UIManager.Instance.OnDialogueLine -= PlayTalkSound;

        if(!_stageConversation && _twoShotCamera != null && CameraStateManager.Instance != null)
            EventManager.Trigger(CameraStateManager.Instance.primaryCameraTag);

        if(_leaving) return;
        _leaving = true;

        // The rescue conversation is over: from now on this gekko lives in its house in the Village (Aldea).
        AldeaRescates.Registrar(RescateId);

        if(ScreenFader.Instance == null)
        {
            LoadNextScene();
            return;
        }

        ScreenFader.Instance.OnFadeMiddle += OnScreenBlack;
        ScreenFader.Instance.StartFade();
    }

    private void OnDestroy()
    {
        if(UIManager.Instance != null) UIManager.Instance.OnDialogueLine -= OnDialogueLine;
        if(UIManager.Instance != null) UIManager.Instance.OnDialogueLine -= PlayTalkSound;
        RestoreBlend();
    }

    // ------------------------------------------------------------------ Staging

    private IEnumerator StageRoutine()
    {
        bool fade = _fadeToStage && ScreenFader.Instance != null;
        if(fade) yield return ScreenFader.Instance.FadeToBlack();

        PlaceCharacters();
        ActivateDialogueCamera();
        _staged = true;
        ApplyShot(); // shot of the line that is already being shown

        if(fade) yield return ScreenFader.Instance.FadeFromBlack();
    }

    private void OnDialogueLine(string speaker)
    {
        _lineIndex++;
        _currentSpeaker = speaker;
        if(_staged) ApplyShot();
    }

    // Plays one Gekko voice sound for the line that is starting (both speakers are gekkos, so every line gets one). The picker never
    // repeats a sound right after itself: the one that sounded has to wait one line before it can sound again.
    private void PlayTalkSound(string speaker)
    {
        if(AudioManager.instance == null) return;

        _talkPicker ??= new NoRepeatSoundPicker(_talkSounds);
        if(_talkPicker.TryPick(out SoundNames sound)) AudioManager.instance.Play(sound);
    }

    // Gekko at a fixed distance in front of Celeste, on the same side it was on (so it doesn't end up inside the plant
    // or a wall), standing on the ground, and the two facing each other.
    private void PlaceCharacters()
    {
        var pj = GameManager.Instance != null ? GameManager.Instance.Pj : null;
        if(pj == null) return;

        Transform player = pj.transform;
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if(dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();

        float distance = _conversationDistance * CharacterHeight(player);
        Vector3 target = transform.position + dir * distance;

        // Settle it on the ground (same surfaces LayerMask the camera uses).
        LayerMask ground = GameManager.Instance.Surfaces;
        if(Physics.Raycast(target + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 10f, ground, QueryTriggerInteraction.Ignore))
            target.y = hit.point.y;

        var rb = player.GetComponentInChildren<Rigidbody>();
        if(rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = target;
        }
        player.position = target;
        player.rotation = Quaternion.LookRotation(-dir, Vector3.up);
        if(rb != null) rb.rotation = player.rotation;

        transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }

    private void ActivateDialogueCamera()
    {
        if(_dialogueCam == null)
        {
            var go = new GameObject("FriendGekko_DialogueCam");
            _dialogueCam = go.AddComponent<CinemachineCamera>();
        }

        // Direct cut to the dialogue camera (the screen is black anyway, but without a blend it doesn't end up halfway).
        var main = Camera.main;
        _brain = main != null ? main.GetComponent<CinemachineBrain>() : null;
        if(_brain != null && !_blendSaved)
        {
            _savedBlend = _brain.DefaultBlend;
            _blendSaved = true;
            _brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
        }

        _dialogueCam.Priority = DialogueCameraPriority;
    }

    private void RestoreBlend()
    {
        if(_brain != null && _blendSaved) _brain.DefaultBlend = _savedBlend;
        _blendSaved = false;
    }

    private void ApplyShot()
    {
        if(_dialogueCam == null || GameManager.Instance == null || GameManager.Instance.Pj == null) return;

        Transform gekko = GameManager.Instance.Pj.transform;
        Transform celeste = transform;

        if(_lineIndex < _twoShotLines)
        {
            SetTwoShot(gekko, celeste);
            return;
        }

        bool gekkoSpeaks = string.Equals(_currentSpeaker, _playerSpeakerName, System.StringComparison.OrdinalIgnoreCase);
        if(gekkoSpeaks) SetCloseUp(gekko, celeste);
        else SetCloseUp(celeste, gekko);
    }

    // Shot of both, in profile (same idea as FrameTwoShot).
    private void SetTwoShot(Transform a, Transform b)
    {
        var lens = _dialogueCam.Lens; lens.FieldOfView = _twoShotFov; _dialogueCam.Lens = lens;

        Vector3 line = b.position - a.position; line.y = 0f;
        if(line.sqrMagnitude < 0.0001f) line = Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, line.normalized);

        float h = Mathf.Max(CharacterHeight(a), CharacterHeight(b));
        Vector3 mid = (a.position + b.position) * 0.5f;
        Vector3 lookPoint = mid + Vector3.up * h * 0.6f;

        float halfV = _twoShotFov * 0.5f * Mathf.Deg2Rad;
        float aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
        float halfH = Mathf.Atan(Mathf.Tan(halfV) * aspect);
        float distance = (line.magnitude * 0.5f + h * 1.2f) / Mathf.Max(Mathf.Tan(halfH), 0.01f);

        Vector3 camPos = lookPoint + side * distance + Vector3.up * distance * Mathf.Tan(12f * Mathf.Deg2Rad);
        PlaceCam(camPos, lookPoint);
    }

    // Close-up of the speaker, with the camera over the listener's shoulder.
    private void SetCloseUp(Transform speaker, Transform listener)
    {
        var lens = _dialogueCam.Lens; lens.FieldOfView = _closeUpFov; _dialogueCam.Lens = lens;

        Vector3 dir = speaker.position - listener.position; dir.y = 0f;
        if(dir.sqrMagnitude < 0.0001f) dir = listener.forward;
        dir.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, dir);

        float h = CharacterHeight(speaker);
        Vector3 face = speaker.position + Vector3.up * h * 0.75f;

        Vector3 camPos = listener.position - dir * (h * _closeUpBack) + side * (h * _closeUpSide);
        camPos.y = face.y + h * _closeUpHeight;
        PlaceCam(camPos, face - Vector3.up * (h * _closeUpFaceRaise));
    }

    private void PlaceCam(Vector3 position, Vector3 lookAt)
    {
        Transform t = _dialogueCam.transform;
        t.position = position;
        t.rotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
    }

    // The character's real height (from its renderers), so the shots work at any scale.
    private static float CharacterHeight(Transform character)
    {
        var renderers = character.GetComponentsInChildren<Renderer>();
        bool any = false;
        Bounds bounds = default;
        foreach(var r in renderers)
        {
            if(r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if(!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        float h = any ? bounds.max.y - character.position.y : 1f;
        return Mathf.Clamp(h, 0.2f, 5f);
    }

    // ------------------------------------------------------------------ Without staging (previous behavior)

    // Puts the dialogue camera in profile, framing both (classic conversation shot), and activates it through
    // CameraStateManager: we don't position it in the Inspector because the player can approach from any side.
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
        float distance = GetFramingDistance(line.magnitude);
        float height = _lookHeightOffset + distance * Mathf.Tan(_downAngle * Mathf.Deg2Rad) + _heightOffset;
        Vector3 camPos = mid + side * distance + Vector3.up * height;

        Transform camTransform = _twoShotCamera.transform;
        camTransform.position = camPos;
        camTransform.rotation = Quaternion.LookRotation(lookPoint - camPos, Vector3.up);

        EventManager.Trigger(_twoShotCameraTag);
    }

    // Distance the camera has to be at for both gekkos to fit (plus _framingPadding on each side)
    // with the dialogue camera's horizontal FOV. Never closer than _sideDistance.
    private float GetFramingDistance(float separation)
    {
        Camera main = Camera.main;
        float aspect = main != null ? main.aspect : 16f / 9f;
        float halfVertical = _twoShotCamera.Lens.FieldOfView * 0.5f * Mathf.Deg2Rad;
        float halfHorizontal = Mathf.Atan(Mathf.Tan(halfVertical) * aspect);

        float halfWidth = separation * 0.5f + _framingPadding;
        float needed = halfWidth / Mathf.Max(Mathf.Tan(halfHorizontal), 0.01f);

        return Mathf.Max(_sideDistance, needed);
    }

    // Called with the screen already black: the scene cut isn't noticeable there.
    private void OnScreenBlack()
    {
        if(ScreenFader.Instance != null) ScreenFader.Instance.OnFadeMiddle -= OnScreenBlack;
        LoadNextScene();
    }

    private void LoadNextScene()
    {
        RestoreBlend();
        AsyncLoader.SetSceneToLoad(_nextScene);
        SceneManager.LoadScene(ScenesDictionary.SceneName[ScenesNames.LoadScene]);
    }
}
