using System;
using UnityEngine;

// Animación por código de la reacción de la planta a un golpe de lengua que NO la purifica (los golpes intermedios de Hits To
// Purify del DataSO). Mismo estilo que PlantBiteWindup: va en el mismo GameObject que CarnivorousPlant, aplica la pose de unos
// huesos en LateUpdate (el Animator los reescribe en cada frame) y sus tiempos se ajustan en el Inspector.
//
// Dura Duration segundos en total y tiene tres fases. Recoil Portion y Recover Portion fijan qué parte de la duración se lleva
// cada una; el Hold es lo que sobra:
//  1. Recoil: los huesos pasan rápido de la pose que traen del Animator a su Target Euler (el respingo).
//  2. Hold: la planta se queda en esa pose. Los huesos que tengan Bob Eulers (hoy joint7_R, la cabeza) hacen un cabeceo: arrancan
//     desde su Target Euler y pasan por esos puntos, en orden, con una curva suave que los atraviesa a todos.
//  3. Recover: vuelven a la pose que tenga el Animator en ese momento.
// El punto de partida del Recoil y el de llegada del Recover son siempre los del Animator, así que no hay saltos ni al arrancar
// ni al terminar, sin importar qué esté haciendo la planta (reposo, escupiendo...). Target Euler y Bob Eulers son rotaciones
// LOCALES absolutas, las mismas que muestra el Inspector en el Transform de cada hueso.
//
// Mientras corre (IsPlaying) la planta es invulnerable, igual que durante la preparación del mordisco: lo aplica
// CarnivorousPlant (CanBeHit), que además pausa su ataque ese tiempo para que no pelee con esta animación por los huesos.
// CarnivorousPlant la dispara en Damage cuando el golpe no purifica; sin este componente la planta reacciona como siempre.
//
// El Execution Order la deja después de PlantBiteWindup (100): si las dos tocaran el mismo hueso en un frame, gana esta.
[DisallowMultipleComponent]
[DefaultExecutionOrder(110)]
public class PlantHurtReaction : MonoBehaviour
{
    [Serializable]
    private struct JointPose
    {
        [Tooltip("Nombre del hueso. Se busca entre los hijos de la planta al arrancar.")]
        public string jointName;
        [Tooltip("Rotación local (Euler) de respingo: a donde llega el Recoil y donde se queda durante el Hold.")]
        public Vector3 targetEuler;
        [Tooltip("Cabeceo (opcional): rotaciones locales (Euler) por las que pasa el hueso, en orden, durante el Hold. Arranca desde Target Euler. Vacío = el hueso se queda quieto en la pose de respingo. Para repetir el cabeceo se repiten los puntos.")]
        public Vector3[] bobEulers;

        public JointPose(string jointName, Vector3 targetEuler, params Vector3[] bobEulers)
        {
            this.jointName = jointName;
            this.targetEuler = targetEuler;
            this.bobEulers = bobEulers;
        }
    }

    [Header("Joints")]
    [Tooltip("Huesos que anima la reacción, con su pose de respingo y, si hace falta, su cabeceo. Un hueso que no se encuentra por nombre se ignora con un warning.")]
    [SerializeField] private JointPose[] _joints =
    {
        new JointPose("joint6_R",      new Vector3(33.6398277f, 183.583099f, 308.656738f)),
        new JointPose("joint4_R",      new Vector3(313.025055f, 90f, 320.033142f)),
        new JointPose("joint6Part1_R", new Vector3(330.287628f, 324.985107f, 18.6812191f)),
        new JointPose("joint7_R",      new Vector3(344.377197f, 196.831207f, 315.817627f),
                                       new Vector3(339.546631f, 167.225983f, 328.282349f),
                                       new Vector3(14.026947f, 200.43512f, 299.491943f),
                                       new Vector3(5.05488348f, 215.61554f, 302.030304f),
                                       new Vector3(335.26593f, 195.803513f, 305.567902f)),
        new JointPose("joint19_R",     new Vector3(313.025055f, 90f, 143.26088f)),
        new JointPose("joint21_R",     new Vector3(19.977354f, 157.289078f, 322.142639f)),
    };

