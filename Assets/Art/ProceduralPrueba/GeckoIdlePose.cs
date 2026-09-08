using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

/// <summary>
/// EL lugar unico para definir la POSE IDLE de las patas (y de cualquier otro hueso)
/// del gecko.
///
/// El modelo viene en pose T: las 4 patas estiradas de costado. El rig Two Bone IK,
/// con peso 1, decide la rotacion de muslo / rodilla / tobillo por su cuenta, asi que
/// escribir una rotacion a mano en esos huesos NO queda: el solver la pisa cada frame.
///
/// Este componente corre DESPUES del rig (LateUpdate, DefaultExecutionOrder alto) y
/// vuelve a poner las patas en la rotacion que definis aca, mezclando con el resultado
/// del IK segun <see cref="_poseWeight"/>.
///
/// Pose sprawl de lagarto (como en la referencia): el MUSLO queda casi en T (abierto al
/// costado) y la RODILLA es la que gira ~90 grados para que la pata baje al piso.
///  - _leftKneeAngle / _rightKneeAngle: el giro PRINCIPAL. Por defecto -90 (lado L) y
///    +90 (lado R) con _legAxis = (0,0,-1): la parte de abajo de la pata cae al piso.
///  - _leftThighAngle / _rightThighAngle: cuanto se abre / adelanta el muslo (0 = T-pose).
///  - _leftAnkleAngle / _rightAnkleAngle: orientacion del pie (0 = sigue a la rodilla).
///  - _poseWeight: 1 = manda esta pose. 0 = manda el IK (caminar). Si asignas _mover,
///    el peso se mueve solo (quieto -> pose idle, caminando -> IK).
///  - _extraBones: cualquier otro hueso (spine, cuello, cola, orejas...) con su Euler.
///
/// Control por codigo: LeftKneeAngle, RightKneeAngle, PoseWeight, SetKneeAngles(),
/// SetBoneEuler().
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(300)] // despues de GeckoSecondaryMotion (100) y del rig
public class GeckoIdlePose : MonoBehaviour
{
    [Serializable]
    public class BonePose
    {
        [Tooltip("Solo para ubicarte en la lista. No afecta nada.")]
        public string label;
        public Transform bone;
        [Tooltip("Rotacion LOCAL (Euler) del hueso en la pose idle. Es exactamente lo que " +
                 "veras en el campo Rotation del Transform de ese hueso.")]
        public Vector3 idleEuler;
    }

    [Header("RODILLA (knee) - el giro principal de la pose idle")]
    [Tooltip("Grados que gira la RODILLA de las patas del lado IZQUIERDO (bones *_L). " +
             "Con _legAxis = (0,0,-1), -90 hace que la parte de abajo de la pata caiga al piso.")]
    [SerializeField] private float _leftKneeAngle = -90f;
    [Tooltip("Grados que gira la RODILLA de las patas del lado DERECHO (bones *_R). " +
             "Con _legAxis = (0,0,-1), +90 hace que la parte de abajo de la pata caiga al piso.")]
    [SerializeField] private float _rightKneeAngle = 90f;

    [Header("MUSLO (thigh) - cuanto se abre la pata (0 = T-pose)")]
    [SerializeField] private float _leftThighAngle = 0f;
    [SerializeField] private float _rightThighAngle = 0f;

    [Header("TOBILLO (ankle) - orientacion del pie (0 = sigue a la rodilla)")]
    [SerializeField] private float _leftAnkleAngle = 0f;
    [SerializeField] private float _rightAnkleAngle = 0f;

    [Header("Eje")]
    [Tooltip("Eje LOCAL sobre el que se giran los huesos de la pata. (0,0,-1) es el que hace " +
             "que -90 en el lado L y +90 en el lado R bajen la pata. Cambia el signo si tu " +
             "convencion es al reves.")]
    [SerializeField] private Vector3 _legAxis = new Vector3(0f, 0f, -1f);

    [Header("Mezcla con el IK")]
    [Range(0f, 1f)]
    [Tooltip("1 = manda esta pose idle (patas fijas). 0 = manda el Two Bone IK (caminar).")]
    [SerializeField] private float _poseWeight = 1f;
    [Tooltip("Opcional. Si se asigna, _poseWeight se mueve solo: 1 cuando esta quieto, " +
             "0 cuando camina.")]
    [SerializeField] private GeckoMover _mover;
    [Tooltip("Que tan rapido mezcla entre pose idle e IK al empezar / frenar de caminar.")]
    [SerializeField] private float _blendSpeed = 8f;
    [Range(0f, 1f)]
    [Tooltip("Cuanto de la POSE se sigue aplicando MIENTRAS CAMINA (con _mover asignado). " +
             "0 = el IK maneja la pata solo (puede perder la forma sprawl). ~0.3 = la pata " +
             "conserva la forma de la pose idle (muslo abierto, rodilla doblada) y el IK solo " +
             "acomoda el pie al piso encima. El peso del IK queda en 1-esto. " +
             "EXPERIMENTAL: 0 = comportamiento normal (solo IK al caminar).")]
    [SerializeField] private float _walkPoseWeight = 0f;
    [Tooltip("Baja el peso de los 4 Two Bone IK Constraint junto con la pose, para que el " +
             "solver no pelee contra la rotacion que ponés aca.")]
    [SerializeField] private bool _alsoDriveIkWeight = true;

