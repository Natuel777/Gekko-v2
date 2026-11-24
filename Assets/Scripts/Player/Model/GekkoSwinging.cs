using UnityEngine;

public sealed class GekkoSwinging
{
    private LineRenderer _lineRenderer;
    private Vector3 _grapplePoint = Vector3.zero, _currentGrapplePoint;
    private SpringJoint _joint;
    private Spring _spring;
    private Rigidbody _rb;
    private float _forwardThrustForce, _horizontalThrustForce, _extendCableSpeed;
    private Vector2 _thrustInput;
    private Transform _camera, _transform, _tongueTip;
    
    #region Tongue Visual
    // How the tongue is drawn: three things, in this order.
    //   1) THE PHYSICAL ROPE (RopeSimulation): decides the base shape. It lags behind when Gekko moves and bounces when Gekko stops.
    //      It can hang (positive slack) or be pre-tensioned (negative slack). The full explanation is above
    //      that class.
    //   2) THE VISUAL TENSION (_ropeTautness): when drawing, the rope's shape is pulled towards the mouth -> anchor line. It is what
    //      keeps the tongue even from end to end.
    //   3) THE WHIP (Spring + sine wave): an "artistic" wave that appears on attaching and fades out on its own.
    // Physics rules, the visual tension straightens it and the whip is added on top only when drawing (see
    // UpdateLineRenderer).

    // Number of rope segments. The simulation and the LineRenderer have _quality + 1 points.
    private int _quality;

    // Whip parameters (they come from Player's Inspector):
    //   _springStrength : how fast it oscillates.       _springDamper : how fast it dies out.
    //   _springVelocity : initial kick on attaching.  _waveCount    : number of wave crests.
    //   _waveHeight     : the wave's maximum height.      _waveAffectCurve : how noticeable the wave is at each point of the
    //                     rope (it must be 0 at both ends so the tongue and the anchor don't come apart).
    private float _springDamper, _springStrength, _springVelocity, _waveCount, _waveHeight;
    private AnimationCurve _waveAffectCurve;

    // The rope simulation (see RopeSimulation.cs).
    private RopeSimulation _rope;

    // List of points handed to the LineRenderer (rope points + whip). It is created only once and reused every frame,
    // so drawing doesn't generate garbage (GC).
    private Vector3[] _ropePoints;

    // Rope length relative to the real distance between its ends (0.05 = 5% longer, -0.02 = 2% shorter):
    //   positive : there is extra rope, it hangs and bulges. A rope with no tension bends sideways with any shortening of
    //              the distance, so however little extra there is the bulge is large (tens of cm on a 4 m rope).
    //   0        : exact. It stays almost straight, but still bulges a little when the ends move.
    //   negative : PRE-TENSIONED rope. It is always stretched, so all segments have the same tension and there is nowhere
    //              to bend. It is what keeps it taut and even from end to end.
    private float _ropeSlack;

    // How much what is SEEN of the rope is straightened: 0 = exactly as the simulation, 1 = straight from the mouth to the anchor. It is only
    // visual, it doesn't change the simulation or the whip (see UpdateLineRenderer).
    private float _ropeTautness;

    // The most pre-tensioned the rope can be: -0.1 = 10% shorter than the distance between its ends. More than that doesn't
    // look better, and a value close to -1 would leave the rope with no length.
    private const float MinRopeSlack = -0.1f;
    #endregion

    #region Height Limit
    // Soft ceiling of the swing (see ApplyHeightLimit): Gekko shouldn't rise above the attach point.
    //   _heightLimitOffset : shifts the ceiling relative to the point's height (0 = exactly at its height).
    //   _heightLimitSpring : how much it pulls Gekko down for each meter it went past the ceiling (m/s² per meter).
    //   _heightLimitDamper : how much it slows its upward velocity while it is past it (m/s² per m/s).
    //   _heightLimitMaxPull: cap on the downward acceleration (m/s²).
    private float _heightLimitOffset, _heightLimitSpring, _heightLimitDamper, _heightLimitMaxPull;
    #endregion

