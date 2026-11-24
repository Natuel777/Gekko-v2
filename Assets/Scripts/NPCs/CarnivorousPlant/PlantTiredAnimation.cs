using System;
using System.Collections.Generic;
using UnityEngine;

// Code-driven animation of the "tired plant": the pose the boss takes in the interval between bites (the DataSO's Bite Interval,
// CarnivorousPlantDataSO.biteInterval), which is the window in which Gekko can damage it (see CarnivorousPlant.CanBeHit).
// It is the visual signal that it is now vulnerable. Same style as PlantBiteWindup and PlantHurtReaction: it goes on the same
// GameObject as CarnivorousPlant, applies the bones' pose in LateUpdate (the Animator rewrites them every frame) and its
// timings are adjusted in the Inspector.
//
// It starts when CarnivorousPlant enters the interval (SetInBiteInterval(true), right after each bite) and is cut when
// the interval ends (SetInBiteInterval(false): the next bite's preparation starts, the player left the range, the
// plant was purified...). Phases:
//  1. Wait: for Start Delay seconds the bones still belong to the Animator, which is playing the bite animation ('nibble',
//     about 0.8 s). That way the bite is seen in full and the plant only gets tired once it has finished biting.
//  2. Settle: the bones go from their default rotation (the one they have in the prefab, which is read when the scene starts) to the
//     tired pose (Target Euler). The first Blend In Time seconds are blended with the pose the Animator left, so there is no
//     jump when taking the bones.
//  3. Hold: the plant stays in that pose until the interval ends. Bones with Shake (today, joint7_R, the head) oscillate
//     between Target Euler and Shake Euler: the head's "no" gesture.
//  4. Release: when the interval ends the bones go back to the Animator's pose over Release Time seconds. If what follows
//     is the bite preparation (PlantBiteWindup), it starts from the tired pose and takes by itself the bones it animates;
//     this Release smooths the rest, the ones the preparation doesn't touch. Rest bones (see below) don't go back to the
//     Animator's pose but to their own.
//
// Rest bones (Rest Joints): today, joint10Part1_R. Its rotation is a constant curve (identity) in the Animator's three clips
// (Idle, nibble and spittle): nothing moves it, so "going back to the Animator's pose" isn't enough to give it back and
// it stayed pinned in the tired pose. That is why it has its own rest pose ((0,0,0) in joint10Part1_R, the factory one) and
// this animation takes it there, until it is exactly on it, when:
//  - the interval ends (the Release), which is also when the bite preparation starts (PlantBiteWindup);
//  - a tongue hit arrives (PlantHurtReaction starts): it releases it over Release Time seconds and doesn't take it again for the
//    rest of that interval (the next interval lowers it again);
//  - the component is turned off in the middle of the animation (the pause turns off all the scene's): it is restored when it is turned back on.
//
// It has no fixed duration: it lasts as long as the interval, also if something stretches it (a hit reaction pauses the attack and,
// with it, the interval). The interval only determines the Settle's time, which shrinks if what is left of the window is short.
//
// It coexists with the other two animations: PlantBiteWindup (Execution Order 100) and PlantHurtReaction (110) are applied after this one
// and read the tired pose as the one the bones have at that moment: the preparation starts from it and so does the hit reaction,
// and when it ends it goes back to it because this animation keeps running underneath.
[DisallowMultipleComponent]
[DefaultExecutionOrder(90)]
public class PlantTiredAnimation : MonoBehaviour
{
    [Serializable]
    private struct JointPose
    {
        [Tooltip("Nombre del hueso. Se busca entre los hijos de la planta al arrancar.")]
        public string jointName;
        [Tooltip("Rotación local (Euler) de la pose de cansada: a donde llega el Settle y donde se queda durante el Hold. Es la misma que muestra el Inspector en el Transform del hueso.")]
        public Vector3 targetEuler;
        [Tooltip("Gesto de 'no' con la cabeza: durante el Hold el hueso oscila entre Target Euler y Shake Euler. Apagado = se queda quieto en Target Euler.")]
        public bool shake;
        [Tooltip("Segunda rotación local (Euler) del gesto de 'no'. Solo cuenta con Shake activo.")]
        public Vector3 shakeEuler;

        public JointPose(string jointName, Vector3 targetEuler)
        {
            this.jointName = jointName;
            this.targetEuler = targetEuler;
            shake = false;
            shakeEuler = Vector3.zero;
        }

        public JointPose(string jointName, Vector3 targetEuler, Vector3 shakeEuler)
            : this(jointName, targetEuler)
        {
            shake = true;
            this.shakeEuler = shakeEuler;
        }
    }

