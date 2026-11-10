using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

// Reemplaza, SOLO en BossLvlOne, los cortes/blends entre las 4 camaras fijas de la cinematica CombatReveal por UNA
// camara animada que pasa por los mismos encuadres sin frenar en cada spot.
//
// - No toca CombatReveal.playable (lo usa tambien LvlOne): crea una copia, BossLvlOne_CombatReveal_UnaCamara.playable,
//   y se la asigna al director "TIMELINE TriggerFight" del boss. Las 4 camaras viejas quedan en la escena sin usar.
// - La camara nueva (CM_CombatReveal_UnaCamara) se mueve con un AnimationClip de pocas keys (una por spot, mas una
//   de entrada y una de salida), con tangentes Catmull-Rom calculadas a mano y en modo Free: velocidad continua, sin
//   el "ease" que hacia frenar en cada encuadre. Las keys se pueden retocar en la ventana Animation.
// - Los tiempos entre spots son proporcionales a la distancia: la camara va a velocidad (casi) constante.
//   Menu: Tools > Gekko > Boss > Camara unica en CombatReveal
public static class BossCombatRevealOneCamera
{
    const string ScenePath = "Assets/Scenes/BossLvlOne.unity";
    const string SourceTimelinePath = "Assets/Art/Timeline/CombatReveal.playable";
    const string NewTimelinePath = "Assets/Art/Timeline/BossLvlOne_CombatReveal_UnaCamara.playable";
    const string ClipPath = "Assets/Art/Timeline/BossLvlOne_CombatReveal_UnaCamara_Anim.anim";
    const string DirectorName = "TIMELINE TriggerFight";
    const string CameraName = "CM_CombatReveal_UnaCamara";
    static readonly string[] SpotNames = { "CinemachineCamera", "CinemachineCamera (1)", "CinemachineCamera (2)", "CinemachineCamera (3)" };

    // Momentos de la cinematica (los mismos del timeline original).
    const float FirstSpotTime = 2.6f;    // casi al final del blend desde la camara del jugador (0 - 3.15)
    const float LastSpotTime = 12.3f;    // antes del blend de vuelta a la camara del jugador (11.67 - 14.32)
    const float SpotSpeedFactor = 0.5f;  // velocidad al pasar por cada spot, relativa a la media del recorrido

    [MenuItem("Tools/Gekko/Boss/Camara unica en CombatReveal")]
    public static void Apply()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if(!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[BossCombatRevealOneCamera] Abri BossLvlOne primero.");
            return;
        }

        PlayableDirector director = FindInScene<PlayableDirector>(scene, d => d.name == DirectorName);
        if(director == null)
        {
            Debug.LogError("[BossCombatRevealOneCamera] No encontre el director '" + DirectorName + "'.");
            return;
        }

        var sourceTimeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(SourceTimelinePath);
        Transform[] spots = SpotNames.Select(n => FindInScene<CinemachineCamera>(scene, c => c.name == n && c.transform.parent != null && c.transform.parent.name == "CombatSplitCams")?.transform).ToArray();
        if(sourceTimeline == null || spots.Any(s => s == null))
        {
            Debug.LogError("[BossCombatRevealOneCamera] Falta el timeline original o alguna de las 4 camaras.");
            return;
        }

        // ---------- camara unica
        GameObject camGo = FindInScene<CinemachineCamera>(scene, c => c.name == CameraName)?.gameObject;
        if(camGo == null)
        {
            camGo = new GameObject(CameraName, typeof(CinemachineCamera), typeof(Animator));
            SceneManager.MoveGameObjectToScene(camGo, scene);
        }
        camGo.transform.SetParent(null, true); // en la raiz: las curvas son posiciones de mundo
        var cam = camGo.GetComponent<CinemachineCamera>();
        cam.Lens = spots[0].GetComponent<CinemachineCamera>().Lens;
        cam.Priority.Value = 0;
        camGo.transform.SetPositionAndRotation(spots[0].position, spots[0].rotation);
        Animator animator = camGo.GetComponent<Animator>();

        AnimationClip clip = BuildClip(spots, (float)sourceTimeline.duration);