    #region Swing Sounds
    // Attach sound (SoundNames.PlayerSwingAttach): plays when the tongue's tip reaches the swing hook (see
    // CheckSwingAttachArrival). The tip approaches the anchor with a Lerp that slows down as it arrives (see DrawTongue) and never
    // touches the exact point, so "arrived" means being at this distance, in meters, or less. With 0.2 it plays ~0.3-0.45 s after throwing
    // the tongue at a hook 3-8 m away (computed with the Lerp formula, not measured in the game).
    private const float SwingAttachArrivalDistance = 0.2f;

    // The attach sound already played for this swing. It is once per thrown tongue: StartSwing re-arms it.
    private bool _swingAttachSoundPlayed;
    #endregion

    private RotateGekkoWhileSwingin _gekkoRotation;
    private GekkoGrapple _grappleBehaviour;
    private GekkoDetection _detection;
    
    #region Properties
    // Hanging from the tongue, swinging with the SpringJoint.
    public bool IsSwinging { get; private set; }
    // During the whole grapple (the wait and the flight). It is never true at the same time as IsSwinging: when the grapple starts, the
    // swing is released.
    public bool IsGrappling => _grappleBehaviour != null && _grappleBehaviour.IsGrappling;
    public Vector2 ThrustInput { set { _thrustInput = value; } }
    public Vector3 GrapplePoint => _grapplePoint;
    public GekkoGrapple GrappleBehaviour => _grappleBehaviour;
    #endregion

    public GekkoSwinging(Transform tongue, LayerMask layers, Transform transform, LineRenderer lineRenderer, Transform cam,
                        float forwardThrustForce, float horizontalThrustForce, float extendCableSpeed,
                        Transform predictionPoint, float predictionSphereRadius, float maxTongueDistance,
                        int quality, float springDamper, float springStrength, float springVelocity,
                        float waveCount, float waveHeight, AnimationCurve waveAffectCurve,
                        float ropeGravity, float ropeDamping, int ropeIterations, float ropeSlack, float ropeTautness,
                        float heightLimitOffset, float heightLimitSpring, float heightLimitDamper, float heightLimitMaxPull)
    {
        _lineRenderer = lineRenderer;
        _tongueTip = tongue;
        _transform = transform;
        _camera = cam;
        _rb = transform.GetComponent<Rigidbody>();
        _forwardThrustForce = forwardThrustForce;
        _horizontalThrustForce = horizontalThrustForce;
        _extendCableSpeed = extendCableSpeed;
        _detection = new GekkoDetection(transform, cam, layers, predictionPoint, predictionSphereRadius, maxTongueDistance);
        _quality = Mathf.Max(1, quality);
        _springDamper = springDamper;
        _springStrength = springStrength;
        _springVelocity = springVelocity;
        _waveCount = waveCount;
        _waveHeight = waveHeight;
        _waveAffectCurve = waveAffectCurve;
        _spring = new Spring();
        _spring.SetTarget(0);
        _spring.SetDamper(_springDamper);
        _spring.SetStrength(_springStrength);

        // The rope simulation and the list of points to draw are created, only once. Both have
        // _quality + 1 points (always one more than segments). They are allocated here so new arrays aren't created every frame.
        _rope = new RopeSimulation(_quality, ropeDamping, ropeGravity, ropeIterations);
        _ropePoints = new Vector3[_quality + 1];

        // The rope's extra length can be negative: a rope shorter than the distance between its ends is always
        // stretched and straight, with the same tension in all segments (pre-tensioned rope). The minimum avoids a length of 0.
        // The visual tension is a percentage: from 0 to 1.
        _ropeSlack = Mathf.Max(MinRopeSlack, ropeSlack);
        _ropeTautness = Mathf.Clamp01(ropeTautness);
        _heightLimitOffset = heightLimitOffset;
        _heightLimitSpring = Mathf.Max(0f, heightLimitSpring);
        _heightLimitDamper = Mathf.Max(0f, heightLimitDamper);
        _heightLimitMaxPull = Mathf.Max(0f, heightLimitMaxPull);
        _gekkoRotation = new RotateGekkoWhileSwingin(this, _rb);
    }