    [Header("Joints")]
    [Tooltip("Huesos que anima la pose de cansada, con su rotación objetivo y, si hace falta, el gesto de 'no'. Un hueso que no se encuentra por nombre se ignora con un warning. La rotación de la que arranca cada uno es la que tiene en el prefab.")]
    [SerializeField] private JointPose[] _joints =
    {
        new JointPose("joint6Part1_R",  new Vector3(0f, 0f, 30.9665623f)),
        new JointPose("joint1Part1_R",  new Vector3(0f, 0f, 311.077271f)),
        new JointPose("joint1Part2_R",  new Vector3(0f, 0f, 347.534058f)),
        new JointPose("joint4_R",       new Vector3(313.025055f, 89.9999771f, 12.7486115f)),
        new JointPose("joint4Part1_R",  new Vector3(0f, 0f, 343.442749f)),
        new JointPose("joint19_R",      new Vector3(313.025055f, 90f, 165.076721f)),
        new JointPose("joint19Part1_R", new Vector3(0f, 0f, 36.4353867f)),
        new JointPose("joint19Part2_R", new Vector3(0f, 0f, 341.656036f)),
        new JointPose("joint21_R",      new Vector3(0f, 180f, 354.340393f)),
        new JointPose("joint21Part1_R", new Vector3(0f, 0f, 329.631989f)),
        new JointPose("joint21Part3_R", new Vector3(0f, 0f, 339.978943f)),
        new JointPose("joint21Part4_R", new Vector3(0f, 0f, 343.800537f)),
        new JointPose("joint7_R",       new Vector3(18.4571705f, 159.046844f, 309.583252f), new Vector3(36.8048172f, 153.666031f, 308.576355f)),
        new JointPose("joint8_R",       new Vector3(357.332031f, 179.883911f, 276.380341f)),
        new JointPose("joint10Part1_R", new Vector3(0f, 0f, 343.093903f)),
        new JointPose("joint13_R",      new Vector3(0f, 0f, 261.306396f)),
        new JointPose("joint15_R",      new Vector3(358.007294f, 179.913315f, 276.98291f)),
    };

    // A rest bone: see "Rest bones" above.
    [Serializable]
    private struct RestJoint
    {
        [Tooltip("Nombre del hueso. Tiene que estar también en Joints (si no, se ignora con un warning).")]
        public string jointName;
        [Tooltip("Rotación local (Euler) de reposo: a la que vuelve el hueso cuando esta animación lo suelta. Es la misma que muestra el Inspector en el Transform del hueso.")]
        public Vector3 restEuler;

        public RestJoint(string jointName, Vector3 restEuler)
        {
            this.jointName = jointName;
            this.restEuler = restEuler;
        }
    }

    [Header("Rest Joints")]
    [Tooltip("Huesos de Joints que el Animator no devuelve solo a su pose (su rotación es una curva constante en todos los clips), con la rotación de reposo a la que hay que llevarlos a mano. Vuelven a ella al terminar el intervalo y cuando llega un golpe de lengua. Un hueso que no está en Joints se ignora con un warning.")]
    [SerializeField] private RestJoint[] _restJoints =
    {
        new RestJoint("joint10Part1_R", Vector3.zero),
    };

    [Header("Timing")]
    [Tooltip("Segundos que la animación espera, desde que arranca el intervalo entre mordiscos, antes de tomar los huesos. Es lo que tarda el Animator en terminar la animación del mordisco ('nibble' dura 0,79 s y a los 0,54 s ya empieza a soltarse): mientras tanto se ve el mordisco entero. 0 = la planta se cansa de inmediato y tapa el mordisco.")]
    [Min(0f)]
    [SerializeField] private float _startDelay = 0.55f;
    [Tooltip("Segundos que tardan los huesos en ir de su rotación por defecto a la pose de cansada. Si lo que queda de ventana (Bite Interval menos Start Delay) es más corto, se achica al 60% de esa ventana.")]
    [Min(0.01f)]
    [SerializeField] private float _settleTime = 0.35f;
    [Tooltip("Segundos de mezcla con la pose que dejó el Animator al tomar los huesos, para que no haya un salto (el Animator todavía está soltando el mordisco). 0 = sin mezcla.")]
    [Min(0f)]
    [SerializeField] private float _blendInTime = 0.15f;
    [Tooltip("Segundos que dura el gesto de 'no' completo (ida y vuelta entre Target Euler y Shake Euler). Menos = sacude la cabeza más rápido.")]
    [Min(0.05f)]
    [SerializeField] private float _shakeCycleTime = 0.4f;
    [Tooltip("Segundos que tardan los huesos en volver a la pose del Animator al terminar el intervalo (los de reposo, a su pose de reposo; también es lo que tardan en soltarse cuando llega un golpe). Los huesos que anima la preparación del mordisco los toma ella, así que esto solo se ve en el resto. 0 = corte directo.")]
    [Min(0f)]
    [SerializeField] private float _releaseTime = 0.3f;

