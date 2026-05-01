using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(DamageHandler))]
public class NetworkPlayerMotor : NetworkBehaviour
{
    //These should probably be tested and updated. The default move speed seems a little slow
    [Header("Move")]
    [SerializeField] private float _moveSpeed = 7f;
    [SerializeField] private float _crouchSpeed = 4f;
    [SerializeField] private float _maxAirSpeed = 7f;
    [SerializeField] private float _groundAcceleration = 50f;
    [SerializeField] private float _airAcceleration = 15f;
    [SerializeField] private float _groundDrag = 6f;
    [SerializeField] private float _jumpVelocity = 7f;
    [SerializeField] private float _gravity = -20f;
    [SerializeField] private LayerMask _groundMask = ~0;
    [SerializeField] private float _groundCheckRadius = 0.35f;
    [SerializeField] private float _groundCheckDistance = 0.15f;
    [SerializeField] private float _minGroundProbeDistance = 0.25f;
    [SerializeField] private float _groundProbeLift = 0.08f;
    [SerializeField] private float _maxGroundAngle = 75f;

    [Header("Jump Assist")]
    [SerializeField] private float _jumpBufferTime = 0.12f;
    [SerializeField] private float _coyoteTime = 0.12f;
    [SerializeField] private float _groundStickVelocity = -1.5f;
    [SerializeField] private int _maxJumpCount = 2;

    [Header("Collision")]
    [SerializeField] private bool _autoApplyLowFrictionMaterial = true;

    [Header("First Person")]
    [SerializeField] private Renderer[] _ownerHiddenRenderers;

    private readonly RaycastHit[] _groundHits = new RaycastHit[8];

    private PlayerInput _playerInput;
    private Rigidbody _rigidbody;
    private CapsuleCollider _capsule;
    private IHealth _health;
    private PlayerCamera _playerCamera;


    //Add other movement features here to use
    //Not sure what else we would need, but is scalable
    private NetworkPlayerInputState _input;
    private NetworkPlayerSlide _slide;
    private NetworkPlayerDash _dash;
    private NetworkPlayerWallRun _wallRun;
    private NetworkPlayerGroundSlam _slam;
    private PlayerStyleController _styleController;

    private float _jumpBufferTimer;
    private float _coyoteTimer; //This is something that I was told was a good idea to have
    private float _verticalVelocity;
    private int _remainingAirJumps;

    private bool _isGrounded;
    private bool _onSlope;
    private RaycastHit _slopeHit;

    private float _standingCapsuleHeight;
    private Vector3 _standingCapsuleCenter;
    private float _crouchedCapsuleHeight;
    private Vector3 _crouchedCapsuleCenter;

    //Honestly not sure why, but these were just what the internet said was good
    public PlayerMovementState State { get; private set; } = PlayerMovementState.Idle;

    public Rigidbody Body { get { return _rigidbody; } }
    public CapsuleCollider Capsule { get { return _capsule; } }
    public PlayerCamera PlayerCameraComponent { get { return _playerCamera; } }
    public NetworkPlayerInputState InputState { get { return _input; } }
    public bool IsGrounded { get { return _isGrounded; } }
    public bool IsOnSlope { get { return _onSlope; } }
    public RaycastHit SlopeHit { get { return _slopeHit; } }
    public float VerticalVelocity { get { return _verticalVelocity; } set { _verticalVelocity = value; } }
    public float MoveSpeed { get { return _moveSpeed; } }
    public float CrouchSpeed { get { return _crouchSpeed; } }
    public float MaxAirSpeed { get { return _maxAirSpeed; } }
    public float GroundAcceleration { get { return _groundAcceleration; } }
    public float AirAcceleration { get { return _airAcceleration; } }
    public float StandingCapsuleHeight { get { return _standingCapsuleHeight; } }
    public Vector3 StandingCapsuleCenter { get { return _standingCapsuleCenter; } }
    public float CrouchedCapsuleHeight { get { return _crouchedCapsuleHeight; } }
    public Vector3 CrouchedCapsuleCenter { get { return _crouchedCapsuleCenter; } }
    public bool HasBufferedJump { get { return _jumpBufferTimer > 0f; } }
    