    // Right click (called by PlayerInputs when the attach button is pressed)
    public void AttachTongue()
    {
        // During a grapple nothing new can be attached: two systems would be driving Gekko at once. The exception
        // is the second jump: it no longer has a tongue or a point to reach, so it can be cut by attaching to another hook.
        bool interruptsSecondJump = _grappleBehaviour != null && _grappleBehaviour.IsSecondJumpFlying;

        if(IsGrappling && !interruptsSecondJump) return;

        switch(_detection.Detect())
        {
            case GekkoDetection.TargetType.Swing:
                StartSwing(_detection.Point, interruptsSecondJump);
                break;

            case GekkoDetection.TargetType.Grapple:
                StartGrapple(_detection.Point);
                break;
        }
    }

    // Starts the swing (SpringJoint) hanging from anchor.
    private void StartSwing(Vector3 anchor, bool interruptsSecondJump)
    {
        // The flight is only cut with a valid point: if there is nothing to attach to, the jump continues as it was (AttachTongue
        // already validated the point). It goes before building the swing because cutting the flight turns the tongue off (HideTongue) and the swing
        // has to turn it on afterwards.
        if(interruptsSecondJump) _grappleBehaviour.EndSecondJump();

        IsSwinging = true;
        _grapplePoint = anchor;
        _gekkoRotation.StartSwing();
        _joint = _transform.gameObject.AddComponent<SpringJoint>();
        _joint.autoConfigureConnectedAnchor = false;
        _joint.connectedAnchor = _grapplePoint;
        // Optimize with sqrMagnitude
        float distanceFromPoint = Vector3.Distance(_transform.position, _grapplePoint);
        _joint.maxDistance = distanceFromPoint * 0.8f; // Check the reason for this value
        _joint.minDistance = distanceFromPoint * 0.25f; // Check the reason for this value
        _joint.spring = 4.5f; // Check the reason for this value
        _joint.damper = 7f; // Check the reason for this value
        _joint.massScale = 4.5f; // Check the reason for this value
        BeginTongue();

        // Gekko throws the tongue towards the hook: it plays on the way out (SoundNames.PlayerSwingTongue). It goes in StartSwing and not in BeginTongue
        // because BeginTongue is shared by the swing and the grapple, and the grapple has its own sound (see GekkoGrapple.TryStart).
        // The attach, on the other hand, only plays when the tip reaches the hook (see CheckSwingAttachArrival): here it is only re-armed.
        _swingAttachSoundPlayed = false;
        if(AudioManager.instance) AudioManager.instance.Play(SoundNames.PlayerSwingTongue);
    }

    // Starts the grapple towards anchor (a GrapplePoint).
    private void StartGrapple(Vector3 anchor)
    {
        // The grapple decides whether it can start (cooldown, valid parabola, or whether it cuts a second jump in flight). If it says
        // no, nothing changed: neither the tongue nor the swing.
        if(!_grappleBehaviour.TryStart(anchor)) return;

        // The grapple already took control of Gekko: if it was hanging from a swing, it is released.
        if(IsSwinging) ReleaseSwing();

        // The tongue comes out of the mouth towards the GrapplePoint and stays drawn until the flight ends (see
        // ArtificialLateUpdate).
        _grapplePoint = anchor;
        BeginTongue();
    }

    // Start of the visual rope. The swing and the grapple share it, and each attach starts from scratch, with no "memory" of the
    // previous one:
    //  1) _currentGrapplePoint (the tip of the rope that travels towards the anchor) leaves from the tongue.
    //  2) Reset(): the whole rope starts collapsed at the tongue and still. Without this it would inherit the shape and
    //     movement of the previous attach. Then, while the tip travels towards the anchor (see DrawTongue), the
    //     rope unrolls behind it.
    //  3) The Spring gets its initial kick: that is what fires the whip.
    // _grapplePoint must have been set before calling it.
    private void BeginTongue()
    {
        _currentGrapplePoint = _tongueTip.position;
        _rope.Reset(_currentGrapplePoint);
        _spring.SetVelocity(_springVelocity);

        // The LineRenderer needs to have one point per rope point (segments + 1) before receiving positions.
        _lineRenderer.positionCount = _quality + 1;

        // The initial pose is drawn right now (without advancing the simulation) so the first frame looks right, whether this
        // method runs before or after ArtificialLateUpdate.
        UpdateLineRenderer();
    }