        // ---------- copia del timeline (el original lo usa LvlOne)
        if(AssetDatabase.LoadAssetAtPath<TimelineAsset>(NewTimelinePath) == null)
            AssetDatabase.CopyAsset(SourceTimelinePath, NewTimelinePath);
        var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(NewTimelinePath);

        // Bindings por nombre de track (la copia tiene otros ids). Se leen del director antes de cambiarle el asset.
        PlayableAsset previous = director.playableAsset;
        var bindingsByName = new Dictionary<string, Object>();
        if(previous is TimelineAsset previousTimeline)
        {
            foreach(TrackAsset track in previousTimeline.GetOutputTracks())
                bindingsByName[track.name] = director.GetGenericBinding(track);
        }

        CinemachineTrack cmTrack = timeline.GetOutputTracks().OfType<CinemachineTrack>().FirstOrDefault();
        if(cmTrack == null)
        {
            Debug.LogError("[BossCombatRevealOneCamera] La copia no tiene Cinemachine Track.");
            return;
        }

        // Saca los 4 spots (y una camara unica de una corrida anterior); quedan las 2 de ThirdPersonManual.
        foreach(TimelineClip c in cmTrack.GetClips().ToList())
        {
            if(c.displayName.StartsWith("CinemachineCamera") || c.displayName == CameraName)
                timeline.DeleteClip(c);
        }

        TimelineClip shotClip = cmTrack.CreateClip<CinemachineShot>();
        shotClip.displayName = CameraName;
        shotClip.start = 0;
        shotClip.duration = 14.32;
        var shot = (CinemachineShot)shotClip.asset;
        shot.DisplayName = CameraName;
        shot.VirtualCamera.exposedName = GUID.Generate().ToString();
        director.SetReferenceValue(shot.VirtualCamera.exposedName, cam);

        // Track de animacion de la camara unica (se recrea si ya existia).
        foreach(TrackAsset t in timeline.GetOutputTracks().Where(t => t.name == "Camara unica (movimiento)").ToList())
            timeline.DeleteTrack(t);
        var animTrack = timeline.CreateTrack<AnimationTrack>(null, "Camara unica (movimiento)");
        animTrack.trackOffset = TrackOffset.ApplyTransformOffsets;
        animTrack.position = Vector3.zero;
        animTrack.rotation = Quaternion.identity;
        TimelineClip animClip = animTrack.CreateClip<AnimationPlayableAsset>();
        ((AnimationPlayableAsset)animClip.asset).clip = clip;
        animClip.displayName = "Recorrido";
        animClip.start = 0;
        animClip.duration = clip.length;

        director.playableAsset = timeline;
        foreach(TrackAsset track in timeline.GetOutputTracks())
        {
            if(track == animTrack)
                director.SetGenericBinding(track, animator);
            else if(bindingsByName.TryGetValue(track.name, out Object binding))
                director.SetGenericBinding(track, binding);
        }