    public void AddMoveSpeedBonus(float bonus)
    {
        _moveSpeed += bonus;
    }

    public void RemoveMoveSpeedBonus(float bonus)
    {
        _moveSpeed -= bonus;
    }
    protected virtual void Awake()
    {
        _playerInput = GetComponent<PlayerInput>();
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        _rigidbody.freezeRotation = true;
        _capsule = GetComponent<CapsuleCollider>();
        _health = GetComponent<DamageHandler>();
        _playerCamera = GetComponent<PlayerCamera>();

        _input = GetOrAddComponent<NetworkPlayerInputState>();
        _slide = GetOrAddComponent<NetworkPlayerSlide>();
        _dash = GetOrAddComponent<NetworkPlayerDash>();
        _wallRun = GetOrAddComponent<NetworkPlayerWallRun>();
        _slam = GetOrAddComponent<NetworkPlayerGroundSlam>();
        _styleController = GetComponent<PlayerStyleController>();

        if (_ownerHiddenRenderers == null || _ownerHiddenRenderers.Length == 0)
            _ownerHiddenRenderers = GetComponentsInChildren<Renderer>(true);

        CacheCapsuleDimensions();
        ConfigureCapsuleMaterial();
        RestoreAirJumps();

        _slide.Initialize(this, _input);
        _dash.Initialize(this, _input);
        _wallRun.Initialize(this, _input);
        _slam.Initialize(this, _input);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (_playerInput != null)
            _playerInput.enabled = IsOwner;

        if (IsOwner)
            ConfigureOwnerBody();
        else
            ConfigureRemoteBody();

        SetOwnerMeshVisibility();
    }

    protected virtual void FixedUpdate()
    {
        if (!IsOwner || _rigidbody == null)
            return;

        if (_health != null && _health.IsDead)
        {
            State = PlayerMovementState.Dead;
            _rigidbody.linearVelocity = Vector3.zero;
            EndFixedStep();
            return;
        }

        float deltaTime = Time.fixedDeltaTime;
        RefreshGroundState();
        TickJumpAssist(deltaTime);

        _dash.TickCooldown(deltaTime);
        _slam.TickCooldown(deltaTime);
        _wallRun.TickCooldown(deltaTime);

        if (!_wallRun.IsWallRunning && !_slam.IsActive && _dash.TryStart() && _dash.IsDashing)
        {
            _slide.ForceStop();
            _slam.ForceStop();
        }

        //One thing I have noticed is that dashing on the ground feels like you stop abruptly. Might need a little touch up to save momentum or something
        if (_dash.IsDashing)
        {
            _dash.TickDash(deltaTime);
            ApplyDrag();
            UpdateState();
            EndFixedStep();
            return;
        }

        if (_slide.IsSliding || _slam.IsActive)
            _wallRun.ForceStop();
        else
            _wallRun.TickDetection();

        if (_wallRun.IsWallRunning)
        {
            _wallRun.ApplyWallRun(deltaTime);
            ApplyDrag();
            UpdateState();
            EndFixedStep();
            return;
        }

        if (_slam.TickSlam())
        {
            ApplyDrag();
            UpdateState();
            EndFixedStep();
            return;
        }

        UpdateVerticalVelocity(deltaTime);
        TryHandleJump();

        _slide.Tick(deltaTime);

        if (_slide.IsSliding)
            _slide.ApplySlidingMovement(deltaTime);
        else
            ApplyMovement();

        ApplySlopeStick();
        ApplyDrag();
        UpdateState();
        EndFixedStep();
    }

    public void Accelerate(Vector3 wishDir, float maxSpeed, float acceleration)
    {
        if (wishDir.sqrMagnitude < 0.0001f || maxSpeed <= 0f || acceleration <= 0f)
            return;

        wishDir.Normalize();

        Vector3 horizontalVelocity = GetHorizontalVelocity();
        float currentSpeed = Vector3.Dot(horizontalVelocity, wishDir);
        float addSpeed = maxSpeed - currentSpeed;
        if (addSpeed <= 0f)
            return;

        float accelSpeed = acceleration * Time.fixedDeltaTime * maxSpeed;
        if (accelSpeed > addSpeed)
            accelSpeed = addSpeed;

        horizontalVelocity += wishDir * accelSpeed;
        SetHorizontalVelocity(horizontalVelocity);
    }

