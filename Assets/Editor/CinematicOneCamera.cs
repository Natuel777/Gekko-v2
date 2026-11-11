using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// Convierte una cinematica hecha con varias camaras fijas (cortes/blends en un Cinemachine Track) en UNA sola camara
// animada que recorre los mismos encuadres sin frenar en cada uno.
//
// Como se usa: seleccionar el GameObject con el PlayableDirector y
//   Tools > Gekko > Cinematicas > Convertir a camara unica (director seleccionado)
//
// Que hace:
//  - "Encuadres" = clips del Cinemachine Track cuya camara es fija (sin Follow ni LookAt). Los clips de la camara del
//    jugador (con Follow, ej. ThirdPersonManual) se dejan tal cual: el blend de entrada y de salida no cambia.
//  - La camara recorre las POSICIONES de los encuadres con un AnimationClip de pocas keys (una por encuadre + entrada +
//    salida), tangentes calculadas a mano (modo Free): velocidad constante, el tiempo entre encuadres es proporcional
//    a la distancia. Se puede retocar en la ventana Animation.
//  - Hacia donde mira:
//      * sin objetivos: copia la rotacion de cada camara fija.
//      * con objetivos (uno por encuadre): arma un rig "CM_<director>_Rig" con la camara (Hard Look At) y un punto
//        "Aim" que viaja de un objetivo al siguiente con la misma sincronizacion. La camara siempre apunta al Aim.
//  - NO toca el timeline original (puede usarlo otra escena): crea "<timeline>_UnaCamara.playable" y se lo asigna al
//    director. Las camaras fijas viejas quedan en la escena, apagadas. Todo con Undo. No guarda la escena.
public static class CinematicOneCamera
{
    const string AnimTrackName = "Camara unica (movimiento)";

    [MenuItem("Tools/Gekko/Cinematicas/Convertir a camara unica (director seleccionado)")]
    static void ConvertSelected()
    {
        var director = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<PlayableDirector>() : null;
        if(director == null)
        {
            EditorUtility.DisplayDialog("Camara unica", "Selecciona el GameObject que tiene el PlayableDirector de la cinematica.", "OK");
            return;
        }
        Debug.Log(Convert(director));
    }

    [MenuItem("Tools/Gekko/Cinematicas/Convertir a camara unica (director seleccionado)", true)]
    static bool ConvertSelectedValidate() => !EditorApplication.isPlaying && Selection.activeGameObject != null
                                             && Selection.activeGameObject.GetComponent<PlayableDirector>() != null;