    public void ArtificialUpdate()
    {
        _grappleBehaviour.ArtificialUpdate();

        // What is in front of the camera (swing, grapple or nothing) and the indicator over the anchor.
        _detection.ArtificialUpdate();

        // Without a joint there is no swing. During the grapple there is no joint either, so the thrust (ODM) doesn't run: just like in the
        // repo, where MovePlayer cuts out with activeGrapple.
        if(!_joint) return;

        ODMGearMovement();
    }

    // Called by Player.LateUpdate() once per frame. The rope is simulated and drawn here and not in ArtificialUpdate because:
    //  1) LateUpdate runs when everything has already moved this frame (the interpolated Rigidbody, the animations). That way the
    //     tongue's FINAL position is read. If it were read earlier, the rope's tip would be a frame behind the mouth
    //     and would look like it was shaking.
    //  2) It is the last thing that happens before the screen is drawn, so exactly what was just simulated is shown.
    public void ArtificialLateUpdate()
    {
        // The tongue is drawn whenever there is a swing OR a grapple with the tongue out. During the grapple there is no joint (the grapple
        // doesn't use one), but the rope keeps going from the mouth to the anchor until the flight ends. The second jump releases it
        // (IsTongueOut), even if there is still flight. With neither of the two there is no rope.
        if(!IsSwinging && !(_grappleBehaviour != null && _grappleBehaviour.IsTongueOut)) return;

        DrawTongue();

        // After drawing: it uses this same frame's tongue tip.
        CheckSwingAttachArrival();
    }

    // The attach sound plays only once per swing, when the tongue's tip (the LineRenderer's: _currentGrapplePoint) reaches the
    // hook. Only in the swing: the grapple's tongue has no arrival sound. If the swing is cut before the tip arrives
    // (the button is released or there is a collision), IsSwinging becomes false and it doesn't play.
    private void CheckSwingAttachArrival()
    {
        if(!IsSwinging || _swingAttachSoundPlayed) return;

        if((_grapplePoint - _currentGrapplePoint).sqrMagnitude > SwingAttachArrivalDistance * SwingAttachArrivalDistance) return;

        _swingAttachSoundPlayed = true;
        if(AudioManager.instance) AudioManager.instance.Play(SoundNames.PlayerSwingAttach);
    }

    public void ArtificialFixedUpdate()
    {
        // Before the joint guard: the grapple's wait (Gekko pinned in the air) needs FixedUpdate and there is no joint.
        _grappleBehaviour.ArtificialFixedUpdate();

        // Before the joint guard: during the grapple there is no joint and Gekko still has to turn towards the point.
        // RotateGekkoWhileSwingin protects itself: it only turns with a swing or a grapple.
        _gekkoRotation.ArtificialFixedUpdate();

        if(!_joint) return;

        ApplyHeightLimit();
    }

    // Soft ceiling of the swing: Gekko can't rise above the attach point (+ _heightLimitOffset). If it goes past it,
    // it is pulled down like a spring. It only acts on the Y axis, so the horizontal swing stays the same.
    // It goes in FixedUpdate because it is real physics on the Rigidbody (unlike ODMGearMovement, which runs in Update).
    //  - Spring: pulls harder the further past the ceiling it went.
    //  - Damper: slows the UPWARD velocity. Only the upward one, so the way back down isn't slowed too.
    //  - Cap: the total acceleration doesn't exceed _heightLimitMaxPull. Without a cap, if Gekko attaches to a point that is far
    //    below it, the spring would pull it with an enormous force all at once.
    private void ApplyHeightLimit()
    {
        float overshoot = _rb.position.y - (_grapplePoint.y + _heightLimitOffset);
        if(overshoot <= 0f) return;

        float upwardSpeed = Mathf.Max(0f, _rb.linearVelocity.y);
        float pull = Mathf.Min(_heightLimitSpring * overshoot + _heightLimitDamper * upwardSpeed, _heightLimitMaxPull);

        // ForceMode.Acceleration: the values are in m/s² and don't depend on the Rigidbody's mass.
        _rb.AddForce(Vector3.down * pull, ForceMode.Acceleration);
    }