    public Vector3 GetHorizontalVelocity()
    {
        return new Vector3(_rigidbody.linearVelocity.x, 0f, _rigidbody.linearVelocity.z);
    }

    public Vector3 GetMoveDirection()
    {
        if (_input == null)
            return Vector3.zero;

        Vector3 wishDirection = (transform.forward * _input.MoveInput.y) + (transform.right * _input.MoveInput.x);
        return wishDirection.sqrMagnitude > 1f ? wishDirection.normalized : wishDirection;
    }

    public Vector3 GetSlopeMoveDirection(Vector3 direction)
    {
        if (!_onSlope)
            return direction.normalized;

        Vector3 projected = Vector3.ProjectOnPlane(direction, _slopeHit.normal);
        return projected.sqrMagnitude > 0.0001f ? projected.normalized : Vector3.zero;
    }

    public void SetHorizontalVelocity(Vector3 horizontalVelocity)
    {
        _rigidbody.linearVelocity = new Vector3(horizontalVelocity.x, _rigidbody.linearVelocity.y, horizontalVelocity.z);
    }

    public void SetVelocity(Vector3 velocity)
    {
        _rigidbody.linearVelocity = velocity;
    }

    public void SetCrouchCollider(bool crouched)
    {
        if (_capsule == null)
            return;

        _capsule.height = crouched ? _crouchedCapsuleHeight : _standingCapsuleHeight;
        _capsule.center = crouched ? _crouchedCapsuleCenter : _standingCapsuleCenter;
        _playerCamera.SetCrouchState(crouched);
    }

    public void ConfigureCrouchCollider(float heightMultiplier)
    {
        if (_capsule == null)
            return;

        float clampedMultiplier = Mathf.Clamp(heightMultiplier, 0.25f, 1f);
        float crouchedHeight = Mathf.Max(_capsule.radius * 2.05f, _standingCapsuleHeight * clampedMultiplier);
        float heightDelta = _standingCapsuleHeight - crouchedHeight;

        _crouchedCapsuleHeight = crouchedHeight;
        _crouchedCapsuleCenter = _standingCapsuleCenter + (Vector3.down * (heightDelta * 0.5f));
    }

    public void ConsumeJumpBuffer()
    {
        _jumpBufferTimer = 0f;
    }

    public void ClearCoyoteTime()
    {
        _coyoteTimer = 0f;
    }

    public void RestoreAirJumps()
    {
        _remainingAirJumps = Mathf.Max(0, _maxJumpCount - 1);
    }

    public void MarkUngrounded()
    {
        _isGrounded = false;
    }

    private void TickJumpAssist(float deltaTime)
    {
        _jumpBufferTimer = Mathf.Max(0f, _jumpBufferTimer - deltaTime);
        _coyoteTimer = Mathf.Max(0f, _coyoteTimer - deltaTime);

        if (_input != null && _input.JumpPressedThisStep)
            _jumpBufferTimer = Mathf.Max(_jumpBufferTimer, _jumpBufferTime);
    }

    private void RefreshGroundState()
    {
        bool wasGrounded = _isGrounded;
        _onSlope = TryGetSlopeHit(out _slopeHit);
        _isGrounded = CheckGrounded() || _onSlope;

        if (_isGrounded)
            _coyoteTimer = _coyoteTime;

        if (_isGrounded && !wasGrounded)
        {
            RestoreAirJumps();
            _slam.ForceStop();
        }
    }

