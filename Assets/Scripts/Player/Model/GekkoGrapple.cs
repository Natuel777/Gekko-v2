using UnityEngine;

public sealed class GekkoGrapple
{
    private enum Phase { Idle, Freezing, Flying }

    // Meters taken below Gekko as the "lowest point" to compute the arc's height (the repo's "- 1",
    // which is meant for a ~2 m player). It is scaled with Gekko's size, just like _overshootYAxis.
    private const float LowestPointOffset = 1f;

    // Extra seconds, on top of the computed flight time, to wait before cutting the flight if it didn't hit anything.
    // The normal flight ends earlier, on collision (see ArtificialOnCollisionEnter) or on reaching the point (see
    // ReachedGrapplePoint): this is just the safety net that in the repo is the ResetRestrictions at 3 s, but
    // adjusted to the real flight instead of a fixed value.
    private const float FlightTimeMargin = 0.5f;

    // SwingAttach layer, the one for swing hooks (Gancho.prefab). It is not a hazard, so GekkoCollision doesn't treat it as
    // one (it doesn't cut the swing).
    public const int SwingAttachLayer = 16;

    // GrapplePoint layer, the one for grapple hooks (GrapplePoint.prefab). Right-clicking while aiming at one starts the
    // grapple (see GekkoDetection). Its trigger opens the second jump window; it is not a hazard either, so
    // GekkoCollision doesn't cut the swing with it.
    public const int GrapplePointLayer = 18;

    private readonly GekkoSwinging _swinging;
    private readonly Rigidbody _rb;
    private readonly Transform _transform;
    private readonly RotateGekkoWhileSwingin _rotation;
    private readonly float _overshootYAxis, _grapplingCD, _delayTime, _finishDistance, _lookSpeed, _secondJumpMultiplier;

    private Phase _phase = Phase.Idle;
    private float _cooldownTimer, _phaseTimer;
    private Vector3 _grapplePoint;
    private bool _secondJumpEnabled = false;

    // Second jump (see TrySecondJump). _secondJumpActive is true while the current flight is one of those jumps.
    // _grappleDistance is what the last grapple measured (from where it started to the hook): the base of the jump's reach.
    private bool _secondJumpActive;
    private float _grappleDistance;

    // true from when the wait starts until the flight ends.
    public bool IsGrappling => _phase != Phase.Idle;
    public bool SecondJumpEnabled => _secondJumpEnabled;

    // The tongue is attached to the hook: during the grapple's wait and flight. The second jump releases it, even if there is
    // still flight (IsGrappling). GekkoSwinging uses it to know whether it has to draw it.
    public bool IsTongueOut => _phase != Phase.Idle && !_secondJumpActive;

    // The current flight is a second jump's. Unlike the grapple, this flight can be interrupted by attaching to
    // a swing (see EndSecondJump) or a new grapple (see TryStart): there is no tongue or point to reach anymore.
    public bool IsSecondJumpFlying => _phase == Phase.Flying && _secondJumpActive;

    public GekkoGrapple(GekkoSwinging swinging, Rigidbody rb, Transform t, float overshootYAxis, float grapplingCD, float delayTime,
                        float finishDistance, RotateGekkoWhileSwingin rotation, float lookSpeed, float secondJumpMultiplier)
    {
        _swinging = swinging;
        _rb = rb;
        _transform = t;
        _overshootYAxis = overshootYAxis;
        _grapplingCD = grapplingCD;
        _delayTime = Mathf.Max(0f, delayTime);
        _finishDistance = Mathf.Max(0f, finishDistance);
        _rotation = rotation;
        _lookSpeed = Mathf.Max(0f, lookSpeed);
        _secondJumpMultiplier = Mathf.Max(0f, secondJumpMultiplier);
    }

