using System;
using System.Collections.Generic;
using UnityEngine;

// Animación por código de la "planta cansada": la pose que toma el boss en el intervalo entre mordiscos (Bite Interval del
// DataSO, CarnivorousPlantDataSO.biteInterval), que es la ventana en la que Gekko puede dañarla (ver CarnivorousPlant.CanBeHit).
// Es la señal visual de que ahora está vulnerable. Mismo estilo que PlantBiteWindup y PlantHurtReaction: va en el mismo
// GameObject que CarnivorousPlant, aplica la pose de los huesos en LateUpdate (el Animator los reescribe en cada frame) y sus
// tiempos se ajustan en el Inspector.
//
// Arranca cuando CarnivorousPlant entra al intervalo (SetInBiteInterval(true), justo después de cada mordisco) y se corta cuando
// el intervalo termina (SetInBiteInterval(false): empieza la preparación del próximo mordisco, el player se fue del rango, la
// planta se purificó...). Fases:
//  1. Wait: durante Start Delay segundos los huesos siguen siendo del Animator, que está con la animación del mordisco ('nibble',
//     unos 0,8 s). Así el mordisco se ve entero y la planta recién se cansa cuando terminó de morder.
//  2. Settle: los huesos pasan de su rotación por defecto (la que tienen en el prefab, que se lee al arrancar la escena) a la pose
//     de cansada (Target Euler). Los primeros Blend In Time segundos se mezclan con la pose que dejó el Animator, para que no haya
//     un salto al tomar los huesos.
//  3. Hold: la planta se queda en esa pose hasta que termina el intervalo. Los huesos con Shake (hoy, joint7_R, la cabeza) oscilan
//     entre Target Euler y Shake Euler: el gesto de "no" con la cabeza.
//  4. Release: al terminar el intervalo los huesos vuelven a la pose del Animator durante Release Time segundos. Si lo que sigue
//     es la preparación del mordisco (PlantBiteWindup), ella arranca desde la pose de cansada y toma sola los huesos que anima;
//     este Release suaviza el resto, los que la preparación no toca. Los huesos de reposo (ver abajo) no vuelven a la pose del
//     Animator sino a la suya.
//
// Huesos de reposo (Rest Joints): hoy, joint10Part1_R. Su rotación es una curva constante (identidad) en los tres clips del
// Animator (Idle, nibble y spittle): no hay nada que la mueva, así que "volver a la pose del Animator" no alcanza para devolverlo y
// se quedaba clavado en la pose de cansada. Por eso tiene una pose de reposo propia ((0,0,0) en joint10Part1_R, la de fábrica) y
// esta animación lo lleva ahí, hasta quedar exactamente en ella, cuando:
//  - termina el intervalo (el Release), que es también cuando arranca la preparación del mordisco (PlantBiteWindup);
//  - llega un golpe de lengua (arranca PlantHurtReaction): lo suelta en Release Time segundos y ya no vuelve a tomarlo por lo que
//    queda de ese intervalo (el próximo intervalo lo vuelve a bajar);
//  - el componente se apaga en plena animación (la pausa apaga todos los de la escena): se lo repone al volver a prenderse.
//
// No tiene una duración fija: dura lo que dure el intervalo, también si algo lo estira (la reacción a un golpe pausa el ataque y,
// con él, el intervalo). Del intervalo sale solo el tiempo del Settle, que se achica si lo que queda de ventana es corto.
//
// Convive con las otras dos animaciones: PlantBiteWindup (Execution Order 100) y PlantHurtReaction (110) se aplican después de esta
// y leen la pose de cansada como la que tienen los huesos en ese momento: la preparación arranca desde ella y la reacción a un
// golpe también, y al terminar vuelve a ella porque esta animación sigue corriendo por debajo.
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

    // Un hueso de reposo: ver "Huesos de reposo" arriba.
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

    // Qué parte de la ventana, como máximo, se puede llevar el Settle: el resto queda para el Hold, donde se ve el gesto de 'no'.
    private const float SettleMaxPortion = 0.6f;

    private enum Phase { Idle, Wait, Active, Release }

    private sealed class Bone
    {
        public Transform transform;
        public JointPose pose;
        public Quaternion defaultRot;  // rotación por defecto (la del prefab), leída en Awake
        public Quaternion target;      // pose de cansada
        public Quaternion shakeTarget; // la otra punta del gesto de 'no'
        public Quaternion from;        // lo que mostraba el hueso cuando la animación lo tomó (para la mezcla del arranque)
        public Quaternion applied;     // lo último que se escribió en el hueso
        public Quaternion releaseFrom; // dónde estaba el hueso cuando arrancó el Release
        public bool hasRest;           // es un hueso de reposo (ver Rest Joints)
        public Quaternion rest;        // su pose de reposo
    }

    private Bone[] _bones = Array.Empty<Bone>();
    private PlantHurtReaction _hurt; // para soltar los huesos de reposo cuando llega un golpe (opcional)
    private Phase _phase;
    private float _elapsed; // tiempo dentro de la fase actual
    private float _settle;  // duración del Settle de esta vez (ver UpdateWait)
    private float _window;  // lo que queda del intervalo una vez pasado el Start Delay
    private bool _restHeld; // llegó un golpe en este intervalo: los huesos de reposo ya no se toman (se limpia en Play)
    private float _restMix; // 0 = los huesos de reposo en la pose de cansada, 1 = en su pose de reposo
    private bool _restPending; // se apagó en plena animación: al prenderse hay que devolver los huesos de reposo a su pose

    // El componente está activo y encontró al menos un hueso. Si no, la planta no hace nada en el intervalo (como siempre).
    public bool IsAvailable => isActiveAndEnabled && _bones.Length > 0;

    // Esperando, en la pose de cansada o soltando los huesos: la animación todavía está en curso.
    public bool IsPlaying => _phase != Phase.Idle;

    private void Awake()
    {
        var bones = new List<Bone>(_joints.Length);

        foreach(JointPose pose in _joints)
        {
            Transform joint = FindBone(transform, pose.jointName);

            if(joint == null)
            {
                Debug.LogWarning($"[PlantTiredAnimation] No encontré el hueso '{pose.jointName}' dentro de '{name}': se ignora.", this);
                continue;
            }

            // Awake corre antes de que el Animator toque los huesos por primera vez: lo que tienen ahora es la pose del prefab.
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
                Debug.LogWarning($"[PlantTiredAnimation] El hueso de reposo '{rest.jointName}' no está entre los Joints de '{name}': se ignora.", this);
                continue;
            }

            bone.hasRest = true;
            bone.rest = Quaternion.Euler(rest.restEuler);
        }

        _hurt = GetComponent<PlantHurtReaction>();
    }

    private void OnEnable()
    {
        // Se había apagado en plena animación (ver OnDisable): los huesos de reposo se quedaron en la pose de cansada y nadie los
        // mueve, así que se los devuelve acá. El resto lo repone el Animator solo.
        if(!_restPending) return;

        _restPending = false;

        foreach(Bone bone in _bones)
            if(bone.hasRest && bone.transform != null) bone.transform.localRotation = bone.rest;
    }

    private void OnDisable()
    {
        // Apagado en medio de la animación: el Animator se queda con los huesos que anima. Los de reposo no los repone nadie, así
        // que se anota para devolverlos cuando se vuelva a prender (OnEnable); mientras está apagado no se los toca, así la pausa
        // deja a toda la planta congelada en la misma pose.
        if(_phase == Phase.Active || _phase == Phase.Release) _restPending = true;

        _phase = Phase.Idle;
    }

    // Arranca la animación. interval es lo que dura el intervalo entre mordiscos (Bite Interval del DataSO): con él se ajusta el
    // Settle. No hay un "termina sola": se corta con Stop cuando el intervalo termina. false si no se puede ahora (apagado, sin
    // huesos); si ya está en curso no hace nada.
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

    // Termina el intervalo: si ya había tomado los huesos, los suelta de a poco (Release); si todavía esperaba, no hay nada que soltar.
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

        // Un golpe de lengua (arranca la reacción de PlantHurtReaction) suelta los huesos de reposo por lo que queda del intervalo.
        // Se mira en cada frame, también durante el Wait, así no se pierde un golpe que llega antes de que la cansada tome los huesos.
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

        // Toma los huesos: lo que muestran ahora (la pose del Animator, que ya está soltando el mordisco) es de donde se mezcla.
        foreach(Bone bone in _bones)
            bone.from = bone.transform.localRotation;

        _settle = Mathf.Min(_settleTime, _window * SettleMaxPortion);

        // Si ya llegó un golpe, los huesos de reposo arrancan sueltos.
        _restMix = _restHeld ? 1f : 0f;

        // Lo que se pasó del Start Delay en este frame ya cuenta como tiempo de animación.
        _elapsed -= _startDelay;
        _phase = Phase.Active;
        UpdateActive();
    }

    private void UpdateActive()
    {
        float k = _settleCurve.Evaluate(Progress(_elapsed, _settle));

        // Al arrancar se mezcla con la pose que dejó el Animator, para que el primer frame no pegue un salto.
        float blend = _blendInTime > 0f ? Mathf.SmoothStep(0f, 1f, _elapsed / _blendInTime) : 1f;

        // Gesto de 'no': 0 = Target Euler, 1 = Shake Euler. Arranca en 0 cuando termina el Settle, así la cabeza llega a su pose y
        // recién ahí empieza a sacudirse, sin saltos.
        float holdTime = Mathf.Max(0f, _elapsed - _settle);
        float shake = 0.5f - 0.5f * Mathf.Cos(holdTime / _shakeCycleTime * Mathf.PI * 2f);

        // Los huesos de reposo se sueltan (hacia su pose de reposo) cuando llega un golpe, en Release Time segundos, y ahí se quedan.
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

        // Hacia la pose que tengan los huesos antes de esta animación en este frame (la del Animator): al llegar no hay salto. Los
        // huesos de reposo no tienen quién los mueva (ver Rest Joints): van a su pose de reposo y terminan exactamente en ella.
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

    // Para probar la animación suelta desde el Inspector (menú de tres puntos del componente), sin pelear con la planta: queda
    // cansada hasta que se la suelta con "Soltar".
    [ContextMenu("Probar cansada (Play Mode)")]
    private void TestPlay()
    {
        if(Application.isPlaying) Play(3f);
    }

    [ContextMenu("Soltar (Play Mode)")]
    private void TestStop()
    {
        if(Application.isPlaying) Stop();
    }
}
