using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.UI;

// Herramienta de un solo uso para armar los dos timelines del boss de BossLvlOne:
//  1) Nucleo roto (ultimo) -> camara de la planta enojada, con movimiento propio en vez de un blend automatico.
//  2) Planta purificada -> dialogo placeholder -> fade a negro.
// Pensada para correrse UNA vez via:
//   Unity.exe -batchmode -quit -projectPath <proyecto> -executeMethod BossLvlOneCinematicBuilder.Build
// Las referencias se resuelven por nombre/jerarquia (confirmados a mano leyendo el YAML de la escena), no por
// fileID: GetLocalIdentifierInFile no devuelve nada util en batchmode en esta version de Unity. No es una
// herramienta reusable para otra escena.
public static class BossLvlOneCinematicBuilder
{
    private const string ScenePath = "Assets/Scenes/BossLvlOne.unity";
    private const string TimelineFolder = "Assets/Art/Timeline";
    private const string ChallengeManagerName = "ChallengeMananger";
    private const string ClearShotName = "ClearShot Camera";       // hermano de ChallengeMananger
    private const string CamFinishChildName = "CinemachineCamera"; // hijo de ClearShot Camera; es el ex _camFinish

    private const double CoreBeatDuration = 2.5;  // BossPlantPhases._camSeconds
    private const double NibbleDelay = 0.8;       // BossPlantPhases._nibbleDelay
    private const double ShieldCamDuration = 3.0; // placeholder, ajustable en el Timeline

    private const double PurifiedTotalDuration = 6.0; // placeholder, ajustable en el Timeline
    private const double DialogueStart = 1.0;
    private const double DialogueEnd = 4.0;
    private const double FadeStart = 4.2;

    [MenuItem("Tools/Gekko/Build Boss LvlOne Cinematics")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject challengeManagerGO = GameObject.Find(ChallengeManagerName);
        if(challengeManagerGO == null)
        {
            Debug.LogError($"[BossLvlOneCinematicBuilder] No se encontro '{ChallengeManagerName}' en la escena. Abortando.");
            return;
        }

        PurificationChallenge challenge = challengeManagerGO.GetComponent<PurificationChallenge>();
        BossPlantPhases phases = challengeManagerGO.GetComponent<BossPlantPhases>();
        if(challenge == null || phases == null)
        {
            Debug.LogError($"[BossLvlOneCinematicBuilder] '{ChallengeManagerName}' no tiene PurificationChallenge/BossPlantPhases. Abortando.");
            return;
        }

        SerializedObject phasesSO = new SerializedObject(phases);
        CinemachineCamera camCoreBroken = phasesSO.FindProperty("_camCoreBroken").objectReferenceValue as CinemachineCamera;
        Animator plantAnimator = phasesSO.FindProperty("_plantAnimator").objectReferenceValue as Animator;

        Transform clearShot = challengeManagerGO.transform.parent != null ? challengeManagerGO.transform.parent.Find(ClearShotName) : null;
        Transform camFinishT = clearShot != null ? clearShot.Find(CamFinishChildName) : null;
        CinemachineCamera camFinish = camFinishT != null ? camFinishT.GetComponent<CinemachineCamera>() : null;

        if(camCoreBroken == null || camFinish == null || plantAnimator == null)
        {
            Debug.LogError($"[BossLvlOneCinematicBuilder] Referencias no encontradas (camCoreBroken={camCoreBroken}, " +
                            $"camFinish={camFinish}, plantAnimator={plantAnimator}). Abortando sin modificar nada.");
            return;
        }

        UIManager uiManager = Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        if(uiManager == null)
        {
            Debug.LogError("[BossLvlOneCinematicBuilder] No se encontro UIManager en la escena. Abortando.");
            return;
        }

        if(!AssetDatabase.IsValidFolder(TimelineFolder))
            AssetDatabase.CreateFolder("Assets/Art", "Timeline");

        GameObject shieldCamGO = BuildShieldDownCamera(camCoreBroken);
        GameObject faderGO = BuildScreenFader();
        ScreenFader fader = faderGO.GetComponent<ScreenFader>();
        GameObject dialogueGO = BuildPurifiedDialogue(challengeManagerGO.transform);

        BossCinematicSignals bridge = challengeManagerGO.GetComponent<BossCinematicSignals>();
        if(bridge == null) bridge = challengeManagerGO.AddComponent<BossCinematicSignals>();
        SerializedObject bridgeSO = new SerializedObject(bridge);
        bridgeSO.FindProperty("_purifiedDialogue").objectReferenceValue = dialogueGO.GetComponent<DialogueNPC>();
        bridgeSO.ApplyModifiedPropertiesWithoutUndo();

        PlayableDirector shieldDownDirector = BuildShieldDownTimeline(camCoreBroken, shieldCamGO, plantAnimator, challengeManagerGO.transform);
        PlayableDirector plantPurifiedDirector = BuildPlantPurifiedTimeline(camFinish, bridge, uiManager, fader, challengeManagerGO.transform);

        SerializedObject challengeSO = new SerializedObject(challenge);
        challengeSO.FindProperty("_shieldDownDirector").objectReferenceValue = shieldDownDirector;
        challengeSO.FindProperty("_plantPurifiedDirector").objectReferenceValue = plantPurifiedDirector;
        challengeSO.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[BossLvlOneCinematicBuilder] Listo: timelines creados, referencias cableadas y escena guardada.");
    }

