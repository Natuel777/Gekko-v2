using UnityEngine;

// Valores del nado. Viven en el inspector de Player (sección "Nado"). Las distancias son para un Gekko de escala 1 y se
// escalan con su tamaño real, igual que en GekkoWater.
[System.Serializable]
public class SwimSettings
{
    [Header("Superficie")]
    [Tooltip("Altura del centro del cuerpo respecto de la superficie mientras flota (negativo = más hundido).")]
    public float floatOffset = -0.15f;
    [Tooltip("Balanceo arriba/abajo al flotar, en metros.")]
    [Min(0f)] public float bobAmplitude = 0.04f;
    [Tooltip("Velocidad del balanceo (ciclos por segundo).")]
    [Min(0f)] public float bobFrequency = 0.6f;
    [Tooltip("Agua menos profunda que esto (desde la superficie hasta el piso) no se nada: se camina, como antes.")]
    [Min(0f)] public float minDepth = 1f;
    [Tooltip("Pendiente máxima (grados) de la superficie para nadar. Más empinado (cascadas, toboganes) sigue siendo tobogán.")]
    [Range(0f, 60f)] public float maxSlope = 20f;

    [Header("Movimiento")]
    [Tooltip("Velocidad de nado como fracción de la velocidad al caminar.")]
    [Range(0.1f, 1.5f)] public float speedFactor = 0.65f;
    [Tooltip("Aceleración horizontal en el agua (m/s²). Más bajo = más 'flotante', tarda más en arrancar y frenar.")]
    [Min(0.1f)] public float acceleration = 10f;
    [Tooltip("Qué parte de la corriente del río lo arrastra. 0 = nada, 1 = lo lleva a la velocidad del río.")]
    [Range(0f, 1f)] public float currentFactor = 0.5f;

    [Header("Salir del agua")]
    [Tooltip("Fuerza del salto desde el agua como fracción del salto normal.")]
    [Range(0.3f, 1.5f)] public float jumpFactor = 0.9f;
    [Tooltip("Altura máxima (sobre la superficie) de una orilla a la que se sube solo al nadar contra ella.")]
    [Min(0f)] public float ledgeMaxHeight = 1f;

    [Header("Buceo (solo en DeepWaterZone)")]
    [Tooltip("Velocidad de bajada manteniendo Bucear (m/s).")]
    [Min(0f)] public float diveSpeed = 2.5f;
    [Tooltip("Velocidad de subida manteniendo Saltar bajo el agua (m/s).")]
    [Min(0f)] public float ascendSpeed = 3f;
    [Tooltip("Velocidad a la que sube solo, sin tocar nada (m/s). 0 = se queda a esa profundidad.")]
    [Min(0f)] public float buoyancySpeed = 0.8f;
    [Tooltip("Aceleración vertical bajo el agua (m/s²).")]
    [Min(0.1f)] public float verticalAcceleration = 8f;

    [Header("Aire")]
    [Tooltip("Segundos que aguanta bajo el agua con la barra llena.")]
    [Min(0.1f)] public float airDuration = 10f;
    [Tooltip("Segundos que tarda en llenarse la barra fuera del agua.")]
    [Min(0.1f)] public float airRefillTime = 1.5f;
    [Tooltip("Sin aire, recibe un golpe cada estos segundos.")]
    [Min(0.1f)] public float drownDamageInterval = 2f;
}

// El nado de Gekko. Clase plana, la maneja PlayerController (ver TrySwim): esta clase decide si Gekko está nadando y mueve el
// Rigidbody en el agua; el controller se encarga del giro, los sonidos, el animator y de lo que pasa al salir.
//
// Cómo flota: mientras nada, Gekko deja de CHOCAR con la layer Water (Rigidbody.excludeLayers) y se apaga la gravedad. La
// altura la lleva este código hacia la superficie que devuelve GekkoWater.TryGetSurface. Así el mismo nado sirve sobre los
// colliders sólidos de agua (lakeCol, RiverSolidSurface...), sobre los WaterZone y en los DeepWaterZone, donde además se
// puede bajar (Bucear) y subir (Saltar).
//
// Cuándo NO nada (y todo sigue como antes): agua poco profunda (se camina por el fondo), superficie empinada (cascadas y
// toboganes, donde la corriente lo hace resbalar), trepando una pared, o colgado de la lengua.
public class GekkoSwimming
{
    public enum StepResult { Swimming, Exited, JumpOut, LedgeHop }

