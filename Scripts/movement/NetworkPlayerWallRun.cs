using UnityEngine;

[DisallowMultipleComponent]
public class NetworkPlayerWallRun : MonoBehaviour
{
    [Header("Wall Run")]
    [SerializeField] private LayerMask _wallMask = ~0;
    [SerializeField] private float _wallCheckDistance = 1f;
    [SerializeField] private float _maxWallRunTime = 1.4f;
    [SerializeField] private float _wallClimbSpeed = 4f;
    [SerializeField] private float _wallJumpUpForce = 10f;
    [SerializeField] private float _wallJumpSideForce = 12f;
    [SerializeField] private float _wallGravityCompensation = 30f;
    [SerializeField] private float _normalVelocityRetention = 0.8f;
    [SerializeField] private float _minWallRunSpeed = 15f;
    [SerializeField] private float _wallRunCooldown = 1.5f;

    private NetworkPlayerMotor _motor;
    private NetworkPlayerInputState _input;

    private bool _isWallRunning;
    private float _wallRunTimer;
    private float _wallRunSpeed;
    private RaycastHit _leftWallHit;
    private RaycastHit _rightWallHit;
    private bool _wallLeft;
    private bool _wallRight;
    //private bool _runningOnRightWall;
    private float _rightWallRunCooldownTimer;
    private float _leftWallRunCooldownTimer;

    public bool IsWallRunning { get { return _isWallRunning; } }
    //public int WallSideSign { get { return _isWallRunning ? (_wallRight ? 1 : -1) : 0; } }
    public int WallSideSign => _currentWallSide;
    private int _currentWallSide = 0; // 1 = right, -1 = left, 0 = none

    public void Initialize(NetworkPlayerMotor motor, NetworkPlayerInputState input)
    {
        _motor = motor;
        _input = input;
    }

    public void TickDetection()
    {
        if (_motor == null || _input == null)
            return;

        UpdateWallDetection();

        if (_isWallRunning)
        {
            bool lostTrackedWall = (_currentWallSide == 1 && !_wallRight) || (_currentWallSide == -1 && !_wallLeft);
            if (lostTrackedWall || _motor.IsGrounded)
            {
                ForceStop();
            }

            return;
        }

        if (_motor.IsGrounded)
            return;

        if (_wallRight)
        {
            //_runningOnRightWall = true;
            StartWallRun(_rightWallHit);
        }
        else if (_wallLeft)
        {
            //_runningOnRightWall = false;
            StartWallRun(_leftWallHit);
        }
    }

    public void TickCooldown(float deltaTime)
    {
        if(_rightWallRunCooldownTimer > 0f)
            _rightWallRunCooldownTimer = Mathf.Max(0f, _rightWallRunCooldownTimer - deltaTime);
        if(_leftWallRunCooldownTimer > 0f)
            _leftWallRunCooldownTimer = Mathf.Max(0f, _leftWallRunCooldownTimer - deltaTime);
    }

    public void ApplyWallRun(float deltaTime)
    {
        if (!_isWallRunning || _motor == null || _input == null)
            return;

        RaycastHit wallHit = _currentWallSide == 1 ? _rightWallHit : _leftWallHit;
        if (_motor.HasBufferedJump)
        {
            WallJump(wallHit.normal);
            return;
        }

        Vector3 wallNormal = wallHit.normal;
        Vector3 wallForward = GetWallForward(wallNormal);
        float wallSpeed = Mathf.Max(_wallRunSpeed, _minWallRunSpeed);

        _motor.Accelerate(wallForward, wallSpeed, _motor.GroundAcceleration);

        if (_input.MoveInput.y > 0.1f)
            _motor.VerticalVelocity = _wallClimbSpeed;
        else if (_input.CrouchHeld)
            _motor.VerticalVelocity = -_wallClimbSpeed;
        else
            _motor.VerticalVelocity = Mathf.MoveTowards(_motor.VerticalVelocity, 0f, _wallGravityCompensation * deltaTime);

        _motor.Body.AddForce(-wallNormal * 100f, ForceMode.Force);

        Vector3 velocity = _motor.Body.linearVelocity;
        _motor.SetVelocity(new Vector3(velocity.x, _motor.VerticalVelocity, velocity.z));

        _wallRunTimer -= deltaTime;
        if (_wallRunTimer <= 0f){
            ForceStop();
        }
    }