    [Header("Curves")]
    [Tooltip("Avance del Settle (0 = rotación por defecto, 1 = pose de cansada) a lo largo del tiempo normalizado de la fase.")]
    [SerializeField] private AnimationCurve _settleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // The most of the window that the Settle can take: the rest is left for the Hold, where the 'no' gesture is seen.
    private const float SettleMaxPortion = 0.6f;

    private enum Phase { Idle, Wait, Active, Release }

    private sealed class Bone
    {
        public Transform transform;
        public JointPose pose;
        public Quaternion defaultRot;  // default rotation (the prefab's), read in Awake
        public Quaternion target;      // tired pose
        public Quaternion shakeTarget; // the other end of the 'no' gesture
        public Quaternion from;        // what the bone showed when the animation took it (for the start blend)
        public Quaternion applied;     // the last thing written to the bone
        public Quaternion releaseFrom; // where the bone was when the Release started
        public bool hasRest;           // it is a rest bone (see Rest Joints)
        public Quaternion rest;        // its rest pose
    }

    private Bone[] _bones = Array.Empty<Bone>();
    private PlantHurtReaction _hurt; // to release the rest bones when a hit arrives (optional)
    private Phase _phase;
    private float _elapsed; // time within the current phase
    private float _settle;  // duration of this time's Settle (see UpdateWait)
    private float _window;  // what is left of the interval once the Start Delay has passed
    private bool _restHeld; // a hit arrived in this interval: the rest bones are no longer taken (cleared in Play)
    private float _restMix; // 0 = rest bones in the tired pose, 1 = in their rest pose
    private bool _restPending; // it was turned off in the middle of the animation: when turned on, the rest bones must be returned to their pose

    // The component is active and found at least one bone. If not, the plant does nothing in the interval (as always).
    public bool IsAvailable => isActiveAndEnabled && _bones.Length > 0;

    // Waiting, in the tired pose or releasing the bones: the animation is still in progress.
    public bool IsPlaying => _phase != Phase.Idle;

    private void Awake()
    {
        var bones = new List<Bone>(_joints.Length);

        foreach(JointPose pose in _joints)
        {
            Transform joint = FindBone(transform, pose.jointName);

            if(joint == null)
            {
                Debug.LogWarning($"[PlantTiredAnimation] Bone '{pose.jointName}' not found inside '{name}': ignored.", this);
                continue;
            }

            // Awake runs before the Animator touches the bones for the first time: what they have now is the prefab's pose.
            bones.Add(new Bone
            {
                transform = joint,
                pose = pose,
                defaultRot = joint.localRotation,
                target = Quaternion.Euler(pose.targetEuler),
                shakeTarget = Quaternion.Euler(pose.shakeEuler)
            });
        }

        _bones = bones.ToArray();

        foreach(RestJoint rest in _restJoints)
        {
            Bone bone = Array.Find(_bones, b => b.pose.jointName == rest.jointName);

            if(bone == null)
            {
                Debug.LogWarning($"[PlantTiredAnimation] Rest bone '{rest.jointName}' is not among the Joints of '{name}': ignored.", this);
                continue;
            }

            bone.hasRest = true;
            bone.rest = Quaternion.Euler(rest.restEuler);
        }

        _hurt = GetComponent<PlantHurtReaction>();
    }

    private void OnEnable()
    {
        // It had been turned off in the middle of the animation (see OnDisable): the rest bones stayed in the tired pose and nobody
        // moves them, so they are given back here. The Animator restores the rest by itself.
        if(!_restPending) return;

        _restPending = false;

        foreach(Bone bone in _bones)
            if(bone.hasRest && bone.transform != null) bone.transform.localRotation = bone.rest;
    }

    private void OnDisable()
    {
        // Turned off in the middle of the animation: the Animator keeps the bones it animates. Nobody restores the rest ones, so
        // it is noted down to give them back when it is turned on again (OnEnable); while it is off they aren't touched, so the pause
        // leaves the whole plant frozen in the same pose.
        if(_phase == Phase.Active || _phase == Phase.Release) _restPending = true;

        _phase = Phase.Idle;
    }

    // Starts the animation. interval is how long the interval between bites lasts (the DataSO's Bite Interval): the Settle is adjusted
    // with it. There is no "ends by itself": it is cut with Stop when the interval ends. false if it can't be done now (turned off, no
    // bones); if it is already in progress it does nothing.
    public bool Play(float interval)
    {
        if(!IsAvailable) return false;
        if(_phase == Phase.Wait || _phase == Phase.Active) return true;

        _window = Mathf.Max(0f, interval - _startDelay);
        _restHeld = false;
        _restMix = 0f;
        _phase = Phase.Wait;
        _elapsed = 0f;
        return true;
    }