        EditorUtility.SetDirty(timeline);
        EditorUtility.SetDirty(director);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log("[BossCombatRevealOneCamera] Listo: '" + DirectorName + "' usa " + NewTimelinePath + " con una sola camara animada.");
    }

    // ---------- recorrido

    static AnimationClip BuildClip(Transform[] spots, float timelineDuration)
    {
        // Tiempos proporcionales a la distancia entre spots => velocidad casi constante.
        var times = new List<float> { FirstSpotTime };
        float total = 0f;
        var segment = new float[spots.Length - 1];
        for(int i = 0; i < segment.Length; i++)
        {
            segment[i] = Vector3.Distance(spots[i].position, spots[i + 1].position);
            total += segment[i];
        }
        float acc = 0f;
        for(int i = 0; i < segment.Length; i++)
        {
            acc += segment[i];
            times.Add(FirstSpotTime + (LastSpotTime - FirstSpotTime) * acc / total);
        }
        float speed = total / (LastSpotTime - FirstSpotTime);

        // Velocidad en cada spot: la camara pasa "de costado" (travelling sobre su eje derecho, hacia el lado donde
        // sigue el recorrido) a velocidad constante. Asi las vueltas en U (spots en los extremos del recorrido) se
        // vuelven curvas amplias en vez de frenar a cero y volver. La altura acompana la del tramo.
        var velocities = new Vector3[spots.Length];
        for(int i = 0; i < spots.Length; i++)
        {
            Vector3 prev = i > 0 ? spots[i - 1].position : spots[i].position;
            Vector3 next = i < spots.Length - 1 ? spots[i + 1].position : spots[i].position;
            float dt = (i < spots.Length - 1 ? times[i + 1] : times[i]) - (i > 0 ? times[i - 1] : times[i]);
            Vector3 flow = next - prev;

            Vector3 right = spots[i].right;
            right.y = 0f;
            right.Normalize();
            float side = Mathf.Sign(Vector3.Dot(right, flow));
            if(side == 0f)
                side = 1f;

            // Mitad de la velocidad media al pasar por el spot: sigue moviendose (no frena) pero se desvia poco del
            // encuadre original (con la velocidad completa se metia en los arboles de al lado).
            Vector3 v = right * side * speed * SpotSpeedFactor;
            v.y = dt > 1e-4f ? flow.y / dt : 0f;
            velocities[i] = v;
        }

        // Keys: entrada (antes del primer spot), los 4 spots y salida (despues del ultimo).
        var positions = new List<Vector3>();
        var eulers = new List<Vector3>();
        var keyTimes = new List<float>();
        var keyVelocities = new List<Vector3>();
        float endTime = timelineDuration;
        const float easeFactor = 0.4f; // entrada/salida mas lentas, pero nunca en cero

        keyTimes.Add(0f);
        Vector3 firstFlat = Vector3.ProjectOnPlane(velocities[0], Vector3.up); // entrada/salida solo en horizontal (no se mete en el piso)
        positions.Add(spots[0].position - firstFlat * FirstSpotTime * easeFactor);
        eulers.Add(spots[0].eulerAngles);
        keyVelocities.Add(firstFlat * easeFactor);
        for(int i = 0; i < spots.Length; i++)
        {
            keyTimes.Add(times[i]);
            positions.Add(spots[i].position);
            eulers.Add(spots[i].eulerAngles);
            keyVelocities.Add(velocities[i]);
        }
        Vector3 lastVelocity = Vector3.ProjectOnPlane(velocities[spots.Length - 1], Vector3.up);
        keyTimes.Add(endTime);
        positions.Add(spots[spots.Length - 1].position + lastVelocity * (endTime - LastSpotTime) * easeFactor * 0.5f);
        eulers.Add(spots[spots.Length - 1].eulerAngles);
        keyVelocities.Add(lastVelocity * easeFactor * 0.5f);

        // Euler sin saltos de 360 (cada angulo va por el camino mas corto respecto del anterior).
        for(int i = 1; i < eulers.Count; i++)
        {
            Vector3 prev = eulers[i - 1];
            Vector3 e = eulers[i];
            e.x = prev.x + Mathf.DeltaAngle(prev.x, e.x);
            e.y = prev.y + Mathf.DeltaAngle(prev.y, e.y);
            e.z = prev.z + Mathf.DeltaAngle(prev.z, e.z);
            eulers[i] = e;
        }

        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if(clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, ClipPath);
        }
        clip.ClearCurves();
        clip.frameRate = 60f;

        string[] posProps = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" };
        string[] rotProps = { "localEulerAnglesRaw.x", "localEulerAnglesRaw.y", "localEulerAnglesRaw.z" };
        for(int axis = 0; axis < 3; axis++)
        {
            SetCurve(clip, posProps[axis], keyTimes, positions.Select(p => p[axis]).ToList(),
                keyVelocities.Select(v => v[axis]).ToList());
            SetCurve(clip, rotProps[axis], keyTimes, eulers.Select(e => e[axis]).ToList(), null);
        }

        EditorUtility.SetDirty(clip);
        return clip;
    }

    // Curva con tangentes en modo Free (Unity no las "aplana" en los extremos). Si se pasan pendientes se usan esas;
    // si no, Catmull-Rom no uniforme (pendiente del vecino anterior al siguiente).
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

    static T FindInScene<T>(Scene scene, System.Func<T, bool> match) where T : Component
    {
        foreach(GameObject root in scene.GetRootGameObjects())
        {
            foreach(T c in root.GetComponentsInChildren<T>(true))
            {
                if(match(c))
                    return c;
            }
        }
        return null;
    }
}