    // Called by GekkoSwinging.ArtificialUpdate every frame, whether there is a swing or not: the cooldown and the flight timers have
    // to keep running after releasing the swing, when there is no joint anymore.
    public void ArtificialUpdate()
    {
        if(_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

        if(_phase == Phase.Idle) return;

        // In the second jump there is no point to reach (the destination is just a reference in the air): it ends on collision
        // or on time.
        if(_phase == Phase.Flying && !_secondJumpActive && ReachedGrapplePoint())
        {
            Finish(true);
            return;
        }

        _phaseTimer -= Time.deltaTime;

        if(_phaseTimer > 0f) return;

        if(_phase == Phase.Freezing) Launch();

        else Finish(true);
    }

    // Called by GekkoSwinging.ArtificialFixedUpdate. It only does something while Gekko waits to be launched: it holds Gekko
    // still (velocity 0; TryStart already turned gravity off). It is the repo's "freeze", which there is rb.velocity = zero.
    public void ArtificialFixedUpdate()
    {
        if(_phase != Phase.Freezing) return;

        _rb.linearVelocity = Vector3.zero;
    }

    // Tries to start the grapple towards grapplePoint: it is the right click aiming at a GrapplePoint (see GekkoDetection).
    // Returns false, without changing anything, if there is still a cooldown, if a grapple is already in progress or if the parabola to that
    // point can't be computed. The only exception to "a grapple is already in progress" is the second jump: that flight is
    // cut and the new grapple takes control. It doesn't go through Finish, so it doesn't start a new cooldown: the one left over
    // from the previous grapple is respected. GekkoSwinging only turns the tongue on (and releases the swing, if there is one) if this returns true.
    public bool TryStart(Vector3 grapplePoint)
    {
        if((_phase != Phase.Idle && !IsSecondJumpFlying) || _cooldownTimer > 0f) return false;

        // It is computed now, even though with a wait the velocity is only used in Launch: that way, if the point has no valid
        // parabola, nothing changes (a second jump in flight, for example, continues as it was).
        if(!TryCalculateLaunch(grapplePoint, out Vector3 velocity, out float flightTime)) return false;

        _grapplePoint = grapplePoint;

        // What this grapple measures (from where it starts to the hook) is the base of the second jump's reach. And since a new grapple
        // is starting, the flight stops being a second jump.
        _grappleDistance = Vector3.Distance(_transform.position, _grapplePoint);
        _secondJumpActive = false;

        // Gekko starts turning, gradually, to look at the point: right away, that is, also during the wait. It is stopped in
        // Finish.
        _rotation.StartLookAtGrapplePoint(_grapplePoint, _lookSpeed);

        // The tongue goes out towards the GrapplePoint (GekkoSwinging draws it as soon as this returns true): it plays once per grapple. It goes here,
        // at the common point of the two paths that return true (with and without a wait) and after all the validations: a click during
        // cooldown, with another grapple in progress or with an invalid parabola returns false earlier and doesn't play. The second jump doesn't go
        // through here (it releases the tongue instead of throwing it).
        if(AudioManager.instance) AudioManager.instance.Play(SoundNames.PlayerGrapple);

        if(_delayTime <= 0f)
        {
            StartFlight(velocity, flightTime);
            return true;
        }

        // Wait: no velocity and no gravity, so it stays pinned in the air and doesn't slowly sink.
        _phase = Phase.Freezing;
        _phaseTimer = _delayTime;
        _rb.useGravity = false;
        _rb.linearVelocity = Vector3.zero;
        return true;
    }


    // Second jump: a jump just like the grapple's (the same parabola and the same arc), but towards where the camera looks
    // instead of towards the hook. It can only be done while the window is open (_secondJumpEnabled: from when Gekko
    // flies into a GrapplePoint's trigger until it leaves), and it is spent when used. It is still done with Space.
    //
    // Where it reaches: it aims at a destination on the camera's line, at (distance of the last grapple x
    // _secondJumpMultiplier) from Gekko. With multiplier 1 it jumps as far as the grapple did; with 2, double; with 0,
    // straight up. The direction is the full camera one, pitch included: with the camera looking down, the
    // destination ends up lower and the jump lands sooner; looking up, it goes higher.
    //
    // direction is where the camera looks (GekkoSwinging passes it). Returns false, without changing anything, if the window
    // is closed or if the jump can't be computed. It ignores the cooldown: it has just been started, when the
    // grapple that opened the window ended.
    public bool TrySecondJump(Vector3 direction)
    {
        if(!_secondJumpEnabled) return false;

        // No second jump in water (the grapple itself no longer starts in water: GekkoDetection cuts it).
        if(GekkoWater.IsInWater(_transform)) return false;

        // With no direction, or without a previous grapple that measured something, there is no reach.
        if(direction.sqrMagnitude <= 0.0001f || _grappleDistance <= 0.0001f) return false;

        direction.Normalize();
        Vector3 target = _transform.position + direction * (_grappleDistance * _secondJumpMultiplier);

        if(!TryCalculateLaunch(target, out Vector3 velocity, out float flightTime)) return false;

        // It is spent: one second jump per window. The flight becomes a second jump (with no tongue and no "reaching the point").
        _secondJumpEnabled = false;
        _secondJumpActive = true;

        // The destination replaces the hook as the parabola's point, the tongue is released and Gekko turns towards where it jumps.
        _grapplePoint = target;
        _swinging.HideTongue();
        _rotation.StartLookInDirection(direction, _lookSpeed);

        StartFlight(velocity, flightTime);
        return true;
    }

    // Called by GekkoSwinging.StartSwing when Gekko attaches to a swing in the middle of a second jump: the flight is cut there and the
    // swing takes control, keeping the velocity it had. It counts like any other end of flight (it starts the grapple's
    // cooldown). It does nothing if the flight is not a second jump: the grapple isn't interrupted this way.
    public void EndSecondJump()
    {
        if(IsSecondJumpFlying) Finish(true);
    }

    public void ArtificialOnCollisionEnter()
    {
        if(_phase == Phase.Flying) Finish(true);
    }

    // The second jump window is opened by a GrapplePoint's trigger (not a swing hook's) while the grapple's
    // flight lasts, and it closes when leaving it (see ArtificialOnTriggerExit).
    public void ArtificialOnTriggerEnter(Collider other)
    {
        if(other.gameObject.layer == GrapplePointLayer && _phase == Phase.Flying)
        {
            ParticleSystem ps = other.GetComponentInChildren<ParticleSystem>();
            ps?.Play(); 
            _secondJumpEnabled = true;
        }

    }

    public void ArtificialOnTriggerExit(Collider other)
    {
        if(other.gameObject.layer == GrapplePointLayer)
        {
            ParticleSystem ps = other.GetComponentInChildren<ParticleSystem>();
            
            // The second jump window closes when leaving the trigger, whether or not there is a particle to turn off (before, it only
            // closed if the hook had a particle).
            _secondJumpEnabled = false;

            if(ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); 
            } 
        }
    }

