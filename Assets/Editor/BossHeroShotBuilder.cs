using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

// Presentación del boss de LvlOne (director "TIMELINE TriggerFight" de BossZone), en este orden:
//  1. Corte desde la cámara del jugador a un CONTRAPICADO de la planta (cámara casi a ras del piso, dentro del anillo
//     del escudo, acercándose despacio) con el cartel de su nombre (BossTitleCard).
//  2. Corte a la cámara del recorrido, que pasa sin frenar por los 3 núcleos (los spots 1-3 de las cámaras viejas de
//     CombatReveal; el spot 0, el frente de la planta, ya lo cubre el contrapicado).
//  3. Blend de vuelta a la cámara del jugador.
//
// - No toca BossLvlOne_CombatReveal_UnaCamara.playable (lo usa también BossLvlOne): trabaja sobre una copia,
//   LvlOne_BossReveal_Contrapicado.playable. El recorrido usa un clip propio (los núcleos), no el de BossLvlOne.
// - Las keys de las dos cámaras son posiciones locales a BossZone/Camaras ("remove start offset" apagado) y se pueden
//   retocar en la ventana Animation.
// - Se puede volver a correr: recrea cámara/clips/tracks/cartel con los valores de abajo.
//   Menu: Tools > Gekko > Boss > Contrapicado + cartel (LvlOne)
public static class BossHeroShotBuilder
{
    const string ScenePath = "Assets/Scenes/LvlOne.unity";
    const string SourceTimelinePath = "Assets/Art/Timeline/BossLvlOne_CombatReveal_UnaCamara.playable";
    const string NewTimelinePath = "Assets/Art/Timeline/LvlOne_BossReveal_Contrapicado.playable";
    const string HeroClipPath = "Assets/Art/Timeline/LvlOne_BossReveal_Contrapicado_Anim.anim";
    const string TourClipPath = "Assets/Art/Timeline/LvlOne_BossReveal_Nucleos_Anim.anim";
    const string FontPath = "Assets/Art/Fonts/MilkyNice SDF.asset";

    const string DirectorPath = "BossZone/Boss/BossLvlOne/CombatReveal-Cinmematic/CombatSplitCams/TIMELINE TriggerFight";
    const string PlantPath = "BossZone/Boss/BossLvlOne/CorruptionChallenge/CarniborousPlantBOSS";
    const string SpotsPath = "BossZone/Boss/BossLvlOne/CombatReveal-Cinmematic/CombatSplitCams";
    const string CamerasParentPath = "BossZone/Camaras";
    const string TitleCardParentPath = "BossZone/Boss/BossLvlOne/CombatReveal-Cinmematic";
    const string TourCameraName = "CM_CombatReveal_UnaCamara";
    const string TourTrackName = "Camara unica (movimiento)";
    const string CameraName = "CM_Boss_Contrapicado";
    const string AnimTrackName = "Contrapicado (movimiento)";
    const string TitleCardName = "BossTitleCard";
    // Encuadres de las cámaras viejas de CombatReveal que muestran núcleos (la (0) es el frente de la planta).
    static readonly string[] CoreSpotNames = { "CinemachineCamera (1)", "CinemachineCamera (2)", "CinemachineCamera (3)" };
    // Orden en que el recorrido muestra los núcleos. A cada uno se le asigna el encuadre que mejor lo mira.
    static readonly string[] CoreOrder = { "NewNucleo_lp (1)", "NewNucleo_lp (2)", "NewNucleo_lp" };

    // Tiempos (segundos del timeline).
    const float HeroStart = 0.6f;       // un instante en la cámara del jugador y corte a la planta
    const float HeroDuration = 6.0f;
    const float CardShow = 1.1f;
    const float CardHide = 5.8f;        // con el fade de salida ya no está cuando corta a los núcleos
    const float TourLead = 1.0f;        // del corte al primer núcleo, ya en movimiento
    const float CoresSpan = 7.33f;      // del primer al último núcleo (mismo ritmo que el recorrido viejo)
    const float SpotSpeedFactor = 0.5f; // velocidad al pasar por cada núcleo, relativa a la media
    const float ViaClearance = 6f;      // si un tramo pasa a menos de esto de un núcleo/la planta, se agrega un punto elevado
    const float ViaLift = 7f;           // cuánto se eleva ese punto intermedio
    const float BlendBackDelay = 0.5f;  // el blend de vuelta arranca este tiempo DESPUÉS de llegar al último núcleo
    const float BlendBack = 2.65f;
    const float PlayerClipTail = 0.5f;