    private bool CheckGrounded()
    {
        if (_capsule == null)
            return false;

        if(_wallRun.IsWallRunning)
        {
            _groundCheckRadius = 0.1f;
            _groundCheckDistance = 0.4f;
        }else
        {
            _groundCheckRadius = 0.35f;
            _groundCheckDistance = 0.15f;
        }

        float radius = Mathf.Min(_groundCheckRadius, _capsule.radius * 0.95f);
        float feetOffset = Mathf.Max(0f, (_capsule.height * 0.5f) - _capsule.radius);
        Vector3 feet = transform.TransformPoint(_capsule.center + (Vector3.down * feetOffset));
        float lift = Mathf.Max(0.01f, _groundProbeLift);
        Vector3 castOrigin = feet + (Vector3.up * lift);
        float castDistance = Mathf.Max(_groundCheckDistance, _minGroundProbeDistance) + lift;
        float minGroundDot = Mathf.Cos(Mathf.Clamp(_maxGroundAngle, 0f, 89f) * Mathf.Deg2Rad);

        int hits = Physics.SphereCastNonAlloc(
            castOrigin,
            radius,
            Vector3.down,
            _groundHits,
            castDistance,
            _groundMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits; i++)
        {
            RaycastHit hit = _groundHits[i];
            Collider hitCollider = hit.collider;
            if (hitCollider == null)
                continue;
            if (hitCollider.transform.IsChildOf(transform))
                continue;
            if (Vector3.Dot(hit.normal, Vector3.up) < minGroundDot)
                continue;

            return true;
        }

        return false;
    }

    private bool TryGetSlopeHit(out RaycastHit slopeHit)
    {
        slopeHit = default(RaycastHit);
        if (_capsule == null)
            return false;

        float rayDistance = Mathf.Max(_groundCheckDistance, _minGroundProbeDistance) + (_capsule.height * 0.5f);
        Vector3 origin = transform.TransformPoint(_capsule.center + (Vector3.up * 0.1f));
        if (!Physics.Raycast(origin, Vector3.down, out slopeHit, rayDistance, _groundMask, QueryTriggerInteraction.Ignore))
            return false;

        if (slopeHit.collider != null && slopeHit.collider.transform.IsChildOf(transform))
            return false;

        float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
        return angle > 0.01f && angle <= _maxGroundAngle;
    }

    private void UpdateVerticalVelocity(float deltaTime)
    {
        if (_isGrounded && _verticalVelocity <= 0f)
        {
            _verticalVelocity = Mathf.Min(_groundStickVelocity, 0f);
            return;
        }

        _verticalVelocity += _gravity * deltaTime;
    }

    private bool TryHandleJump()
    {
        if (_jumpBufferTimer <= 0f)
            return false;

        if (_isGrounded || _coyoteTimer > 0f)
        {
            _verticalVelocity = _jumpVelocity;
            ConsumeJumpBuffer();
            ClearCoyoteTime();
            MarkUngrounded();
            return true;
        }

        if (_remainingAirJumps > 0)
        {
            _verticalVelocity = _jumpVelocity;
            ConsumeJumpBuffer();
            _remainingAirJumps--;
            return true;
        }

        return false;
    }

    private void ApplyMovement()
    {

        Vector3 wishDirection = GetMoveDirection();
        //I have added a combo/style system. Higher your style is, the faster you move
        float speedMultiplier = GetStyleMoveSpeedMultiplier();
        float maxSpeed = _moveSpeed * speedMultiplier;

        if (_slide.WantsCrouchSpeed)
            maxSpeed = _crouchSpeed * speedMultiplier;

        if (_isGrounded && _onSlope)
        {
            Accelerate(GetSlopeMoveDirection(wishDirection), maxSpeed, _groundAcceleration);
        }
        else if (_isGrounded)
        {
            Accelerate(wishDirection, maxSpeed, _groundAcceleration);
        }
        else
        {
            Accelerate(wishDirection, _maxAirSpeed * speedMultiplier, _airAcceleration);
        }

        Vector3 velocity = _rigidbody.linearVelocity;
        if (_isGrounded && _onSlope)
        {
            Vector3 projected = Vector3.ProjectOnPlane(new Vector3(velocity.x, 0f, velocity.z), _slopeHit.normal);
            float vertical = Mathf.Max(projected.y, _verticalVelocity);
            _rigidbody.linearVelocity = new Vector3(projected.x, vertical, projected.z);
        }
        else
        {
            _rigidbody.linearVelocity = new Vector3(velocity.x, _verticalVelocity, velocity.z);
        }
    }

