using UnityEngine;

/// <summary>
/// Cadena de huesos con inercia: el mismo resorte que usa la cola en
/// <see cref="GeckoSecondaryMotion"/>, pero empaquetado para poder colgarlo de
/// cualquier cadena (orejas, lengua, antenas) sin tocar ese script.
///
/// Diferencia con la cola: la cola se retrasa contra el giro del CUERPO, porque cuelga
/// del cuerpo. Las orejas cuelgan de la cabeza, así que acá el retraso se mide contra la
/// velocidad angular del hueso PADRE de la cadena. Así las orejas rebotan cuando el gecko
/// gira la cabeza, no solo cuando gira todo el bicho — que es lo que uno espera al verlas.
///
/// Corre en LateUpdate DESPUÉS del rig de Animation Rigging (mismo orden de ejecución que
/// GeckoSecondaryMotion). Guarda su propio estado y no lee de vuelta los huesos, así la
/// cadena no se rompe si algo más toca la jerarquía.
/// </summary>
[DefaultExecutionOrder(100)]
public class GeckoSpringChain : MonoBehaviour
{
    #region Inspector
    [Header("Cadena")]
    [Tooltip("Huesos de la cadena, de la base a la punta. Si queda vacío se autocompleta " +
             "siguiendo el primer hijo desde 'Raíz' hacia abajo.")]
    [SerializeField] private Transform[] _bones;

    [Tooltip("Hueso base de la cadena. Solo se usa para autocompletar si _bones está vacío.")]
    [SerializeField] private Transform _root;

    [Tooltip("Cuántos huesos tomar al autocompletar.")]
    [Min(1)]
    [SerializeField] private int _autoFillLength = 3;

    [Tooltip("Contra qué se mide la inercia. Vacío = el padre del primer hueso.")]
    [SerializeField] private Transform _driver;

    [Header("Resorte")]
    [Tooltip("Rigidez: más alto = la cadena vuelve más rápido a su pose de reposo.")]
    [SerializeField] private float _stiffness = 150f;
    [Tooltip("Amortiguación: más alto = menos rebote. Muy bajo y queda temblando.")]
    [SerializeField] private float _damping = 12f;

    [Header("Inercia")]
    [Tooltip("Grados que se retrasa la cadena por cada rad/s de giro del hueso padre.")]
    [SerializeField] private float _lagPerAngularSpeed = 9f;
    [Tooltip("El retraso se acumula hacia la punta: cada segmento multiplica por esto.")]
    [SerializeField] private float _lagFalloff = 1.35f;
    [Tooltip("Tope del retraso por segmento, en grados. Evita que se doble sobre sí misma.")]
    [SerializeField] private float _maxLagDeg = 35f;

    [Header("Rebote por aceleración")]
    [Tooltip("Grados de rebote por cada m/s2 de aceleración del padre. Da el sacudón al " +
             "arrancar, frenar y caer.")]
    [SerializeField] private float _bouncePerAccel = 0.6f;
    [SerializeField] private float _maxBounceDeg = 20f;

    [Header("Gravedad y reposo")]
    [Tooltip("Grados que cae cada segmento por su propio peso, acumulados hacia la punta.")]
    [SerializeField] private float _droopDeg = 1.5f;
    [Tooltip("Eje local sobre el que cae. Depende de cómo esté orientado el hueso.")]
    [SerializeField] private Vector3 _droopAxis = Vector3.right;

    [Header("Balanceo idle")]
    [SerializeField] private float _idleSwayDeg = 2.5f;
    [SerializeField] private float _idleSwayFreq = 0.7f;
    [Tooltip("Desfase del balanceo. Poner distinto en cada oreja para que no se muevan al unísono.")]
    [SerializeField] private float _idleSwayPhase;
    [SerializeField] private Vector3 _idleSwayAxis = Vector3.up;
    #endregion

    #region Estado
    private Quaternion[] _rest;
    private Quaternion[] _current;
    private Vector3[] _velocity;      // velocidad angular en rad/s, en local del padre

    private Quaternion _lastDriverRot;
    private Vector3 _lastDriverPos;
    private Vector3 _lastDriverVel;
    private bool _ready;
    #endregion

    private void Awake()
    {
        AutoFill();

        if (_bones == null || _bones.Length == 0)
        {
            enabled = false;
            return;
        }

        if (_driver == null && _bones[0] != null)
        {
            _driver = _bones[0].parent;
        }

        _rest = new Quaternion[_bones.Length];
        _current = new Quaternion[_bones.Length];
        _velocity = new Vector3[_bones.Length];

        for (int i = 0; i < _bones.Length; i++)
        {
            if (_bones[i] == null) continue;
            _rest[i] = _bones[i].localRotation;
            _current[i] = _bones[i].localRotation;
        }

        if (_driver != null)
        {
            _lastDriverRot = _driver.rotation;
            _lastDriverPos = _driver.position;
        }

        _ready = true;
    }