    public void ForceStop()
    {
        _isWallRunning = false;

        if (_currentWallSide == 1)
            _rightWallRunCooldownTimer = _wallRunCooldown;
        else if (_currentWallSide == -1)
            _leftWallRunCooldownTimer = _wallRunCooldown;
        
        _currentWallSide = 0;
    }

    private void UpdateWallDetection()
    {
        Vector3 origin = _motor.Capsule != null ? _motor.Capsule.bounds.center : _motor.transform.position;
        _wallRight = Physics.Raycast(origin, _motor.transform.right, out _rightWallHit, _wallCheckDistance, _wallMask, QueryTriggerInteraction.Ignore);
        _wallLeft = Physics.Raycast(origin, -_motor.transform.right, out _leftWallHit, _wallCheckDistance, _wallMask, QueryTriggerInteraction.Ignore);
    }

    private void StartWallRun(RaycastHit wallHit)
    {   
        int side = _wallRight ? 1 : -1;

        if (side == 1 && _rightWallRunCooldownTimer > 0f)
            return;
        if (side == -1 && _leftWallRunCooldownTimer > 0f)
            return;

        _currentWallSide = side;

        _isWallRunning = true;
        _wallRunTimer = _maxWallRunTime;
        _motor.RestoreAirJumps();

        if (side == 1)
            _leftWallRunCooldownTimer = 0f;
        else
            _rightWallRunCooldownTimer = 0f;

        Vector3 wallNormal = wallHit.normal;
        Vector3 flatVelocity = _motor.GetHorizontalVelocity();
        Vector3 normalComponent = Vector3.Project(flatVelocity, wallNormal);
        Vector3 parallelComponent = flatVelocity - normalComponent;
        Vector3 retainedVelocity = parallelComponent + (normalComponent * _normalVelocityRetention);

        float retainedSpeed = retainedVelocity.magnitude;
        _wallRunSpeed = Mathf.Max(retainedSpeed, _minWallRunSpeed);

        Vector3 finalVelocity = retainedVelocity;
        if (retainedSpeed > 0f && retainedSpeed < _minWallRunSpeed)
            finalVelocity = retainedVelocity.normalized * _minWallRunSpeed;
        else if (retainedSpeed <= 0f)
            finalVelocity = GetWallForward(wallNormal) * _minWallRunSpeed;

        _motor.SetVelocity(new Vector3(finalVelocity.x, Mathf.Max(0f, _motor.Body.linearVelocity.y), finalVelocity.z));
        _motor.VerticalVelocity = Mathf.Max(0f, _motor.VerticalVelocity);
    }

    private Vector3 GetWallForward(Vector3 wallNormal)
    {
        Vector3 wallForward = Vector3.Cross(wallNormal, Vector3.up);
        if ((_motor.transform.forward - wallForward).sqrMagnitude > (_motor.transform.forward + wallForward).sqrMagnitude)
            wallForward = -wallForward;

        return wallForward.normalized;
    }

    private void WallJump(Vector3 wallNormal)
    {
        ForceStop();
        _motor.ConsumeJumpBuffer();
        _motor.ClearCoyoteTime();
        _motor.MarkUngrounded();

        Vector3 horizontalVelocity = _motor.GetHorizontalVelocity() + (wallNormal * _wallJumpSideForce);
        _motor.VerticalVelocity = _wallJumpUpForce;
        _motor.SetVelocity(new Vector3(horizontalVelocity.x, _motor.VerticalVelocity, horizontalVelocity.z));
    }
}
