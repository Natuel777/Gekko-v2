using System;
using System.Collections.Generic;
using UnityEngine;

// Animación por código de la "preparación" del ataque cuerpo a cuerpo de la planta (el mordisco).
//
// Tiene dos fases, y la duración de cada una se configura en el Inspector:
//  1. Windup: los huesos pasan de su rotación base (la de fábrica) a la rotación objetivo, de forma progresiva.
//  2. Return: vuelven rápido a la rotación base.
// Cuando termina el Return la preparación está completa (Completed) y recién ahí PlantBiteState dispara el mordisco de
// siempre (CarnivorousPlant.Bite, con su animación 'nibble'). No reemplaza a esa animación: es el paso previo.
//
// Las rotaciones son LOCALES, las mismas que muestra el Inspector en el Transform de cada hueso. En joint7_R el eje Z no sigue
// el camino base -> objetivo: durante el Windup barre de Z From a Z To (ver JointPose.sweepZ).
//
// El Animator reescribe estos huesos en cada frame (Idle, nibble y spittle tienen curvas de todos), así que la pose se aplica
// en LateUpdate, después de él. El Execution Order la deja además después del LateUpdate de CarnivorousPlant, que es donde se
// aplica el aim de joint7_R: mientras corre la preparación gana esta pose.
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public class PlantBiteWindup : MonoBehaviour
{
    [Serializable]
    private struct JointPose
    {
        [Tooltip("Nombre del hueso. Se busca entre los hijos de la planta al arrancar.")]
        public string jointName;
        [Tooltip("Rotación local (Euler) de fábrica: de acá arranca el Windup y a esta vuelve el Return.")]
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
    [Tooltip("Huesos que anima la preparación, con su rotación base (la de fábrica) y la objetivo. Un hueso que no se encuentra por nombre se ignora con un warning.")]
    [SerializeField] private JointPose[] _joints =
    {
        new JointPose("joint6_R",      new Vector3(0f, 180f, 312.531738f),                new Vector3(28.5997677f, 210.009979f, 320.348785f)),
        new JointPose("joint4_R",      new Vector3(313.025055f, 90f, 336.135864f),        new Vector3(313.025055f, 90.0000305f, 304.036926f)),
        new JointPose("joint6Part1_R", new Vector3(0f, 0f, 359.506653f),                  new Vector3(313.521484f, 330.644043f, 21.8529816f)),
        new JointPose("joint7_R",      new Vector3(356.112091f, 184.17572f, 312.88327f),  new Vector3(10.3371201f, 168.730225f, 312.00238f), -3.811f, -84.533f),
        new JointPose("joint19_R",     new Vector3(313.025055f, 90f, 206.72084f),         new Vector3(313.025085f, 90f, 146.8815f)),
        new JointPose("joint21_R",     new Vector3(0f, 180f, 313.276215f),                new Vector3(2.2570734f, 138.473328f, 329.567627f)),
    };

    [Header("Timing")]
    [Tooltip("Segundos que tardan los huesos en ir de la rotación base a la objetivo.")]
    [Min(0f)]
    [SerializeField] private float _windupDuration = 3f;
    [Tooltip("Segundos que tardan en volver de la objetivo a la base. Cuando termina, sale el mordisco.")]
    [Min(0f)]
    [SerializeField] private float _returnDuration = 0.5f;
    [Tooltip("Segundos de mezcla con la pose del Animator al arrancar, para que no haya un salto (sobre todo en el Z de joint7_R). 0 = sin mezcla.")]
    [Min(0f)]
    [SerializeField] private float _blendInTime = 0.15f;

    [Header("Curves")]
    [Tooltip("Avance del Windup (0 = base, 1 = objetivo) a lo largo del tiempo normalizado de la fase.")]
    [SerializeField] private AnimationCurve _windupCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Avance del Return (0 = donde estaba, 1 = base) a lo largo del tiempo normalizado de la fase.")]
    [SerializeField] private AnimationCurve _returnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private enum Phase { Idle, Windup, Return }

    private sealed class Bone
    {
        public Transform transform;
        public JointPose pose;
        public Quaternion baseRot;
        public Quaternion applied;    // lo último que se escribió en el hueso
        public Quaternion returnFrom; // dónde estaba el hueso cuando arrancó el Return
    }

    private Bone[] _bones = Array.Empty<Bone>();
    private Phase _phase;
    private float _elapsed;
    private bool _cancelled;

    // El componente está activo y encontró al menos un hueso. Si no, PlantBiteState muerde de una, como siempre.
    public bool IsAvailable => isActiveAndEnabled && _bones.Length > 0;

    // Windup o Return en curso: los huesos son de esta animación (el aim de joint7_R espera).
    public bool IsPlaying => _phase != Phase.Idle;

    // La última preparación llegó hasta el final (no la cortó un Cancel). Se apaga al arrancar otra.
    public bool Completed { get; private set; }

    private void Awake()
    {
        var bones = new List<Bone>(_joints.Length);

        foreach(JointPose pose in _joints)
        {
            Transform joint = FindBone(transform, pose.jointName);

            if(joint == null)
            {
                Debug.LogWarning($"[PlantBiteWindup] No encontré el hueso '{pose.jointName}' dentro de '{name}': se ignora.", this);
                continue;
            }

            bones.Add(new Bone { transform = joint, pose = pose, baseRot = Quaternion.Euler(pose.baseEuler) });
        }

        _bones = bones.ToArray();
    }

    private void OnDisable()
    {
        // Apagado en medio de la preparación: el Animator se queda con los huesos y no cuenta como completa.
        _phase = Phase.Idle;
        Completed = false;
    }

    // Arranca la preparación desde la pose base. false si no se puede ahora: apagado, sin huesos, o todavía volviendo de
    // una preparación cortada (quien llama reintenta en el próximo frame).
    public bool Play()
    {
        if(!IsAvailable || _phase != Phase.Idle) return false;

        _phase = Phase.Windup;
        _elapsed = 0f;
        _cancelled = false;
        Completed = false;
        return true;
    }

    // Corta el Windup: los huesos vuelven a la base con el mismo Return rápido, desde donde estén. No cuenta como completa.
    public void Cancel()
    {
        if(_phase != Phase.Windup) return;

        _cancelled = true;
        BeginReturn();
    }

    private void LateUpdate()
    {
        if(_phase == Phase.Idle) return;

        _elapsed += Time.deltaTime;

        if(_phase == Phase.Windup) UpdateWindup();
        else UpdateReturn();
    }

    private void UpdateWindup()
    {
        float progress = Progress(_windupDuration);
        float k = _windupCurve.Evaluate(progress);

        // Al arrancar se mezcla con la pose que acaba de dejar el Animator, para que el primer frame no pegue un salto.
        float blend = _blendInTime > 0f ? Mathf.SmoothStep(0f, 1f, _elapsed / _blendInTime) : 1f;

        foreach(Bone bone in _bones)
        {
            Quaternion pose = WindupPose(bone.pose, k);
            if(blend < 1f) pose = Quaternion.Slerp(bone.transform.localRotation, pose, blend);

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

    // El Return arranca desde lo último que se escribió (el final del Windup, o donde lo cortó el Cancel), así no hay saltos.
    private void BeginReturn()
    {
        foreach(Bone bone in _bones)
            bone.returnFrom = bone.applied;

        _phase = Phase.Return;
        _elapsed = 0f;
    }

    private void Finish()
    {
        // Termina exactamente en la rotación base, aunque la curva no llegue a 1.
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

    private static Quaternion WindupPose(JointPose pose, float k)
    {
        float x = LerpAngle(pose.baseEuler.x, pose.targetEuler.x, k);
        float y = LerpAngle(pose.baseEuler.y, pose.targetEuler.y, k);
        float z = pose.sweepZ ? Mathf.LerpUnclamped(pose.zFrom, pose.zTo, k) : LerpAngle(pose.baseEuler.z, pose.targetEuler.z, k);

        return Quaternion.Euler(x, y, z);
    }

    // Como Mathf.LerpAngle (por el camino más corto) pero sin limitar k a 0..1: una curva con rebote se ve como está dibujada.
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

    // Para probar la animación suelta desde el Inspector (menú de tres puntos del componente), sin acercar al player.
    [ContextMenu("Probar preparación (Play Mode)")]
    private void TestPlay()
    {
        if(Application.isPlaying) Play();
    }
}