    // The interval ends: if it had already taken the bones, it releases them gradually (Release); if it was still waiting, there is nothing to release.
    public void Stop()
    {
        if(_phase == Phase.Wait)
        {
            _phase = Phase.Idle;
            return;
        }

        if(_phase != Phase.Active) return;

        foreach(Bone bone in _bones)
            bone.releaseFrom = bone.applied;

        _phase = Phase.Release;
        _elapsed = 0f;
    }

    private void LateUpdate()
    {
        if(_phase == Phase.Idle) return;

        // A tongue hit (PlantHurtReaction starts) releases the rest bones for what is left of the interval.
        // It is checked every frame, also during the Wait, so a hit that arrives before the tired pose takes the bones isn't missed.
        if(_hurt != null && _hurt.IsPlaying) _restHeld = true;

        _elapsed += Time.deltaTime;

        switch(_phase)
        {
            case Phase.Wait: UpdateWait(); break;
            case Phase.Active: UpdateActive(); break;
            default: UpdateRelease(); break;
        }
    }

    private void UpdateWait()
    {
        if(_elapsed < _startDelay) return;

        // Takes the bones: what they show now (the Animator's pose, which is already releasing the bite) is what it blends from.
        foreach(Bone bone in _bones)
            bone.from = bone.transform.localRotation;

        _settle = Mathf.Min(_settleTime, _window * SettleMaxPortion);

        // If a hit already arrived, the rest bones start released.
        _restMix = _restHeld ? 1f : 0f;

        // What went past the Start Delay in this frame already counts as animation time.
        _elapsed -= _startDelay;
        _phase = Phase.Active;
        UpdateActive();
    }

    private void UpdateActive()
    {
        float k = _settleCurve.Evaluate(Progress(_elapsed, _settle));

        // On start it blends with the pose the Animator left, so the first frame doesn't jump.
        float blend = _blendInTime > 0f ? Mathf.SmoothStep(0f, 1f, _elapsed / _blendInTime) : 1f;

        // 'No' gesture: 0 = Target Euler, 1 = Shake Euler. It starts at 0 when the Settle ends, so the head reaches its pose and
        // only then starts shaking, without jumps.
        float holdTime = Mathf.Max(0f, _elapsed - _settle);
        float shake = 0.5f - 0.5f * Mathf.Cos(holdTime / _shakeCycleTime * Mathf.PI * 2f);

        // The rest bones are released (towards their rest pose) when a hit arrives, over Release Time seconds, and they stay there.
        float restStep = _releaseTime > 0f ? Time.deltaTime / _releaseTime : 1f;
        _restMix = Mathf.MoveTowards(_restMix, _restHeld ? 1f : 0f, restStep);
        float restK = Mathf.SmoothStep(0f, 1f, _restMix);

        foreach(Bone bone in _bones)
        {
            Quaternion pose = bone.pose.shake && _elapsed > _settle
                ? Quaternion.Slerp(bone.target, bone.shakeTarget, shake)
                : Quaternion.SlerpUnclamped(bone.defaultRot, bone.target, k);

            if(bone.hasRest && restK > 0f) pose = Quaternion.Slerp(pose, bone.rest, restK);

            if(blend < 1f) pose = Quaternion.Slerp(bone.from, pose, blend);

            Apply(bone, pose);
        }
    }

    private void UpdateRelease()
    {
        float progress = Progress(_elapsed, _releaseTime);
        float k = Mathf.SmoothStep(0f, 1f, progress);

        // Towards the pose the bones have before this animation in this frame (the Animator's): on arrival there is no jump. The rest
        // bones have nobody moving them (see Rest Joints): they go to their rest pose and end exactly on it.
        foreach(Bone bone in _bones)
        {
            Quaternion destination = bone.hasRest ? bone.rest : bone.transform.localRotation;

            Apply(bone, Quaternion.Slerp(bone.releaseFrom, destination, k));
        }

        if(progress >= 1f) _phase = Phase.Idle;
    }

    private static float Progress(float elapsed, float duration) => duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;

    private static void Apply(Bone bone, Quaternion rotation)
    {
        bone.transform.localRotation = rotation;
        bone.applied = rotation;
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

    // To test the animation on its own from the Inspector (the component's three-dot menu), without fighting the plant: it stays
    // tired until it is released with "Release".
    [ContextMenu("Test tired (Play Mode)")]
    private void TestPlay()
    {
        if(Application.isPlaying) Play(3f);
    }

    [ContextMenu("Release (Play Mode)")]
    private void TestStop()
    {
        if(Application.isPlaying) Stop();
    }
}