    private const string AirEvent = "OnPlayerAirChanged";

    private readonly Rigidbody _rb;
    private readonly Transform _pj;
    private readonly CapsuleCollider _collider;
    private readonly LayerMask _groundMask;
    private readonly LayerMask _climbMask;
    private readonly SwimSettings _s;

    private bool _swimming;
    private GekkoWater.WaterInfo _water;
    private LayerMask _originalExclude;
    private float _reenterBlock;
    private float _air = 1f;
    private float _lastSentAir = -1f;
    private float _drownTimer;
    private float _bobPhase;

    public bool IsSwimming => _swimming;
    // Todo el cuerpo debajo de la superficie.
    public bool IsUnderwater { get; private set; }
    public bool CanDiveHere => _swimming && _water.CanDive;
    public float Air01 => _air;
    public float JumpFactor => _s.jumpFactor;

    private float Scale => _pj.lossyScale.x;
    private float Radius => _collider.radius * Scale;
    private Vector3 Center => _pj.TransformPoint(_collider.center);

    public GekkoSwimming(Rigidbody rb, Transform pj, CapsuleCollider collider, LayerMask groundMask, LayerMask climbMask, SwimSettings settings)
    {
        _rb = rb;
        _pj = pj;
        _collider = collider;
        // Fondo y orillas: TODO lo que choca físicamente con Gekko (menos el agua), no solo las layers de piso del juego. Muchos
        // fondos de lago están en Default (Ground, BehindCascade...) y, si no se los contara, un charco de 5 cm parecería hondo.
        int solid = 0;
        int playerLayer = pj.gameObject.layer;
        for(int i = 0; i < 32; i++)
            if(!Physics.GetIgnoreLayerCollision(playerLayer, i)) solid |= 1 << i;
        _groundMask = (groundMask | solid) & ~(1 << GekkoWater.WaterLayer) & ~(1 << playerLayer);
        _climbMask = climbMask & ~(1 << GekkoWater.WaterLayer);
        _s = settings ?? new SwimSettings();
        _bobPhase = Random.value * 10f;
    }

    // Aire: baja bajo el agua y se recarga afuera. Corre en cada FixedUpdate, nade o no.
    public void TickAir(float dt)
    {
        if(_swimming && IsUnderwater)
        {
            _air = Mathf.Max(0f, _air - dt / _s.airDuration);

            if(_air <= 0f)
            {
                _drownTimer += dt;
                if(_drownTimer >= _s.drownDamageInterval)
                {
                    _drownTimer = 0f;
                    EventManager.Trigger<float>("OnPlayerDamaged", 1f);
                }
            }
        }
        else
        {
            _air = Mathf.Min(1f, _air + dt / _s.airRefillTime);
            _drownTimer = 0f;
        }

        if(_reenterBlock > 0f) _reenterBlock -= dt;

        if(!Mathf.Approximately(_air, _lastSentAir))
        {
            _lastSentAir = _air;
            EventManager.Trigger<float>(AirEvent, _air);
        }
    }

    // ¿Tiene que empezar a nadar? Gekko en agua plana y profunda, con el cuerpo ya tocando la superficie y sin subir.
    public bool TryEnter()
    {
        if(_swimming || _reenterBlock > 0f) return false;

        // Subiendo rápido (recién saltó del agua): todavía no.
        if(_rb.linearVelocity.y > 0.5f) return false;

        Vector3 center = Center;
        if(!GekkoWater.TryGetSurface(center, 2f * Scale, 3f * Scale, out GekkoWater.WaterInfo water)) return false;
        if(water.NoSwim || !water.IsFlat(_s.maxSlope)) return false;

        // Entra cuando la panza toca el agua. Parado sobre un collider de agua el centro queda un radio por encima de su cara
        // de arriba, así que eso también entra.
        if(center.y > water.SurfaceY + Radius + 0.15f * Scale) return false;
        if(!IsDeep(center, water)) return false;

        Enter(water);
        return true;
    }