    // Contrapicado, relativo a la planta (ángulo respecto de hacia dónde mira, distancia horizontal, altura sobre el
    // piso). Arranca un poco más lejos y se acerca rodeándola hacia donde abre la boca.
    const float StartAngle = 14f, StartDistance = 3.3f, StartHeight = 0.25f;
    const float EndAngle = 30f, EndDistance = 2.5f, EndHeight = 0.35f;
    const float LookHeightOverPivot = 2.1f; // a qué altura de la planta mira (la cabeza)
    const float FieldOfView = 60f;

    [MenuItem("Tools/Gekko/Boss/Contrapicado + cartel (LvlOne)")]
    public static void Apply()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if(!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[BossHeroShotBuilder] Abri LvlOne primero.");
            return;
        }

        PlayableDirector director = GameObject.Find(DirectorPath)?.GetComponent<PlayableDirector>();
        Transform plant = GameObject.Find(PlantPath)?.transform;
        Transform spotsParent = GameObject.Find(SpotsPath)?.transform;
        Transform camerasParent = GameObject.Find(CamerasParentPath)?.transform;
        Transform cardParent = GameObject.Find(TitleCardParentPath)?.transform;
        CinemachineCamera tourCam = camerasParent != null ? camerasParent.Find(TourCameraName)?.GetComponent<CinemachineCamera>() : null;
        Transform[] spots = spotsParent != null ? CoreSpotNames.Select(n => spotsParent.Find(n)).ToArray() : null;
        if(director == null || plant == null || camerasParent == null || cardParent == null || tourCam == null
           || spots == null || spots.Any(s => s == null))
        {
            Debug.LogError("[BossHeroShotBuilder] Falta el director, la planta, BossZone/Camaras, la camara del recorrido o algun spot de nucleo.");
            return;
        }

        Transform[] coreSpots = OrderSpotsByCores(scene, spots);
        if(coreSpots == null) return;

        // ---------- timeline: copia (si el director todavía usa el original)
        var current = director.playableAsset as TimelineAsset;
        string currentPath = current != null ? AssetDatabase.GetAssetPath(current) : null;
        var bindingsByName = new Dictionary<string, Object>();
        if(current != null)
            foreach(TrackAsset track in current.GetOutputTracks())
                bindingsByName[track.name] = director.GetGenericBinding(track);

        TimelineAsset timeline;
        if(currentPath == NewTimelinePath)
        {
            timeline = current;
        }
        else
        {
            if(currentPath != SourceTimelinePath)
                Debug.LogWarning("[BossHeroShotBuilder] El director usaba " + currentPath + "; se copia igual " + SourceTimelinePath + ".");
            AssetDatabase.DeleteAsset(NewTimelinePath);
            AssetDatabase.CopyAsset(SourceTimelinePath, NewTimelinePath);
            timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(NewTimelinePath);
        }

        // ---------- cámara del contrapicado
        GameObject camGo = camerasParent.Find(CameraName)?.gameObject;
        if(camGo == null)
        {
            camGo = new GameObject(CameraName, typeof(CinemachineCamera), typeof(Animator));
            Undo.RegisterCreatedObjectUndo(camGo, "Contrapicado");
            camGo.transform.SetParent(camerasParent, false);
        }
        var heroCam = camGo.GetComponent<CinemachineCamera>();
        LensSettings lens = tourCam.Lens;
        lens.FieldOfView = FieldOfView;
        lens.NearClipPlane = 0.05f; // está pegada a las enredaderas del escudo
        heroCam.Lens = lens;
        heroCam.Priority.Value = 0;
        Animator heroAnimator = camGo.GetComponent<Animator>();