    // One frame of the tongue. There are 5 steps, always in this order:
    //   1) the whip advances   2) the two ends are placed   3) the rope's length is computed
    //   4) the simulation advances   5) it is drawn
    private void DrawTongue()
    {
        float deltaTime = Time.deltaTime;

        // STEP 1 - The whip advances a little: the Spring loses strength and the wave shrinks until it disappears.
        // (BeginTongue gave it the initial kick with SetVelocity.)
        _spring.Update(deltaTime);

        // STEP 2 - Where are the rope's two ends?
        //  - The mouth: Gekko's tongue's real position this frame. It moves with Gekko, even while swinging.
        //  - The anchor: it doesn't jump straight to the target. _currentGrapplePoint starts at the tongue and approaches
        //    _grapplePoint (the real attach point) a bit each frame (Lerp, about 8 times per second). It gives the
        //    effect of a "fired" tongue, and the rope unrolls behind it.
        Vector3 tongueTipPos = _tongueTip.position;
        _currentGrapplePoint = Vector3.Lerp(_currentGrapplePoint, _grapplePoint, deltaTime * 8f);

        // STEP 3 - How long is the rope? The distance between its ends plus extra length (_ropeSlack), which can be negative.
        //  - Positive: the rope hangs and bulges. Example: ends 5 m apart and slack 0.05 -> the rope is 5.25 m long.
        //  - Negative: the rope is pre-tensioned. Example: ends 5 m apart and slack -0.02 -> it is 4.9 m long, shorter than the gap,
        //    so it ends up stretched and straight, with the same tension in all segments. A small value is used (-0.02).
        //  - _joint.maxDistance is NOT used: the joint is configured at 0.8 x the initial distance, that is, 20% SHORTER than
        //    the real gap between the ends. A rope stretched that much would look like a bar.
        //  - Since it follows the current distance, shortening or lengthening the cable (ODMGearMovement) needs no special
        //    case: the rope adjusts itself.
        float visibleDistance = Vector3.Distance(tongueTipPos, _currentGrapplePoint);
        float restLength = visibleDistance * (1f + _ropeSlack);

        // STEP 4 - The simulation advances: the points in between move by inertia and gravity, and the segments
        // keep their length (the details are explained above RopeSimulation).
        _rope.Simulate(deltaTime, tongueTipPos, _currentGrapplePoint, restLength);

        // STEP 5 - It is drawn: the points are copied to the LineRenderer, straightened according to _ropeTautness and with the whip.
        UpdateLineRenderer();
    }

    // Copies the rope's points to the LineRenderer, straightens them according to _ropeTautness and adds the whip to them. It doesn't
    // advance anything: it only draws the current state (that is why BeginTongue can also use it to show the initial pose).
    private void UpdateLineRenderer()
    {
        // The points the simulation computed: 0 is the tongue and the last one is the anchor.
        Vector3[] simulated = _rope.Points;
        Vector3 ropeStart = simulated[0];
        Vector3 ropeEnd = simulated[_quality];

        // Which way the whip curves: an "up" direction perpendicular to the rope. It is obtained by rotating
        // a regular "up" so that it looks along the rope (LookRotation).
        // The rope's direction is computed with _grapplePoint (the real anchor) and NEVER with _currentGrapplePoint: on the
        // first frame of each attach _currentGrapplePoint equals the tongue, the direction would be a zero vector and
        // LookRotation(Vector3.zero) throws a warning. The if is a safety net in case tongue and anchor coincide.
        Vector3 ropeDir = _grapplePoint - simulated[0];
        Vector3 up = ropeDir.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(ropeDir.normalized) * Vector3.up
            : Vector3.up;

        for(int i = 0; i <= _quality; i++)
        {
            // Relative position of the point within the rope: 0 at the tongue, 0.5 in the middle, 1 at the anchor.
            float delta = i / (float)_quality;

            // The whip at this point = height * wave * the spring's current strength * attenuation curve:
            //  - sine wave: goes up and down _waveCount times along the rope.
            //  - _spring.Value: starts high on attaching and dies out on its own (that is why the whip disappears).
            //  - curve: it is 0 at both ends, so the tongue and the anchor don't come apart even with the wave at its maximum.
            Vector3 whip = up * _waveHeight * Mathf.Sin(delta * _waveCount * Mathf.PI) * _spring.Value * _waveAffectCurve.Evaluate(delta);

            // Visual tension: the simulated point moves towards its place on the tongue -> anchor line, which is where it would be if
            // the rope were perfectly taut. With _ropeTautness at 0 it stays where the simulation left it; with 1, it is
            // on the line. Both ends are already on the line, so they don't move. It is only when drawing: the
            // simulation goes on by itself and the deviation is recomputed every frame, it doesn't accumulate.
            Vector3 shapedPoint = Vector3.Lerp(simulated[i], Vector3.Lerp(ropeStart, ropeEnd, delta), _ropeTautness);

            // Final point = straightened point + whip. The whip is only added here, when drawing, and NOT inside the
            // simulation: if it went in there, the rope would treat it as real movement and drag it along. The visual tension doesn't
            // straighten it either: the wave always stays complete. That way the whip remains a
            // controllable effect and the physics provides the natural movement.
            _ropePoints[i] = shapedPoint + whip;
        }

        // All the points are handed to the LineRenderer at once (cheaper than a SetPosition per point).
        _lineRenderer.SetPositions(_ropePoints);
    }