    private void ApplySlopeStick()
    {
        if (!_isGrounded || !_onSlope || _slide.IsSliding)
            return;

        if (_input != null && _input.JumpHeld && HasBufferedJump)
            return;

        Vector3 gravity = Vector3.up * _gravity;
        Vector3 slopeGravity = Vector3.ProjectOnPlane(gravity, _slopeHit.normal);
        _rigidbody.AddForce(-slopeGravity, ForceMode.Acceleration);
    }

    private void ApplyDrag()
    {
        _rigidbody.linearDamping = (_isGrounded && !_wallRun.IsWallRunning && !_dash.IsDashing) ? _groundDrag : 0f;
    }

    private void UpdateState()
    {
        if (_health != null && _health.IsDead)
        {
            State = PlayerMovementState.Dead;
        }
        else if (_dash.IsDashing)
        {
            State = PlayerMovementState.Dashing;
        }
        else if (_wallRun.IsWallRunning)
        {
            State = PlayerMovementState.WallRunning;
        }
        else if (_slam.IsSuperSlamming)
        {
            State = PlayerMovementState.SuperSlamming;
        }
        else if (_slam.IsSlamming)
        {
            State = PlayerMovementState.Slamming;
        }
        else if (_slide.IsSliding)
        {
            State = PlayerMovementState.Sliding;
        }
        else if (_slide.WantsCrouchSpeed)
        {
            State = PlayerMovementState.Crouching;
        }
        else if (!_isGrounded)
        {
            State = PlayerMovementState.Air;
        }
        else if (_input == null || _input.MoveInput.sqrMagnitude <= 0.01f)
        {
            State = PlayerMovementState.Idle;
        }
        else
        {
            State = PlayerMovementState.Walking;
        }
    }

    private void EndFixedStep()
    {
        if (_input != null)
            _input.ClearTransientFlags();
    }

    private float GetStyleMoveSpeedMultiplier()
    {
        if (_styleController == null)
            _styleController = GetComponent<PlayerStyleController>();

        return _styleController != null ? _styleController.GetMoveSpeedMultiplier() : 1f;
    }

    private void CacheCapsuleDimensions()
    {
        if (_capsule == null)
            return;

        _standingCapsuleHeight = _capsule.height;
        _standingCapsuleCenter = _capsule.center;
        ConfigureCrouchCollider(0.55f);
    }

    private void ConfigureCapsuleMaterial()
    {
        if (!_autoApplyLowFrictionMaterial || _capsule == null || _capsule.sharedMaterial != null)
            return;

        PhysicsMaterial material = new PhysicsMaterial("PlayerLowFriction")
        {
            staticFriction = 0f,
            dynamicFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounciness = 0f,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };

        _capsule.sharedMaterial = material;
    }

    private void ConfigureOwnerBody()
    {
        if (_capsule != null)
            _capsule.enabled = true;

        if (_rigidbody == null)
            return;

        _rigidbody.isKinematic = false;
        _rigidbody.useGravity = false;
        _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
    }

    private void ConfigureRemoteBody()
    {
        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
            _rigidbody.linearVelocity = Vector3.zero;
        }

        if (_capsule != null)
            _capsule.enabled = false;
    }

    private void SetOwnerMeshVisibility()
    {
        bool visible = !IsOwner;
        if (_ownerHiddenRenderers == null)
            return;

        for (int i = 0; i < _ownerHiddenRenderers.Length; i++)
        {
            Renderer targetRenderer = _ownerHiddenRenderers[i];
            if (targetRenderer != null)
                targetRenderer.enabled = visible;
        }
    }

    private T GetOrAddComponent<T>() where T : Component
    {
        T component = GetComponent<T>();
        if (component == null)
            component = gameObject.AddComponent<T>();

        return component;
    }
}