    [Header("Timing")]
    [Tooltip("Segundos que dura la reacción completa (Recoil + Hold + Recover). Mientras corre, la planta es invulnerable y su ataque queda en pausa.")]
    [Min(0.05f)]
    [SerializeField] private float _duration = 2f;
    [Tooltip("Qué parte de la duración se va en el Recoil (el respingo inicial, rápido). 0.15 = el 15%.")]
    [Range(0f, 1f)]
    [SerializeField] private float _recoilPortion = 0.15f;
    [Tooltip("Qué parte de la duración se va en el Recover (la vuelta a la pose del Animator). Lo que no se llevan el Recoil ni el Recover es el Hold, donde se hace el cabeceo: con los valores por defecto, 1.2 s repartidos en partes iguales entre los puntos del cabeceo.")]
    [Range(0f, 1f)]
    [SerializeField] private float _recoverPortion = 0.25f;

    [Header("Curves")]
    [Tooltip("Avance del Recoil (0 = pose del Animator, 1 = Target Euler) a lo largo del tiempo normalizado de la fase. Por defecto arranca rápido y frena al llegar.")]
    [SerializeField] private AnimationCurve _recoilCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 2f), new Keyframe(1f, 1f, 0f, 0f));
    [Tooltip("Avance del Recover (0 = donde estaba, 1 = pose del Animator) a lo largo del tiempo normalizado de la fase.")]
    [SerializeField] private AnimationCurve _recoverCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private enum Phase { Idle, Recoil, Hold, Recover }

    private sealed class Bone
    {
        public Transform transform;
        public Quaternion target;      // pose de respingo
        public Vector3[] path;         // Target Euler + cabeceo, con los ángulos "desenrollados" (ver BuildPath)
        public Quaternion applied;     // lo último que se escribió en el hueso
        public Quaternion recoverFrom; // dónde estaba el hueso cuando arrancó el Recover
    }

    private Bone[] _bones = Array.Empty<Bone>();
    private Phase _phase;
    private float _elapsed; // tiempo dentro de la fase actual

    // El componente está activo y encontró al menos un hueso. Si no, la planta reacciona como siempre (sin animación).
    public bool IsAvailable => isActiveAndEnabled && _bones.Length > 0;

    // Recoil, Hold o Recover en curso: la planta es invulnerable y los huesos son de esta animación.
    public bool IsPlaying => _phase != Phase.Idle;

    // Lo que dura la reacción completa. CarnivorousPlant pausa su ataque ese tiempo.
    public float Duration => _duration;

    private float RecoilTime => _duration * Mathf.Clamp01(_recoilPortion);
    private float RecoverTime => _duration * Mathf.Clamp01(_recoverPortion);
    private float HoldTime => Mathf.Max(0f, _duration - RecoilTime - RecoverTime);

    private void Awake()
    {
        var bones = new System.Collections.Generic.List<Bone>(_joints.Length);

        foreach(JointPose pose in _joints)
        {
            Transform joint = FindBone(transform, pose.jointName);

            if(joint == null)
            {
                Debug.LogWarning($"[PlantHurtReaction] No encontré el hueso '{pose.jointName}' dentro de '{name}': se ignora.", this);
                continue;
            }

            bones.Add(new Bone
            {
                transform = joint,
                target = Quaternion.Euler(pose.targetEuler),
                path = BuildPath(pose.targetEuler, pose.bobEulers)
            });
        }

        _bones = bones.ToArray();
    }

    // Que Recoil Portion + Recover Portion no pasen de 1: el Hold no puede durar menos que nada.
    private void OnValidate()
    {
        _recoverPortion = Mathf.Min(_recoverPortion, 1f - _recoilPortion);
    }

    private void OnDisable()
    {
        // Apagado en medio de la reacción: el Animator se queda con los huesos.
        _phase = Phase.Idle;
    }

    // Arranca la reacción desde la pose que tengan los huesos. false si no se puede: apagado, sin huesos, o ya está en curso.
    public bool Play()
    {
        if(!IsAvailable || _phase != Phase.Idle) return false;

        _phase = Phase.Recoil;
        _elapsed = 0f;
        return true;
    }

    // Corta la reacción de golpe: los huesos vuelven al Animator en el próximo frame.
    public void Cancel()
    {
        _phase = Phase.Idle;
    }

    private void LateUpdate()
    {
        if(_phase == Phase.Idle) return;

        _elapsed += Time.deltaTime;

        switch(_phase)
        {
            case Phase.Recoil: UpdateRecoil(); break;
            case Phase.Hold: UpdateHold(); break;
            default: UpdateRecover(); break;
        }
    }

    private void UpdateRecoil()
    {
        float k = _recoilCurve.Evaluate(Progress(RecoilTime));

        // Desde la pose que dejó el Animator en este frame: sin salto al arrancar.
        foreach(Bone bone in _bones)
            Apply(bone, Quaternion.SlerpUnclamped(bone.transform.localRotation, bone.target, k));

        if(_elapsed >= RecoilTime) NextPhase(Phase.Hold, RecoilTime);
    }

    private void UpdateHold()
    {
        float progress = Progress(HoldTime);

        // Los huesos sin cabeceo se quedan en el respingo; los que lo tienen recorren su camino de punta a punta.
        foreach(Bone bone in _bones)
        {
            Quaternion pose = bone.path.Length > 1
                ? Quaternion.Euler(EvaluatePath(bone.path, progress * (bone.path.Length - 1)))
                : bone.target;

            Apply(bone, pose);
        }

        if(_elapsed >= HoldTime) NextPhase(Phase.Recover, HoldTime);
    }

    private void UpdateRecover()
    {
        float k = _recoverCurve.Evaluate(Progress(RecoverTime));

        // Hacia la pose que tenga el Animator en este frame: al llegar no hay salto.
        foreach(Bone bone in _bones)
            Apply(bone, Quaternion.SlerpUnclamped(bone.recoverFrom, bone.transform.localRotation, k));

        if(_elapsed >= RecoverTime) _phase = Phase.Idle;
    }

    // Pasa a la fase siguiente conservando lo que sobró del frame, así las tres fases suman Duration justo.
    private void NextPhase(Phase next, float phaseTime)
    {
        _elapsed = Mathf.Max(0f, _elapsed - phaseTime);
        _phase = next;

        // El Recover arranca desde lo último que se escribió (el final del Hold, que en la cabeza es el último punto del cabeceo).
        if(next == Phase.Recover)
            foreach(Bone bone in _bones)
                bone.recoverFrom = bone.applied;
    }

    private float Progress(float duration) => duration > 0f ? Mathf.Clamp01(_elapsed / duration) : 1f;

    private static void Apply(Bone bone, Quaternion rotation)
    {
        bone.transform.localRotation = rotation;
        bone.applied = rotation;
    }

    // Camino del Hold: Target Euler y después los puntos del cabeceo. Cada punto se "desenrolla" respecto del anterior (por eje,
    // el camino más corto) para que el spline no dé la vuelta entera al pasar de 350° a 10°. Es el mismo criterio por eje que
    // usa PlantBiteWindup para interpolar sus poses.
    private static Vector3[] BuildPath(Vector3 targetEuler, Vector3[] bobEulers)
    {
        int bobCount = bobEulers != null ? bobEulers.Length : 0;
        var path = new Vector3[1 + bobCount];
        path[0] = targetEuler;

        for(int i = 1; i < path.Length; i++)
        {
            Vector3 previous = path[i - 1];
            Vector3 next = bobEulers[i - 1];

            path[i] = new Vector3(
                previous.x + Mathf.DeltaAngle(previous.x, next.x),
                previous.y + Mathf.DeltaAngle(previous.y, next.y),
                previous.z + Mathf.DeltaAngle(previous.z, next.z));
        }

        return path;
    }

    // Spline Catmull-Rom (uniforme) que pasa por todos los puntos del camino. s va de 0 (el primero) a path.Length - 1 (el
    // último): un tramo por cada unidad.
    private static Vector3 EvaluatePath(Vector3[] path, float s)
    {
        if(path.Length == 1) return path[0];

        s = Mathf.Clamp(s, 0f, path.Length - 1);
        int i = Mathf.Min((int)s, path.Length - 2);
        float t = s - i;
        float t2 = t * t;
        float t3 = t2 * t;

        return (2f * t3 - 3f * t2 + 1f) * path[i]
             + (t3 - 2f * t2 + t) * Tangent(path, i)
             + (-2f * t3 + 3f * t2) * path[i + 1]
             + (t3 - t2) * Tangent(path, i + 1);
    }

    private static Vector3 Tangent(Vector3[] path, int i)
    {
        Vector3 previous = path[Mathf.Max(i - 1, 0)];
        Vector3 next = path[Mathf.Min(i + 1, path.Length - 1)];

        return (next - previous) * 0.5f;
    }

    private static Transform FindBone(Transform root, string boneName)
    {
        if(string.IsNullOrEmpty(boneName)) return null;

        foreach(Transform child in root)
        {
            if(child.name == boneName) return child;

            Transform found = FindBone(child, boneName);
            if(found != null) return found;
        }

        return null;
    }

    // Para probar la animación suelta desde el Inspector (menú de tres puntos del componente), sin pegarle a la planta.
    [ContextMenu("Probar reacción (Play Mode)")]
    private void TestPlay()
    {
        if(Application.isPlaying) Play();
    }
}