        AnimationClip heroClip = BuildHeroClip(plant, camerasParent, out Vector3 heroLocalPos, out Quaternion heroLocalRot);
        camGo.transform.SetLocalPositionAndRotation(heroLocalPos, heroLocalRot);

        // ---------- recorrido por los núcleos
        float tourStart = HeroStart + HeroDuration;
        float firstCore = tourStart + TourLead;
        float lastCore = firstCore + CoresSpan;
        float playerBackStart = lastCore + BlendBackDelay;
        float tourEnd = playerBackStart + BlendBack;
        Vector3[] avoid = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PurificationCore>(true))
            .Select(c => c.transform.position).Append(plant.position + Vector3.up * LookHeightOverPivot).ToArray();
        AnimationClip tourClip = BuildTourClip(coreSpots, avoid, camerasParent, firstCore - tourStart, tourEnd - tourStart);

        // ---------- Cinemachine track
        CinemachineTrack cmTrack = timeline.GetOutputTracks().OfType<CinemachineTrack>().FirstOrDefault();
        if(cmTrack == null)
        {
            Debug.LogError("[BossHeroShotBuilder] El timeline no tiene Cinemachine Track.");
            return;
        }

        foreach(TimelineClip c in cmTrack.GetClips().Where(c => c.displayName == CameraName).ToList())
            timeline.DeleteClip(c);

        TimelineClip tourShot = cmTrack.GetClips().FirstOrDefault(c => c.displayName == TourCameraName);
        List<TimelineClip> playerShots = cmTrack.GetClips().Where(c => c.displayName != TourCameraName).OrderBy(c => c.start).ToList();
        if(tourShot == null || playerShots.Count < 2)
        {
            Debug.LogError("[BossHeroShotBuilder] No encontre el clip del recorrido o los dos de la camara del jugador.");
            return;
        }

        // Jugador -> (corte) contrapicado -> (corte) recorrido -> (blend) jugador.
        TimelineClip playerIn = playerShots[0];
        TimelineClip playerBack = playerShots[playerShots.Count - 1];
        playerIn.start = 0;
        playerIn.duration = HeroStart;

        TimelineClip heroShot = cmTrack.CreateClip<CinemachineShot>();
        heroShot.displayName = CameraName;
        heroShot.start = HeroStart;
        heroShot.duration = HeroDuration;
        var shot = (CinemachineShot)heroShot.asset;
        shot.DisplayName = CameraName;
        shot.VirtualCamera.exposedName = GUID.Generate().ToString();
        director.SetReferenceValue(shot.VirtualCamera.exposedName, heroCam);

        tourShot.start = tourStart;
        tourShot.duration = tourEnd - tourStart;

        playerBack.start = playerBackStart;
        playerBack.duration = BlendBack + PlayerClipTail;

        // ---------- tracks de animación
        foreach(TrackAsset t in timeline.GetOutputTracks().Where(t => t.name == AnimTrackName).ToList())
            timeline.DeleteTrack(t);
        AnimationTrack heroTrack = timeline.CreateTrack<AnimationTrack>(null, AnimTrackName);
        AddAnimClip(heroTrack, heroClip, "Acercamiento", HeroStart, HeroDuration);

        AnimationTrack tourTrack = timeline.GetOutputTracks().OfType<AnimationTrack>().FirstOrDefault(t => t.name == TourTrackName);
        if(tourTrack == null)
        {
            Debug.LogError("[BossHeroShotBuilder] No encontre el track '" + TourTrackName + "'.");
            return;
        }
        foreach(TimelineClip c in tourTrack.GetClips().ToList())
            timeline.DeleteClip(c);
        AddAnimClip(tourTrack, tourClip, "Nucleos", tourStart, tourEnd - tourStart);

        // ---------- bindings
        Undo.RecordObject(director, "Contrapicado");
        director.playableAsset = timeline;
        foreach(TrackAsset track in timeline.GetOutputTracks())
        {
            if(track == heroTrack)
                director.SetGenericBinding(track, heroAnimator);
            else if(bindingsByName.TryGetValue(track.name, out Object binding))
                director.SetGenericBinding(track, binding);
        }

        // ---------- cartel
        GameObject cardGo = cardParent.Find(TitleCardName)?.gameObject;
        if(cardGo == null)
        {
            cardGo = new GameObject(TitleCardName, typeof(BossTitleCard));
            Undo.RegisterCreatedObjectUndo(cardGo, "Cartel boss");
            cardGo.transform.SetParent(cardParent, false);
        }
        var card = cardGo.GetComponent<BossTitleCard>();
        if(card == null) card = cardGo.AddComponent<BossTitleCard>();
        var so = new SerializedObject(card);
        so.FindProperty("_director").objectReferenceValue = director;
        so.FindProperty("_showAt").floatValue = CardShow;
        so.FindProperty("_hideAt").floatValue = CardHide;
        so.FindProperty("_font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(timeline);
        EditorUtility.SetDirty(director);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log($"[BossHeroShotBuilder] Listo: contrapicado {HeroStart}-{tourStart} s (cartel {CardShow}-{CardHide}), " +
                  $"nucleos {firstCore:0.00}-{lastCore:0.00} s, vuelta al jugador {playerBackStart:0.00}-{tourEnd:0.00} s, total {timeline.duration:0.00} s.");
    }

    // Para cada núcleo de CoreOrder, el encuadre (sin repetir) cuyo forward apunta más directo a él.
    static Transform[] OrderSpotsByCores(Scene scene, Transform[] spots)
    {
        var cores = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PurificationCore>(true)).ToList();
        var free = new List<Transform>(spots);
        var result = new Transform[CoreOrder.Length];

        for(int i = 0; i < CoreOrder.Length; i++)
        {
            PurificationCore core = cores.FirstOrDefault(c => c.name == CoreOrder[i]);
            if(core == null || free.Count == 0)
            {
                Debug.LogError("[BossHeroShotBuilder] No encontre el nucleo '" + CoreOrder[i] + "' (o no quedan encuadres libres).");
                return null;
            }

            Transform best = free.OrderBy(s => Vector3.Angle(s.forward, core.transform.position - s.position)).First();
            free.Remove(best);
            result[i] = best;
            Debug.Log("[BossHeroShotBuilder] " + (i + 1) + ". " + core.name + " -> " + best.name);
        }
        return result;
    }

    static void AddAnimClip(AnimationTrack track, AnimationClip clip, string name, float start, float duration)
    {
        track.trackOffset = TrackOffset.ApplyTransformOffsets;
        track.position = Vector3.zero;
        track.rotation = Quaternion.identity;
        TimelineClip tc = track.CreateClip<AnimationPlayableAsset>();
        var asset = (AnimationPlayableAsset)tc.asset;
        asset.clip = clip;
        asset.removeStartOffset = false; // las keys son posiciones absolutas (locales a BossZone/Camaras), no un desplazamiento
        tc.displayName = name;
        tc.start = start;
        tc.duration = duration;
    }

    // ------------------------------------------------------------------ contrapicado

    // Dos keys (inicio/fin) a velocidad constante.
    static AnimationClip BuildHeroClip(Transform plant, Transform parent, out Vector3 startLocalPos, out Quaternion startLocalRot)
    {
        Vector3 fwd = Vector3.ProjectOnPlane(plant.forward, Vector3.up).normalized;
        Vector3 look = plant.position + Vector3.up * LookHeightOverPivot;

        Pose start = Frame(plant.position, fwd, look, StartAngle, StartDistance, StartHeight);
        Pose end = Frame(plant.position, fwd, look, EndAngle, EndDistance, EndHeight);

        var times = new List<float> { 0f, HeroDuration };
        var positions = new List<Vector3> { parent.InverseTransformPoint(start.position), parent.InverseTransformPoint(end.position) };
        var eulers = UnwrapEulers(new List<Vector3> { LocalEuler(parent, start.rotation), LocalEuler(parent, end.rotation) });
        Vector3 v = (positions[1] - positions[0]) / HeroDuration;

        startLocalPos = positions[0];
        startLocalRot = Quaternion.Euler(eulers[0]);
        return WriteClip(HeroClipPath, times, positions, new List<Vector3> { v, v }, eulers);
    }

    static Pose Frame(Vector3 pivot, Vector3 fwd, Vector3 look, float angle, float distance, float height)
    {
        Vector3 dir = Quaternion.Euler(0f, angle, 0f) * fwd;
        Vector3 pos = pivot + dir * distance;
        pos.y = pivot.y + height;
        return new Pose(pos, Quaternion.LookRotation(look - pos, Vector3.up));
    }

    // ------------------------------------------------------------------ recorrido por los núcleos

    // Misma idea que BossCombatRevealOneCamera: tiempos proporcionales a la distancia, pasa por cada spot "de costado"
    // a media velocidad sin frenar, con una key de entrada (ya en movimiento) y una de salida. Además:
    //  - Si el tramo que sigue va hacia el núcleo que el spot está mirando, al pasar por el spot retrocede y sube en vez
    //    de seguir de frente (si no, se mete en el núcleo).
    //  - Si un tramo pasa cerca de algo a esquivar (núcleos, planta), se agrega un punto intermedio elevado.
    static AnimationClip BuildTourClip(Transform[] spots, Vector3[] avoid, Transform parent, float firstSpotTime, float endTime)
    {
        // ---- waypoints: spots + puntos intermedios
        var pos = new List<Vector3>();
        var rot = new List<Quaternion>();
        var spotIndex = new List<int>(); // índice del spot, o -1 si es un punto intermedio
        for(int i = 0; i < spots.Length; i++)
        {
            pos.Add(spots[i].position);
            rot.Add(spots[i].rotation);
            spotIndex.Add(i);

            if(i == spots.Length - 1) continue;
            Vector3 a = spots[i].position, b = spots[i + 1].position;
            if(avoid.Any(p => DistanceToSegment(p, a, b) < ViaClearance))
            {
                pos.Add(Vector3.Lerp(a, b, 0.5f) + Vector3.up * ViaLift);
                rot.Add(Quaternion.Slerp(spots[i].rotation, spots[i + 1].rotation, 0.5f));
                spotIndex.Add(-1);
            }
        }

        int n = pos.Count;
        var times = new float[n];
        float total = 0f;
        for(int i = 1; i < n; i++) total += Vector3.Distance(pos[i - 1], pos[i]);
        times[0] = firstSpotTime;
        float acc = 0f;
        for(int i = 1; i < n; i++)
        {
            acc += Vector3.Distance(pos[i - 1], pos[i]);
            times[i] = firstSpotTime + CoresSpan * acc / total;
        }
        float speed = total / CoresSpan;

        // ---- velocidades
        var vel = new Vector3[n];
        for(int i = 0; i < n; i++)
        {
            Vector3 prev = i > 0 ? pos[i - 1] : pos[i];
            Vector3 next = i < n - 1 ? pos[i + 1] : pos[i];
            float dt = (i < n - 1 ? times[i + 1] : times[i]) - (i > 0 ? times[i - 1] : times[i]);
            Vector3 flow = next - prev;

            if(spotIndex[i] < 0)
            {
                vel[i] = dt > 1e-4f ? flow / dt : Vector3.zero; // punto intermedio: Catmull-Rom
                continue;
            }

            Transform spot = spots[spotIndex[i]];
            Vector3 right = Vector3.ProjectOnPlane(spot.right, Vector3.up).normalized;
            float side = Mathf.Sign(Vector3.Dot(right, flow));
            if(side == 0f) side = 1f;

            Vector3 v = right * side * speed * SpotSpeedFactor;
            v.y = dt > 1e-4f ? flow.y / dt : 0f;

            // El tramo siguiente va "hacia adelante" (hacia el núcleo que mira): sale retrocediendo y subiendo.
            Vector3 fwdFlat = Vector3.ProjectOnPlane(spot.forward, Vector3.up).normalized;
            if(i < n - 1 && Vector3.Dot(Vector3.ProjectOnPlane(pos[i + 1] - pos[i], Vector3.up), fwdFlat) > 0f)
            {
                v += -fwdFlat * speed * SpotSpeedFactor * 0.6f;
                v.y = Mathf.Max(v.y, 0f) + speed * SpotSpeedFactor * 0.5f;
            }
            vel[i] = v;
        }

        // ---- keys: entrada, waypoints y salida
        const float easeFactor = 0.4f;
        var keyTimes = new List<float>();
        var worldPos = new List<Vector3>();
        var worldVel = new List<Vector3>();
        var worldRot = new List<Quaternion>();

        Vector3 inFlat = Vector3.ProjectOnPlane(vel[0], Vector3.up);
        keyTimes.Add(0f);
        worldPos.Add(pos[0] - inFlat * firstSpotTime * easeFactor);
        worldVel.Add(inFlat * easeFactor);
        worldRot.Add(rot[0]);

        for(int i = 0; i < n; i++)
        {
            keyTimes.Add(times[i]);
            worldPos.Add(pos[i]);
            worldVel.Add(vel[i]);
            worldRot.Add(rot[i]);
        }

        Vector3 outFlat = Vector3.ProjectOnPlane(vel[n - 1], Vector3.up);
        keyTimes.Add(endTime);
        worldPos.Add(pos[n - 1] + outFlat * (endTime - times[n - 1]) * easeFactor * 0.5f);
        worldVel.Add(outFlat * easeFactor * 0.5f);
        worldRot.Add(rot[n - 1]);

        var positions = worldPos.Select(parent.InverseTransformPoint).ToList();
        var localVel = worldVel.Select(parent.InverseTransformDirection).ToList();
        var eulers = UnwrapEulers(worldRot.Select(r => LocalEuler(parent, r)).ToList());
        return WriteClip(TourClipPath, keyTimes, positions, localVel, eulers);
    }

    static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
        return Vector3.Distance(p, a + ab * t);
    }

    // ------------------------------------------------------------------ curvas

    static Vector3 LocalEuler(Transform parent, Quaternion worldRot) => (Quaternion.Inverse(parent.rotation) * worldRot).eulerAngles;

    // Euler sin saltos de 360 (cada ángulo va por el camino más corto respecto del anterior).
    static List<Vector3> UnwrapEulers(List<Vector3> eulers)
    {
        for(int i = 1; i < eulers.Count; i++)
        {
            Vector3 prev = eulers[i - 1];
            Vector3 e = eulers[i];
            for(int a = 0; a < 3; a++) e[a] = prev[a] + Mathf.DeltaAngle(prev[a], e[a]);
            eulers[i] = e;
        }
        return eulers;
    }

    static AnimationClip WriteClip(string path, List<float> times, List<Vector3> positions, List<Vector3> velocities, List<Vector3> eulers)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }
        clip.ClearCurves();
        clip.frameRate = 60f;

        string[] posProps = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" };
        string[] rotProps = { "localEulerAnglesRaw.x", "localEulerAnglesRaw.y", "localEulerAnglesRaw.z" };
        for(int axis = 0; axis < 3; axis++)
        {
            SetCurve(clip, posProps[axis], times, positions.Select(p => p[axis]).ToList(), velocities.Select(v => v[axis]).ToList());
            SetCurve(clip, rotProps[axis], times, eulers.Select(e => e[axis]).ToList(), null);
        }

        EditorUtility.SetDirty(clip);
        return clip;
    }

    // Tangentes en modo Free. Si se pasan pendientes se usan esas; si no, Catmull-Rom no uniforme.
    static void SetCurve(AnimationClip clip, string property, List<float> times, List<float> values, List<float> slopes)
    {
        var keys = new Keyframe[times.Count];
        for(int i = 0; i < times.Count; i++)
        {
            float slope;
            if(slopes != null)
                slope = slopes[i];
            else if(i == 0)
                slope = (values[1] - values[0]) / (times[1] - times[0]);
            else if(i == times.Count - 1)
                slope = (values[i] - values[i - 1]) / (times[i] - times[i - 1]);
            else
                slope = (values[i + 1] - values[i - 1]) / (times[i + 1] - times[i - 1]);

            keys[i] = new Keyframe(times[i], values[i], slope, slope);
        }

        var curve = new AnimationCurve(keys);
        for(int i = 0; i < keys.Length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Free);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Free);
            AnimationUtility.SetKeyBroken(curve, i, false);
        }

        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), property), curve);
    }
}
