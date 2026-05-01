using UnityEngine;

[DisallowMultipleComponent]
public class NetworkPlayerSlide : MonoBehaviour
{
    [Header("Slide")]
    [SerializeField] private float _crouchColliderHeightMultiplier = 0.55f;
    [SerializeField] private float _slideDuration = 0.65f;
    [SerializeField] private float _slideAcceleration = 28f;
    [SerializeField] private float _maxSlideSpeed = 16f;
    [SerializeField] private float _slideSteerLerp = 7f;
    [SerializeField] private float _slideStartSpeedThreshold = 4f;

    private NetworkPlayerMotor _motor;
    private NetworkPlayerInputState _input;

    private bool _isSliding;
    private float _slideTimer;
    private float _slideSpeed;
    private Vector3 _slideDirection;

    public bool IsSliding { get { return _isSliding; } }
    public bool WantsCrouchSpeed
    {
        get { return !_isSliding && _motor != null && _input != null && _input.CrouchHeld && _motor.IsGrounded; }
    }

    public void Initialize(NetworkPlayerMotor motor, NetworkPlayerInputState input)
    {
        _motor = motor;
        _input = input;
        _motor.ConfigureCrouchCollider(_crouchColliderHeightMultiplier);
        UpdateColliderState();
    }

    public void Tick(float deltaTime)
    {
        if (_motor == null || _input == null)
            return;

        float horizontalSpeed = _motor.GetHorizontalVelocity().magnitude;
        if (!_isSliding && _input.CrouchHeld && _motor.IsGrounded && horizontalSpeed >= _slideStartSpeedThreshold)
            StartSlide(horizontalSpeed);

        if (_isSliding)
        {
            if (_input.CrouchReleasedThisStep)
                ForceStop();

            if (_isSliding && (!_motor.IsGrounded || (_motor.IsOnSlope && _motor.Body.linearVelocity.y < 0.1f)))
                _slideTimer = _slideDuration;

            if (_isSliding)
            {
                _slideTimer -= deltaTime;
                if (_slideTimer <= 0f)
                    ForceStop();
            }
        }

        UpdateColliderState();
    }

    public void ApplySlidingMovement(float deltaTime)
    {
        if (_motor == null)
            return;

        Vector3 inputDirection = _motor.GetMoveDirection();
        if (inputDirection.sqrMagnitude > 0.01f)
        {
            _slideDirection = Vector3.Lerp(
                _slideDirection,
                inputDirection.normalized,
                _slideSteerLerp * deltaTime).normalized;
        }

        if (_motor.IsOnSlope)
        {
            Vector3 slopeDirection = _motor.GetSlopeMoveDirection(_slideDirection);
            if (slopeDirection.sqrMagnitude > 0.0001f)
                _slideDirection = slopeDirection;

            Vector3 downSlopeDirection = Vector3.ProjectOnPlane(Vector3.down, _motor.SlopeHit.normal).normalized;
            if (Vector3.Dot(_slideDirection, downSlopeDirection) > 0f)
                _slideSpeed = Mathf.Min(_maxSlideSpeed, _slideSpeed + (_slideAcceleration * deltaTime));
        }
        else
        {
            _slideSpeed = Mathf.MoveTowards(_slideSpeed, _motor.CrouchSpeed, _slideAcceleration * deltaTime);
        }

        Vector3 targetVelocity = _slideDirection * _slideSpeed;
        if (_motor.IsOnSlope)
        {
            Vector3 projected = Vector3.ProjectOnPlane(targetVelocity, _motor.SlopeHit.normal);
            float vertical = Mathf.Max(projected.y, _motor.VerticalVelocity);
            _motor.SetVelocity(new Vector3(projected.x, vertical, projected.z));
        }
        else
        {
            _motor.SetVelocity(new Vector3(targetVelocity.x, _motor.VerticalVelocity, targetVelocity.z));
        }
    }

    public void ForceStop()
    {
        _isSliding = false;
        UpdateColliderState();
    }

    private void StartSlide(float horizontalSpeed)
    {
        _isSliding = true;
        _slideTimer = _slideDuration;
        _slideSpeed = Mathf.Max(horizontalSpeed, _slideStartSpeedThreshold);

        Vector3 currentHorizontal = _motor.GetHorizontalVelocity();
        Vector3 inputDirection = _motor.GetMoveDirection();
        if (currentHorizontal.sqrMagnitude > 0.0001f)
        {
            _slideDirection = currentHorizontal.normalized;
        }
        else if (inputDirection.sqrMagnitude > 0.0001f)
        {
            _slideDirection = inputDirection;
        }
        else
        {
            _slideDirection = _motor.transform.forward;
        }
    }

    private void UpdateColliderState()
    {
        if (_motor == null || _input == null)
            return;

        bool crouched = _isSliding || (_input.CrouchHeld && _motor.IsGrounded);
        _motor.SetCrouchCollider(crouched);
    }
}
