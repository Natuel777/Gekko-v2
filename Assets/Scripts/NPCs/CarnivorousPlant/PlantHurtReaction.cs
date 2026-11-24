using System;
using UnityEngine;

// Code-driven animation of the plant's reaction to a tongue hit that does NOT purify it (the intermediate hits of the DataSO's Hits To
// Purify). Same style as PlantBiteWindup: it goes on the same GameObject as CarnivorousPlant, applies some bones' pose in
// LateUpdate (the Animator rewrites them every frame) and its timings are adjusted in the Inspector.
//
// It lasts Duration seconds in total and has three phases. Recoil Portion and Recover Portion set how much of the duration each
// one takes; the Hold is what is left:
//  1. Recoil: the bones quickly go from the rotation they had at the moment of the hit to their Target Euler (the flinch).
//  2. Hold: the plant stays in that pose. Bones with Bob Eulers (today joint7_R, the head) do a nod: they start
//     from their Target Euler and pass through those points, in order, with a smooth curve that goes through all of them.
//  3. Recover: they go back to whatever pose they have at that moment underneath this animation.
// The Recoil's starting point is each bone's current rotation when the hit arrives (read in Play): the tired one
// (PlantTiredAnimation) if the plant is in the interval between bites, which is when it can be hit, or the Animator's.
// The Recover's destination is the pose underneath on each frame: the tired one if that animation is still running (it runs underneath
// while this one lasts), and otherwise the Animator's. That way there are no jumps either on start or on end, no matter what the
// plant is doing (tired, resting, spitting...). Target Euler and Bob Eulers are absolute LOCAL rotations, the same ones the
// Inspector shows on each bone's Transform.
//
// While it runs (IsPlaying) the plant is invulnerable, just like during the bite preparation: CarnivorousPlant (CanBeHit)
// enforces it, and it also pauses its attack for that time so it doesn't fight this animation over the bones.
// CarnivorousPlant triggers it in Damage when the hit doesn't purify; without this component the plant reacts as usual.
//
// Execution Order places it after PlantTiredAnimation (90) and PlantBiteWindup (100): if they touched the same bone in a frame,
// this one wins, and what those left written on the bones is the base pose the Recover goes back to.
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
    [Tooltip("Avance del Recoil (0 = rotación que tenía el hueso al llegar el golpe, 1 = Target Euler) a lo largo del tiempo normalizado de la fase. Por defecto arranca rápido y frena al llegar.")]
    [SerializeField] private AnimationCurve _recoilCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 2f), new Keyframe(1f, 1f, 0f, 0f));
    [Tooltip("Avance del Recover (0 = donde estaba, 1 = la pose de abajo: la de cansada o la del Animator) a lo largo del tiempo normalizado de la fase.")]
    [SerializeField] private AnimationCurve _recoverCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private enum Phase { Idle, Recoil, Hold, Recover }

    private sealed class Bone
    {
        public Transform transform;
        public Quaternion target;      // flinch pose
        public Quaternion startRot;    // the rotation the bone had when the hit arrived (see Play)
        public Vector3[] path;         // Target Euler + nod, with the angles "unwrapped" (see BuildPath)
        public Quaternion applied;     // the last thing written to the bone
        public Quaternion recoverFrom; // where the bone was when the Recover started
    }

    private Bone[] _bones = Array.Empty<Bone>();
    private Phase _phase;
    private float _elapsed; // time within the current phase

    // The component is active and found at least one bone. If not, the plant reacts as usual (no animation).
    public bool IsAvailable => isActiveAndEnabled && _bones.Length > 0;

    // Recoil, Hold or Recover in progress: the plant is invulnerable and the bones belong to this animation.
    public bool IsPlaying => _phase != Phase.Idle;

    // How long the whole reaction lasts. CarnivorousPlant pauses its attack for that time.
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
                Debug.LogWarning($"[PlantHurtReaction] Bone '{pose.jointName}' not found inside '{name}': ignored.", this);
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

    // So that Recoil Portion + Recover Portion don't exceed 1: the Hold can't last less than nothing.
    private void OnValidate()
    {
        _recoverPortion = Mathf.Min(_recoverPortion, 1f - _recoilPortion);
    }

    private void OnDisable()
    {
        // Turned off in the middle of the reaction: the Animator keeps the bones.
        _phase = Phase.Idle;
    }

    // Starts the reaction from each bone's current rotation. false if it can't: turned off, no bones, or already in progress.
    public bool Play()
    {
        if(!IsAvailable || _phase != Phase.Idle) return false;

        // What the bones have now is what is seen on screen: the tired pose if the hit arrived in the interval between
        // bites (PlantTiredAnimation). Play is called from the tongue damage, which runs before the Animator rewrites
        // this frame's bones, so they still hold what was drawn in the previous one.
        foreach(Bone bone in _bones)
            bone.startRot = bone.transform.localRotation;

        _phase = Phase.Recoil;
        _elapsed = 0f;
        return true;
    }

    // Cuts the reaction at once: the bones go back to the Animator on the next frame.
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

        // From the rotation each bone had when the hit arrived: no jump on start.
        foreach(Bone bone in _bones)
            Apply(bone, Quaternion.SlerpUnclamped(bone.startRot, bone.target, k));

        if(_elapsed >= RecoilTime) NextPhase(Phase.Hold, RecoilTime);
    }

    private void UpdateHold()
    {
        float progress = Progress(HoldTime);

        // Bones without a nod stay in the flinch; the ones that have it travel their path from end to end.
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

        // Towards the pose the bones have underneath this animation in this frame (the tired one, which keeps running, or the
        // Animator's): on arrival there is no jump.
        foreach(Bone bone in _bones)
            Apply(bone, Quaternion.SlerpUnclamped(bone.recoverFrom, bone.transform.localRotation, k));

        if(_elapsed >= RecoverTime) _phase = Phase.Idle;
    }

    // Moves on to the next phase keeping what was left over from the frame, so the three phases add up to exactly Duration.
    private void NextPhase(Phase next, float phaseTime)
    {
        _elapsed = Mathf.Max(0f, _elapsed - phaseTime);
        _phase = next;

        // The Recover starts from the last thing written (the end of the Hold, which on the head is the nod's last point).
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

    // Hold path: Target Euler and then the nod's points. Each point is "unwrapped" relative to the previous one (per axis, the
    // shortest way) so the spline doesn't go all the way around when passing from 350° to 10°. It is the same per-axis criterion
    // PlantBiteWindup uses to interpolate its poses.
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

    // Catmull-Rom spline (uniform) that passes through all the points of the path. s goes from 0 (the first) to path.Length - 1 (the
    // last): one segment per unit.
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

    // To test the animation on its own from the Inspector (the component's three-dot menu), without hitting the plant.
    [ContextMenu("Test reaction (Play Mode)")]
    private void TestPlay()
    {
        if(Application.isPlaying) Play();
    }
}
