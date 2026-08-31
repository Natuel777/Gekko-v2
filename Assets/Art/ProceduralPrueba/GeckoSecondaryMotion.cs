using UnityEngine;

/// <summary>
/// Movimiento secundario procedural del Gecko, TODO por código (sin clips de animación):
///
///  - Cola con inercia: cadena de resortes, cada segmento arrastra detrás del anterior
///    y hace "latigazo" al girar; balanceo sutil al estar quieto y caída por gravedad.
///  - Columna que se curva hacia adentro del giro (serpenteo) y se endereza al soltar.
///  - Bob vertical + roll lateral al caminar; respiración leve al estar quieto.
///  - Cuello/cabeza que mira hacia donde se mueve (o a un target) con límites de ángulo.
///
/// Corre en LateUpdate, DESPUÉS del rig de Animation Rigging (que solo toca las patas).
/// Guarda su propio estado y NO lee de vuelta los huesos, así la cadena de la cola no se
/// rompe aunque otro sistema toque la jerarquía.
/// </summary>
[DefaultExecutionOrder(100)]
public class GeckoSecondaryMotion : MonoBehaviour
{
    #region Inspector
    [Header("Referencias (se autocompletan por nombre si quedan vacías)")]
    [SerializeField] private Transform _hips;
    [SerializeField] private Transform _spine1;
    [SerializeField] private Transform _spine2;
    [SerializeField] private Transform _neck;
    [SerializeField] private Transform[] _tail;              // Gecko_Tail1 .. Tail4
    [SerializeField] private Rigidbody _bodyRb;              // opcional, solo para robustez

    [Header("Cola — inercia / latigazo")]
    [SerializeField] private bool _tailEnabled = true;
    [Tooltip("Rigidez del resorte de cada segmento (más alto = la cola sigue más rápido al cuerpo).")]
    [SerializeField] private float _tailStiffness = 120f;
    [Tooltip("Amortiguación del resorte (más alto = menos rebote / coletazo).")]
    [SerializeField] private float _tailDamping = 14f;
    [Tooltip("Cuánto se retrasa la cola contra el giro del cuerpo (grados de latigazo por rad/s).")]
    [SerializeField] private float _tailWhip = 22f;
    [Tooltip("El latigazo se acumula un poco más en cada segmento hacia la punta.")]
    [SerializeField] private float _tailWhipFalloff = 1.25f;
    [Tooltip("Balanceo idle de la cola: amplitud (grados) y frecuencia (Hz).")]
    [SerializeField] private float _tailIdleSwayDeg = 5f;
    [SerializeField] private float _tailIdleSwayFreq = 0.8f;
    [Tooltip("Caída de la cola por gravedad: grados hacia abajo, acumulados por segmento.")]
    [SerializeField] private float _tailDroopDeg = 2.5f;

    [Header("Columna — curva en los giros")]
    [SerializeField] private bool _spineEnabled = true;
    [Tooltip("Grados que se curva la columna por cada rad/s de giro del cuerpo.")]
    [SerializeField] private float _spineBendPerTurn = 7f;
    [SerializeField] private float _spineMaxBendDeg = 16f;
    [Tooltip("Qué tan rápido la columna llega a la curva objetivo y vuelve a la recta.")]
    [SerializeField] private float _spineResponse = 9f;

    [Header("Bob / respiración")]
    [SerializeField] private bool _bobEnabled = true;
    [Tooltip("Altura del bob vertical al caminar (metros).")]
    [SerializeField] private float _bobHeight = 0.006f;
    [Tooltip("Ciclos de bob por metro recorrido.")]
    [SerializeField] private float _bobCyclesPerMeter = 2.2f;
    [Tooltip("Roll lateral del cuerpo al caminar (grados).")]
    [SerializeField] private float _bobRollDeg = 3.5f;
    [Tooltip("Velocidad (m/s) a la que el bob llega a su amplitud máxima.")]
    [SerializeField] private float _bobFullSpeed = 0.6f;
    [Tooltip("Respiración al estar quieto: amplitud (grados de pitch del pecho) y frecuencia (Hz).")]
    [SerializeField] private float _breathDeg = 1.4f;
    [SerializeField] private float _breathFreq = 0.45f;

    [Header("Cabeza / cuello")]
    [SerializeField] private bool _headEnabled = true;
    [Tooltip("A qué mirar. Vacío = mira hacia donde se mueve (y al frente si está quieto).")]
    [SerializeField] private Transform _lookTarget;
    [SerializeField] private float _headMaxYawDeg = 45f;
    [SerializeField] private float _headMaxPitchDeg = 22f;
    [SerializeField] private float _headResponse = 6f;
    #endregion

