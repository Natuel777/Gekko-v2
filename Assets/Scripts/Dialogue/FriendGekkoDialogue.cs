using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

// Diálogo del gekko azul (Celeste) que aparece al purificar a CarniborousPlantBOSS (ver BossGekkoReveal).
// Puesta en escena de la charla (si _stageConversation está prendido):
//   1. Fundido corto a negro (solo el fundido de ScreenFader, sin tocar inputs: el diálogo ya bloquea a Gekko).
//   2. En negro: Gekko se ubica frente a Celeste a una distancia fija y los dos se miran.
//   3. Cámara de diálogo propia (prioridad alta, corte directo): la primera línea es un plano de los dos y desde la
//      segunda corta a un primer plano del que habla, por encima del hombro del otro (según UIManager.OnDialogueLine).
// Los tamaños de los planos se calculan con la altura real de los personajes, así funciona con cualquier escala.
// Al cerrar la conversación dispara la salida del nivel: fade a negro (ScreenFader) y pantalla de carga.
public class FriendGekkoDialogue : DialogueNPC
{
    [Header("Siguiente escena")]
    [Tooltip("Escena a la que se va después de hablar con el gekko (pasa por SceneLoader).")]
    [SerializeField] private ScenesNames _nextScene = ScenesNames.Aldea;

    [Header("Puesta en escena de la charla")]
    [Tooltip("Ubica a Gekko frente a Celeste y usa planos fijos (plano doble + primer plano del que habla).")]
    [SerializeField] private bool _stageConversation = true;
    [Tooltip("Fundido corto a negro para que no se note el acomodo de los personajes.")]
    [SerializeField] private bool _fadeToStage = true;
    [Tooltip("Distancia entre los dos, en alturas de Gekko (2.2 = poco más de dos Gekkos de distancia).")]
    [Min(0.5f)]
    [SerializeField] private float _conversationDistance = 2.2f;
    [Tooltip("Nombre de hablante (en speakerNames del diálogo) que corresponde a Gekko. Cualquier otro = Celeste.")]
    [SerializeField] private string _playerSpeakerName = "Gekko";
    [Tooltip("Cuántas líneas se ven en plano doble antes de pasar a primeros planos.")]
    [Min(0)]
    [SerializeField] private int _twoShotLines = 1;

    [Header("Primer plano (por encima del hombro)")]
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

    [Header("Cámara de diálogo (sin puesta en escena)")]
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
    private bool _staged;          // ya están ubicados y la cámara de diálogo está activa
    private int _lineIndex = -1;
    private string _currentSpeaker;
    private CinemachineCamera _dialogueCam;
    private CinemachineBrain _brain;
    private CinemachineBlendDefinition _savedBlend;
    private bool _blendSaved;

    // Arranca solo al aparecer (ver BossGekkoReveal), con el player donde haya quedado tras la pelea: si se
    // cerrara por distancia, terminaría en el primer frame y saltaría directo a la escena siguiente.
    public override bool CloseByDistance => false;

    public override void OnDialogueStart()
    {
        base.OnDialogueStart();

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

        if(!_stageConversation && _twoShotCamera != null && CameraStateManager.Instance != null)
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

    private void OnDestroy()
    {
        if(UIManager.Instance != null) UIManager.Instance.OnDialogueLine -= OnDialogueLine;
        RestoreBlend();
    }

    // ------------------------------------------------------------------ Puesta en escena

    private IEnumerator StageRoutine()
    {
        bool fade = _fadeToStage && ScreenFader.Instance != null;
        if(fade) yield return ScreenFader.Instance.FadeToBlack();

        PlaceCharacters();
        ActivateDialogueCamera();
        _staged = true;
        ApplyShot(); // plano de la línea que ya se esté mostrando

        if(fade) yield return ScreenFader.Instance.FadeFromBlack();
    }

    private void OnDialogueLine(string speaker)
    {
        _lineIndex++;
        _currentSpeaker = speaker;
        if(_staged) ApplyShot();
    }

    // Gekko a una distancia fija frente a Celeste, del mismo lado en que estaba (así no queda dentro de la planta
    // ni de una pared), apoyado en el piso, y los dos mirándose.
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

        // Asentarlo en el piso (mismo LayerMask de superficies que usa la cámara).
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

        // Corte directo hacia la cámara de diálogo (la pantalla está en negro igual, pero sin blend no queda a medio camino).
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

    // Plano de los dos, de perfil (misma idea que FrameTwoShot).
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

    // Primer plano del que habla, con la cámara por encima del hombro del que escucha.
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

    // Altura real del personaje (de sus renderers), para que los planos sirvan a cualquier escala.
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

    // ------------------------------------------------------------------ Sin puesta en escena (comportamiento anterior)

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
        float distance = GetFramingDistance(line.magnitude);
        float height = _lookHeightOffset + distance * Mathf.Tan(_downAngle * Mathf.Deg2Rad) + _heightOffset;
        Vector3 camPos = mid + side * distance + Vector3.up * height;

        Transform camTransform = _twoShotCamera.transform;
        camTransform.position = camPos;
        camTransform.rotation = Quaternion.LookRotation(lookPoint - camPos, Vector3.up);

        EventManager.Trigger(_twoShotCameraTag);
    }

    // Distancia a la que tiene que estar la cámara para que entren los dos gekkos (más _framingPadding a cada lado)
    // con el FOV horizontal de la cámara de diálogo. Nunca más cerca que _sideDistance.
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

    // Se llama con la pantalla ya en negro: ahí no se nota el corte de escena.
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