    private void Enter(GekkoWater.WaterInfo water)
    {
        _swimming = true;
        _water = water;
        GekkoWater.PlayerSwimming = true;

        _originalExclude = _rb.excludeLayers;
        _rb.excludeLayers = _originalExclude | (1 << GekkoWater.WaterLayer);
        _rb.useGravity = false;

        // Cae al agua: se frena de golpe hacia abajo, como al zambullirse.
        Vector3 v = _rb.linearVelocity;
        v.y *= 0.3f;
        _rb.linearVelocity = v;
    }

    // Sale del nado (vuelve la gravedad y el choque con el agua). blockSeconds: cuánto tarda en poder volver a entrar.
    public void Exit(float blockSeconds = 0.3f)
    {
        if(!_swimming) return;

        _swimming = false;
        IsUnderwater = false;
        GekkoWater.PlayerSwimming = false;
        _rb.excludeLayers = _originalExclude;
        _rb.useGravity = true;
        _reenterBlock = blockSeconds;
    }

    // Un paso de física nadando. moveDir: dirección horizontal del input en mundo (ya relativa a la cámara), con largo = fuerza
    // del input (0..1). jumpPressedNow: se apretó Saltar en este paso (flanco). jumpHeld/diveHeld: se mantienen apretados.
    public StepResult Step(Vector3 moveDir, float walkSpeed, bool jumpPressedNow, bool jumpHeld, bool diveHeld, float dt)
    {
        Vector3 center = Center;

        if(!GekkoWater.TryGetSurface(center, 2f * Scale, (_water.CanDive ? 20f : 3f) * Scale, out GekkoWater.WaterInfo water)
           || water.NoSwim || !water.IsFlat(_s.maxSlope) || !IsDeep(center, water, 0.7f))
        {
            Exit();
            return StepResult.Exited;
        }
        _water = water;

        // Algo lo sacó por arriba del agua (subió una playa, lo levantó una plataforma): deja de nadar.
        if(center.y > water.SurfaceY + Radius + 0.2f * Scale)
        {
            Exit();
            return StepResult.Exited;
        }

        float floatY = water.SurfaceY + _s.floatOffset * Scale;
        IsUnderwater = center.y + Radius < water.SurfaceY;
        bool atSurface = center.y >= floatY - 0.3f * Scale;

        // --- Salir del agua -------------------------------------------------------------------------------------------
        if(jumpPressedNow && atSurface)
        {
            Exit();
            return StepResult.JumpOut;
        }

        if(moveDir.sqrMagnitude > 0.01f && atSurface)
        {
            StepResult edge = CheckShore(center, moveDir.normalized, water);
            if(edge != StepResult.Swimming) return edge;
        }

        // --- Horizontal: hacia el input, más la corriente -----------------------------------------------------------
        Vector3 velocity = _rb.linearVelocity;
        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
        // Algunos ríos llevan más fuerte que el valor general (RiverSolidSurface._swimCurrentFactor).
        float currentFactor = water.CurrentFactor >= 0f ? water.CurrentFactor : _s.currentFactor;
        Vector3 current = water.Current * currentFactor;
        current.y = 0f;
        Vector3 target = moveDir * walkSpeed * _s.speedFactor + current;
        horizontal = Vector3.MoveTowards(horizontal, target, _s.acceleration * dt);

        // --- Vertical ------------------------------------------------------------------------------------------------
        float targetVy;
        bool canDive = water.CanDive;

        if(canDive && diveHeld)
        {
            targetVy = -_s.diveSpeed;
        }
        else if(canDive && jumpHeld && !atSurface)
        {
            targetVy = _s.ascendSpeed;
        }
        else if(canDive && !atSurface)
        {
            // Bajo el agua sin tocar nada: sube despacio solo.
            targetVy = _s.buoyancySpeed;
        }
        else
        {
            // Flotando: un resorte hacia la altura de flotación, con un balanceo suave.
            _bobPhase += dt * _s.bobFrequency * Mathf.PI * 2f;
            float bob = Mathf.Sin(_bobPhase) * _s.bobAmplitude * Scale;
            targetVy = Mathf.Clamp((floatY + bob - center.y) * 6f, -3f, 3f);
        }

        // Bajo el agua no se pasa de la superficie (ahí se engancha el resorte) ni del fondo de la zona.
        // Cerca del fondo frena de a poco (si no, con la inercia se pasa y asoma por debajo del volumen del agua).
        if(canDive && targetVy < 0f)
        {
            float room = center.y - Radius - water.BottomY;
            targetVy = Mathf.Max(targetVy, -Mathf.Max(0f, room) * 3f);
        }

        float vy = Mathf.MoveTowards(velocity.y, targetVy, _s.verticalAcceleration * dt);
        _rb.linearVelocity = new Vector3(horizontal.x, vy, horizontal.z);
        _rb.useGravity = false;

        return StepResult.Swimming;
    }