    // edgeSpeedFactor: velocidad al entrar al primer encuadre y al salir del ultimo, relativa a la del recorrido (nunca 0).
    // lookPoints: puntos de mundo a los que mirar, uno por encuadre (null = usar la rotacion de cada camara fija).
    public static string Convert(PlayableDirector director, float edgeSpeedFactor = 0.5f, Vector3[] lookPoints = null)
    {
        var source = director.playableAsset as TimelineAsset;
        if(source == null) return "[Camara unica] El director no tiene un TimelineAsset.";

        string sourcePath = AssetDatabase.GetAssetPath(source);
        CinemachineTrack cmTrack = source.GetOutputTracks().OfType<CinemachineTrack>().FirstOrDefault();
        if(cmTrack == null) return "[Camara unica] El timeline no tiene Cinemachine Track.";
        if(sourcePath.EndsWith("_UnaCamara.playable")) return "[Camara unica] Este director ya usa un timeline convertido (" + sourcePath + ").";

        // ---------- encuadres (camaras fijas) en orden
        var spots = new List<(TimelineClip clip, CinemachineCamera cam)>();
        foreach(TimelineClip clip in cmTrack.GetClips().OrderBy(c => c.start))
        {
            if(!(clip.asset is CinemachineShot shot)) continue;
            var cam = shot.VirtualCamera.Resolve(director) as CinemachineCamera;
            if(cam == null || cam.Follow != null || cam.LookAt != null) continue;
            spots.Add((clip, cam));
        }
        if(spots.Count < 2) return "[Camara unica] Hacen falta al menos 2 camaras fijas; hay " + spots.Count + ".";
        bool aimMode = lookPoints != null;
        if(aimMode && lookPoints.Length != spots.Count)
            return "[Camara unica] Hay " + spots.Count + " encuadres y " + lookPoints.Length + " objetivos: tiene que haber uno por encuadre.";

        double spanStart = spots.Min(s => s.clip.start);
        double spanEnd = spots.Max(s => s.clip.end);

        // blendShare: que parte de los blends con la camara del jugador se usa para recorrer (0.6 = arranca cuando el
        // blend de entrada va por el 60 % y termina al 40 % del de salida). Mas alto = recorrido mas lento.
        const float blendShare = 0.6f;
        float firstSpotTime = (float)(spots[0].clip.start + blendShare * Mathf.Max(0f, (float)spots[0].clip.blendInDuration));
        var last = spots[spots.Count - 1].clip;
        float lastSpotTime = (float)(last.end - blendShare * Mathf.Max(0f, (float)last.blendOutDuration));
        if(lastSpotTime <= firstSpotTime + 0.5f) lastSpotTime = (float)last.end;

        Transform parent = spots[0].cam.transform.parent;
        Vector3 ToLocal(Vector3 world) => parent != null ? parent.InverseTransformPoint(world) : world;
        var positions = spots.Select(s => ToLocal(s.cam.transform.position)).ToList();
        var rotations = spots.Select(s => parent != null ? (Quaternion.Inverse(parent.rotation) * s.cam.transform.rotation) : s.cam.transform.rotation).ToList();

        // Tiempos proporcionales a la distancia => velocidad constante.
        // Cada tramo "pesa" su distancia + lo que gira la mirada (metrosPorGrado), asi dos encuadres cercanos pero que
        // miran a lados distintos no se resuelven con un latigazo.
        const float metersPerDegree = 0.15f;
        var segment = new List<float>();
        var segDist = new List<float>();
        for(int i = 0; i < positions.Count - 1; i++)
        {
            float dist = Vector3.Distance(positions[i], positions[i + 1]);
            Vector3 dirA = aimMode ? (ToLocal(lookPoints[i]) - positions[i]) : rotations[i] * Vector3.forward;
            Vector3 dirB = aimMode ? (ToLocal(lookPoints[i + 1]) - positions[i + 1]) : rotations[i + 1] * Vector3.forward;
            segDist.Add(dist);
            segment.Add(dist + Vector3.Angle(dirA, dirB) * metersPerDegree);
        }
        float total = Mathf.Max(0.01f, segment.Sum());
        var times = new List<float> { firstSpotTime };
        float acc = 0f;
        foreach(float len in segment) { acc += len; times.Add(firstSpotTime + (lastSpotTime - firstSpotTime) * acc / total); }
        var segSpeed = new List<float>();
        for(int i = 0; i < segDist.Count; i++) segSpeed.Add(segDist[i] / Mathf.Max(0.01f, times[i + 1] - times[i]));
        float speed = segDist.Sum() / (lastSpotTime - firstSpotTime);

        // Velocidad en cada encuadre: direccion de Catmull-Rom (curva suave por los puntos), modulo = promedio de los
        // tramos vecinos (nunca cero: la camara no frena en los encuadres).
        var velocities = new List<Vector3>();
        for(int i = 0; i < positions.Count; i++)
        {
            Vector3 prev = positions[Mathf.Max(0, i - 1)];
            Vector3 next = positions[Mathf.Min(positions.Count - 1, i + 1)];
            float a = segSpeed[Mathf.Max(0, i - 1)], b = segSpeed[Mathf.Min(segSpeed.Count - 1, i)];
            float mag = Mathf.Max(0.3f, 0.5f * (a + b));
            velocities.Add((next - prev).normalized * mag);
        }

        float clipStart = (float)spanStart, clipEnd = (float)spanEnd;
        var keyTimes = new List<float> { clipStart };
        keyTimes.AddRange(times);
        keyTimes.Add(clipEnd);

        var keyPos = new List<Vector3> { positions[0] - velocities[0] * (firstSpotTime - clipStart) * edgeSpeedFactor };
        keyPos.AddRange(positions);
        Vector3 lastVel = velocities[velocities.Count - 1];
        keyPos.Add(positions[positions.Count - 1] + lastVel * (clipEnd - lastSpotTime) * edgeSpeedFactor);

        var keyVel = new List<Vector3> { velocities[0] * edgeSpeedFactor };
        keyVel.AddRange(velocities);
        keyVel.Add(lastVel * edgeSpeedFactor);

        var keyEuler = new List<Vector3> { rotations[0].eulerAngles };
        keyEuler.AddRange(rotations.Select(r => r.eulerAngles));
        keyEuler.Add(rotations[rotations.Count - 1].eulerAngles);
        for(int i = 1; i < keyEuler.Count; i++)
        {
            Vector3 p = keyEuler[i - 1], e = keyEuler[i];
            keyEuler[i] = new Vector3(p.x + Mathf.DeltaAngle(p.x, e.x), p.y + Mathf.DeltaAngle(p.y, e.y), p.z + Mathf.DeltaAngle(p.z, e.z));
        }

        // Punto de mira: quieto en el primer objetivo hasta el primer encuadre, viaja entre objetivos y queda en el ultimo.
        List<Vector3> keyAim = null;
        if(aimMode)
        {
            keyAim = new List<Vector3> { ToLocal(lookPoints[0]) };
            keyAim.AddRange(lookPoints.Select(ToLocal));
            keyAim.Add(ToLocal(lookPoints[lookPoints.Length - 1]));
        }

        for(int i = 0; i < keyTimes.Count; i++) keyTimes[i] -= clipStart; // relativas al inicio del clip

        string baseName = System.IO.Path.GetFileNameWithoutExtension(sourcePath);
        string folder = System.IO.Path.GetDirectoryName(sourcePath).Replace('\\', '/');
        string timelinePath = folder + "/" + baseName + "_UnaCamara.playable";
        string clipPath = folder + "/" + baseName + "_UnaCamara_Anim.anim";

        // ---------- objetos de la escena
        string baseObj = "CM_" + director.name.Replace("TIMELINE", "").Trim(' ', '-');
        GameObject animatedRoot;   // el que tiene el Animator (lo anima el timeline)
        CinemachineCamera newCam;
        string camPath;            // ruta de la camara dentro del Animator ("" = el mismo objeto)
        string aimPath = null;

        if(aimMode)
        {
            animatedRoot = new GameObject(baseObj + "_Rig", typeof(Animator));
            Undo.RegisterCreatedObjectUndo(animatedRoot, "Camara unica");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(animatedRoot, director.gameObject.scene);
            if(parent != null) animatedRoot.transform.SetParent(parent, false);
            animatedRoot.transform.SetSiblingIndex(spots[0].cam.transform.GetSiblingIndex());

            var camGo = new GameObject("Camara", typeof(CinemachineCamera), typeof(CinemachineHardLookAt));
            camGo.transform.SetParent(animatedRoot.transform, false);
            camGo.transform.localPosition = positions[0];
            var aimGo = new GameObject("Aim");
            aimGo.transform.SetParent(animatedRoot.transform, false);
            aimGo.transform.localPosition = keyAim[0];

            newCam = camGo.GetComponent<CinemachineCamera>();
            newCam.LookAt = aimGo.transform;
            camPath = "Camara";
            aimPath = "Aim";
        }
        else
        {
            animatedRoot = new GameObject(baseObj + "_UnaCamara", typeof(CinemachineCamera), typeof(Animator));
            Undo.RegisterCreatedObjectUndo(animatedRoot, "Camara unica");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(animatedRoot, director.gameObject.scene);
            if(parent != null) animatedRoot.transform.SetParent(parent, false);
            animatedRoot.transform.SetSiblingIndex(spots[0].cam.transform.GetSiblingIndex());
            animatedRoot.transform.localPosition = positions[0];
            animatedRoot.transform.localRotation = rotations[0];
            newCam = animatedRoot.GetComponent<CinemachineCamera>();
            camPath = "";
        }
        newCam.Lens = spots[0].cam.Lens;
        newCam.Priority.Value = spots[0].cam.Priority.Value;

        // ---------- AnimationClip
        var anim = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if(anim == null) { anim = new AnimationClip(); AssetDatabase.CreateAsset(anim, clipPath); }
        anim.ClearCurves();
        anim.frameRate = 60f;
        string[] posProps = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" };
        string[] rotProps = { "localEulerAnglesRaw.x", "localEulerAnglesRaw.y", "localEulerAnglesRaw.z" };
        for(int a = 0; a < 3; a++)
        {
            SetCurve(anim, camPath, posProps[a], keyTimes, keyPos.Select(p => p[a]).ToList(), keyVel.Select(v => v[a]).ToList());
            if(aimMode) SetCurve(anim, aimPath, posProps[a], keyTimes, keyAim.Select(p => p[a]).ToList(), null);
            else SetCurve(anim, camPath, rotProps[a], keyTimes, keyEuler.Select(e => e[a]).ToList(), null);
        }
        EditorUtility.SetDirty(anim);

        // ---------- copia del timeline
        AssetDatabase.DeleteAsset(timelinePath);
        AssetDatabase.CopyAsset(sourcePath, timelinePath);
        var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
        var bindingsByName = new Dictionary<string, Object>();
        foreach(TrackAsset t in source.GetOutputTracks()) bindingsByName[t.name] = director.GetGenericBinding(t);

        CinemachineTrack newTrack = timeline.GetOutputTracks().OfType<CinemachineTrack>().First();
        var spotNames = new HashSet<string>(spots.Select(s => s.clip.displayName));
        foreach(TimelineClip c in newTrack.GetClips().ToList())
            if(spotNames.Contains(c.displayName)) timeline.DeleteClip(c);

        TimelineClip shotClip = newTrack.CreateClip<CinemachineShot>();
        shotClip.displayName = baseObj;
        shotClip.start = spanStart;
        shotClip.duration = spanEnd - spanStart;
        var newShot = (CinemachineShot)shotClip.asset;
        newShot.DisplayName = baseObj;
        newShot.VirtualCamera.exposedName = GUID.Generate().ToString();

        var animTrack = timeline.CreateTrack<AnimationTrack>(null, AnimTrackName);
        animTrack.trackOffset = TrackOffset.ApplyTransformOffsets;
        TimelineClip animClip = animTrack.CreateClip<AnimationPlayableAsset>();
        ((AnimationPlayableAsset)animClip.asset).clip = anim;
        animClip.displayName = "Recorrido";
        animClip.start = spanStart;
        animClip.duration = spanEnd - spanStart;
        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssetIfDirty(timeline);
        AssetDatabase.SaveAssetIfDirty(anim);

        Undo.RecordObject(director, "Camara unica");
        director.playableAsset = timeline;
        director.SetReferenceValue(newShot.VirtualCamera.exposedName, newCam);
        foreach(TrackAsset t in timeline.GetOutputTracks())
        {
            if(t == animTrack) director.SetGenericBinding(t, animatedRoot.GetComponent<Animator>());
            else if(bindingsByName.TryGetValue(t.name, out Object b)) director.SetGenericBinding(t, b);
        }

        // ---------- camaras viejas apagadas
        foreach(var s in spots)
        {
            Undo.RecordObject(s.cam.gameObject, "Apagar camara fija");
            s.cam.gameObject.SetActive(false);
        }

        EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        return "[Camara unica] " + director.name + ": " + spots.Count + " encuadres -> " + animatedRoot.name + (aimMode ? " (mirando objetivos)" : "") +
               ". Timeline: " + timelinePath + " | encuadres en " + string.Join(", ", times.Select(t => t.ToString("0.00") + "s")) +
               " | velocidad " + speed.ToString("0.0") + " m/s";
    }

    static void SetCurve(AnimationClip clip, string path, string property, List<float> times, List<float> values, List<float> slopes)
    {
        var keys = new Keyframe[times.Count];
        for(int i = 0; i < times.Count; i++)
        {
            float slope;
            if(slopes != null) slope = slopes[i];
            else if(i == 0) slope = (values[1] - values[0]) / (times[1] - times[0]);
            else if(i == times.Count - 1) slope = (values[i] - values[i - 1]) / (times[i] - times[i - 1]);
            else slope = (values[i + 1] - values[i - 1]) / (times[i + 1] - times[i - 1]);
            keys[i] = new Keyframe(times[i], values[i], slope, slope);
        }
        var curve = new AnimationCurve(keys);
        for(int i = 0; i < keys.Length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Free);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Free);
        }
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
    }
}