    [Header("Al CAMINAR (pose idle apagada)")]
    [Tooltip("Mientras camina, fuerza los 4 TOBILLOS al MISMO angulo (_left/rightAnkleAngle) que " +
             "usa la pose idle. Asi el pie tiene la misma orientacion parado y caminando y no " +
             "queda torcido por el IK.")]
    [SerializeField] private bool _lockAnkleWhileWalking = true;

    [Header("Otros huesos (reposo)")]
    [Tooltip("Cualquier hueso que quieras fijar en la pose idle: spine, cuello, cola, orejas... " +
             "Se aplica su rotacion local exacta, mezclada con _poseWeight igual que las patas.")]
    [SerializeField] private List<BonePose> _extraBones = new List<BonePose>();

    [Header("Editor")]
    [Tooltip("Mostrar la pose idle tambien en el editor, sin darle Play.")]
    [SerializeField] private bool _previewInEditor = true;

    // Huesos de pata resueltos por JERARQUIA (los nombres del FBX estan duplicados:
    // la rodilla delantera derecha se llama igual que la trasera izquierda).
    private Transform _thighFL, _kneeFL, _ankleFL;
    private Transform _thighFR, _kneeFR, _ankleFR;
    private Transform _thighBL, _kneeBL, _ankleBL;
    private Transform _thighBR, _kneeBR, _ankleBR;

    private TwoBoneIKConstraint[] _ik;
    private float _weight;
    private bool _ok;

    /// <summary> Giro de la rodilla, lado L. Editable desde gameplay. </summary>
    public float LeftKneeAngle { get => _leftKneeAngle; set => _leftKneeAngle = value; }
    /// <summary> Giro de la rodilla, lado R. Editable desde gameplay. </summary>
    public float RightKneeAngle { get => _rightKneeAngle; set => _rightKneeAngle = value; }
    /// <summary> 1 = pose idle fija, 0 = IK de caminar. Editable desde gameplay. </summary>
    public float PoseWeight { get => _poseWeight; set => _poseWeight = Mathf.Clamp01(value); }

    public void SetKneeAngles(float left, float right)
    {
        _leftKneeAngle = left;
        _rightKneeAngle = right;
    }

    /// <summary> Cambia la rotacion idle de un hueso de la lista _extraBones por su label. </summary>
    public void SetBoneEuler(string label, Vector3 euler)
    {
        foreach (var b in _extraBones)
            if (b != null && b.bone != null && b.label == label) { b.idleEuler = euler; return; }
    }

    private void OnEnable()
    {
        if (_extraBones == null) _extraBones = new List<BonePose>();
        try { Resolve(); }
        catch (Exception e) { Debug.LogException(e, this); _ok = false; }
        _weight = _poseWeight;
    }

    private void OnDisable()
    {
        // Devolvemos el control total al IK al apagar el componente.
        if (_ik != null)
            foreach (var c in _ik) if (c != null) c.weight = 1f;
    }

    private void OnValidate()
    {
        _poseWeight = Mathf.Clamp01(_poseWeight);
        if (_legAxis.sqrMagnitude < 0.0001f) _legAxis = new Vector3(0f, 0f, -1f);
        if (!Application.isPlaying) Resolve();
    }

    private void Resolve()
    {
        ResolveLeg("Gecko_Thigh_F_L", ref _thighFL, ref _kneeFL, ref _ankleFL);
        ResolveLeg("Gecko_Thigh_F_R", ref _thighFR, ref _kneeFR, ref _ankleFR);
        ResolveLeg("Gecko_Thigh_B_L", ref _thighBL, ref _kneeBL, ref _ankleBL);
        ResolveLeg("Gecko_Thigh_B_R", ref _thighBR, ref _kneeBR, ref _ankleBR);

        var rb = GetComponent<RigBuilder>();
        var list = new List<TwoBoneIKConstraint>();
        if (rb != null)
            foreach (var l in rb.layers)
                if (l.rig != null)
                    list.AddRange(l.rig.GetComponentsInChildren<TwoBoneIKConstraint>(true));
        _ik = list.ToArray();

        _ok = _thighFL != null && _thighFR != null && _thighBL != null && _thighBR != null;
    }