    // Nadando contra algo: una pared trepable (sale del agua para que el controller lo pegue a la pared) o una orilla baja (se
    // sube de un salto).
    private StepResult CheckShore(Vector3 center, Vector3 dir, GekkoWater.WaterInfo water)
    {
        // Hasta un poco más allá de la nariz: el mismo alcance con el que DetectSurface encuentra una pared de frente (desde
        // la punta del cuerpo, 0.4 m más). Si se saliera del agua por una pared más lejana, el controller no la encontraría.
        float reach = (_collider.height * 0.5f - _collider.radius + 0.35f) * Scale;
        LayerMask mask = _groundMask | _climbMask;

        // El primer choque que no sea el propio Gekko.
        int count = Physics.SphereCastNonAlloc(center, Radius * 0.6f, dir, _hits, reach, mask, QueryTriggerInteraction.Ignore);
        RaycastHit hit = default;
        bool found = false;
        for(int i = 0; i < count; i++)
        {
            if(_hits[i].collider.attachedRigidbody == _rb || _hits[i].distance <= 0f) continue;
            if(!found || _hits[i].distance < hit.distance) { hit = _hits[i]; found = true; }
        }
        if(!found) return StepResult.Swimming;

        // Una rampa suave no es una pared: se sigue nadando y el chequeo de profundidad lo saca cuando haga pie.
        if(hit.normal.y > 0.6f) return StepResult.Swimming;

        bool climbable = (_climbMask.value & (1 << hit.collider.gameObject.layer)) != 0;
        if(climbable)
        {
            Exit(0.6f);
            return StepResult.Exited;
        }

        // Orilla: ¿hay piso arriba, a poca altura sobre el agua?
        float maxUp = _s.ledgeMaxHeight * Scale;
        Vector3 probe = hit.point + dir * Radius + Vector3.up * (maxUp + 0.3f * Scale);
        if(!Physics.Raycast(probe, Vector3.down, out RaycastHit top, maxUp + 0.6f * Scale, _groundMask, QueryTriggerInteraction.Ignore))
            return StepResult.Swimming;

        float height = top.point.y - water.SurfaceY;
        if(height <= 0f || height > maxUp || top.normal.y < 0.7f) return StepResult.Swimming;

        Exit(0.5f);

        // Salto justo para pasar el borde (con un margen) y un empujón hacia la orilla.
        float g = Mathf.Abs(Physics.gravity.y);
        float rise = top.point.y + Radius - center.y + 0.35f * Scale;
        float vUp = Mathf.Sqrt(2f * g * Mathf.Max(rise, 0.1f));
        _rb.linearVelocity = dir * 3f * Mathf.Max(Scale, 0.5f) + Vector3.up * vUp;
        return StepResult.LedgeHop;
    }

    // Hay suficiente agua debajo para nadar: sin piso a menos de minDepth de la superficie (también en los DeepWaterZone: así
    // en sus orillas bajas se camina). factor < 1 al seguir nadando (histéresis): para entrar hace falta más profundidad que
    // para no salir, así en el borde no titila entre nadar y caminar.
    private bool IsDeep(Vector3 center, GekkoWater.WaterInfo water, float factor = 1f)
    {
        Vector3 origin = new Vector3(center.x, water.SurfaceY, center.z) + Vector3.up * 0.05f * Scale;
        int count = Physics.SphereCastNonAlloc(origin, Radius * 0.5f, Vector3.down, _hits, _s.minDepth * factor * Scale,
            _groundMask, QueryTriggerInteraction.Ignore);

        // El rayo pasa por el propio Gekko (está en el agua, justo ahí): sus colliders no cuentan como fondo.
        for(int i = 0; i < count; i++)
            if(_hits[i].collider.attachedRigidbody != _rb) return false;

        return true;
    }

    private readonly RaycastHit[] _hits = new RaycastHit[16];
}