    #region Estado
    private Quaternion _hipsRest, _spine1Rest, _spine2Rest, _neckRest;
    private Vector3 _hipsRestPos;
    private Quaternion[] _tailRest;
    private Quaternion[] _tailCur;     // rotación LOCAL actual de cada segmento
    private Vector3[] _tailVel;        // velocidad angular (grados/s, en local del padre)

    private Vector3 _lastPos;
    private float _lastYaw;
    private float _bobPhase;
    private float _spineBend;
    private float _headYaw, _headPitch;
    private bool _ready;
    #endregion

    private void Awake()
    {
        AutoFill();

        if (_hips)   { _hipsRest = _hips.localRotation; _hipsRestPos = _hips.localPosition; }
        if (_spine1) _spine1Rest = _spine1.localRotation;
        if (_spine2) _spine2Rest = _spine2.localRotation;
        if (_neck)   _neckRest   = _neck.localRotation;

        if (_tail != null && _tail.Length > 0)
        {
            _tailRest = new Quaternion[_tail.Length];
            _tailCur  = new Quaternion[_tail.Length];
            _tailVel  = new Vector3[_tail.Length];
            for (int i = 0; i < _tail.Length; i++)
            {
                if (_tail[i] == null) continue;
                _tailRest[i] = _tail[i].localRotation;
                _tailCur[i]  = _tail[i].localRotation;
            }
        }

        _lastPos = transform.position;
        _lastYaw = transform.eulerAngles.y;
        _ready = true;
    }

    private void LateUpdate()
    {
        if (!_ready) return;
        float dt = Time.deltaTime;
        if (dt <= 0.00001f) return;

        Vector3 vel = (transform.position - _lastPos) / dt;
        _lastPos = transform.position;
        float planarSpeed = Vector3.ProjectOnPlane(vel, transform.up).magnitude;

        float yaw = transform.eulerAngles.y;
        float yawRate = Mathf.DeltaAngle(_lastYaw, yaw) * Mathf.Deg2Rad / dt; // rad/s
        _lastYaw = yaw;

        if (_spineEnabled) UpdateSpine(dt, yawRate);
        if (_bobEnabled)   UpdateBob(dt, planarSpeed);
        if (_headEnabled)  UpdateHead(dt, vel, planarSpeed);
        if (_tailEnabled)  UpdateTail(dt, yawRate);
    }

    // -------------------------------------------------------------------------
    private void UpdateSpine(float dt, float yawRate)
    {
        float target = Mathf.Clamp(-yawRate * _spineBendPerTurn, -_spineMaxBendDeg, _spineMaxBendDeg);
        _spineBend = Mathf.Lerp(_spineBend, target, 1f - Mathf.Exp(-_spineResponse * dt));

        Quaternion bend = Quaternion.AngleAxis(_spineBend, Vector3.up);
        if (_spine1) _spine1.localRotation = _spine1Rest * bend;
        if (_spine2) _spine2.localRotation = _spine2Rest * bend;
    }

    private void UpdateBob(float dt, float speed)
    {
        float amt = Mathf.Clamp01(speed / Mathf.Max(_bobFullSpeed, 0.01f));

        // distancia recorrida -> fase (así el bob va "al paso" y no por reloj)
        _bobPhase += speed * _bobCyclesPerMeter * Mathf.PI * 2f * dt;

        float bobY  = Mathf.Sin(_bobPhase) * _bobHeight * amt;
        float roll  = Mathf.Sin(_bobPhase * 0.5f) * _bobRollDeg * amt;
        float breath = Mathf.Sin(Time.time * _breathFreq * Mathf.PI * 2f) * _breathDeg * (1f - amt);

        if (_hips)
        {
            _hips.localPosition = _hipsRestPos + Vector3.up * bobY;
            _hips.localRotation = _hipsRest * Quaternion.Euler(0f, 0f, roll);
        }
        if (_spine1 && Mathf.Abs(breath) > 0.0001f)
            _spine1.localRotation = _spine1.localRotation * Quaternion.Euler(breath, 0f, 0f);
    }