    private void ResolveLeg(string thighName, ref Transform thigh, ref Transform knee, ref Transform ankle)
    {
        thigh = FindBone(thighName);
        knee = ankle = null;
        if (thigh == null || thigh.childCount == 0) return;
        knee = thigh.GetChild(0);
        if (knee.childCount > 0) ankle = knee.GetChild(0);
    }

    private Transform FindBone(string boneName)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == boneName) return t;
        return null;
    }

    private void LateUpdate()
    {
        if (!_ok) return;

        // --- peso efectivo ---
        if (Application.isPlaying)
        {
            if (_mover != null)
            {
                float goal = _mover.IsMoving ? Mathf.Clamp01(_walkPoseWeight) : 1f;
                _weight = Mathf.MoveTowards(_weight, goal, _blendSpeed * Time.deltaTime);
                _weight = Mathf.Min(_weight, _poseWeight); // _poseWeight sigue siendo el tope general
            }
            else
            {
                _weight = _poseWeight;
            }
        }
        else
        {
            _weight = _previewInEditor ? _poseWeight : 0f;
        }

        // --- tobillos: mientras camina el IK ya no rota el tobillo (targetRotationWeight=0),
        //     asi que queda con la ultima rotacion rara. Si _lockAnkleWhileWalking esta activo
        //     los forzamos SIEMPRE al mismo angulo que en la pose idle, para que el pie tenga
        //     la misma orientacion parado y caminando y no se vea torcido. ---
        if (_lockAnkleWhileWalking && _weight < 0.999f)
        {
            Vector3 axis = _legAxis.sqrMagnitude > 0.0001f ? _legAxis.normalized : Vector3.back;
            SetLocal(_ankleFL, Quaternion.AngleAxis(_leftAnkleAngle, axis), 1f);
            SetLocal(_ankleBL, Quaternion.AngleAxis(_leftAnkleAngle, axis), 1f);
            SetLocal(_ankleFR, Quaternion.AngleAxis(_rightAnkleAngle, axis), 1f);
            SetLocal(_ankleBR, Quaternion.AngleAxis(_rightAnkleAngle, axis), 1f);
        }

        if (_weight <= 0.0001f)
        {
            if (_alsoDriveIkWeight && _ik != null)
                foreach (var c in _ik) if (c != null) c.weight = 1f;
            return;
        }

        // --- patas (pose idle) ---
        ApplyLeg(_thighFL, _kneeFL, _ankleFL, _leftThighAngle, _leftKneeAngle, _leftAnkleAngle);
        ApplyLeg(_thighBL, _kneeBL, _ankleBL, _leftThighAngle, _leftKneeAngle, _leftAnkleAngle);
        ApplyLeg(_thighFR, _kneeFR, _ankleFR, _rightThighAngle, _rightKneeAngle, _rightAnkleAngle);
        ApplyLeg(_thighBR, _kneeBR, _ankleBR, _rightThighAngle, _rightKneeAngle, _rightAnkleAngle);

        // --- otros huesos ---
        if (_extraBones != null)
            for (int i = 0; i < _extraBones.Count; i++)
            {
                var b = _extraBones[i];
                if (b == null || b.bone == null) continue;
                b.bone.localRotation = Quaternion.Slerp(b.bone.localRotation, Quaternion.Euler(b.idleEuler), _weight);
            }

        // --- peso del IK: se baja a la par de la pose para que no peleen ---
        if (_alsoDriveIkWeight && _ik != null)
            foreach (var c in _ik) if (c != null) c.weight = 1f - _weight;
    }

    private void ApplyLeg(Transform thigh, Transform knee, Transform ankle,
                          float thighAngle, float kneeAngle, float ankleAngle)
    {
        Vector3 axis = _legAxis.sqrMagnitude > 0.0001f ? _legAxis.normalized : Vector3.back;

        // Muslo, rodilla y tobillo se llevan a (identidad * su angulo). Es a proposito:
        // cuando _alsoDriveIkWeight lleva el peso del IK a 0, el solver deja de tocar estos
        // huesos y quedarian CLAVADOS en la ultima rotacion rara que calculo el IK.
        if (thigh != null)
            thigh.localRotation = Quaternion.Slerp(thigh.localRotation, Quaternion.AngleAxis(thighAngle, axis), _weight);

        if (knee != null)
            knee.localRotation = Quaternion.Slerp(knee.localRotation, Quaternion.AngleAxis(kneeAngle, axis), _weight);

        if (ankle != null)
            ankle.localRotation = Quaternion.Slerp(ankle.localRotation, Quaternion.AngleAxis(ankleAngle, axis), _weight);
    }

    private static void SetLocal(Transform bone, Quaternion target, float w)
    {
        if (bone != null)
            bone.localRotation = w >= 1f ? target : Quaternion.Slerp(bone.localRotation, target, w);
    }
}