    // Gekko is already at _finishDistance or less from the attach point. The meters are for a scale-1 Gekko and are
    // scaled with its real size, just like _overshootYAxis.
    private bool ReachedGrapplePoint()
    {
        float distance = _finishDistance * _transform.lossyScale.y;
        return (_grapplePoint - _transform.position).sqrMagnitude <= distance * distance;
    }

    // Cuts the grapple without cooldown. Used by the teleport (PlayerController.Teleport): a grapple in progress points at an
    // anchor that is now far away, and if it was waiting it would shoot towards it from the new place.
    public void Cancel()
    {
        // The second jump window doesn't survive the teleport either: Gekko ends up far from the hook.
        _secondJumpEnabled = false;

        if(_phase != Phase.Idle) Finish(false);
    }

    // End of the wait: the parabola is recomputed from where Gekko ended up and it is launched.
    private void Launch()
    {
        if(!TryCalculateLaunch(_grapplePoint, out Vector3 velocity, out float flightTime))
        {
            // It shouldn't happen (TryStart already validated it), but if it does it can't stay pinned: it is cut.
            Finish(true);
            return;
        }

        StartFlight(velocity, flightTime);
    }

    // The equivalent of the repo's SetVelocity: the velocity is assigned ONCE. The repo's 0.1 s Invoke isn't needed here:
    // the Rigidbody's drag is 0 and the controller is already asleep (IsGrappling), so nothing overwrites it.
    private void StartFlight(Vector3 velocity, float flightTime)
    {
        _phase = Phase.Flying;
        _phaseTimer = flightTime + FlightTimeMargin;
        _rb.useGravity = true;
        _rb.linearVelocity = velocity;
    }