    // Paso aditivo: cablea el mismo SkipCinematicPrompt (creado por AddSkipPrompt) en el
    // ParkourCinematicTrigger de "TIMELINE TriggerFight" (la cinematica inicial que presenta
    // los nucleos y la planta). Correr DESPUES de AddSkipPrompt.
    [MenuItem("Tools/Gekko/Wire Boss LvlOne Intro Skip")]
    public static void WireIntroSkip()
    {
        const string triggerFightName = "TIMELINE TriggerFight";

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject triggerGO = GameObject.Find(triggerFightName);
        ParkourCinematicTrigger trigger = triggerGO != null ? triggerGO.GetComponent<ParkourCinematicTrigger>() : null;
        SkipCinematicPrompt prompt = Object.FindFirstObjectByType<SkipCinematicPrompt>(FindObjectsInactive.Include);

        if(trigger == null || prompt == null)
        {
            Debug.LogError($"[BossLvlOneCinematicBuilder] No se encontro '{triggerFightName}' (ParkourCinematicTrigger) o el SkipCinematicPrompt existente. Abortando.");
            return;
        }

        SerializedObject so = new SerializedObject(trigger);
        so.FindProperty("_skipPrompt").objectReferenceValue = prompt;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BossLvlOneCinematicBuilder] Listo: skip de la cinematica inicial cableado.");
    }

    // Paso aditivo: arma la pantalla de creditos de cierre (fade a negro permanente + scroll vertical) y
    // reengancha la signal "FadeToBlack" del timeline de planta purificada para que la dispare a ella en vez
    // de a ScreenFader (ese vuelve solo a la jugabilidad; el cierre de nivel no). Correr DESPUES de Build.
    [MenuItem("Tools/Gekko/Build Boss LvlOne Credits")]
    public static void AddCreditsScreen()
    {
        const string plantPurifiedDirectorName = "Director_PlantPurified";
        const string fadeSignalName = "FadeToBlack";

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject challengeManagerGO = GameObject.Find(ChallengeManagerName);
        BossCinematicSignals bridge = challengeManagerGO != null ? challengeManagerGO.GetComponent<BossCinematicSignals>() : null;
        GameObject directorGO = GameObject.Find(plantPurifiedDirectorName);
        SignalReceiver receiver = directorGO != null ? directorGO.GetComponent<SignalReceiver>() : null;
        UIManager uiManager = Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        Canvas rootCanvas = uiManager != null ? uiManager.GetComponentInParent<Canvas>() : null;

        if(bridge == null || receiver == null || rootCanvas == null)
        {
            Debug.LogError("[BossLvlOneCinematicBuilder] Falta BossCinematicSignals, el SignalReceiver de " +
                            $"'{plantPurifiedDirectorName}' o el Canvas raiz. Abortando.");
            return;
        }

        SignalAsset fadeSignal = null;
        int fadeIndex = -1, i = 0;
        foreach(SignalAsset asset in receiver.GetRegisteredSignals())
        {
            if(asset != null && asset.name == fadeSignalName) { fadeSignal = asset; fadeIndex = i; break; }
            i++;
        }

        if(fadeSignal == null)
        {
            Debug.LogError($"[BossLvlOneCinematicBuilder] No se encontro la signal '{fadeSignalName}' en '{plantPurifiedDirectorName}'. Abortando.");
            return;
        }

        CreditsScreen credits = BuildCreditsScreen(rootCanvas.transform);

        UnityEvent endingReaction = new UnityEvent();
        UnityEventTools.AddVoidPersistentListener(endingReaction, bridge.PlayEndingSequence);
        receiver.ChangeReactionAtIndex(fadeIndex, endingReaction);

        SerializedObject bridgeSO = new SerializedObject(bridge);
        bridgeSO.FindProperty("_creditsScreen").objectReferenceValue = credits;
        bridgeSO.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BossLvlOneCinematicBuilder] Listo: pantalla de creditos creada y enganchada al cierre.");
    }

    private static CreditsScreen BuildCreditsScreen(Transform canvasParent)
    {
        Color bg = new Color(15f / 255f, 29f / 255f, 25f / 255f, 1f);
        Color accent = new Color(0.498f, 0.965f, 1f, 1f);      // #7ff6ff
        Color cream = new Color(1f, 0.965f, 0.867f, 1f);       // #fff6dd

        GameObject root = new GameObject("CreditsScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(canvasParent, false);
        root.SetActive(false);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1100;
        RectTransform rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        GameObject blackGO = new GameObject("BlackBackground", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        blackGO.transform.SetParent(root.transform, false);
        RectTransform blackRt = blackGO.GetComponent<RectTransform>();
        blackRt.anchorMin = Vector2.zero;
        blackRt.anchorMax = Vector2.one;
        blackRt.offsetMin = Vector2.zero;
        blackRt.offsetMax = Vector2.zero;
        blackGO.GetComponent<Image>().color = bg;
        CanvasGroup blackGroup = blackGO.GetComponent<CanvasGroup>();
        blackGroup.alpha = 0f;
        blackGroup.interactable = false;
        blackGroup.blocksRaycasts = false;

        GameObject contentGO = new GameObject("CreditsContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGO.transform.SetParent(root.transform, false);
        RectTransform contentRt = contentGO.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0.5f, 0f);
        contentRt.anchorMax = new Vector2(0.5f, 0f);
        contentRt.pivot = new Vector2(0.5f, 0f);
        contentRt.sizeDelta = new Vector2(900f, 0f);
        contentRt.anchoredPosition = new Vector2(0f, -700f);

        VerticalLayoutGroup layout = contentGO.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 40f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        contentGO.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        AddCreditsLine(contentGO.transform, "¡Gracias por jugar!", 48f, FontStyles.Bold, accent);
        AddCreditsLine(contentGO.transform, "Devs:", 32f, FontStyles.Bold, cream);
        AddCreditsLine(contentGO.transform, "Angelo Barchada\nCinthia Cañete\nNahuel Bertoni\nIara Martinez", 26f, FontStyles.Normal, cream);

        CreditsScreen credits = root.AddComponent<CreditsScreen>();
        SerializedObject so = new SerializedObject(credits);
        so.FindProperty("_blackGroup").objectReferenceValue = blackGroup;
        so.FindProperty("_creditsContent").objectReferenceValue = contentRt;
        so.ApplyModifiedPropertiesWithoutUndo();

        return credits;
    }

    private static void AddCreditsLine(Transform parent, string text, float fontSize, FontStyles style, Color color)
    {
        GameObject go = new GameObject("Line", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
    }

    // Paso aditivo (separado de Build): agrega el prompt de "mantene E para saltear" y lo cablea en
    // PurificationChallenge._skipPrompt. Se corre una sola vez, despues de Build, via:
    //   Unity.exe -batchmode -quit -executeMethod BossLvlOneCinematicBuilder.AddSkipPrompt
    [MenuItem("Tools/Gekko/Build Boss LvlOne Skip Prompt")]
    public static void AddSkipPrompt()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject challengeManagerGO = GameObject.Find(ChallengeManagerName);
        PurificationChallenge challenge = challengeManagerGO != null ? challengeManagerGO.GetComponent<PurificationChallenge>() : null;
        UIManager uiManager = Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        Canvas rootCanvas = uiManager != null ? uiManager.GetComponentInParent<Canvas>() : null;

        if(challenge == null || rootCanvas == null)
        {
            Debug.LogError($"[BossLvlOneCinematicBuilder] No se encontro PurificationChallenge o el Canvas raiz de UIManager. Abortando.");
            return;
        }

        SkipCinematicPrompt prompt = BuildSkipPrompt(rootCanvas.transform);

        SerializedObject challengeSO = new SerializedObject(challenge);
        challengeSO.FindProperty("_skipPrompt").objectReferenceValue = prompt;
        challengeSO.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BossLvlOneCinematicBuilder] Listo: skip prompt creado y cableado.");
    }

    private static SkipCinematicPrompt BuildSkipPrompt(Transform canvasParent)
    {
        GameObject root = new GameObject("SkipCinematicPrompt", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(canvasParent, false);

        RectTransform rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 0f);
        rootRt.anchorMax = new Vector2(0.5f, 0f);
        rootRt.pivot = new Vector2(0.5f, 0f);
        rootRt.sizeDelta = new Vector2(280f, 70f);
        rootRt.anchoredPosition = new Vector2(0f, 60f);

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        GameObject label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(root.transform, false);
        RectTransform labelRt = label.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0f, 0.5f);
        labelRt.anchorMax = new Vector2(1f, 1f);
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;
        TextMeshProUGUI labelText = label.GetComponent<TextMeshProUGUI>();
        labelText.text = "Mantené E para saltear";
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.fontSize = 24f;
        labelText.color = Color.white;

        GameObject fillBg = new GameObject("FillBackground", typeof(RectTransform), typeof(Image));
        fillBg.transform.SetParent(root.transform, false);
        RectTransform bgRt = fillBg.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0.1f, 0f);
        bgRt.anchorMax = new Vector2(0.9f, 0.35f);
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        Image bgImage = fillBg.GetComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.6f);

        GameObject fillGO = new GameObject("FillAmount", typeof(RectTransform), typeof(Image));
        fillGO.transform.SetParent(fillBg.transform, false);
        RectTransform fillRt = fillGO.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        Image fillImage = fillGO.GetComponent<Image>();
        fillImage.color = new Color(0.9f, 0.85f, 0.2f, 1f);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 0f;

        SkipCinematicPrompt prompt = root.AddComponent<SkipCinematicPrompt>();
        SerializedObject so = new SerializedObject(prompt);
        so.FindProperty("_group").objectReferenceValue = group;
        so.FindProperty("_fill").objectReferenceValue = fillImage;
        so.ApplyModifiedPropertiesWithoutUndo();

        return prompt;
    }

    #region Scene objects
    private static GameObject BuildShieldDownCamera(CinemachineCamera reference)
    {
        GameObject go = new GameObject("Cam_ShieldDown_PlantaEnojada");
        go.transform.SetParent(reference.transform.parent, false);
        go.transform.position = reference.transform.position + reference.transform.right * 2.5f;
        go.transform.rotation = reference.transform.rotation;

        CinemachineCamera cam = go.AddComponent<CinemachineCamera>();
        cam.Priority = 30;
        go.AddComponent<Animator>();
        go.SetActive(false);
        return go;
    }

    private static GameObject BuildScreenFader()
    {
        GameObject canvasGO = new GameObject("Canvas_ScreenFader", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        GameObject imageGO = new GameObject("Image_Fade", typeof(RectTransform), typeof(Image));
        imageGO.transform.SetParent(canvasGO.transform, false);
        RectTransform rt = imageGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = imageGO.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = false;

        ScreenFader fader = canvasGO.AddComponent<ScreenFader>();
        SerializedObject so = new SerializedObject(fader);
        so.FindProperty("_fadeImage").objectReferenceValue = img;
        so.ApplyModifiedPropertiesWithoutUndo();

        return canvasGO;
    }

    private static GameObject BuildPurifiedDialogue(Transform parent)
    {
        GameObject go = new GameObject("Dialogue_PlantPurified", typeof(SphereCollider));
        go.transform.SetParent(parent, false);

        SphereCollider col = go.GetComponent<SphereCollider>();
        col.enabled = false;

        DialogueNPC npc = go.AddComponent<DialogueNPC>();
        SerializedObject so = new SerializedObject(npc);
        SerializedProperty dialogueProp = so.FindProperty("_dialogue");
        dialogueProp.FindPropertyRelative("speakerName").stringValue = "Planta";
        SerializedProperty sentences = dialogueProp.FindPropertyRelative("sentences");
        sentences.arraySize = 1;
        sentences.GetArrayElementAtIndex(0).stringValue = "PLACEHOLDER: la planta ha sido purificada.";
        so.ApplyModifiedPropertiesWithoutUndo();

        return go;
    }

    private static Animator GetOrAddAnimator(GameObject go)
    {
        Animator anim = go.GetComponent<Animator>();
        if(anim == null) anim = go.AddComponent<Animator>();
        return anim;
    }
    #endregion

    #region Timelines
    private static PlayableDirector BuildShieldDownTimeline(CinemachineCamera camCoreBroken, GameObject shieldCamGO, Animator plantAnimator, Transform parent)
    {
        TimelineAsset timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        AssetDatabase.CreateAsset(timeline, $"{TimelineFolder}/BossLvlOne_ShieldDown.playable");

        ActivationTrack coreBrokenTrack = timeline.CreateTrack<ActivationTrack>("Cam Nucleo Roto (activation)");
        coreBrokenTrack.postPlaybackState = ActivationTrack.PostPlaybackState.Revert;
        TimelineClip coreClip = coreBrokenTrack.CreateDefaultClip();
        coreClip.start = 0;
        coreClip.duration = CoreBeatDuration;

        SignalTrack signalTrack = timeline.CreateTrack<SignalTrack>("Signals");
        SignalAsset nibbleSignal = CreateSignalAsset(timeline, "Nibble");
        SignalEmitter nibbleEmitter = signalTrack.CreateMarker<SignalEmitter>(NibbleDelay);
        nibbleEmitter.asset = nibbleSignal;

        ActivationTrack shieldCamTrack = timeline.CreateTrack<ActivationTrack>("Cam Planta Enojada (activation)");
        shieldCamTrack.postPlaybackState = ActivationTrack.PostPlaybackState.Revert;
        TimelineClip shieldActClip = shieldCamTrack.CreateDefaultClip();
        shieldActClip.start = CoreBeatDuration;
        shieldActClip.duration = ShieldCamDuration;

        AnimationTrack shieldAnimTrack = timeline.CreateTrack<AnimationTrack>("Cam Planta Enojada (movimiento)");
        AnimationClip animClip = BuildPlaceholderCameraClip(shieldCamGO.transform, ShieldCamDuration, "BossLvlOne_ShieldDown_CamAnim", timeline);
        TimelineClip shieldAnimClip = shieldAnimTrack.CreateClip(animClip);
        shieldAnimClip.start = CoreBeatDuration;
        shieldAnimClip.duration = ShieldCamDuration;

        GameObject directorGO = new GameObject("Director_ShieldDown");
        directorGO.transform.SetParent(parent, false);
        PlayableDirector director = directorGO.AddComponent<PlayableDirector>();
        director.playableAsset = timeline;
        director.playOnAwake = false;

        director.SetGenericBinding(coreBrokenTrack, camCoreBroken.gameObject);
        director.SetGenericBinding(shieldCamTrack, shieldCamGO);
        director.SetGenericBinding(shieldAnimTrack, GetOrAddAnimator(shieldCamGO));

        SignalReceiver receiver = directorGO.AddComponent<SignalReceiver>();
        director.SetGenericBinding(signalTrack, receiver);

        UnityEvent nibbleReaction = new UnityEvent();
        UnityEventTools.AddStringPersistentListener(nibbleReaction, plantAnimator.SetTrigger, "nibble");
        receiver.AddReaction(nibbleSignal, nibbleReaction);

        EditorUtility.SetDirty(timeline);
        return director;
    }

    private static PlayableDirector BuildPlantPurifiedTimeline(CinemachineCamera camFinish, BossCinematicSignals bridge, UIManager uiManager, ScreenFader fader, Transform parent)
    {
        TimelineAsset timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        AssetDatabase.CreateAsset(timeline, $"{TimelineFolder}/BossLvlOne_PlantPurified.playable");

        ActivationTrack camTrack = timeline.CreateTrack<ActivationTrack>("Cam Planta Purificada (activation)");
        camTrack.postPlaybackState = ActivationTrack.PostPlaybackState.Revert;
        TimelineClip camClip = camTrack.CreateDefaultClip();
        camClip.start = 0;
        camClip.duration = PurifiedTotalDuration;

        AnimationTrack camAnimTrack = timeline.CreateTrack<AnimationTrack>("Cam Planta Purificada (movimiento)");
        AnimationClip animClip = BuildPlaceholderCameraClip(camFinish.transform, PurifiedTotalDuration, "BossLvlOne_PlantPurified_CamAnim", timeline);
        TimelineClip animTimelineClip = camAnimTrack.CreateClip(animClip);
        animTimelineClip.start = 0;
        animTimelineClip.duration = PurifiedTotalDuration;

        SignalTrack signalTrack = timeline.CreateTrack<SignalTrack>("Signals");
        SignalAsset startDialogueSignal = CreateSignalAsset(timeline, "StartDialogue");
        SignalAsset endDialogueSignal = CreateSignalAsset(timeline, "EndDialogue");
        SignalAsset fadeSignal = CreateSignalAsset(timeline, "FadeToBlack");

        signalTrack.CreateMarker<SignalEmitter>(DialogueStart).asset = startDialogueSignal;
        signalTrack.CreateMarker<SignalEmitter>(DialogueEnd).asset = endDialogueSignal;
        signalTrack.CreateMarker<SignalEmitter>(FadeStart).asset = fadeSignal;

        GameObject directorGO = new GameObject("Director_PlantPurified");
        directorGO.transform.SetParent(parent, false);
        PlayableDirector director = directorGO.AddComponent<PlayableDirector>();
        director.playableAsset = timeline;
        director.playOnAwake = false;

        director.SetGenericBinding(camTrack, camFinish.gameObject);
        director.SetGenericBinding(camAnimTrack, GetOrAddAnimator(camFinish.gameObject));

        SignalReceiver receiver = directorGO.AddComponent<SignalReceiver>();
        director.SetGenericBinding(signalTrack, receiver);

        UnityEvent startReaction = new UnityEvent();
        UnityEventTools.AddVoidPersistentListener(startReaction, bridge.PlayPurifiedDialogue);
        receiver.AddReaction(startDialogueSignal, startReaction);

        UnityEvent endReaction = new UnityEvent();
        UnityEventTools.AddVoidPersistentListener(endReaction, uiManager.EndDialogue);
        receiver.AddReaction(endDialogueSignal, endReaction);

        UnityEvent fadeReaction = new UnityEvent();
        UnityEventTools.AddVoidPersistentListener(fadeReaction, fader.StartFade);
        receiver.AddReaction(fadeSignal, fadeReaction);

        EditorUtility.SetDirty(timeline);
        return director;
    }

    private static SignalAsset CreateSignalAsset(TimelineAsset owner, string name)
    {
        SignalAsset asset = ScriptableObject.CreateInstance<SignalAsset>();
        asset.name = name;
        AssetDatabase.AddObjectToAsset(asset, owner);
        return asset;
    }

    private static AnimationClip BuildPlaceholderCameraClip(Transform cam, double duration, string clipName, TimelineAsset owner)
    {
        AnimationClip clip = new AnimationClip { name = clipName, legacy = false };

        Vector3 startPos = cam.localPosition;
        Vector3 startEuler = cam.localEulerAngles;
        Vector3 endPos = startPos + cam.forward * 0.5f;
        Vector3 endEuler = startEuler + new Vector3(0f, 5f, 0f);

        SetPositionCurve(clip, startPos, endPos, (float)duration);
        SetRotationCurve(clip, startEuler, endEuler, (float)duration);

        AssetDatabase.AddObjectToAsset(clip, owner);
        return clip;
    }

    private static void SetPositionCurve(AnimationClip clip, Vector3 start, Vector3 end, float duration)
    {
        string[] props = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" };
        for(int i = 0; i < 3; i++)
        {
            AnimationCurve curve = new AnimationCurve(new Keyframe(0f, start[i]), new Keyframe(duration, end[i]));
            clip.SetCurve("", typeof(Transform), props[i], curve);
        }
    }

    private static void SetRotationCurve(AnimationClip clip, Vector3 startEuler, Vector3 endEuler, float duration)
    {
        string[] props = { "localEulerAnglesRaw.x", "localEulerAnglesRaw.y", "localEulerAnglesRaw.z" };
        for(int i = 0; i < 3; i++)
        {
            AnimationCurve curve = new AnimationCurve(new Keyframe(0f, startEuler[i]), new Keyframe(duration, endEuler[i]));
            clip.SetCurve("", typeof(Transform), props[i], curve);
        }
    }
    #endregion
}