    // Space with the second jump window open (see GekkoGrapple.TrySecondJump): it jumps towards where the camera looks.
    // PlayerInputs calls it ONCE per press, not every frame while it is held down.
    public void TrySecondJump() => _grappleBehaviour.TrySecondJump(_camera.forward);

    // Cuts a grapple in progress, without cooldown. Called by PlayerController.Teleport: the flight points at an anchor that
    // ended up far away after the teleport.
    public void CancelGrapple() => _grappleBehaviour?.Cancel();

    // Called by Player.OnCollisionEnter with every solid collision. While hanging from the swing it cuts it, as always. Otherwise the collision
    // goes to the grapple, which uses it to end the flight.
    public void ArtificialOnCollisionEnter()
    {
        if(IsSwinging) StopSwing();
        else _grappleBehaviour.ArtificialOnCollisionEnter();
    }

    // Requires a lot of optimization
    // Cuts the swing and turns the tongue off. Called by PlayerInputs (when the tongue button is released), collisions and triggers.
    public void StopSwing()
    {
        // Releasing the tongue button in the middle of a grapple doesn't cut it: the flight ends on its own, on collision, on reaching the point
        // or on time (see GekkoGrapple). In the repo nothing cuts the flight either except touching something.
        if(IsGrappling) return;

        ReleaseSwing();
        HideTongue();
    }

    // Releases the swing: without a joint, IsSwinging = false (with that, PlayerController, the height ceiling and the rotation towards the
    // point stop treating Gekko as swinging). It does NOT touch the tongue: during the grapple it keeps being drawn.
    private void ReleaseSwing()
    {
        IsSwinging = false;
        Object.Destroy(_joint);
        // Destroy is deferred (it runs at the end of the frame): without this line, ArtificialUpdate and ArtificialFixedUpdate
        // would keep seeing the joint "alive" for the rest of this frame and would keep applying thrust, ceiling and rotation to it.
        _joint = null;
    }

    // Turns the tongue off: the LineRenderer is left with no points and the whip is reset. The rope doesn't need to be reset here:
    // each new attach starts with _rope.Reset() in BeginTongue. It is public because GekkoGrapple calls it when the
    // flight ends (while the grapple lasts the tongue stays drawn).
    public void HideTongue()
    {
        _lineRenderer.positionCount = 0;
        _spring.Reset();
    }

    private void ODMGearMovement()
    {
        Vector3 flatForward = Vector3.ProjectOnPlane(_camera.forward, Vector3.up).normalized;
        Vector3 flatRight = Vector3.ProjectOnPlane(_camera.right, Vector3.up).normalized;
        _rb.AddForce(flatRight * _thrustInput.x * _horizontalThrustForce * Time.deltaTime);
        _rb.AddForce(flatForward * _thrustInput.y * _forwardThrustForce * Time.deltaTime);
    }

    public GekkoSwinging InitializeGrappleBehaviour(float overshootYAxis, float grapplingCD, float delayTime,
                                                    float finishDistance, float lookSpeed, float secondJumpMultiplier)
    {
        _grappleBehaviour = new GekkoGrapple(this, _rb, _transform, overshootYAxis, grapplingCD, delayTime,
                                             finishDistance, _gekkoRotation, lookSpeed, secondJumpMultiplier);
        return this;
    }
}