    // The only place where a grapple ends (by collision, by reaching the point, by time, by cancellation or by an invalid
    // computation).
    private void Finish(bool startCooldown)
    {
        _phase = Phase.Idle;
        _phaseTimer = 0f;
        _secondJumpActive = false;

        if(startCooldown) _cooldownTimer = _grapplingCD;

        // If it was cut during the wait, gravity was off. If not, it was already on and this changes nothing. In both cases
        // PlayerController decides it again in its next FixedUpdate.
        _rb.useGravity = true;

        // The flight ends, the tongue ends. The controller wakes up on its own: there is no IsGrappling anymore.
        _swinging.HideTongue();

        // There is nothing left to look at: PlayerController wakes up and straightens Gekko on its own.
        _rotation.StopLookAtGrapplePoint();
    }

    // Builds the parabola. The arc's height comes out the same as in the repo's ExecuteGrapple: it starts from a point lower than
    // Gekko and reaches the attach point plus a margin (_overshootYAxis), so the arc passes ABOVE the
    // point and doesn't graze it. The meters are for a scale-1 Gekko and are scaled with its real size.
    // Returns false if the result is useless (see the return).
    // target is the point the parabola must pass through: the hook (grapple) or the second jump's destination.
    private bool TryCalculateLaunch(Vector3 target, out Vector3 velocity, out float flightTime)
    {
        float scale = _transform.lossyScale.y;
        Vector3 start = _transform.position;
        float lowestY = start.y - LowestPointOffset * scale;
        float overshoot = _overshootYAxis * scale;

        float grapplePointRelativeYPos = target.y - lowestY;
        float highestPointOnArc = grapplePointRelativeYPos + overshoot;

        // If the point is below the lowest point, the arc only rises by the margin.
        if(grapplePointRelativeYPos < 0f) highestPointOnArc = overshoot;

        velocity = CalculateJumpVelocity(start, target, highestPointOnArc, out flightTime);

        // A NaN or infinity can't be assigned to a Rigidbody (Unity throws an error and doesn't apply it), and such a flight time
        // would leave the flight endless. It happens, for example, with a negative _overshootYAxis.
        return IsValid(velocity) && IsValid(flightTime) && flightTime > 0f;
    }

    // Initial velocity to go from startPoint to endPoint in a parabola whose highest point is trajectoryHeight above
    // startPoint. It assumes the only thing accelerating Gekko is Physics.gravity.
    // flightTime is the time it takes to reach endPoint: it is exactly the denominator of velocityXZ, so it comes for free.
    public Vector3 CalculateJumpVelocity(Vector3 startPoint, Vector3 endPoint, float trajectoryHeight, out float flightTime)
    {
        float gravity = Physics.gravity.y;
        float displacementY = endPoint.y - startPoint.y;
        Vector3 displacementXZ = new Vector3(endPoint.x - startPoint.x, 0f, endPoint.z - startPoint.z);

        // Time to go up to the highest point + time to come down from there to endPoint.
        flightTime = Mathf.Sqrt(-2f * trajectoryHeight / gravity)
                   + Mathf.Sqrt(2f * (displacementY - trajectoryHeight) / gravity);

        Vector3 velocityY = Vector3.up * Mathf.Sqrt(-2f * gravity * trajectoryHeight);
        Vector3 velocityXZ = displacementXZ / flightTime;

        return velocityXZ + velocityY;
    }

    private static bool IsValid(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static bool IsValid(Vector3 value) => IsValid(value.x) && IsValid(value.y) && IsValid(value.z);
}
