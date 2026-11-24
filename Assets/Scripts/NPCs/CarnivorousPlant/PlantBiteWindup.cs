using System;
using System.Collections.Generic;
using UnityEngine;

// Code-driven animation of the plant's melee attack "preparation" (the bite).
//
// It has two phases, and the duration of each one is set in the Inspector:
//  1. Windup: the bones go from the pose they start with to the target rotation, progressively. They start from the pose
//     the plant has at that moment: if it comes from the interval between bites, the tired one (PlantTiredAnimation), so the
//     transition is direct and without jumps; otherwise, the one the Animator has.
//  2. Return: they quickly go back to the base rotation (the factory one), which is where the preparation ends.
// When the Return ends the preparation is complete (Completed) and only then does PlantBiteState trigger the usual bite
// (CarnivorousPlant.Bite, with its 'nibble' animation). It doesn't replace that animation: it is the step before it.
//
// The rotations are LOCAL, the same ones the Inspector shows on each bone's Transform. On joint7_R the Z axis doesn't follow the
// start -> target path: during the Windup it sweeps from Z From to Z To (see JointPose.sweepZ), and the start blend
// (Blend In Time) is what takes it from the pose it came with to that Z From. Besides the head (joint6_R and joint7_R) it animates the
// leaves (joint1*, joint4*, joint6Part*, joint19*, joint21*): each one goes, axis by axis, from the pose it starts with to its target.
//
// Also, during the Windup the whole plant (the root transform) turns towards the player, around its vertical axis and at a
// limited speed (Root Turn Speed). That is what aims the bite's HitBox: since the bones go back to their base pose in the Return, the
// direction it bites in is defined by this turn. In the Return the plant stays still, so the player can dodge the
// bite by leaving the zone (or by running faster than it turns). The plant keeps facing where it ended up.
//
// PlantSpitBehaviour also uses it: before each spit the plant does this same preparation and only spits when it
// finishes. That is chosen by Play(..., forSpit: true), and then the whole animation (Windup, Return and blend) runs Spit Speed
// Multiplier times faster. The bite keeps using it at normal speed.
//
// The Animator rewrites these bones every frame (Idle, nibble and spittle have curves for all of them), so the pose is applied
// in LateUpdate, after it. Execution Order also places it after CarnivorousPlant's LateUpdate, which is where
// joint7_R's aim is applied: while the preparation runs, this pose wins.
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public class PlantBiteWindup : MonoBehaviour
{
    [Serializable]
    private struct JointPose
    {
        [Tooltip("Nombre del hueso. Se busca entre los hijos de la planta al arrancar.")]
        public string jointName;
        [Tooltip("Rotación local (Euler) de fábrica: a esta vuelve el Return y en ella termina la preparación. El Windup ya no arranca de acá sino de la pose que tengan los huesos en ese momento (la de cansada, si viene del intervalo entre mordiscos).")]
        public Vector3 baseEuler;
        [Tooltip("Rotación local (Euler) a la que llega el Windup.")]
        public Vector3 targetEuler;
        [Tooltip("Si está activo, el eje Z no va de la base al objetivo: durante el Windup va de Z From a Z To.")]
        public bool sweepZ;
        public float zFrom;
        public float zTo;

        public JointPose(string jointName, Vector3 baseEuler, Vector3 targetEuler)
        {
            this.jointName = jointName;
            this.baseEuler = baseEuler;
            this.targetEuler = targetEuler;
            sweepZ = false;
            zFrom = 0f;
            zTo = 0f;
        }

        public JointPose(string jointName, Vector3 baseEuler, Vector3 targetEuler, float zFrom, float zTo)
            : this(jointName, baseEuler, targetEuler)
        {
            sweepZ = true;
            this.zFrom = zFrom;
            this.zTo = zTo;
        }
    }

    [Header("Joints")]
    [Tooltip("Huesos que anima la preparación (la cabeza y las hojas), con su rotación base (la de fábrica) y la objetivo. Un hueso que no se encuentra por nombre se ignora con un warning.")]
    [SerializeField] private JointPose[] _joints =
    {
        new JointPose("joint6_R",      new Vector3(0f, 180f, 312.531738f),                new Vector3(28.5997677f, 210.009979f, 320.348785f)),
        new JointPose("joint4_R",      new Vector3(313.025055f, 90f, 336.135864f),        new Vector3(313.025055f, 90.0000076f, 314.567627f)),
        new JointPose("joint6Part1_R", new Vector3(0f, 0f, 359.506653f),                  new Vector3(0f, 0f, 359.506653f)),
        new JointPose("joint7_R",      new Vector3(356.112091f, 184.17572f, 312.88327f),  new Vector3(10.3371201f, 168.730225f, 312.00238f), -3.811f, -84.533f),
        new JointPose("joint19_R",     new Vector3(313.025055f, 90f, 206.72084f),         new Vector3(313.025055f, 90f, 228.794861f)),
        new JointPose("joint21_R",     new Vector3(0f, 180f, 313.276215f),                new Vector3(0f, 180f, 309.197266f)),
        // The leaves that were added later. The base is the rotation each bone has in the prefab.
        new JointPose("joint6Part2_R",  new Vector3(0f, 0f, 359.506653f),  new Vector3(0f, 0f, 344.839569f)),
        new JointPose("joint1_R",       new Vector3(0f, 0f, 44.545004f),   new Vector3(0f, 0f, 25.7183571f)),
        new JointPose("joint1Part1_R",  new Vector3(0f, 0f, 356.76f),      new Vector3(0f, 0f, 331.908081f)),
        new JointPose("joint19Part1_R", new Vector3(0f, 0f, 359.95333f),   new Vector3(0f, 0f, 24.4871197f)),
        new JointPose("joint21Part1_R", new Vector3(0f, 0f, 0.023333331f), new Vector3(0f, 0f, 332.629303f)),
        new JointPose("joint21Part2_R", new Vector3(0f, 0f, 0.023333331f), new Vector3(0f, 0f, 338.607269f)),
        new JointPose("joint21Part3_R", new Vector3(0f, 0f, 0.023333331f), new Vector3(0f, 0f, 338.475098f)),
        new JointPose("joint21Part4_R", new Vector3(0f, 0f, 0.023333331f), new Vector3(0f, 0f, 340.939728f)),
        new JointPose("joint4Part1_R",  new Vector3(0f, 0f, 1.62f),        new Vector3(0f, 0f, 337.221741f)),
    };

    [Header("Timing")]
    [Tooltip("Segundos que tardan los huesos en ir de la pose con la que arrancan a la rotación objetivo.")]
    [Min(0f)]
    [SerializeField] private float _windupDuration = 3f;
    [Tooltip("Segundos que tardan en volver de la objetivo a la base. Cuando termina, sale el mordisco.")]
    [Min(0f)]
    [SerializeField] private float _returnDuration = 0.5f;
    [Tooltip("Segundos de mezcla con la pose con la que arrancan los huesos, para que no haya un salto (sobre todo en el Z de joint7_R, que barre de Z From a Z To sin partir de donde estaba). 0 = sin mezcla.")]
    [Min(0f)]
    [SerializeField] private float _blendInTime = 0.15f;
    [Tooltip("Velocidad de la preparación cuando es para escupir (PlantSpitBehaviour) y no para el mordisco. 2 = el doble de rápido: Windup, Return y mezcla duran la mitad. 1 = igual que el mordisco.")]
    [Min(0.01f)]
    [SerializeField] private float _spitSpeedMultiplier = 2f;

    [Header("Root Rotation")]
    [Tooltip("Grados por segundo a los que gira toda la planta hacia el player mientras dura el Windup (solo sobre su eje vertical). En el Return queda quieta. 0 = no gira. Al escupir, que la animación corre más rápida, gira proporcionalmente más rápido (por Spit Speed Multiplier) para llegar a la misma orientación que en el mordisco.")]
    [Min(0f)]
    [SerializeField] private float _rootTurnSpeed = 90f;
    [Tooltip("Eje LOCAL de la planta que tiene que quedar apuntando al player (hacia donde muerde). Si gira para el lado equivocado o queda de costado, probá otro eje (±X, ±Z).")]
    [SerializeField] private Vector3 _rootFacingAxis = Vector3.forward;

    [Header("Curves")]
    [Tooltip("Avance del Windup (0 = base, 1 = objetivo) a lo largo del tiempo normalizado de la fase.")]
    [SerializeField] private AnimationCurve _windupCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Avance del Return (0 = donde estaba, 1 = base) a lo largo del tiempo normalizado de la fase.")]
    [SerializeField] private AnimationCurve _returnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Feedback")]
    [SerializeField] private ParticleSystem _preparingAttackPS;
    
    private enum Phase { Idle, Windup, Return }

    private sealed class Bone
    {
        public Transform transform;
        public JointPose pose;
        public Quaternion baseRot;
        public Quaternion startRot;   // the pose the bone had when the Windup started (see Play)
        public Vector3 startEuler;    // the same, in Euler: the Windup interpolates per axis, from here
        public Quaternion applied;    // the last thing written to the bone
        public Quaternion returnFrom; // where the bone was when the Return started
    }

    private Bone[] _bones = Array.Empty<Bone>();
    private Phase _phase;
    private float _elapsed;
    private float _speed = 1f; // 1 for the bite, Spit Speed Multiplier for the spit
    private bool _cancelled;
    private Transform _target; // who the plant turns towards during the Windup

    // The component is active and found at least one bone. If not, PlantBiteState bites right away, as usual.
    public bool IsAvailable => isActiveAndEnabled && _bones.Length > 0;

    // Windup or Return in progress: the bones belong to this animation (joint7_R's aim waits).
    public bool IsPlaying => _phase != Phase.Idle;

    // The last preparation reached the end (a Cancel didn't cut it). Turned off when another one starts.
    public bool Completed { get; private set; }

    private void Awake()
    {
        var bones = new List<Bone>(_joints.Length);

        foreach(JointPose pose in _joints)
        {
            Transform joint = FindBone(transform, pose.jointName);

            if(joint == null)
            {
                Debug.LogWarning($"[PlantBiteWindup] Bone '{pose.jointName}' not found inside '{name}': ignored.", this);
                continue;
            }

            bones.Add(new Bone { transform = joint, pose = pose, baseRot = Quaternion.Euler(pose.baseEuler) });
        }

        _bones = bones.ToArray();
    }

    private void OnDisable()
    {
        // Turned off in the middle of the preparation: the Animator keeps the bones and it doesn't count as complete.
        _phase = Phase.Idle;
        Completed = false;
    }

    // Starts the preparation from the base pose. lookTarget is who the whole plant turns towards during the Windup (null = it doesn't
    // turn). forSpit = the preparation is for spitting (PlantSpitBehaviour) and not for the bite: it runs Spit Speed Multiplier
    // times faster, and the plant turns towards lookTarget just like in the bite (with the turn speed scaled by the same
    // factor, so it reaches the same orientation). The preparation's particle (Preparing Attack PS) belongs to the bite only: when
    // spitting it doesn't play. false if it can't be done now: turned off, no bones, or still returning from a cut preparation (the
    // caller retries on the next frame).
    public bool Play(Transform lookTarget = null, bool forSpit = false)
    {
        if(!IsAvailable || _phase != Phase.Idle) return false;

        // The Windup starts from the pose the bones have now: the tired one if the plant comes from the interval between bites
        // (PlantTiredAnimation), or the Animator's. It is what is shown on screen: Play is called from Update, before the Animator
        // rewrites this frame's bones, so they still hold what was drawn in the previous one (the tired pose included,
        // which is applied in LateUpdate).
        foreach(Bone bone in _bones)
        {
            bone.startRot = bone.transform.localRotation;
            bone.startEuler = bone.startRot.eulerAngles;
        }

        // Current implementation, replace later with GenericPS.Play()
        if(_preparingAttackPS != null && !forSpit) _preparingAttackPS.Play();
        _target = lookTarget;
        _speed = forSpit ? _spitSpeedMultiplier : 1f;
        _phase = Phase.Windup;
        _elapsed = 0f;
        _cancelled = false;
        Completed = false;
        return true;
    }

    // Stops the Windup: the bones go back to the base with the same quick Return, from wherever they are. It doesn't count as complete.
    public void Cancel()
    {
        if(_phase != Phase.Windup) return;

        _cancelled = true;
        BeginReturn();
    }

    private void LateUpdate()
    {
        if(_phase == Phase.Idle) return;

        _elapsed += Time.deltaTime * _speed;

        if(_phase == Phase.Windup) UpdateWindup();
        else UpdateReturn();
    }

    // Turns the whole plant towards the player, only around its own vertical axis (so it keeps whatever tilt it has in the
    // scene) and at a limited speed: that is why a fast-moving player escapes its zone. The speed is scaled by
    // _speed (just like the animation's time): when spitting, the Windup lasts less and turns proportionally faster, so
    // it reaches the same orientation as in the bite.
    private void TurnRoot()
    {
        if(_target == null || _rootTurnSpeed <= 0f || _rootFacingAxis.sqrMagnitude < 0.0001f) return;

        Vector3 up = transform.up;
        Vector3 facing = Vector3.ProjectOnPlane(transform.TransformDirection(_rootFacingAxis), up);
        Vector3 toTarget = Vector3.ProjectOnPlane(_target.position - transform.position, up);

        if(facing.sqrMagnitude < 0.0001f || toTarget.sqrMagnitude < 0.0001f) return;

        float maxStep = _rootTurnSpeed * _speed * Time.deltaTime;
        float step = Mathf.Clamp(Vector3.SignedAngle(facing, toTarget, up), -maxStep, maxStep);

        transform.Rotate(up, step, Space.World);
    }

    private void UpdateWindup()
    {
        TurnRoot();

        float progress = Progress(_windupDuration);
        float k = _windupCurve.Evaluate(progress);

        // On start it blends with the pose the bones came with, so the first frame doesn't jump (especially on
        // joint7_R's Z, which doesn't start from where it was but from Z From).
        float blend = _blendInTime > 0f ? Mathf.SmoothStep(0f, 1f, _elapsed / _blendInTime) : 1f;

        foreach(Bone bone in _bones)
        {
            Quaternion pose = WindupPose(bone, k);
            if(blend < 1f) pose = Quaternion.Slerp(bone.startRot, pose, blend);

            Apply(bone, pose);
        }

        if(progress >= 1f) BeginReturn();
    }

    private void UpdateReturn()
    {
        float progress = Progress(_returnDuration);
        float k = _returnCurve.Evaluate(progress);

        foreach(Bone bone in _bones)
            Apply(bone, Quaternion.SlerpUnclamped(bone.returnFrom, bone.baseRot, k));

        if(progress >= 1f) Finish();
    }

    // The Return starts from the last thing written (the end of the Windup, or where Cancel cut it), so there are no jumps.
    private void BeginReturn()
    {
        foreach(Bone bone in _bones)
            bone.returnFrom = bone.applied;

        _phase = Phase.Return;
        _elapsed = 0f;
    }

    private void Finish()
    {
        // It ends exactly at the base rotation, even if the curve doesn't reach 1.
        foreach(Bone bone in _bones)
            Apply(bone, bone.baseRot);

        _phase = Phase.Idle;
        Completed = !_cancelled;
    }

    private float Progress(float duration) => duration > 0f ? Mathf.Clamp01(_elapsed / duration) : 1f;

    private static void Apply(Bone bone, Quaternion rotation)
    {
        bone.transform.localRotation = rotation;
        bone.applied = rotation;
    }

    // Windup pose at progress k: from the pose the bone started with (startEuler) to the target, axis by axis.
    private static Quaternion WindupPose(Bone bone, float k)
    {
        JointPose pose = bone.pose;
        Vector3 from = bone.startEuler;

        float x = LerpAngle(from.x, pose.targetEuler.x, k);
        float y = LerpAngle(from.y, pose.targetEuler.y, k);
        float z = pose.sweepZ ? Mathf.LerpUnclamped(pose.zFrom, pose.zTo, k) : LerpAngle(from.z, pose.targetEuler.z, k);

        return Quaternion.Euler(x, y, z);
    }

    // Like Mathf.LerpAngle (along the shortest path) but without clamping k to 0..1: a bouncing curve looks the way it is drawn.
    private static float LerpAngle(float from, float to, float k) => from + Mathf.DeltaAngle(from, to) * k;

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

    // To test the animation on its own from the Inspector (the component's three-dot menu), without bringing the player close.
    [ContextMenu("Test preparation (Play Mode)")]
    private void TestPlay()
    {
        if(Application.isPlaying) Play();
    }
}