    private void UpdateHead(float dt, Vector3 vel, float speed)
    {
        if (_neck == null) return;

        Vector3 aimDir;
        if (_lookTarget != null)
            aimDir = _lookTarget.position - _neck.position;
        else if (speed > 0.05f)
            aimDir = vel;
        else
            aimDir = transform.forward;

        aimDir = aimDir.normalized;
        if (aimDir.sqrMagnitude < 0.001f) aimDir = transform.forward;

        // ángulo del aim relativo al cuerpo
        Vector3 local = transform.InverseTransformDirection(aimDir);
        float targetYaw   = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg,  -_headMaxYawDeg,  _headMaxYawDeg);
        float targetPitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg, -_headMaxPitchDeg, _headMaxPitchDeg);

        float k = 1f - Mathf.Exp(-_headResponse * dt);
        _headYaw   = Mathf.Lerp(_headYaw, targetYaw, k);
        _headPitch = Mathf.Lerp(_headPitch, targetPitch, k);

        _neck.localRotation = _neckRest * Quaternion.Euler(_headPitch, _headYaw, 0f);
    }

    private void UpdateTail(float dt, float yawRate)
    {
        if (_tail == null) return;

        // el "latigazo": la cola se retrasa contra el giro del cuerpo
        float whipBase = -yawRate * _tailWhip;
        float idleSway = Mathf.Sin(Time.time * _tailIdleSwayFreq * Mathf.PI * 2f) * _tailIdleSwayDeg;

        for (int i = 0; i < _tail.Length; i++)
        {
            if (_tail[i] == null) continue;

            float seg = i + 1f;
            float whip   = whipBase * Mathf.Pow(_tailWhipFalloff, i);
            float sway   = idleSway * (0.4f + 0.15f * seg);
            float droop  = _tailDroopDeg * seg;

            // objetivo LOCAL de este segmento respecto de su padre: pose de reposo
            // + latigazo/sway en yaw + caída en pitch
            Quaternion targetLocal = _tailRest[i] * Quaternion.Euler(droop, whip + sway, 0f);

            // resorte crítico-ish hacia el objetivo (integración semi-implícita)
            Quaternion diff = targetLocal * Quaternion.Inverse(_tailCur[i]);
            diff.ToAngleAxis(out float ang, out Vector3 axis);
            if (ang > 180f) ang -= 360f;
            if (float.IsInfinity(axis.x) || axis.sqrMagnitude < 0.0001f) { _tailCur[i] = targetLocal; _tailVel[i] = Vector3.zero; }
            else
            {
                Vector3 toTarget = axis.normalized * (ang * Mathf.Deg2Rad);
                Vector3 accel = toTarget * _tailStiffness - _tailVel[i] * _tailDamping;
                _tailVel[i] += accel * dt;
                Vector3 step = _tailVel[i] * dt * Mathf.Rad2Deg;
                _tailCur[i] = Quaternion.AngleAxis(step.magnitude, step.sqrMagnitude > 1e-8f ? step.normalized : Vector3.up) * _tailCur[i];
            }

            _tail[i].localRotation = _tailCur[i];
        }
    }

    // -------------------------------------------------------------------------
    private void AutoFill()
    {
        var all = GetComponentsInChildren<Transform>(true);
        if (_hips == null)   _hips   = Find(all, "Gecko_Hips");
        if (_spine1 == null) _spine1 = Find(all, "Gecko_Spine1");
        if (_spine2 == null) _spine2 = Find(all, "Gecko_Spine2");
        if (_neck == null)   _neck   = Find(all, "Gecko_Neck");
        if (_bodyRb == null) _bodyRb = GetComponent<Rigidbody>();

        if (_tail == null || _tail.Length == 0)
        {
            var list = new System.Collections.Generic.List<Transform>();
            for (int n = 1; n <= 8; n++)
            {
                var t = Find(all, "Gecko_Tail" + n);
                if (t == null) break;
                list.Add(t);
            }
            _tail = list.ToArray();
        }
    }

    private static Transform Find(Transform[] all, string n)
    {
        foreach (var t in all) if (t.name == n) return t;
        return null;
    }

    private void OnDisable()
    {
        // volver a la pose de reposo para no dejar el bicho torcido en el editor
        if (!_ready) return;
        if (_hips)   { _hips.localRotation = _hipsRest; _hips.localPosition = _hipsRestPos; }
        if (_spine1) _spine1.localRotation = _spine1Rest;
        if (_spine2) _spine2.localRotation = _spine2Rest;
        if (_neck)   _neck.localRotation = _neckRest;
        if (_tail != null)
            for (int i = 0; i < _tail.Length; i++)
                if (_tail[i]) _tail[i].localRotation = _tailRest[i];
    }
}