    private void LateUpdate()
    {
        if (!_ready || _driver == null) return;

        float dt = Time.deltaTime;
        if (dt <= 0.00001f) return;

        // --- Velocidad angular del padre, en mundo.
        Quaternion delta = _driver.rotation * Quaternion.Inverse(_lastDriverRot);
        _lastDriverRot = _driver.rotation;

        delta.ToAngleAxis(out float deltaAngle, out Vector3 deltaAxis);
        if (deltaAngle > 180f) deltaAngle -= 360f;
        if (float.IsNaN(deltaAxis.x) || deltaAxis.sqrMagnitude < 1e-8f)
        {
            deltaAxis = Vector3.up;
            deltaAngle = 0f;
        }

        Vector3 angularVelocity = deltaAxis.normalized * (deltaAngle * Mathf.Deg2Rad / dt);

        // --- Aceleración lineal del padre, en mundo.
        Vector3 driverVel = (_driver.position - _lastDriverPos) / dt;
        _lastDriverPos = _driver.position;
        Vector3 accel = (driverVel - _lastDriverVel) / dt;
        _lastDriverVel = driverVel;

        float sway = Mathf.Sin((Time.time + _idleSwayPhase) * _idleSwayFreq * Mathf.PI * 2f) * _idleSwayDeg;

        for (int i = 0; i < _bones.Length; i++)
        {
            Transform bone = _bones[i];
            if (bone == null || bone.parent == null) continue;

            float falloff = Mathf.Pow(_lagFalloff, i);
            float segment = i + 1f;

            // El retraso y el rebote se calculan en MUNDO y se pasan al espacio local del
            // padre de este hueso. El padre ya fue actualizado en esta misma pasada, así
            // que la cadena se propaga sola de la base a la punta.
            Quaternion toLocal = Quaternion.Inverse(bone.parent.rotation);

            Quaternion lagRot = Quaternion.identity;
            float angularMag = angularVelocity.magnitude;
            if (angularMag > 1e-4f)
            {
                float lagDeg = Mathf.Clamp(-angularMag * _lagPerAngularSpeed * falloff, -_maxLagDeg, _maxLagDeg);
                Vector3 axisLocal = toLocal * angularVelocity.normalized;
                lagRot = Quaternion.AngleAxis(lagDeg, axisLocal);
            }

            Quaternion bounceRot = Quaternion.identity;
            float accelMag = accel.magnitude;
            if (accelMag > 1e-3f && _bouncePerAccel > 0f)
            {
                float bounceDeg = Mathf.Clamp(-accelMag * _bouncePerAccel * falloff, -_maxBounceDeg, _maxBounceDeg);
                // El rebote gira alrededor del eje perpendicular a la aceleración y al
                // hueso: es lo que hace que la oreja se vaya para atrás al acelerar.
                Vector3 boneDirWorld = bone.parent.rotation * Vector3.up;
                Vector3 axisWorld = Vector3.Cross(accel.normalized, boneDirWorld);
                if (axisWorld.sqrMagnitude > 1e-6f)
                {
                    bounceRot = Quaternion.AngleAxis(bounceDeg, toLocal * axisWorld.normalized);
                }
            }

            Quaternion droopRot = Quaternion.AngleAxis(_droopDeg * segment, _droopAxis.normalized);
            Quaternion swayRot = Quaternion.AngleAxis(sway * (0.5f + 0.2f * segment), _idleSwayAxis.normalized);

            Quaternion target = _rest[i] * lagRot * bounceRot * droopRot * swayRot;

            IntegrateSpring(i, target, dt);
            bone.localRotation = _current[i];
        }
    }

    /// <summary>
    /// Resorte angular con integración semi-implícita. Es la misma matemática que usa la
    /// cola: se calcula el giro que falta hasta el objetivo, se acelera hacia él y se
    /// frena con la amortiguación.
    /// </summary>
    private void IntegrateSpring(int index, Quaternion target, float dt)
    {
        Quaternion diff = target * Quaternion.Inverse(_current[index]);
        diff.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;

        if (float.IsNaN(axis.x) || float.IsInfinity(axis.x) || axis.sqrMagnitude < 1e-8f)
        {
            _current[index] = target;
            _velocity[index] = Vector3.zero;
            return;
        }

        Vector3 toTarget = axis.normalized * (angle * Mathf.Deg2Rad);
        Vector3 acceleration = toTarget * _stiffness - _velocity[index] * _damping;
        _velocity[index] += acceleration * dt;

        Vector3 step = _velocity[index] * dt * Mathf.Rad2Deg;
        if (step.sqrMagnitude > 1e-10f)
        {
            _current[index] = Quaternion.AngleAxis(step.magnitude, step.normalized) * _current[index];
        }
    }

    private void AutoFill()
    {
        if (_bones != null && _bones.Length > 0) return;
        if (_root == null) return;

        var list = new System.Collections.Generic.List<Transform>();
        Transform cursor = _root;
        for (int i = 0; i < _autoFillLength && cursor != null; i++)
        {
            list.Add(cursor);
            cursor = cursor.childCount > 0 ? cursor.GetChild(0) : null;
        }

        _bones = list.ToArray();
    }

    private void OnDisable()
    {
        // Volver a la pose de reposo para no dejar el bicho torcido en el editor.
        if (!_ready || _bones == null) return;
        for (int i = 0; i < _bones.Length; i++)
        {
            if (_bones[i] != null) _bones[i].localRotation = _rest[i];
        }
    }

    /// <summary>Configuración desde código, para el armado automático del rig.</summary>
    public void Configure(Transform root, int length, float swayPhase, Vector3 droopAxis, Vector3 swayAxis)
    {
        _root = root;
        _autoFillLength = length;
        _idleSwayPhase = swayPhase;
        _droopAxis = droopAxis;
        _idleSwayAxis = swayAxis;
        _bones = null;
    }
}
